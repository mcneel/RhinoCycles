/**
Copyright 2014-2024 Robert McNeel and Associates

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

using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.PlugIns;
using Rhino.Render;
using Rhino.UI;
using RhinoCyclesCore;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Database;
using RhinoCyclesCore.RenderEngines;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace RhinoCycles.Viewport
{
	/// <summary>
	/// Our class information implementation with which we can register our
	/// RealtimeDisplayMode implementation RenderedViewport with
	/// RhinoCommon.
	/// </summary>
	public class RenderedViewportClassInfo : RealtimeDisplayModeClassInfo
	{
		public override string Name => Localization.LocalizeString("Cycles", 8);

		public override Guid GUID => new Guid("69E0C7A5-1C6A-46C8-B98B-8779686CD181");

		public override bool DrawOpenGl => false;

		public override Type RealtimeDisplayModeType => typeof(RenderedViewport);

		public override bool DontRegisterAttributesOnStart => true;
	}

	public class RenderedViewport : RealtimeDisplayMode, IRenderedViewportCallbacks
	{
		private static int _runningSerial;
		private readonly int _serial;

		private uint _docSerialNumber;

		private bool _started;
		private bool _frameAvailable;

		private bool _locked;

		private bool IsLocked
		{
			get
			{
				return _locked;
			}
			set
			{
				_locked = value;
				if(_cycles!=null) _cycles.Locked = _locked;
			}
		}


		public bool IsSynchronizing { get; private set; }

		private bool _forCapture;
		private ViewportRenderEngine _cycles;
		private ModalRenderEngine _modal;

		private DateTime _startTime;
		private DateTime _lastTime;
		private int _samples = -1;
		private int _maxSamples;
		private string _status = "";

		private bool _okOgl = false;

		public RenderedViewport()
		{
			RcCore.It.StartLogStopwatch("RenderedViewport | Raytraced");
			RcCore.It.AddLogString("RenderedViewport ctor entry");
			_runningSerial ++;
			_serial = _runningSerial;
			(ApplicationAndDocumentSettings.RcPlugIn as Plugin)?.InitialiseCSycles();

			HudPlayButtonLeftClicked += RenderedViewport_HudPlayButtonPressed;
			HudPauseButtonLeftClicked += RenderedViewport_HudPauseButtonPressed;
			HudLockButtonLeftClicked += RenderedViewport_HudLockButtonPressed;
			HudUnlockButtonLeftClicked += RenderedViewport_HudUnlockButtonPressed;
			HudProductNameLeftClicked += RenderedViewport_HudPlayProductNamePressed;
			HudStatusTextLeftClicked += RenderedViewport_HudPlayStatusTextPressed;
			HudTimeLeftClicked += RenderedViewport_HudPlayTimePressed;
			MaxPassesChanged += RenderedViewport_MaxPassesChanged;
			RcCore.It.AddLogString("RenderedViewport ctor exit");
		}

		public override void PostConstruct()
		{
			_okOgl = false;
			SetUseDrawOpenGl(_okOgl);
			RcCore.It.AddLogString("Post-Construct RenderedViewport for Raytraced completed");
		}

		private void RenderedViewport_MaxPassesChanged(object sender, HudMaxPassesChangedEventArgs e)
		{
			ChangeSamples(e.MaxPasses);
		}

		public override bool HudAllowEditMaxPasses()
		{
			return true;
		}

		private void RenderedViewport_HudUnlockButtonPressed(object sender, EventArgs e)
		{
			IsLocked = false;
		}

		private void RenderedViewport_HudLockButtonPressed(object sender, EventArgs e)
		{
			IsLocked = true;
		}

		private void RenderedViewport_HudPauseButtonPressed(object sender, EventArgs e)
		{
			_lastTime = DateTime.UtcNow;
			_cycles?.Pause();
		}

		private void RenderedViewport_HudPlayButtonPressed(object sender, EventArgs e)
		{
			_startTime = _startTime + (DateTime.UtcNow - _lastTime);
			_cycles?.Continue();
		}

		private bool _showRenderDevice = false;
		private void RenderedViewport_HudPlayProductNamePressed(object sender, EventArgs e)
		{
			_showRenderDevice = !_showRenderDevice;
			RhinoApp.OutputDebugString("product name pressed\n");
		}

		private void RenderedViewport_HudPlayStatusTextPressed(object sender, EventArgs e)
		{
			RhinoApp.OutputDebugString("status text pressed\n");
		}

		private void RenderedViewport_HudPlayTimePressed(object sender, EventArgs e)
		{
			RhinoApp.OutputDebugString("time pressed\n");
		}

		private DisplayPipelineAttributes Dpa { get; set; } = null;
		public override void CreateWorld(RhinoDoc doc, ViewInfo viewInfo, DisplayPipelineAttributes displayPipelineAttributes)
		{
			_docSerialNumber = doc.RuntimeSerialNumber;
			Dpa = displayPipelineAttributes;
		}

		private Thread _modalThread;
		//private readonly object timerLock = new object();
		IAllSettings eds;

		public override bool StartRenderer(int w, int h, RhinoDoc doc, ViewInfo rhinoView, ViewportInfo viewportInfo, bool forCapture, RenderWindow renderWindow)
		{
			RcCore.It.AddLogString("Raytraced: RenderedViewport::StartRenderer entry");
			_docSerialNumber = doc.RuntimeSerialNumber;
			eds = RhinoCyclesCore.Utilities.GetEngineDocumentSettings(_docSerialNumber);
			_started = true;
			_forCapture = forCapture;
			SetUseDrawOpenGl(false);
			var requestedChannels = renderWindow.GetRequestedRenderChannelsAsStandardChannels();

			List<RenderWindow.StandardChannels> reqChanList = requestedChannels
					.Distinct()
					.Where(chan => chan != RenderWindow.StandardChannels.AlbedoRGB)
					.ToList();

			foreach(var reqChan in reqChanList) {
				RcCore.It.AddLogString($"Raytraced: RenderedViewport AddChannel {reqChan} start");
				renderWindow.AddChannel(reqChan);
				RcCore.It.AddLogString($"Raytraced: RenderedViewport AddChannel {reqChan} end");
			}

			if (forCapture)
			{
				var mre = new ModalRenderEngine(doc, PlugIn.IdFromName("RhinoCycles"), rhinoView, viewportInfo, Dpa, false)
				{
					BufferRectangle = viewportInfo.GetScreenPort(),
					FullSize = viewportInfo.GetScreenPort().Size
				};
				mre.SetCallbackForCapture();

				_cycles = null;
				_modal = mre;

				var rs = new Size(w, h);

				renderWindow.SetSize(rs);

				mre.RenderWindow = renderWindow;

				mre.RenderDimension = rs;
				mre.Database.RenderDimension = rs;

				mre.StatusTextUpdated += Mre_StatusTextUpdated;

				mre.Database.LinearWorkflowChanged += DatabaseLinearWorkflowChanged;
				mre.HandleDevice(eds);

				mre.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

				_modalThread = new Thread(RenderOffscreen)
				{
					Name = $"Cycles offscreen viewport rendering with ModalRenderEngine {_serial}"
				};
				_modalThread.Start(mre);

				return true;
			}


			_frameAvailable = false;

			var renderSize = new Size(w, h);
			var pixelSize = eds.PixelSize;

			RcCore.It.AddLogString($"Raytraced: RenderedViewport SetSize start");
			renderWindow.SetSize(renderSize);
			RcCore.It.AddLogString($"Raytraced: RenderedViewport SetSize end");
			renderWindow.SetRenderOutputRect(
				new Rectangle(0, 0, w / pixelSize, h / pixelSize)
			);

			RcCore.It.AddLogString($"Raytraced: RenderedViewport ViewportRenderEngine ctor start");
			_cycles = new ViewportRenderEngine(doc.RuntimeSerialNumber, PlugIn.IdFromName("RhinoCycles"), rhinoView, Dpa)
			{
				BufferRectangle = viewportInfo.GetScreenPort(),
				FullSize = viewportInfo.GetScreenPort().Size,
				RenderedViewport = this,
			};
			RcCore.It.AddLogString($"Raytraced: RenderedViewport ViewportRenderEngine ctor end");

			_cycles.StatusTextUpdated += CyclesStatusTextUpdated; // render engine tells us status texts for the hud
			_cycles.RenderStarted += CyclesRenderStarted; // render engine tells us when it actually is rendering
			_cycles.StartSynchronizing += CyclesStartSynchronizing;
			_cycles.Synchronized += CyclesSynchronized;
			_cycles.PassRendered += CyclesPassRendered;
			_cycles.Database.LinearWorkflowChanged += DatabaseLinearWorkflowChanged;
			_cycles.UploadProgress += _cycles_UploadProgress;

			_cycles.RenderWindow = renderWindow;
			// pixel size needs to be set before calling SetRenderSize, since that uses CalculateNativeRenderSize
			// which in turn relies on PixelSize
			_cycles.PixelSize = pixelSize;
			_cycles.SetRenderSize(w, h);

			_cycles.HandleDevice(eds);

			_maxSamples = eds.Samples;

			RcCore.It.AddLogString("Raytraced: RenderedViewport CreateWorld start");
			_cycles.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session
			RcCore.It.AddLogString("Raytraced: RenderedViewport CreateWorld end");

			_startTime = DateTime.UtcNow;
			_lastTime = _startTime;
			_cycles.StartRenderThread(_cycles.Renderer, $"A cool Cycles viewport rendering thread {_serial}");

			RcCore.It.AddLogString("Raytraced: RenderedViewport::StartRenderer exit");

			return true;
		}

		private void _cycles_UploadProgress(object sender, UploadProgressEventArgs e)
		{
			_status = e.Message;
			if (!_cycles.CancelRender && _cycles.IsUploading)
			{
				RcCore.It.AddLogStringIfVerbose("_cycles_UploadProgress: SignalRedraw start");
				SignalRedraw();
				RcCore.It.AddLogStringIfVerbose("_cycles_UploadProgress: SignalRedraw end");
			}
		}

		private double _progress;
		private void Mre_StatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			_progress = e.Progress;
		}

		public void RenderOffscreen(object o)
		{
			RcCore.It.AddLogString("Raytraced: RenderedViewport.RenderOffScreen entry");
			if (o is ModalRenderEngine mre)
			{
				RcCore.It.AddLogString("Raytraced: RenderedViewport.RenderOffScreen Renderer start");
				mre.Renderer();
				RcCore.It.AddLogString("Raytraced: RenderedViewport.RenderOffScreen Renderer end");
				RcCore.It.AddLogString("Raytraced: RenderedViewport.RenderOffScreen SaveRenderedBuffer start");
				mre.SaveRenderedBuffer(0);
				RcCore.It.AddLogString("Raytraced: RenderedViewport.RenderOffScreen SaveRenderedBuffer end");
				_frameAvailable = true;
			}
			RcCore.It.AddLogString("Raytraced: RenderedViewport.RenderOffScreen exit");
		}

		public override bool IsFrameBufferAvailable(ViewInfo view)
		{
			if (_cycles != null)
			{
				if (IsSynchronizing) return false;
				if (!_frameAvailable) return false;
				return _cycles.Database.AreViewsEqual(GetView(), view);
			}
			if (_modal != null)
			{
				return _frameAvailable;
			}

			return false;
		}

		void CyclesPassRendered(object sender, ViewportRenderEngine.PassRenderedEventArgs e)
		{
			if (_cycles?.IsRendering ?? false)
			{
				if (!IsSynchronizing)
				{
					if (e.Sample > 0 || !e.OnlyUpdateHud)
					{
						_frameAvailable = true;
						PutResultsIntoRenderWindowBuffer();
						_lastTime = DateTime.UtcNow;
						_samples = e.Sample;
						SetView(e.View);
					}

					if(e.OnlyUpdateHud) {
						_startTime = _startTime + (DateTime.UtcNow - _lastTime);
						_samples = e.Sample;
					}

					if (!_cycles.CancelRender)
					{
						RcCore.It.AddLogStringIfVerbose("CyclesPassRendered: SignalRedraw start");
						SignalRedraw();
						RcCore.It.AddLogStringIfVerbose("CyclesPassRendered: SignalRedraw end");
					}
				}
			}
		}

		void CyclesStartSynchronizing(object sender, EventArgs e)
		{
			//ALB 2022/09/14
			//https://mcneel.myjetbrains.com/youtrack/issue/RH-70028/Raytraced-crash-when-switching-to-other-display-mode
			//write operation for bool is atomic in C#
			//lock (timerLock)
			//{
				IsSynchronizing = true;
			//}
		}

		void CyclesSynchronized(object sender, EventArgs e)
		{
			_startTime = DateTime.UtcNow;
			//lock (timerLock)
			{
				_samples = -1;
				_frameAvailable = false;
				IsSynchronizing = false;
			}
		}

		void DatabaseLinearWorkflowChanged(object sender, LinearWorkflowChangedEventArgs e)
		{
			var ppg = e.Lwf.PostProcessGamma;
			LinearWorkflow.PostProcessGamma = ppg;
			//var rengine = _cycles ?? _modal as RenderEngine;

			//if (rengine == null) return;

			//ALB 2019.11.7 - I can't find a case where this is actually used in a real way.  It is not called when
			//doing a rendering, it is called from Raytraced, but it is always just setting the gamma to 1.0 (presumably because
			//gamma correction is done later in a shader in the viewport), and I can't get the previews to work at the moment.
			//If it turns out I have broken preview gamma, we need to find another way to fix this without using Get/SetAdjust
			/*var imageadjust = rengine.RenderWindow.GetAdjust();
			imageadjust.Gamma = ppg;
			rengine.RenderWindow.SetAdjust(imageadjust);*/
		}

		void CyclesRenderStarted(object sender, ViewportRenderEngine.RenderStartedEventArgs e)
		{
			_started = true;
		}
		private void _ResetRenderTime()
		{
			_startTime = _startTime + (DateTime.UtcNow - _lastTime);
			_lastTime = _startTime;
		}

		public void ChangeSamples(int samples)
		{
			var vud = RhinoCyclesCore.Utilities.GetEngineDocumentSettings(_docSerialNumber);
			if (vud == null) vud = RcCore.It.AllSettings;
			if(vud!=null)
			{
				vud.Samples = samples;
			}
			UpdateMaxSamples(samples);
			_cycles?.ChangeSamples(samples);
			_ResetRenderTime();
		}
		public void UpdateMaxSamples(int samples)
		{
			_maxSamples = samples;
		}

		public void ChangeIntegrator(IntegratorSettings integrator)
		{
			_cycles?.ChangeIntegrator(integrator);
			_ResetRenderTime();
		}

		public void ToggleNoShadows()
		{
			_cycles?.ToggleNoShadows();
		}

		void CyclesStatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			if (_cycles?.IsWaiting ?? false) _status = Localization.LocalizeString("Paused", 1);
			else
			{
				_status = e.Samples <= 0 ? e.StatusText : "";
				if (!_cycles.CancelRender && _cycles.IsUploading)
				{
					RcCore.It.AddLogStringIfVerbose("CyclesStatusTextUpdated: SignalRedraw start");
					SignalRedraw();
					RcCore.It.AddLogStringIfVerbose("CyclesStatusTextUpdated: SignalRedraw end");
				}
			}
		}

		public override bool ShowCaptureProgress()
		{
			return true;
		}

		public override double CaptureProgress()
		{
			return _progress;
		}

		public override void GetRenderSize(out int width, out int height)
		{
			if (_cycles != null)
			{
				width = _cycles.RenderDimension.Width;
				height = _cycles.RenderDimension.Height;
			} else
			{
				width = _modal.RenderDimension.Width;
				height = _modal.RenderDimension.Height;
			}
		}

		public void PutResultsIntoRenderWindowBuffer()
		{
			if (_frameAvailable)
				_cycles.BlitPixelsToRenderWindowChannel();
		}

		public override bool OnRenderSizeChanged(int width, int height)
		{
			_startTime = DateTime.UtcNow;
			_frameAvailable = false;
			_samples = -1;

			return true;
		}

		public override void ShutdownRenderer()
		{
			RcCore.It.AddLogString("Raytraced: RenderedViewport.ShutdownRenderer entry");
			if (_forCapture)
			{
				_modal?.StopRendering();
				_modal?.Dispose();
			}
			else
			{
				_cycles?.StopRendering();
				_cycles?.Dispose();
			}
			RcCore.It.AddLogString("Raytraced: RenderedViewport.ShutdownRenderer exit");
		}

		public override bool IsRendererStarted()
		{
			return _started;
		}

		public override bool IsCompleted()
		{
			bool rc = false;
			if (_forCapture)
			{
				rc = _frameAvailable;
			}
			else
			{
				var state = _cycles?.State ?? State.Stopped;
				rc = state == State.Rendering && _frameAvailable && _samples == _maxSamples;
			}
			return rc;
		}

		public override string HudProductName()
		{
			if (_cycles == null) return "-";
			if (_cycles.RenderDevice == null) return "?";
			var pn = Localization.LocalizeString("Cycles", 9);
			var cpu = _cycles.RenderDevice.IsCpu;
			if (_showRenderDevice && !cpu) pn = $"{pn}@{_cycles.RenderDevice.NiceName}";
			else if (_showRenderDevice && cpu) pn = $"{pn}@{_cycles.RenderDevice.NiceName}x{_cycles.ThreadCount}";

			if(_cycles.IsFallbackRenderDevice) {
				var originalDevice = RcCore.It.IsDeviceReady(RcCore.It.AllSettings.RenderDevice);
				if (!originalDevice.isDeviceReady)
				{
					var fallback = Localization.LocalizeString("fallback to CPU, kernels still compiling", 97);
					pn = $"{pn} - {fallback}";
				} else {
					if(RcCore.It.CompileProcessError) {
						var error = Localization.LocalizeString("Error compiling kernels, check _RhinoCyclesCompileLog", 98);
						pn = $"{pn} - {error}";
					}
					else {
						var reset = Localization.LocalizeString("Compile finished. Restart Raytraced", 99);
						pn = $"{pn} - {reset}";
					}
				}
			}
			return pn;
		}

		public override string HudCustomStatusText()
		{
			return _status;
		}

		public override int HudMaximumPasses()
		{
			return _maxSamples;
		}

		public override int LastRenderedPass()
		{
			//ALB 2022/09/14
			//https://mcneel.myjetbrains.com/youtrack/issue/RH-70028/Raytraced-crash-when-switching-to-other-display-mode
			//read operation for int is atomic in C#
			int samplesLocal = _samples;
			//lock(timerLock)
			{
				samplesLocal = _samples;
			}
			return samplesLocal;
		}
		public override int HudLastRenderedPass()
		{
			return LastRenderedPass();
		}

		public override bool HudRendererPaused()
		{
			var st = _cycles?.State ?? State.Stopped;
			return st==State.Waiting || _status.Equals("Idle");
		}

		public override bool HudRendererLocked()
		{
			return IsLocked;
		}

		public override bool HudShowMaxPasses()
		{
			return eds.ShowMaxPasses;
		}

		public override bool HudShowPasses()
		{
			return true;
		}

		public override bool HudShowCustomStatusText()
		{
			return true;
		}

		public override DateTime HudStartTime()
		{
			return _startTime;
		}

		/// <summary>
		/// Get a list of all RenderedViewport instances currently
		/// in use for the given Rhino document.
		/// </summary>
		/// <param name="doc">RhinoDoc instance to check in</param>
		/// <returns>List with zero or more RenderedViewport instances</returns>
		public static List<RenderedViewport> GetRenderedViewports(RhinoDoc doc)
		{
			List<RenderedViewport> rvps = new List<RenderedViewport>();
			foreach(var view in doc.Views)
			{
				if (view.RealtimeDisplayMode is RenderedViewport rvp)
				{
					rvps.Add(rvp);
				}
			}

			return rvps;
		}
	}
}
