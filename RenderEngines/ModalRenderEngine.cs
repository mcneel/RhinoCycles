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
using RhinoCyclesCore;
using Rhino;
using Rhino.DocObjects;
using Rhino.Render;
using sdd = System.Diagnostics.Debug;

namespace RhinoCycles
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

		private void MRE_Database_ViewChanged(object sender, RhinoCyclesCore.Database.ChangeDatabase.ViewChangedEventArgs e)
		{
			//ViewCrc = e.Crc;
		}

		/// <summary>
		/// Entry point for a new render process. This is to be done in a separate thread.
		/// </summary>
		public void Renderer()
		{
			var cycles_engine = this;

			var client = cycles_engine.Client;
			var rw = cycles_engine.RenderWindow;

			if (rw == null) return; // we don't have a window to write to...

			var size = cycles_engine.RenderDimension;
			var samples = cycles_engine.Settings.Samples;

			#region pick a render device

			var render_device = cycles_engine.Settings.SelectedDevice == -1
				? Device.FirstGpu
				: Device.GetDevice(cycles_engine.Settings.SelectedDevice);

			if (cycles_engine.Settings.Verbose) sdd.WriteLine(String.Format("Using device {0}", render_device.Name + " " + render_device.Description));
			#endregion

			var scene = CreateScene(client, render_device, cycles_engine);

			#region set up session parameters
			var session_params = new SessionParameters(client, render_device)
			{
				Experimental = false,
				Samples = samples,
				TileSize = render_device.IsGpu ? new Size(256, 256) : new Size(32, 32),
				TileOrder = TileOrder.HilbertSpiral,
				Threads = (uint)(render_device.IsGpu ? 0 : cycles_engine.Settings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				Background = true,
				ProgressiveRefine = true,
				Progressive = true,
			};
			#endregion

			if (cycles_engine.CancelRender) return;

			#region create session for scene
			cycles_engine.Session = new Session(client, session_params, scene);
			#endregion

			// register callbacks before starting any rendering
			cycles_engine.SetCallbacks();

			// main render loop, including restarts
			#region start the rendering thread, wait for it to complete, we're rendering now!

			cycles_engine.Database.OneShot();
			cycles_engine.m_flush = false;
			cycles_engine.UploadData();

			// lets first reset session
			cycles_engine.Session.Reset((uint)size.Width, (uint)size.Height, (uint)samples);
			// then reset scene
			cycles_engine.Session.Scene.Reset();
			// and actually start
			// we're rendering again
			cycles_engine.Session.Start();
			// ... aaaaand we wait
			cycles_engine.Session.Wait();

			cycles_engine.CancelRender = true;
			#endregion

#if DEBUG
			SaveRenderedBufferAsImage(client, cycles_engine, size, "RC_modal_renderer");
#endif

			// we're done now, so lets clean up our session.
			cycles_engine.Session.Destroy();

			// set final status string and progress to 1.0f to signal completed render
			cycles_engine.SetProgress(rw, String.Format("Render ready {0} samples, duration {1}", cycles_engine.RenderedSamples+1, cycles_engine.TimeString), 1.0f);
			cycles_engine.CancelRender = true;

			// signal the render window we're done.
			rw.EndAsyncRender(RenderWindow.RenderSuccessCode.Completed);
		}

	}

}
