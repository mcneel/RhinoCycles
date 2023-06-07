/**
Copyright 2014-2021 Robert McNeel and Associates

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
		public bool FastPreview { get; set; } = false;

		private void ModalRenderEngineCommonConstruct()
		{
			//Client = new Client();
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

			var size = cyclesEngine.RenderDimension;

			IAllSettings engineSettings = eds;

			#region pick a render device
			HandleDevice(eds);
			if (FastPreview)
			{
				engineSettings = new FastPreviewEngineSettings(eds);
			}
			else
			{
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
			}
			MaxSamples = Attributes?.RealtimeRenderPasses ?? engineSettings.Samples;
			MaxSamples = (MaxSamples < 1) ? engineSettings.Samples : MaxSamples;
			(bool isReady, Device possibleRenderDevice) = RcCore.It.IsDeviceReady(RenderDevice);
			RenderDevice = possibleRenderDevice;
			IsFallbackRenderDevice = !isReady;

			/*if (engineSettings.Verbose) sdd.WriteLine(
				$"Using device {RenderDevice.UiName + " " + RenderDevice.Description}");*/
			#endregion

			#region set up session parameters
			var sessionParams = new SessionParameters(RenderDevice)
			{
				Experimental = false,
				Samples = MaxSamples,
				TileSize = TileSize(RenderDevice),
				Threads = (uint)(RenderDevice.IsGpu ? 0 : engineSettings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				Background = false,
				PixelSize = 1,
			};
			#endregion

			if (cyclesEngine.CancelRender) return;

			#region create session for scene
			cyclesEngine.Session = RcCore.It.CreateSession(sessionParams);
			#endregion

			// TODO: XXXX fix up scene creation InitializeSceneSettings(client, Session, RenderDevice, cyclesEngine, engineSettings);
			HandleIntegrator(eds);

			// Set up passes
			foreach (var reqPass in reqPassTypes)
			{
				Session.AddPass(reqPass);
			}

			// register callbacks before starting any rendering
			cyclesEngine.SetCallbacks();

			// main render loop, including restarts
			#region start the rendering loop, wait for it to complete, we're rendering now!

			if (cyclesEngine.CancelRender)
				return;

			cyclesEngine.Database?.Flush();
			var renderSuccess = cyclesEngine.UploadData();

			if (renderSuccess)
			{
				cyclesEngine.Session.Reset(size.Width, size.Height, MaxSamples, BufferRectangle.X, BufferRectangle.Top, FullSize.Width, FullSize.Height);
				cyclesEngine.Session.Start();

				var throttle = Math.Max(0, engineSettings.ThrottleMs);
				int lastRenderedSample = -1;

				while (!Finished)
				{
					UpdateCallback(cyclesEngine.Session.Id);

					if (RenderedSamples == -13)
					{
						renderSuccess = false;
						Finished = true;
						cyclesEngine.CancelRender = true;
					}
					else if (!capturing && !Finished && RenderedSamples > lastRenderedSample)
					{
						lastRenderedSample = RenderedSamples;

						cyclesEngine.BlitPixelsToRenderWindowChannel();
						cyclesEngine.RenderWindow.Invalidate();
						cyclesEngine.Database.ResetChangeQueue();
					}

					Thread.Sleep(throttle);

					if (cyclesEngine.IsStopped)
						break;
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
			cyclesEngine?.Database.ResetChangeQueue();

			// we're done now, so lets clean up our session.
			RcCore.It.ReleaseSession(cyclesEngine.Session);
			cyclesEngine.Database?.Dispose();
			cyclesEngine.Database = null;
			cyclesEngine.State = State.Stopped;

			if (!capturing && renderSuccess)
			{
				// set final status string and progress to 1.0f to signal completed render
				cyclesEngine.SetProgress(rw,
					String.Format(Localization.LocalizeString("Render ready {0} samples, duration {1}", 39), cyclesEngine.RenderedSamples + 1, cyclesEngine.TimeString), 1.0f);
			}

			if (!renderSuccess)
			{
				rw.SetProgress(Localization.LocalizeString("An error occured while trying to render. The render may be incomplete or not started.", 65), 1.0f);
				Action showErrorDialog = () =>
				{
				CrashReporterDialog dlg = new CrashReporterDialog(Localization.LocalizeString("Error while rendering", 66), Localization.LocalizeString(
@"An error was detected while rendering with Rhino Render.

If there is a result visible you can save it still.

Please click the link below for more information.", 67));
					dlg.ShowModal(RhinoEtoApp.MainWindow);
				};
				RhinoApp.InvokeOnUiThread(showErrorDialog);
			}
			cyclesEngine.CancelRender = true;
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


		private bool isDisposed = false;
		public override void Dispose(bool isDisposing)
		{
			if(isDisposing) {
				if(!isDisposed) {
					isDisposed = true;
				}
			}
			base.Dispose(isDisposing);
		}
	}

}
