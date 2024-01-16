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
using System.Diagnostics;

namespace RhinoCyclesCore.RenderEngines
{
	public class ViewportRenderEngine : RenderEngine
	{
		public ViewportRenderEngine(uint docRuntimeSerialNumber, Guid pluginId, ViewInfo view, Rhino.Display.DisplayPipelineAttributes attr) : base(pluginId, docRuntimeSerialNumber, view, null, attr, true)
		{
			State = State.Rendering;

			Database.ViewChanged += Database_ViewChanged;
			BeginChangesNotified += ViewportRenderEngine_BeginChangesNotified;

#region create callbacks for Cycles
			m_logger_callback = ViewportLoggerCallback;

			CSycles.log_to_stdout(false);
			//CSycles.set_logger(m_logger_callback);
#endregion

		}

		public IRenderedViewportCallbacks RenderedViewport { get; set; }

		private bool _bvhUploaded = false;
		private void ViewportRenderEngine_BeginChangesNotified(object sender, EventArgs e)
		{
			if (IsUploading || !_bvhUploaded)
			{
				return;
			}
			Session.QuickCancel();
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
			// TODO: XXXX session disposal Client?.Dispose();
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
			public PassRenderedEventArgs(int sample, ViewInfo view, bool onlyUpdateHud)
			{
				Sample = sample;
				View = view;
				OnlyUpdateHud = onlyUpdateHud;
			}

			/// <summary>
			/// The completed sample (pass).
			/// </summary>
			public int Sample { get; private set; }

			public ViewInfo View { get; private set; }
			public bool OnlyUpdateHud { get; private set; }
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
			if(RenderWindow != null)
			{
#if DEBUG
				RenderWindow.EnableDebugThreadCheck(enabled: false);
#endif
				Size size = new (w, h);
				RenderWindow.SetSize(size);
				RenderDimension = size;
				FullSize = size;

				// For rendering with pixel size > 2 we need to tell the render
				// window that the result is in a specific area - the render window
				// and Post Effects system will handle upscaling and everything
				// automatically.
				Size scaledSize = CalculateNativeRenderSize();
				Rectangle outputRectangle = new (
					x: 0,
					y: 0,
					width: scaledSize.Width,
					height: scaledSize.Height
				);
				RenderWindow.SetRenderOutputRect(outputRectangle);

#if DEBUG
				RenderWindow.EnableDebugThreadCheck(enabled: true);
#endif
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

		//bool _firstDone = false;

		/// <summary>
		/// Event gets fired when the renderer has started.
		/// </summary>
		public event EventHandler<RenderStartedEventArgs> RenderStarted;

		public bool Locked { get; set; }

		private void HandleRenderCrash()
		{
			Session.QuickCancel();
			State = State.Stopping;
			Action switchToWireframe = () =>
			{
				RhinoApp.RunScript("_SetDisplayMode _Rendered", false);
				CrashReporterDialog dlg = new CrashReporterDialog(Localization.LocalizeString("Error while using Raytraced display mode", 68), Localization.LocalizeString(
@"An error was detected while using the Raytraced display mode.

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
			RcCore.It.AddLogString("ViewportRenderEngine.Renderer entry");
			Locked = false;

			var doc = RhinoDoc.FromRuntimeSerialNumber(m_doc_serialnumber);
			EngineDocumentSettings eds = new EngineDocumentSettings(m_doc_serialnumber);
			var vi = new ViewInfo(doc.Views.ActiveView.ActiveViewport);
			var vpi = vi.Viewport;

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
#endif
			#endregion

			#region set up session parameters
			ThreadCount = (RenderDevice.IsCpu ? eds.Threads : 0);
			int pixelSize = Math.Max(1, RcCore.It.AllSettings.PixelSize);
			var sessionParams = new SessionParameters(RenderDevice)
			{
				Experimental = false,
				Samples = (int)MaxSamples,
				TileSize = TileSize(RenderDevice),
				Threads = (uint)ThreadCount,
				ShadingSystem = ShadingSystem.SVM,
				Background = false,
				PixelSize = pixelSize,
				UseResolutionDivider = true,
			};
			#endregion

			if (this == null || ShouldBreak) return;

			#region create session for scene
			Session = RcCore.It.CreateSession( sessionParams);
			#endregion

			InitializeSceneSettings(Session, RenderDevice, this, eds);

			// Set up passes
			foreach (var reqPass in reqPassTypes)
			{
				Session.AddPass(reqPass);
			}

			RcCore.It.AddLogString("ViewportRenderEngine.Renderer Session.Reset start");
			Session.Reset(
				width: FullSize.Width,
				height: FullSize.Height,
				samples: MaxSamples,
				full_x: 0, full_y: 0,
				full_width: FullSize.Width,
				full_height: FullSize.Height,
				pixel_size: pixelSize);
			RcCore.It.AddLogString("ViewportRenderEngine.Renderer Session.Reset end");

			// main render loop, including restarts
			#region start the rendering thread, wait for it to complete, we're rendering now!

			if(ShouldBreak) return;

			RcCore.It.AddLogString("ViewportRenderEngine.Renderer Database.Flush start");
			Database.Flush();
			RcCore.It.AddLogString("ViewportRenderEngine.Renderer Database.Flush end");

			if(ShouldBreak) return;

			RcCore.It.AddLogString("ViewportRenderEngine.Renderer UploadData start");
			UploadData();
			RcCore.It.AddLogString("ViewportRenderEngine.Renderer UploadData end");

			if(ShouldBreak) return;

			Database.ResetChangeQueue();

			#endregion

			// We've got Cycles rendering now, notify anyone who cares
			RenderStarted?.Invoke(sender: this, e: new RenderStartedEventArgs(success: !CancelRender));

			RcCore.It.AddLogString("ViewportRenderEngine.Renderer Session.Start start");
			Session.Start();
			RcCore.It.AddLogString("ViewportRenderEngine.Renderer Session.Start end");

			State lastState = State.Unset;
			int lastRenderedSample = 0;
			bool renderingDone = false;

			while (this != null && !ShouldBreak)
			{
				// If state changed
				if(State != lastState)
				{
					// Pause behavior
					if (State == State.Waiting)
					{
						Session.SetPause(true);
					}
					else if (lastState == State.Waiting)
					{
						Session.SetPause(false);
					}

					lastState = State;
				}

				if (Flush)
				{
					CheckFlushQueue();
					Synchronize();
					Flush = false;
					Finished = false;
					_needReset = true;

				}

				if (_needReset)
				{
					var size = FullSize;
					RcCore.It.AddLogString("ViewportRenderEngine.Renderer Session.Reset start");
					Session.Reset(
						width: size.Width,
						height: size.Height,
						samples: MaxSamples,
						full_x: 0,
						full_y: 0,
						full_width: size.Width,
						full_height: size.Height,
						pixel_size: pixelSize);
					RcCore.It.AddLogString("ViewportRenderEngine.Renderer Session.Reset end");
					lastRenderedSample = -1;
					renderingDone = false;
					_needReset = false;
				}

				if (!Finished)
				{
					UpdateCallback(Session.Id);
				}

				// If we have rendered a new sample
				if(RenderedSamples > lastRenderedSample)
				{
					if (!Finished && RenderedSamples < MaxSamples)
					{
						PassRendered?.Invoke(
							sender: this,
							e: new PassRenderedEventArgs(sample: RenderedSamples, view: View, onlyUpdateHud: false)
						);
					}
					if (!renderingDone && Finished)
					{
						PassRendered?.Invoke(
							sender: this,
							e: new PassRenderedEventArgs(sample: RenderedSamples, view: View, onlyUpdateHud: false)
						);
						renderingDone = true;
					}

					lastRenderedSample = RenderedSamples;
				}

				Thread.Sleep(millisecondsTimeout: _throttle);
			}

			if (this != null)
			{
				Database.ResetChangeQueue();
			}
			RcCore.It.AddLogString("ViewportRenderEngine.Renderer exit");
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

				Session.WaitUntilLocked();

				if (UploadData())
				{
					State = State.Rendering;
					_needReset = true;
				}

				Session.Unlock();

				if (CancelRender)
				{
					State = State.Stopping;
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
