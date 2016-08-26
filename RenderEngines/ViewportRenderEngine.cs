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
using Rhino.DocObjects;

namespace RhinoCycles
{
	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(uint docRuntimeSerialNumber, Guid pluginId, Rhino.DocObjects.ViewInfo view) : base(pluginId, docRuntimeSerialNumber, view, null, true)
		{
			Client = new Client();
			State = State.Rendering;

			Database.ViewChanged += Database_ViewChanged;

			this.ChangesReady += ViewportRenderEngine_ChangesReady;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
			m_test_cancel_callback = null;
			m_display_update_callback = DisplayUpdateHandler;

			CSycles.log_to_stdout(false);
#endregion
			
		}

		private void ViewportRenderEngine_ChangesReady(object sender, EventArgs e)
		{
			CheckFlushQueue();
			Synchronize();
		}

		void Database_ViewChanged(object sender, ChangeDatabase.ViewChangedEventArgs e)
		{
			if (e.SizeChanged) SetRenderSize(e.NewSize.Width, e.NewSize.Height);
			View = e.View;
		}

		/// <summary>
		/// Event argument for PassRendered. It holds the sample (pass)
		/// that has been completed.
		/// </summary>
		public class PassRenderedEventArgs : EventArgs
		{
			public PassRenderedEventArgs(int sample, ViewInfo view)
			{
				Sample = sample;
				View = view;
			}

			/// <summary>
			/// The completed sample (pass).
			/// </summary>
			public int Sample { get; private set; }

			public ViewInfo View { get; private set; }
		}
		/// <summary>
		/// Event that gets fired when the render engine completes handling one
		/// pass (sample) from Cycles.
		/// </summary>
		public event EventHandler<PassRenderedEventArgs> PassRendered;

		public void DisplayUpdateHandler(uint sessionId, int sample)
		{
			if (Session.IsPaused()) return;
			// after first 10 frames have been rendered only update every third.
			if (sample > 10 && sample < (Settings.Samples-2) && sample % 3 != 0) return;
			if (CancelRender) return;
			if (Flush) return;
			if (State != State.Rendering) return;
			lock (display_lock)
			{
				if (Session.Scene.TryLock())
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
					PassRendered?.Invoke(this, new PassRenderedEventArgs(sample, View));
					Session.Scene.Unlock();
					//sdd.WriteLine(string.Format("display update, sample {0}", sample));
					// now signal whoever is interested
				}
			}
		}

		/// <summary>
		/// Set new size for the internal RenderWindow object.
		/// </summary>
		/// <param name="w">Width in pixels</param>
		/// <param name="h">Height in pixels</param>
		public void SetRenderSize(int w, int h)
		{
			lock (display_lock)
			{
				RenderWindow.SetSize(new Size(w, h));
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
		public void Renderer()
		{
			var cycles_engine = this;

			var client = cycles_engine.Client;
			var rw = cycles_engine.RenderWindow;

			if (rw == null) return;

			var samples = cycles_engine.Settings.Samples;

			#region pick a render device

			var render_device = cycles_engine.Settings.SelectedDevice == -1
				? Device.FirstCuda
				: Device.GetDevice(cycles_engine.Settings.SelectedDevice);

			if (cycles_engine.Settings.Verbose) sdd.WriteLine($"Using device {render_device.Name} {render_device.Description}");
			#endregion

			var scene = CreateScene(client, render_device, cycles_engine);


			#region set up session parameters
			var session_params = new SessionParameters(client, render_device)
			{
				Experimental = false,
				Samples = samples,
				TileSize = render_device.IsCpu ? new Size(32, 32) : new Size(256, 256),
				TileOrder = TileOrder.Center,
				Threads = (uint)(render_device.IsCpu ? cycles_engine.Settings.Threads : 0),
				ShadingSystem = ShadingSystem.SVM,
				StartResolution = render_device.IsCpu ? 16 : 64,
				SkipLinearToSrgbConversion = true,
				Background = false,
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
			Synchronized?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler StartSynchronizing;

		public void TriggerStartSynchronizing()
		{
			StartSynchronizing?.Invoke(this, EventArgs.Empty);
		}

		public void Synchronize()
		{
			if (Session != null && State == State.Uploading)
			{
				TriggerStartSynchronizing();
				Session.SetPause(true);
				Session.Scene.Lock();
				if (UploadData())
				{
					State = State.Rendering;
					var size = RenderDimension;
					Session.Scene.Unlock();

					// lets first reset session
					Session.Reset((uint) size.Width, (uint) size.Height, (uint) Settings.Samples);
					// then reset scene
					Session.Scene.Reset();

					m_flush = false;
					TriggerSynchronized();
				} else
				{
					Session.Scene.Unlock();
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
			Session?.SetSamples(samples);
			Session?.SetPause(false);
		}

	}

}
