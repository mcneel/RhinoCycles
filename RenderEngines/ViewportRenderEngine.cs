/**
Copyright 2014-2015 Robert McNeel and Associates

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
using Rhino.Display;
using Rhino.Render;
using sdd = System.Diagnostics.Debug;

namespace RhinoCycles
{
	public delegate void RenderSizeUnsetHandler(object sender, EventArgs e);

	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(RhinoDoc doc, Guid pluginId, RhinoView view)
		{
			m_plugin_id = pluginId;
			m_doc_serialnumber = doc.RuntimeSerialNumber;
			m_view = view;
			if (doc != null)
			{
				Database = new ChangeDatabase(pluginId, this, m_doc_serialnumber, view);
			}
			RenderThread = null;
			Client = new Client();
			State = State.Rendering;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = UpdateRenderTileCallback;
			m_write_render_tile_callback = WriteRenderTileCallback;
			m_test_cancel_callback = null;

			CSycles.log_to_stdout(false);
#endregion
			
		}


		private bool m_size_set;

		private bool IsRenderSizeSet
		{
			get
			{
				lock (size_setter_lock)
				{
					return m_size_set;
				}
			}
		}

		private readonly object size_setter_lock = new object();
		public void SetRenderSize(int w, int h)
		{
			lock (size_setter_lock)
			{
				m_size_set = false;
				RenderWindow.SetSize(new Size(w, h));
				m_size_set = true;
			}
		}

		public event RenderSizeUnsetHandler RenderSizeUnset;


		public void UnsetRenderSize()
		{
			m_size_set = false;
			if (RenderSizeUnset != null)
			{
				RenderSizeUnset(this, new EventArgs());
			}
		}
		/// <summary>
		/// Entry point for viewport interactive rendering
		/// </summary>
		/// <param name="oPipe"></param>
		public static void Renderer(object oPipe)
		{
			var cycles_engine = (ViewportRenderEngine)oPipe;

			var client = cycles_engine.Client;
			var rw = cycles_engine.RenderWindow;

			if (rw == null) return;

			var size = cycles_engine.RenderDimension;
			var samples = cycles_engine.Settings.Samples;

			cycles_engine.m_measurements.Reset();

			#region pick a render device

			var render_device = cycles_engine.Settings.SelectedDevice == -1
				? Device.FirstCuda
				: Device.GetDevice(cycles_engine.Settings.SelectedDevice);

			if (cycles_engine.Settings.Verbose) sdd.WriteLine(String.Format("Using device {0}", render_device.Name + " " + render_device.Description));
			#endregion

			var scene = CreateScene(client, render_device, cycles_engine);


			#region set up session parameters
			var session_params = new SessionParameters(client, render_device)
			{
				Experimental = false,
				Samples = samples,
				TileSize = render_device.IsCuda ? new Size(256, 256) : new Size(32, 32),
				Threads = (uint)(render_device.IsCuda ? 0 : cycles_engine.Settings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				Background = true,
				ProgressiveRefine = true, //rw != null,
				Progressive = true, //rw != null,
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

			cycles_engine.Database.Flush();
			cycles_engine.State = State.Uploading;
			while (cycles_engine.State != State.Stopped)
			{
				if (cycles_engine.State == State.Waiting)
				{
					Thread.Sleep(5);
					if(cycles_engine.Flush && cycles_engine.Database!=null) cycles_engine.Database.Flush();
					if(cycles_engine.Database!=null && cycles_engine.Database.HasChanges()) cycles_engine.State = State.Uploading;
				}

				cycles_engine.RenderedSamples = 0;
				cycles_engine.TimeString = "";
				// engine is ready to upload, do so
				if (cycles_engine.State == State.Uploading)
				{
					cycles_engine.UploadData();
					cycles_engine.State = State.Rendering;
					// uploading done, rendering again
				}

				if (cycles_engine.State != State.Rendering) continue;

				while (!cycles_engine.IsRenderSizeSet)
				{
					Thread.Sleep(10);
				}

				size = cycles_engine.RenderDimension;
				//cycles_engine.RenderWindow.SetSize(size);

				// lets first reset session
				cycles_engine.Session.Reset((uint)size.Width, (uint)size.Height, (uint)samples);
				// then reset scene
				cycles_engine.Session.Scene.Reset();
				// and actually start
				// we're rendering again
				cycles_engine.Session.Start();
				// ... aaaaand we wait
				cycles_engine.Session.Wait();

				if (cycles_engine.State != State.Uploading)
				{
					cycles_engine.State = State.Waiting;
				}
			}

			#endregion

			// we're done now, so lets clean up our session.
			cycles_engine.Session.Destroy();
		}
	}

}
