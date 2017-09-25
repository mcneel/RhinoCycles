//#define YES
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
using Rhino.DocObjects;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Database;
using Rhino;
using System.Collections.Generic;

namespace RhinoCyclesCore.RenderEngines
{
	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(uint docRuntimeSerialNumber, Guid pluginId, ViewInfo view, Rhino.Display.DisplayPipelineAttributes attr) : base(pluginId, docRuntimeSerialNumber, view, null, attr, true)
		{
			Client = new Client();
			State = State.Rendering;

			Database.ViewChanged += Database_ViewChanged;

			ChangesReady += ViewportRenderEngine_ChangesReady;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
			if(!RcCore.It.CanUseDrawOpenGl())
				m_write_render_tile_callback = WriteRenderTileCallback;
			m_test_cancel_callback = null;
			m_display_update_callback = null;
			m_logger_callback = ViewportLoggerCallback;

			CSycles.log_to_stdout(false);
			//CSycles.set_logger(Client.Id, m_logger_callback);
#endregion
			
		}

		public void ViewportLoggerCallback(string msg) {
			RcCore.OutputDebugString($"{msg}\n");
		}


		private bool _disposed;
		protected override void Dispose(bool isDisposing)
		{
			if (_disposed) return;

			Database?.Dispose();
			Client?.Dispose();
			base.Dispose(isDisposing);
			_disposed = true;
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

		public void DrawOpenGl(float alpha)
		{
			var width = RenderDimension.Width;
			var height = RenderDimension.Height;
			Session.RhinoDraw(width, height, alpha);
			
		}

		/// <summary>
		/// Set new size for the internal RenderWindow object.
		/// </summary>
		/// <param name="w">Width in pixels</param>
		/// <param name="h">Height in pixels</param>
		public void SetRenderSize(int w, int h)
		{
			RenderWindow?.SetSize(new Size(w, h));
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

		public bool Locked { get; set; }

		/// <summary>
		/// Entry point for viewport interactive rendering
		/// </summary>
		public void Renderer()
		{
			var cyclesEngine = this;
			Locked = false;

			var vi = new ViewInfo(RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport);
			var vpi = vi.Viewport;

			var client = cyclesEngine.Client;
			var rw = cyclesEngine.RenderWindow;

			if (rw == null) return;

			_throttle = RcCore.It.EngineSettings.ThrottleMs;
			_samples = Attributes?.RealtimeRenderPasses ?? RcCore.It.EngineSettings.Samples;

			#region pick a render device

#if YES
			var rd0 = Device.GetDevice(0);
			var rd1 = Device.GetDevice(1);
			var rd2 = Device.GetDevice(2);
			var rd3 = Device.GetDevice(3);
			var rd4 = Device.GetDevice(4);
			var rdlist = new List<Device>();
			//rdlist.Add(rd0);
			rdlist.Add(rd1);
			rdlist.Add(rd2);
			//rdlist.Add(rd3);
			//rdlist.Add(rd4);

			var renderDevice = Device.CreateMultiDevice(rdlist);

#else
			TriggerCurrentViewportSettingsRequested();
			var renderDevice = RenderDevice; // RcCore.It.EngineSettings.RenderDevice;
#endif

			#endregion

			var scene = CreateScene(client, renderDevice, cyclesEngine);

			#region set up session parameters
			var sessionParams = new SessionParameters(client, renderDevice)
			{
				Experimental = false,
				Samples = (int)_samples,
				TileSize = renderDevice.IsCpu ? new Size(32, 32) : new Size(RcCore.It.EngineSettings.TileX, RcCore.It.EngineSettings.TileY),
				TileOrder = TileOrder.Center,
				Threads = (uint)(renderDevice.IsCpu ? RcCore.It.EngineSettings.Threads : 0),
				ShadingSystem = ShadingSystem.SVM,
				SkipLinearToSrgbConversion = true,
				DisplayBufferLinear = true,
				Background = !RcCore.It.CanUseDrawOpenGl(),
				ProgressiveRefine = true,
				Progressive = true,
				PixelSize = RenderEngine.OnDpi ? RcCore.It.EngineSettings.PixelSize : 1,
			};
			if(RcCore.It.CanUseDrawOpenGl()) sessionParams.StartResolution = RcCore.It.EngineSettings.StartResolution;
			#endregion

			if (cyclesEngine.CancelRender) return;

			#region create session for scene
			cyclesEngine.Session = new Session(client, sessionParams, scene);
			#endregion

			TriggerCurrentViewportSettingsRequested();

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
				cyclesEngine.Session.PrepareRun();
			}

			#endregion

			// We've got Cycles rendering now, notify anyone who cares
			cyclesEngine.RenderStarted?.Invoke(cyclesEngine, new RenderStartedEventArgs(!cyclesEngine.CancelRender));

			while (!IsStopped)
			{
				if(_needReset) {
					_needReset = false;
					var size = RenderDimension;

					Session.Scene.Integrator.NoShadows = RcCore.It.EngineSettings.NoShadows;
					Session.Scene.Integrator.TagForUpdate();

					// lets first reset session
					Session.Reset(size.Width, size.Height, _samples);
					// then reset scene
					Session.Scene.Reset();
				}
				if(cyclesEngine.IsRendering && cyclesEngine.Session.Sample()) {
					cyclesEngine.PassRendered?.Invoke(cyclesEngine, new PassRenderedEventArgs(-1, View));
				}
				Thread.Sleep(_throttle);
				if(!Locked && !CancelRender && !IsStopped && Flush) {
					TriggerChangesReady();
				}
			}

			cyclesEngine.Session.EndRun();

			cyclesEngine.Session.Destroy();
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
				
				if (UploadData())
				{
					State = State.Rendering;
					_needReset = true;

					m_flush = false;
				}

				if (CancelRender)
				{
					State = State.Stopped;
				}
				TriggerSynchronized();
			}
		}

		public void ChangeSamples(int samples)
		{
			_samples = samples;
			Session?.SetSamples(samples);
		}

		public void ToggleNoShadows()
		{
			_needReset = true;
		}
	}

}
