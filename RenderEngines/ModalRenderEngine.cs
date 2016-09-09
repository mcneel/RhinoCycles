/**
Copyright 2014-2016 Robert McNeel and Associates

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
using ccl;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
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

			CSycles.log_to_stdout(false);

			#endregion
		}

		private void MRE_Database_ViewChanged(object sender, Database.ChangeDatabase.ViewChangedEventArgs e)
		{
			//ViewCrc = e.Crc;
		}

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
			var samples = cyclesEngine.Settings.Samples;

			#region pick a render device

			var renderDevice = cyclesEngine.Settings.SelectedDevice == -1
				? Device.FirstGpu
				: Device.GetDevice(cyclesEngine.Settings.SelectedDevice);

			if (cyclesEngine.Settings.Verbose) sdd.WriteLine(
				$"Using device {renderDevice.Name + " " + renderDevice.Description}");
			#endregion

			var scene = CreateScene(client, renderDevice, cyclesEngine);

			#region set up session parameters
			var sessionParams = new SessionParameters(client, renderDevice)
			{
				Experimental = false,
				Samples = samples,
				TileSize = renderDevice.IsGpu ? new Size(256, 256) : new Size(32, 32),
				TileOrder = TileOrder.HilbertSpiral,
				Threads = (uint)(renderDevice.IsGpu ? 0 : cyclesEngine.Settings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				Background = true,
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
			#region start the rendering thread, wait for it to complete, we're rendering now!

			cyclesEngine.Database.OneShot();
			cyclesEngine.m_flush = false;
			cyclesEngine.UploadData();

			// lets first reset session
			cyclesEngine.Session.Reset((uint)size.Width, (uint)size.Height, (uint)samples);
			// then reset scene
			cyclesEngine.Session.Scene.Reset();
			// and actually start
			// we're rendering again
			cyclesEngine.Session.Start();
			// ... aaaaand we wait
			cyclesEngine.Session.Wait();

			cyclesEngine.CancelRender = true;
			#endregion

#if DEBUG
			SaveRenderedBufferAsImage(client, cyclesEngine, size, "RC_modal_renderer");
#endif

			// we're done now, so lets clean up our session.
			cyclesEngine.Session.Destroy();

			// set final status string and progress to 1.0f to signal completed render
			cyclesEngine.SetProgress(rw,
				$"Render ready {cyclesEngine.RenderedSamples + 1} samples, duration {cyclesEngine.TimeString}", 1.0f);
			cyclesEngine.CancelRender = true;

			// signal the render window we're done.
			rw.EndAsyncRender(RenderWindow.RenderSuccessCode.Completed);
		}

	}

}
