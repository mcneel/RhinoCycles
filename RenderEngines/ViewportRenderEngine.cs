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
using System.Threading;
using ccl;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Database;
using sdd = System.Diagnostics.Debug;

namespace RhinoCyclesCore.RenderEngines
{
	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(uint docRuntimeSerialNumber, Guid pluginId, ViewInfo view) : base(pluginId, docRuntimeSerialNumber, view, null, true)
		{
			Client = new Client();
			State = State.Rendering;

			Database.ViewChanged += Database_ViewChanged;

			ChangesReady += ViewportRenderEngine_ChangesReady;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
			m_test_cancel_callback = null;
			m_display_update_callback = DisplayUpdateHandler;

			CSycles.log_to_stdout(false);
#endregion
			
		}


		private bool _disposed;
		protected override void Dispose(bool isDisposing)
		{
			lock (DisplayLock)
			{
				if (_disposed) return;

				Database?.Dispose();
				Client?.Dispose();
				base.Dispose(isDisposing);
				_disposed = true;
			}
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
			if (_syncing) return;
			// after first 10 frames have been rendered only update every third.
			if (sample > 10 && sample < (Settings.Samples-2) && sample % 3 != 0) return;
			if (CancelRender) return;
			if (State != State.Rendering) return;
			lock (DisplayLock)
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
			lock (DisplayLock)
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
			var cyclesEngine = this;

			var client = cyclesEngine.Client;
			var rw = cyclesEngine.RenderWindow;

			if (rw == null) return;

			var samples = cyclesEngine.Settings.Samples;

			#region pick a render device

			var renderDevice = cyclesEngine.Settings.SelectedDevice == -1
				? Device.FirstCuda
				: Device.GetDevice(cyclesEngine.Settings.SelectedDevice);

			#endregion

			var scene = CreateScene(client, renderDevice, cyclesEngine);


			#region set up session parameters
			var sessionParams = new SessionParameters(client, renderDevice)
			{
				Experimental = false,
				Samples = samples,
				TileSize = renderDevice.IsCpu ? new Size(32, 32) : new Size(256, 256),
				TileOrder = TileOrder.Center,
				Threads = (uint)(renderDevice.IsCpu ? cyclesEngine.Settings.Threads : 0),
				ShadingSystem = ShadingSystem.SVM,
				StartResolution = renderDevice.IsCpu ? 16 : 64,
				SkipLinearToSrgbConversion = true,
				DisplayBufferLinear = true,
				Background = false,
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

			cyclesEngine.CheckFlushQueue();
			if (!cyclesEngine.CancelRender)
			{
				cyclesEngine.Synchronize();
			}
			if (!cyclesEngine.CancelRender)
			{
				cyclesEngine.Session.Start();
			}

			#endregion

			// We've got Cycles rendering now, notify anyone who cares
			cyclesEngine.RenderStarted?.Invoke(cyclesEngine, new RenderStartedEventArgs(!cyclesEngine.CancelRender));

			while (!IsStopped)
			{
				Thread.Sleep(10);
				if(Flush)
					TriggerChangesReady();
			}
		}

		public event EventHandler Synchronized;

		public void TriggerSynchronized()
		{
			lock (_syncLock)
			{
				Synchronized?.Invoke(this, EventArgs.Empty);
				_syncing = false;
			}
		}

		private readonly object _syncLock = new object();
		private bool _syncing;
		public event EventHandler StartSynchronizing;

		public void TriggerStartSynchronizing()
		{
			lock (_syncLock)
			{
				_syncing = true;
				StartSynchronizing?.Invoke(this, EventArgs.Empty);
			}
		}

		public void Synchronize()
		{
			if (Session != null && State == State.Uploading)
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

					m_flush = false;
				}

				if (CancelRender)
				{
					State = State.Stopped;
				}
				// unpause
				Session.SetPause(false);
				TriggerSynchronized();
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
