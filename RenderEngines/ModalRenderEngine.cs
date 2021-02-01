/**
Copyright 2014-2017 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using ccl;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.UI;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using sdd = System.Diagnostics.Debug;

namespace RhinoCyclesCore.RenderEngines
{
	public class ModalRenderEngine : RenderEngine
	{
		public ModalRenderEngine(RhinoDoc doc, Guid pluginId, ViewInfo view, bool isProductRender) : this(doc, pluginId, view, null, null, isProductRender) { }
		public ModalRenderEngine(RhinoDoc doc, Guid pluginId, ViewInfo view, ViewportInfo viewport, Rhino.Display.DisplayPipelineAttributes attributes, bool isProductRender)
			: base(pluginId, doc.RuntimeSerialNumber, view, viewport, attributes, false)
		{
			IsProductRender = isProductRender;
			Quality = doc.RenderSettings.AntialiasLevel;
			ModalRenderEngineCommonConstruct();
		}

		AntialiasLevel Quality { get; set; }
		public bool IsProductRender { get; set; }

		private void ModalRenderEngineCommonConstruct()
		{
			Client = new Client();
			State = State.Rendering;

			Database.ViewChanged += MRE_Database_ViewChanged;

			#region create callbacks for Cycles

			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
			m_test_cancel_callback = null;
			m_display_update_callback = null;

			CSycles.log_to_stdout(false);

			#endregion
		}
		private void MRE_Database_ViewChanged(object sender, Database.ChangeDatabase.ViewChangedEventArgs e)
		{
			//ViewCrc = e.Crc;
		}

		bool capturing = false;
		public void SetCallbackForCapture()
		{
			capturing = true;
			m_update_render_tile_callback = null;
			m_logger_callback = null;
			m_write_render_tile_callback = null;
		}

		private int maxSamples;
		public int requestedSamples { get; set; }

		/// <summary>
		/// Entry point for a new render process. This is to be done in a separate thread.
		/// </summary>
		public void Renderer()
		{
			var cyclesEngine = this;
			EngineDocumentSettings eds = new EngineDocumentSettings(m_doc_serialnumber);

			var rw = cyclesEngine.RenderWindow;

			if (rw == null) return; // we don't have a window to write to...

			var requestedChannels = rw.GetRequestedRenderChannelsAsStandardChannels();

			List<RenderWindow.StandardChannels> reqChanList = requestedChannels
					.Distinct()
					.Where(chan => chan != RenderWindow.StandardChannels.AlbedoRGB)
					.ToList();
			List<ccl.PassType> reqPassTypes = reqChanList
					.Select(chan => PassTypeForStandardChannel(chan))
					.ToList();

			var client = cyclesEngine.Client;
			var size = cyclesEngine.RenderDimension;

			IAllSettings engineSettings = eds;
			if (!eds.UseDocumentSamples)
			{
				switch (Quality)
				{
					case AntialiasLevel.Draft:
						engineSettings = new DraftPresetEngineSettings(eds);
						break;
					case AntialiasLevel.Good:
						engineSettings = new GoodPresetEngineSettings(eds);
						break;
					case AntialiasLevel.High:
						engineSettings = new FinalPresetEngineSettings(eds);
						break;
					case AntialiasLevel.None:
					default:
						engineSettings = new LowPresetEngineSettings(eds);
						break;
				}
			}
			requestedSamples = Attributes?.RealtimeRenderPasses ?? engineSettings.Samples;
			requestedSamples = (requestedSamples < 1) ? engineSettings.Samples : requestedSamples;

			#region pick a render device
			HandleDeviceAndIntegrator(eds);
			var renderDevice = engineSettings.RenderDevice;

			/*if (engineSettings.Verbose) sdd.WriteLine(
				$"Using device {renderDevice.Name + " " + renderDevice.Description}");*/
			#endregion

			HandleDeviceAndIntegrator(eds);
			maxSamples = requestedSamples;

			#region set up session parameters
			var sessionParams = new SessionParameters(client, renderDevice)
			{
				Experimental = false,
				Samples = requestedSamples,
				TileSize = TileSize(renderDevice),
				TileOrder = TileOrder.Center,
				Threads = (uint)(renderDevice.IsGpu ? 0 : engineSettings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				SkipLinearToSrgbConversion = true,
				DisplayBufferLinear = true,
				Background = false,
				ProgressiveRefine = true,
				Progressive = true,
				PixelSize = 1,
			};
			#endregion

			if (cyclesEngine.CancelRender) return;

			#region create session for scene
			cyclesEngine.Session = RcCore.It.CreateSession(client, sessionParams);
			#endregion

			CreateScene(client, Session, renderDevice, cyclesEngine, engineSettings);

			// Set up passes
			foreach (var reqPass in reqPassTypes)
			{
				Session.AddPass(reqPass);
			}

			// register callbacks before starting any rendering
			cyclesEngine.SetCallbacks();

			// main render loop, including restarts
			#region start the rendering loop, wait for it to complete, we're rendering now!

			if (cyclesEngine.CancelRender) return;

			cyclesEngine.Database?.Flush();
			var rc = cyclesEngine.UploadData();

			if (rc)
			{
				cyclesEngine.Session.PrepareRun();

				long lastUpdate = DateTime.Now.Ticks;
				long curUpdate = DateTime.Now.Ticks; // remember, 10000 ticks in a millisecond
				const long updateInterval = 1000 * 10000;

				// lets first reset session
				int cycles_full_y = FullSize.Height - BufferRectangle.Bottom;
				cyclesEngine.Session.Reset(size.Width, size.Height, requestedSamples, BufferRectangle.X, cycles_full_y, FullSize.Width, FullSize.Height);
				// and actually start
				bool stillrendering = true;
				var throttle = Math.Max(0, engineSettings.ThrottleMs);
				int sample = -1;
				while (stillrendering)
				{
					if (cyclesEngine.IsRendering)
					{
						sample = cyclesEngine.Session.Sample();
						stillrendering = sample > -1;
						curUpdate = DateTime.Now.Ticks;
						if (!capturing && stillrendering && (sample == 0 || (curUpdate - lastUpdate) > updateInterval))
						{
							lastUpdate = curUpdate;
							cyclesEngine.BlitPixelsToRenderWindowChannel();
							cyclesEngine.RenderWindow.Invalidate();
						}
					}
					Thread.Sleep(throttle);
					if (cyclesEngine.IsStopped) break;
				}
				if (!cyclesEngine.CancelRender)
				{
					cyclesEngine.BlitPixelsToRenderWindowChannel();
					cyclesEngine.RenderWindow.Invalidate();
				}

			}
			#endregion

			/*if (engineSettings.SaveDebugImages)
			{
				var tmpf = RenderEngine.TempPathForFile($"RC_modal_renderer.png");
				cyclesEngine.RenderWindow.SaveRenderImageAs(tmpf, true);
			}*/

			// we're done now, so lets clean up our session.
			RcCore.It.ReleaseSession(cyclesEngine.Session);
			cyclesEngine.Database?.Dispose();
			cyclesEngine.Database = null;
			cyclesEngine.State = State.Stopped;

			if (!capturing)
			{
				// set final status string and progress to 1.0f to signal completed render
				cyclesEngine.SetProgress(rw,
					String.Format(Localization.LocalizeString("Render ready {0} samples, duration {1}", 39), cyclesEngine.RenderedSamples + 1, cyclesEngine.TimeString), 1.0f);
				// signal the render window we're done.
				//rw.EndAsyncRender(RenderWindow.RenderSuccessCode.Completed);
			}
			//cyclesEngine.CancelRender = true;
		}

		public bool SupportsPause()
		{
			return true;
		}

		public void ResumeRendering()
		{
			State = State.Rendering;
		}

		public void PauseRendering()
		{
			State = State.Waiting;
		}
	}

}
