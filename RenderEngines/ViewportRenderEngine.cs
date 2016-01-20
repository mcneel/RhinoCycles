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
using System.Drawing.Imaging;
using ccl;
using Rhino.Display;
using Rhino.Render;
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
			m_display_update_callback = DisplayUpdateHandler;

			CSycles.log_to_stdout(false);
#endregion
			
		}

		public class PassRenderedEventArgs : EventArgs
		{
			public PassRenderedEventArgs(int sample)
			{
				Sample = sample;
			}

			public int Sample { get; private set; }
		}
		public event EventHandler<PassRenderedEventArgs> PassRendered;

		public void DisplayUpdateHandler(uint sessionId, int sample)
		{
			if (CancelRender) return;
			if (m_setting_size) return;
			if (Flush) return;
			lock (size_setter_lock)
			{
				// copy display buffer data into ccycles pixel buffer
				Session.DrawNogl(RenderDimension.Width, RenderDimension.Height);
				// copy stuff into renderwindow dib
				using (var channel = RenderWindow.OpenChannel(RenderWindow.StandardChannels.RGBA))
				{
					if (channel != null)
					{
						var pixelbuffer = new PixelBuffer(CSycles.session_get_buffer(Client.Id, sessionId));
						var size = RenderDimension;
						var rect = new Rectangle(0, 0, RenderDimension.Width, RenderDimension.Height);
						channel.SetValues(rect, size, pixelbuffer);
					}
				}
				SaveRenderedBuffer(sample);
				sdd.WriteLine(string.Format("display update, sample {0}", sample));
				// now signal whoever is interested
				var handler = PassRendered;
				if (handler != null)
				{
					handler(this, new PassRenderedEventArgs(sample));
				}
			}
		}

		private readonly object size_setter_lock = new object();
		public void SetRenderSize(int w, int h)
		{
			lock (size_setter_lock)
			{
				RenderWindow.SetSize(new Size(w, h));
				m_setting_size = false;
			}
		}

		private bool m_setting_size = false;
		public event EventHandler RenderSizeUnset;
		public void UnsetRenderSize()
		{
			lock (size_setter_lock)
			{
				m_setting_size = true;
				var handler = RenderSizeUnset;
				if (handler != null)
				{
					handler(this, EventArgs.Empty);
				}
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
				TileSize = render_device.IsCpu? new Size(32, 32) : new Size(256, 256),
				Threads = (uint)(render_device.IsCpu ?  cycles_engine.Settings.Threads : 0),
				ShadingSystem = ShadingSystem.SVM,
				StartResolution = 128,
				SkipLinearToSrgbConversion = true,
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

		public event EventHandler Synchronized;

		public void TriggerSynchronized()
		{
			var handler = Synchronized;
			if (handler != null)
			{
				handler(this, EventArgs.Empty);
			}
		}

		public uint ViewCrc { get; set; }

		public void Synchronize()
		{
			if (State == State.Uploading)
			{
				Session.SetPause(true);
				if (UploadData())
				{
					State = State.Rendering;
					var size = RenderDimension;

					// lets first reset session
					Session.Reset((uint) size.Width, (uint) size.Height, (uint) Settings.Samples);
					// then reset scene
					Session.Scene.Reset();

					TriggerSynchronized();
				}

				if (CancelRender)
				{
					State = State.Stopped;
				}
				// unpause
				Session.SetPause(false);
			}
		}

		public void ChangeSamples(int samples)
		{
			Settings.Samples = samples;
			Session.SetSamples(samples);
			Session.SetPause(false);
		}

		public void SaveRenderedBuffer(int sample)
		{
			var tmpf = string.Format("{0}\\RC_viewport_renderer_{1}.png", Environment.GetEnvironmentVariable("TEMP"), sample.ToString("D5"));
			RenderWindow.SaveDibAsBitmap(tmpf);
			/*var bmp = RenderWindow.GetBitmap();
			bmp.Save(tmpf, ImageFormat.Png);*/
		}
	}

}
