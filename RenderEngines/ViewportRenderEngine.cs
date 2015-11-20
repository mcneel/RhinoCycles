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
using ccl;
using Rhino;
using Rhino.Display;
using sdd = System.Diagnostics.Debug;

namespace RhinoCycles
{
	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(uint docRuntimeSerialNumber, Guid pluginId, RhinoView view) : base(pluginId, docRuntimeSerialNumber, view, true)
		{
			RenderThread = null;
			Client = new Client();
			State = State.Rendering;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
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

		public event EventHandler RenderSizeUnset;

		public void UnsetRenderSize()
		{
			m_size_set = false;
			var handler = RenderSizeUnset;
			if (handler != null)
			{
				handler(this, new EventArgs());
			}
		}

		/// <summary>
		/// Event gets fired when the renderer has started.
		/// </summary>
		public event EventHandler RenderStarted;

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
				StartResolution = 128,
				Background = false,
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

			cycles_engine.CheckFlushQueue();
			cycles_engine.Synchronize();
			cycles_engine.Session.Start();

			#endregion

			var handler = cycles_engine.RenderStarted;
			// We've got Cycles rendering now, notify anyone who cares
			if (handler != null)
			{
				handler(cycles_engine, EventArgs.Empty);
			}

		}

		public void Synchronize()
		{

			if (State == State.Uploading)
			{
				Session.SetPause(true);
				UploadData();
				State = State.Rendering;
				var size = RenderDimension;

				// lets first reset session
				Session.Reset((uint)size.Width, (uint)size.Height, (uint)Settings.Samples);
				// then reset scene
				Session.Scene.Reset();
				// unpause
				Session.SetPause(false);
			}
		}
	}

}
