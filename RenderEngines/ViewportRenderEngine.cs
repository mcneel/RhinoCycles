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
using System.Drawing.Imaging;
using ccl;
using RhinoCyclesCore;
using Rhino.Display;
using Rhino.Render;
using RhinoCyclesCore.Database;
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

			Database.ViewChanged += Database_ViewChanged;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
			m_test_cancel_callback = null;
			m_display_update_callback = DisplayUpdateHandler;

			CSycles.log_to_stdout(false);
#endregion
			
		}

		public event EventHandler<ChangeDatabase.ViewChangedEventArgs> ViewChanged;

		void Database_ViewChanged(object sender, ChangeDatabase.ViewChangedEventArgs e)
		{
			lock (size_setter_lock)
			{
				if (e.SizeChanged)
				{
					m_setting_size = true;
				}
			}
			ViewCrc = e.Crc;
			var handler = ViewChanged;
			if (handler != null)
			{
				handler(this, e);
			}
		}

		/// <summary>
		/// Event argument for PassRendered. It holds the sample (pass)
		/// that has been completed.
		/// </summary>
		public class PassRenderedEventArgs : EventArgs
		{
			public PassRenderedEventArgs(int sample)
			{
				Sample = sample;
			}

			/// <summary>
			/// The completed sample (pass).
			/// </summary>
			public int Sample { get; private set; }
		}
		/// <summary>
		/// Event that gets fired when the render engine completes handling one
		/// pass (sample) from Cycles.
		/// </summary>
		public event EventHandler<PassRenderedEventArgs> PassRendered;

		private bool m_setting_size;

		public readonly object m_display_lock = new object();
		private bool acquired_display_lock = false;

		public void DisplayUpdateHandler(uint sessionId, int sample)
		{
			// try to get a lock, but don't be too fussed if we don't get it at the first try,
			// just try the next time.
			try
			{
				System.Threading.Monitor.TryEnter(m_display_lock, ref acquired_display_lock);
				if (acquired_display_lock)
				{

					if (CancelRender) return;
					if (m_setting_size) return;
					if (Flush) return;
					if (State != State.Rendering) return;
					lock (size_setter_lock)
					{
						// copy display buffer data into ccycles pixel buffer
						Session.DrawNogl(RenderDimension.Width, RenderDimension.Height);
						// copy stuff into renderwindow dib
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
#if DEBUGxx
						SaveRenderedBuffer(sample);
#endif
						sdd.WriteLine(string.Format("display update, sample {0}", sample));
						// now signal whoever is interested
						var handler = PassRendered;
						if (handler != null)
						{
							handler(this, new PassRenderedEventArgs(sample));
						}
					}
				}
			} finally
			{
				if (acquired_display_lock)
				{
					acquired_display_lock = false;
					System.Threading.Monitor.Exit(m_display_lock);
				}
			}
		}

		private readonly object size_setter_lock = new object();
		/// <summary>
		/// Set new size for the internal RenderWindow object.
		/// </summary>
		/// <param name="w">Width in pixels</param>
		/// <param name="h">Height in pixels</param>
		public void SetRenderSize(int w, int h)
		{
			lock (size_setter_lock)
			{
				RenderWindow.SetSize(new Size(w, h));
				m_setting_size = false;
			}
		}

		public class RenderStartedEventArgs : EventArgs
		{
			public bool Success { get; private set; }

			public RenderStartedEventArgs(bool success)
			{
				Success = success;
			}
		}

		/// <summary>
		/// Event gets fired when the renderer has started.
		/// </summary>
		public event EventHandler<RenderStartedEventArgs> RenderStarted;

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
			if (!cycles_engine.CancelRender)
			{
				cycles_engine.Synchronize();
			}
			if (!cycles_engine.CancelRender)
			{
				cycles_engine.Session.Start();
			}

			#endregion

			var handler = cycles_engine.RenderStarted;
			// We've got Cycles rendering now, notify anyone who cares
			if (handler != null)
			{
				handler(cycles_engine, new RenderStartedEventArgs(!cycles_engine.CancelRender));
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

		public event EventHandler StartSynchronizing;

		public void TriggerStartSynchronizing()
		{
			var handler = StartSynchronizing;
			if (handler != null)
			{
				handler(this, EventArgs.Empty);
			}
		}

		public void Synchronize()
		{
			if (State == State.Uploading)
			{
				TriggerStartSynchronizing();
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

	}

}
