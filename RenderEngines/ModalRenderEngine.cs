/**
Copyright 2014-2024 Robert McNeel and Associates

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

using ccl;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.UI;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RhinoCyclesCore.RenderEngines
{
	public class ModalRenderEngine : RenderEngine
	{
		public ModalRenderEngine(RhinoDoc doc, Guid pluginId, ViewInfo view, bool isProductRender) : this(doc, pluginId, view, null, null, isProductRender) { }
		public ModalRenderEngine(RhinoDoc doc, Guid pluginId, ViewInfo view, ViewportInfo viewport, Rhino.Display.DisplayPipelineAttributes attributes, bool isProductRender)
			: base(pluginId, doc.RuntimeSerialNumber, view, viewport, attributes, false)
		{
			RcCore.It.AddLogString("ModalRenderEngine constructor entry");
			IsProductRender = isProductRender;
			Quality = doc.RenderSettings.AntialiasLevel;
			ModalRenderEngineCommonConstruct();
			RcCore.It.AddLogString("ModalRenderEngine constructor exit");
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
		}

		public void UpdateRenderWindow()
		{
			BlitPixelsToRenderWindowChannel();
			RenderWindow.Invalidate();
		}

		/// <summary>
		/// Entry point for a new render process. This is to be done in a separate thread.
		/// </summary>
		public void Renderer()
		{
			RcCore.It.AddLogString("ModalRenderEngine.Renderer entry");
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

			// Due to code above we will have many duplicate passes. For now, delete duplicates.
			reqPassTypes = reqPassTypes.Distinct().ToList();

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
				UseResolutionDivider = false,
			};
			#endregion

			if (cyclesEngine.ShouldBreak) return;

			#region create session for scene
			RcCore.It.AddLogString("ModalRenderEngine.Renderer CreateSession");
			cyclesEngine.Session = RcCore.It.CreateSession(sessionParams);
			RcCore.It.AddLogString("ModalRenderEngine.Renderer CreateSession done");
			#endregion

			InitializeSceneSettings(cyclesEngine.Session, RenderDevice, cyclesEngine, engineSettings);
			HandleIntegrator(engineSettings);

			// Set up passes
			foreach (var reqPass in reqPassTypes)
			{
				Session.AddPass(reqPass);
			}

			RcCore.It.AddLogString("ModalRenderEngine.Renderer Session.Reset");
			cyclesEngine.Session.Reset(
				width: size.Width,
				height: size.Height,
				samples: MaxSamples,
				full_x: BufferRectangle.X,
				full_y: BufferRectangle.Top,
				full_width: FullSize.Width,
				full_height: FullSize.Height,
				pixel_size: 1);
			RcCore.It.AddLogString("ModalRenderEngine.Renderer Session.Reset done");

			// main render loop, including restarts
			#region start the rendering loop, wait for it to complete, we're rendering now!

			if (cyclesEngine.ShouldBreak)
				return;

			RcCore.It.AddLogString("ModalRenderEngine.Renderer Flush");
			cyclesEngine.Database?.Flush();
			RcCore.It.AddLogString("ModalRenderEngine.Renderer Flush done");
			if(cyclesEngine.ShouldBreak)
				return;
			cyclesEngine.Session.WaitUntilLocked();
			RcCore.It.AddLogString("ModalRenderEngine.Renderer UploadData");
			var renderSuccess = cyclesEngine.UploadData();
			RcCore.It.AddLogString("ModalRenderEngine.Renderer UploadData done");
			cyclesEngine.Session.Unlock();
			cyclesEngine.Database.ResetChangeQueue();

			if (renderSuccess)
			{
				RcCore.It.AddLogString("ModalRenderEngine.Renderer Session.Start");
				cyclesEngine.Session.Start();
				RcCore.It.AddLogString("ModalRenderEngine.Renderer Session.Start done");

				var throttle = Math.Max(0, engineSettings.ThrottleMs);
				int lastRenderedSample = 0;
				int lastRenderedTiles = 0;

				while (!Finished)
				{
					UpdateCallback(cyclesEngine.Session.Id);

					if (RenderedSamples == -13)
					{
						renderSuccess = false;
						Finished = true;
						cyclesEngine.CancelRender = true;
					}
					else if (!capturing && !Finished && (RenderedSamples > lastRenderedSample || RenderedTiles > lastRenderedTiles))
					{
						if(RenderedTiles > lastRenderedTiles && lastRenderedTiles >= 0)
						{
							RenderedSamples = 0;
						}

						lastRenderedSample = RenderedSamples;
						lastRenderedTiles = RenderedTiles;

						cyclesEngine.UpdateRenderWindow();
					}


					Thread.Sleep(throttle);

					if (cyclesEngine.ShouldBreak)
						break;
				}

				if (!cyclesEngine.ShouldBreak)
				{
					cyclesEngine.UpdateRenderWindow();
				}
			}
			#endregion
				RcCore.It.AddLogString("ModalRenderEngine.Renderer Stopping");

			while(State == State.Stopping) {
				Thread.Sleep(10);
			}

			RcCore.It.AddLogString("ModalRenderEngine.Renderer StopTheRenderer");
			cyclesEngine.StopTheRenderer();
			RcCore.It.AddLogString("ModalRenderEngine.Renderer StopTheRenderer done");

			// we're done now, so lets clean up our session.
			RcCore.It.AddLogString("ModalRenderEngine.Renderer Database.Dispose");
			cyclesEngine.Database?.Dispose();
			RcCore.It.AddLogString("ModalRenderEngine.Renderer Database.Dispose done");
			cyclesEngine.Database = null;
			cyclesEngine.State = State.Stopped;

			if (!capturing && renderSuccess)
			{
				// set final status string and progress to 1.0f to signal completed render
				cyclesEngine.SetProgress(rw,
					String.Format(Localization.LocalizeString("Render ready {0} samples, duration {1}", 39), cyclesEngine.RenderedSamples, cyclesEngine.TimeString), 1.0f);
			}

			cyclesEngine.CancelRender = true;

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
			RcCore.It.AddLogString("ModalRenderEngine.Renderer exiting");
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
