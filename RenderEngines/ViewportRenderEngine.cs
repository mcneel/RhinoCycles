//#define YES
/**
Copyright 2014-2021 Robert McNeel and Associates

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
using Rhino.UI;
using System.Collections.Generic;
using RhinoCyclesCore.Settings;
using System.Linq;

namespace RhinoCyclesCore.RenderEngines
{
	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(uint docRuntimeSerialNumber, Guid pluginId, ViewInfo view, Rhino.Display.DisplayPipelineAttributes attr) : base(pluginId, docRuntimeSerialNumber, view, null, attr, true)
		{
			Client = new Client();
			State = State.Rendering;

			Database.ViewChanged += Database_ViewChanged;
			BeginChangesNotified += ViewportRenderEngine_BeginChangesNotified;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = null;
			m_write_render_tile_callback = null;
			m_write_render_tile_callback = null;
			m_test_cancel_callback = null;
			m_display_update_callback = null;
			m_logger_callback = ViewportLoggerCallback;

			CSycles.log_to_stdout(false);
			//CSycles.set_logger(Client.Id, m_logger_callback);
#endregion

		}

		public IRenderedViewportCallbacks RenderedViewport { get; set; }

		private bool _bvhUploaded = false;
		private bool _sessionCancelFlagged = false;
		private void ViewportRenderEngine_BeginChangesNotified(object sender, EventArgs e)
		{
			if (IsUploading || !_bvhUploaded)
			{
				_sessionCancelFlagged = true;
				return;
			}
			Session?.Cancel("Begin changes notification");
		}

		public void ViewportLoggerCallback(string msg) {
			RcCore.OutputDebugString($"{msg}\n");
		}


		private bool _disposed;
		public override void Dispose(bool isDisposing)
		{
			if (_disposed) return;

			Database?.Dispose();
			Database = null;
			Client?.Dispose();
			base.Dispose(isDisposing);
			_disposed = true;
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

		//bool _firstDone = false;

		/// <summary>
		/// Event gets fired when the renderer has started.
		/// </summary>
		public event EventHandler<RenderStartedEventArgs> RenderStarted;

		public bool Locked { get; set; }

		private void HandleRenderCrash()
		{
			Session?.Cancel("Problem during rendering detected");
			State = State.Stopped;
			Action switchToWireframe = () =>
			{
				RhinoApp.RunScript("_SetDisplayMode _Rendered", false);
				CrashReporterDialog dlg = new CrashReporterDialog(Localization.LocalizeString("Error while using Raytraced", 68), Localization.LocalizeString(
@"An error was detected while using Raytraced.

To ensure stability the display mode was switched to Rendered.

Please click the link below for more information.", 69));
				dlg.ShowModal(RhinoEtoApp.MainWindow);
			};
			RhinoApp.InvokeOnUiThread(switchToWireframe);
		}

		/// <summary>
		/// Entry point for viewport interactive rendering
		/// </summary>
		public void Renderer()
		{
			Locked = false;

			var doc = RhinoDoc.FromRuntimeSerialNumber(m_doc_serialnumber);
			EngineDocumentSettings eds = new EngineDocumentSettings(m_doc_serialnumber);
			var vi = new ViewInfo(doc.Views.ActiveView.ActiveViewport);
			var vpi = vi.Viewport;

			var client = Client;
			var rw = RenderWindow;

			if (rw == null) return;

			var requestedChannels = rw.GetRequestedRenderChannelsAsStandardChannels();

			List<ccl.PassType> reqPassTypes = requestedChannels
					.Where(chan => chan != Rhino.Render.RenderWindow.StandardChannels.AlbedoRGB)
					.Select(chan => PassTypeForStandardChannel(chan))
					.Distinct()
					.ToList();

			_throttle = eds.ThrottleMs;
			MaxSamples = Attributes?.RealtimeRenderPasses ?? eds.Samples;
			MaxSamples = MaxSamples > 0 ? MaxSamples : eds.Samples;

			#region pick a render device

#if YES
			var rd0 = Device.GetDevice(0);
			var rd1 = Device.GetDevice(1);
			var rd2 = Device.GetDevice(2);
			var rd3 = Device.GetDevice(3);
			var rd4 = Device.GetDevice(4);
			var rdlist = new List<Device>();
			//rdlist.Add(rd0);
			//rdlist.Add(rd1);
			rdlist.Add(rd1);
			rdlist.Add(rd2);
			//rdlist.Add(rd0);
			//rdlist.Add(rd3);
			//rdlist.Add(rd4);

			var renderDevice = Device.CreateMultiDevice(rdlist);

#else
			HandleDevice(eds);
			var renderDevice = RenderDevice;
#endif
			(bool isReady, Device possibleRenderDevice) = RcCore.It.IsDeviceReady(renderDevice);
			RenderDevice = possibleRenderDevice;
			IsFallbackRenderDevice = !isReady;

			#endregion

			var pixelSize = (int)eds.DpiScale;

			#region set up session parameters
			ThreadCount = (RenderDevice.IsCpu ? eds.Threads : 0);
			var sessionParams = new SessionParameters(client, RenderDevice)
			{
				Experimental = false,
				Samples = (int)MaxSamples,
				TileSize = TileSize(RenderDevice),
				TileOrder = TileOrder.Center,
				Threads = (uint)ThreadCount,
				ShadingSystem = ShadingSystem.SVM,
				SkipLinearToSrgbConversion = true,
				DisplayBufferLinear = true,
				Background = false,
				ProgressiveRefine = eds.UseStartResolution,
				Progressive = true,
				StartResolution = eds.StartResolution,
				PixelSize = pixelSize,
			};
			#endregion

			if (this == null || CancelRender) return;

			#region create session for scene
			Session = RcCore.It.CreateSession(client, sessionParams);
			#endregion

			CreateScene(client, Session, RenderDevice, this, eds);

			// Set up passes
			foreach (var reqPass in reqPassTypes)
			{
				Session.AddPass(reqPass);
			}

			// register callbacks before starting any rendering
			SetCallbacks();

			// main render loop, including restarts
			#region start the rendering thread, wait for it to complete, we're rendering now!

			_textureBakeQuality = eds.TextureBakeQuality;

			if (this != null && !CancelRender)
			{
				CheckFlushQueue();
			}
			if (this != null && !CancelRender)
			{
				Synchronize();
				Flush = false;
			}

			if (this != null && !CancelRender)
			{
				Session.PrepareRun();
			}

			#endregion

			// We've got Cycles rendering now, notify anyone who cares
			RenderStarted?.Invoke(this, new RenderStartedEventArgs(!CancelRender));

			while (this != null && !IsStopped)
			{
				if (this != null && _needReset)
				{
					_needReset = false;
					var size = RenderDimension;

					HandleIntegrator(eds);

					Session.Scene.Integrator.NoShadows = eds.NoShadows;
					Session.Scene.Integrator.TagForUpdate();

					// lets reset session
					if (Session.Reset(size.Width, size.Height, MaxSamples, 0, 0, size.Width, size.Height) != 0)
					{
						HandleRenderCrash();
						break;
					}
				}
				if (this != null && !Flush && IsRendering)
				{
					var smpl = Session.Sample();
					if (smpl == -13)
					{
						HandleRenderCrash();
						break;
					}
					if (smpl > -1 && !Flush)
					{
						PassRendered?.Invoke(this, new PassRenderedEventArgs(smpl + 1, View));
						Database.ResetChangeQueue();
					}
				}
				_bvhUploaded = true;
				if (this != null && !Locked && !CancelRender && !IsStopped && Flush)
				{
					if (_sessionCancelFlagged)
					{
						Session?.Cancel("Changes detected");
					}
					CheckFlushQueue();
					Synchronize();
					_needReset = true;
					Flush = false;
					_sessionCancelFlagged = false;
				}
				else
				{
					Thread.Sleep(_throttle);
				}
			}

			if (this != null)
			{
				Database.ResetChangeQueue();
				RcCore.It.ReleaseSession(Session);
			}
		}

		/// <summary>
		/// Check if we should change render engine status. If the changequeue
		/// has notified us of any changes Flush will be true. If we're rendering
		/// then move to State.Halted and cancel our current render progress.
		/// </summary>
		public void CheckFlushQueue()
		{
			if (this == null || CancelRender) return;
			if (this==null || State != State.Rendering || Database == null) return;
			if (State == State.Waiting) Continue();

			// flush the queue
			if (!CancelRender)
			{
				Database.ResetChangeQueue();
				Database.Flush();
			}

			// if we've got actually changes we care about
			// change state to signal need for uploading
			if (!CancelRender && HasSceneChanges())
			{
				if (!CancelRender && Database.HasBvhChanges()) {
					_bvhUploaded = false;
				}
				State = State.Uploading;
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

				if (UploadData())
				{
					State = State.Rendering;
					_needReset = true;
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
			MaxSamples = Math.Max(1, samples);
			Session?.SetSamples(MaxSamples);
		}
		public void ChangeIntegrator(IntegratorSettings integrator)
		{
			if (Session != null)
			{
				var cyclesIntegrator = Session.Scene.Integrator;
				cyclesIntegrator.Seed = integrator.Seed;
				cyclesIntegrator.MaxBounce = integrator.MaxBounce;
				cyclesIntegrator.MaxDiffuseBounce = integrator.MaxDiffuseBounce;
				cyclesIntegrator.MaxGlossyBounce = integrator.MaxGlossyBounce;
				cyclesIntegrator.MaxTransmissionBounce = integrator.MaxTransmissionBounce;
				cyclesIntegrator.MaxVolumeBounce = integrator.MaxVolumeBounce;
				cyclesIntegrator.TransparentMaxBounce = integrator.MaxTransparentBounce;
				cyclesIntegrator.TagForUpdate();
				_needReset = true;
			}
		}

		public void ToggleNoShadows()
		{
			_needReset = true;
		}
	}

}
