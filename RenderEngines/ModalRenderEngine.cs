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
using System.Drawing;
using System.Threading;
using ccl;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Core;
using sdd = System.Diagnostics.Debug;

namespace RhinoCyclesCore.RenderEngines
{
	public class ModalRenderEngine : RenderEngine
	{

		public ModalRenderEngine(RhinoDoc doc, Guid pluginId, ViewInfo view, ViewportInfo viewport)
			: base(pluginId, doc.RuntimeSerialNumber, view, viewport, false)
		{
			ModalRenderEngineCommonConstruct();
		}

		/// <summary>
		/// Construct a new render engine
		/// </summary>
		/// <param name="doc"></param>
		/// <param name="pluginId">Id of the plugin for which the render engine is created</param>
		public ModalRenderEngine(RhinoDoc doc, Guid pluginId) : base(pluginId, doc.RuntimeSerialNumber, false)
		{
			ModalRenderEngineCommonConstruct();
		}

		private void ModalRenderEngineCommonConstruct()
		{
			Client = new Client();
			State = State.Rendering;

			Database.ViewChanged += MRE_Database_ViewChanged;

			#region create callbacks for Cycles

			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = UpdateRenderTileCallback;
			m_write_render_tile_callback = WriteRenderTileCallback;
			m_test_cancel_callback = null;
			m_display_update_callback = null;

			CSycles.log_to_stdout(false);

			#endregion
		}
		public void DisplayUpdateHandler(uint sessionId, int sample)
		{
			if (Session.IsPaused()) return;
			// after first 10 frames have been rendered only update every third.
			if (sample > 10 && sample < (RcCore.It.EngineSettings.Samples - 2) && sample % 3 != 0) return;
			if (CancelRender) return;
			if (State != State.Rendering) return;
			// copy display buffer data into ccycles pixel buffer
			Session.DrawNogl(RenderDimension.Width, RenderDimension.Height);
			using (var channel = RenderWindow.OpenChannel(RenderWindow.StandardChannels.RGBA))
			{
				if (CancelRender) return;
				if (channel != null)
				{
					if (CancelRender) return;
					var pixelbuffer = new PixelBuffer(CSycles.session_get_buffer(Client.Id, sessionId));
					var size = RenderDimension;
					var rect = new Rectangle(0, 0, RenderDimension.Width, RenderDimension.Height);
					if (CancelRender) return;
					channel.SetValues(rect, size, pixelbuffer);
				}
			}
			SaveRenderedBuffer(sample);

			if (CancelRender || sample >= maxSamples) Session.Cancel("done");

		}

		private void MRE_Database_ViewChanged(object sender, Database.ChangeDatabase.ViewChangedEventArgs e)
		{
			//ViewCrc = e.Crc;
		}

		private int maxSamples;

		/// <summary>
		/// Entry point for a new render process. This is to be done in a separate thread.
		/// </summary>
		public void Renderer()
		{
			var cyclesEngine = this;

			var client = cyclesEngine.Client;
			var rw = cyclesEngine.RenderWindow;

			if (rw == null) return; // we don't have a window to write to...

			var size = cyclesEngine.RenderDimension;
			var samples = RcCore.It.EngineSettings.Samples;
			maxSamples = samples;

			#region pick a render device

			var renderDevice = RcCore.It.EngineSettings.SelectedDevice == -1
				? Device.FirstCuda
				: Device.GetDevice(RcCore.It.EngineSettings.SelectedDevice);

			if (RcCore.It.EngineSettings.Verbose) sdd.WriteLine(
				$"Using device {renderDevice.Name + " " + renderDevice.Description}");
			#endregion

			var scene = CreateScene(client, renderDevice, cyclesEngine);

			#region set up session parameters
			var sessionParams = new SessionParameters(client, renderDevice)
			{
				Experimental = false,
				Samples = samples,
				TileSize = renderDevice.IsCpu ? new Size(32, 32) : new Size(RcCore.It.EngineSettings.TileX, RcCore.It.EngineSettings.TileY),
				TileOrder = TileOrder.Center,
				Threads = (uint)(renderDevice.IsGpu ? 0 : RcCore.It.EngineSettings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				Background = true,
				DisplayBufferLinear = true,
				ProgressiveRefine = true,
				Progressive = true,
			};
			#endregion

			if (cyclesEngine.CancelRender) return;

			#region create session for scene
			cyclesEngine.Session = new Session(client, sessionParams, scene);
			#endregion

			// register callbacks before starting any rendering
			cyclesEngine.SetCallbacks();

			// main render loop, including restarts
			#region start the rendering loop, wait for it to complete, we're rendering now!

			//cyclesEngine.Database.OneShot();
			cyclesEngine.m_flush = false;
			cyclesEngine.UploadData();

			cyclesEngine.Session.PrepareRun();

			// lets first reset session
			cyclesEngine.Session.Reset((uint)size.Width, (uint)size.Height, (uint)samples);
			// then reset scene
			cyclesEngine.Session.Scene.Reset();
			// and actually start
			bool stillrendering = true;
			var throttle = Math.Max(0, RcCore.It.EngineSettings.ThrottleMs);
			while (stillrendering)
			{
				if (cyclesEngine.IsRendering)
				{
					stillrendering = cyclesEngine.Session.Sample();
					Thread.Sleep(throttle);
				}
				else
				{
					Thread.Sleep(100);
				}
				if (cyclesEngine.IsStopped) break;
			}

			cyclesEngine.Session.EndRun();

			cyclesEngine.CancelRender = true;
			#endregion

			if (RcCore.It.EngineSettings.SaveDebugImages)
			{
				var tmpf = $"{Environment.GetEnvironmentVariable("TEMP")}\\RC_modal_renderer.png";
				cyclesEngine.RenderWindow.SaveRenderImageAs(tmpf, true);
			}

			// we're done now, so lets clean up our session.
			cyclesEngine.Session.Destroy();

			cyclesEngine.Database?.Dispose();

			// set final status string and progress to 1.0f to signal completed render
			cyclesEngine.SetProgress(rw,
				$"Render ready {cyclesEngine.RenderedSamples + 1} samples, duration {cyclesEngine.TimeString}", 1.0f);
			cyclesEngine.CancelRender = true;

			// signal the render window we're done.
			rw.EndAsyncRender(RenderWindow.RenderSuccessCode.Completed);
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
