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

		public override bool DrawOpenGl => true;

		public override Type RealtimeDisplayModeType => typeof(RenderedViewport);

		public override bool DontRegisterAttributesOnStart => true;
	}

	public class RenderedViewport : RealtimeDisplayMode
	{
		private static int _runningSerial;
		private readonly int _serial;

		private bool _started;
		private bool _available;
		private bool _frameAvailable;

		private bool _locked;

		private bool Locked
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

		private ViewportRenderEngine _cycles;
		private ModalRenderEngine _modal;

		private DateTime _startTime;
		private DateTime _lastTime;
		private int _samples = -1;
		private int _maxSamples;
		private string _status = "";

		public RenderedViewport()
		{
			_runningSerial ++;
			_serial = _runningSerial;
			(EngineSettings.RcPlugIn as Plugin)?.InitialiseCSycles();
			_available = true;

			HudPlayButtonPressed += RenderedViewport_HudPlayButtonPressed;
			HudPauseButtonPressed += RenderedViewport_HudPauseButtonPressed;
			HudLockButtonPressed += RenderedViewport_HudLockButtonPressed;
			HudUnlockButtonPressed += RenderedViewport_HudUnlockButtonPressed;
			HudProductNamePressed += RenderedViewport_HudPlayProductNamePressed;
			HudStatusTextPressed += RenderedViewport_HudPlayStatusTextPressed;
			HudTimePressed += RenderedViewport_HudPlayTimePressed;
			MaxPassesChanged += RenderedViewport_MaxPassesChanged;
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
			Locked = false;
		}

		private void RenderedViewport_HudLockButtonPressed(object sender, EventArgs e)
		{
			Locked = true;
		}

		private void RenderedViewport_HudPauseButtonPressed(object sender, EventArgs e)
		{
			_cycles?.Pause();
		}

		private void RenderedViewport_HudPlayButtonPressed(object sender, EventArgs e)
		{
			_startTime = _startTime + (DateTime.UtcNow - _lastTime);
			_cycles?.Continue();
		}

		private void RenderedViewport_HudPlayProductNamePressed(object sender, EventArgs e)
		{
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

		public override void CreateWorld(RhinoDoc doc, ViewInfo viewInfo, DisplayPipelineAttributes displayPipelineAttributes)
		{
		}

		private Thread _modalThread;

		public override bool StartRenderer(int w, int h, RhinoDoc doc, ViewInfo rhinoView, ViewportInfo viewportInfo, bool forCapture, RenderWindow renderWindow)
		{
			if (forCapture)
			{
				ModalRenderEngine mre = new ModalRenderEngine(doc, PlugIn.IdFromName("RhinoCycles"), rhinoView, viewportInfo);
				_cycles = null;
				_modal = mre;

				var rs = new Size(w, h);

				mre.RenderWindow = renderWindow;

				mre.RenderDimension = rs;
				mre.Database.RenderDimension = rs;

				mre.StatusTextUpdated += Mre_StatusTextUpdated;

				mre.Database.LinearWorkflowChanged += DatabaseLinearWorkflowChanged;

				mre.SetFloatTextureAsByteTexture(false); // mre.Settings.RenderDeviceIsOpenCl);

				mre.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

				_modalThread = new Thread(RenderOffscreen)
				{
					Name = $"Cycles offscreen viewport rendering with ModalRenderEngine {_serial}"
				};
				_modalThread.Start(mre);

				return true;
			}

			_available = false; // the renderer hasn't started yet. It'll tell us when it has.
			_frameAvailable = false;
			_sameView = false;

			_cycles = new ViewportRenderEngine(doc.RuntimeSerialNumber, PlugIn.IdFromName("RhinoCycles"), rhinoView);

			_cycles.StatusTextUpdated += CyclesStatusTextUpdated; // render engine tells us status texts for the hud
			_cycles.RenderStarted += CyclesRenderStarted; // render engine tells us when it actually is rendering
			_cycles.StartSynchronizing += CyclesStartSynchronizing;
			_cycles.Synchronized += CyclesSynchronized;
			_cycles.PassRendered += CyclesPassRendered;
			_cycles.Database.LinearWorkflowChanged += DatabaseLinearWorkflowChanged;
			_cycles.UploadProgress += _cycles_UploadProgress;

			ViewportSettingsChanged += _cycles.ViewportSettingsChangedHandler;
			_cycles.CurrentViewportSettingsRequested += _cycles_CurrentViewportSettingsRequested;
			var renderSize = Rhino.Render.RenderPipeline.RenderSize(doc);

			_cycles.RenderWindow = renderWindow;
			_cycles.RenderDimension = renderSize;

			_maxSamples = RcCore.It.EngineSettings.Samples;

			_cycles.SetFloatTextureAsByteTexture(false); // m_cycles.Settings.RenderDeviceIsOpenCl);

			_startTime = DateTime.UtcNow; // use _startTime to time CreateWorld
			_cycles.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

			var createWorldDuration = DateTime.UtcNow - _startTime;

			RhinoApp.OutputDebugString($"CreateWorld({RcCore.It.EngineSettings.FlushAtEndOfCreateWorld}): {createWorldDuration.Ticks} ticks ({createWorldDuration.TotalMilliseconds} ms)\n");

			_startTime = DateTime.UtcNow;
			_lastTime = _startTime;

			_cycles.StartRenderThread(_cycles.Renderer, $"A cool Cycles viewport rendering thread {_serial}");

			return true;
		}

		private void _cycles_UploadProgress(object sender, UploadProgressEventArgs e)
		{
			_status = e.Message;
			if(!_cycles.CancelRender) SignalRedraw();
		}

		private void _cycles_CurrentViewportSettingsRequested(object sender, EventArgs e)
		{
			IViewportSettings vud;
			if (RcCore.It.EngineSettings.AllowViewportSettingsOverride)
			{
				vud = Plugin.GetActiveViewportSettings();
				if(vud==null) vud = RcCore.It.EngineSettings;
			} else
			{
				vud = RcCore.It.EngineSettings;
			}
			if (vud == null) return;
			TriggerViewportSettingsChanged(vud);
		}

		public event EventHandler<ViewportSettingsChangedArgs> ViewportSettingsChanged;

		private double _progress;
		private void Mre_StatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			_progress = e.Progress;
		}

		public void RenderOffscreen(object o)
		{
			var mre = o as ModalRenderEngine;
			if (mre != null)
			{
				mre.Renderer();
#if DEBUG
				mre.SaveRenderedBuffer(0);
#endif
				_available = true;
				_frameAvailable = true;
				_sameView = false;
			}
		}

		public override bool IsFrameBufferAvailable(ViewInfo view)
		{
			if (_cycles != null)
			{
				if (!_sameView && _cycles.ViewSet)
				{
					_sameView = _cycles.Database.AreViewsEqual(view, _cycles.View);
				}
				return _frameAvailable && _sameView && _samples > -1;
			}
			if (_modal != null)
			{
				return _frameAvailable;
			}

			return false;
		}

		private bool _sameView;
		void CyclesPassRendered(object sender, ViewportRenderEngine.PassRenderedEventArgs e)
		{
			if (_cycles?.IsRendering ?? false)
			{
				_frameAvailable = true;
				_available = true;
				_lastTime = DateTime.UtcNow;

				if(!_cycles.CancelRender) SignalRedraw();
			}
		}

		void CyclesStartSynchronizing(object sender, EventArgs e)
		{
			_samples = -1;
			_frameAvailable = false;
			_sameView = false;
			IsSynchronizing = true;
		}

		void CyclesSynchronized(object sender, EventArgs e)
		{
			_startTime = DateTime.UtcNow;
			_frameAvailable = false;
			_sameView = false;
			_samples = -1;
			IsSynchronizing = false;
		}

		void DatabaseLinearWorkflowChanged(object sender, LinearWorkflowChangedEventArgs e)
		{
			LinearWorkflow.PostProcessGamma = e.Lwf.PostProcessGamma;
			var rengine = _cycles ?? _modal as RenderEngine;

			if (rengine == null) return;

			var imageadjust = rengine.RenderWindow.GetAdjust();
			imageadjust.Gamma = e.Lwf.PostProcessGamma;
			rengine.RenderWindow.SetAdjust(imageadjust);
		}

		void CyclesRenderStarted(object sender, ViewportRenderEngine.RenderStartedEventArgs e)
		{
			_available = false;
			_started = true;
		}
		private void _ResetRenderTime()
		{
			_startTime = _startTime + (DateTime.UtcNow - _lastTime);
			_lastTime = _startTime;
		}

		public void ChangeSamples(int samples)
		{
			var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) vud = RcCore.It.EngineSettings;
			if(vud!=null)
			{
				vud.Samples = samples;
			}
			_maxSamples = samples;
			_cycles?.ChangeSamples(samples);
			_ResetRenderTime();
		}

		public void TriggerViewportSettingsChanged(IViewportSettings settings)
		{
			ViewportSettingsChanged?.Invoke(this, new ViewportSettingsChangedArgs(settings));
		}

		void CyclesStatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			//Rhino.RhinoApp.OutputDebugString($"{e.StatusText}\n");
			_samples = e.Samples;

			if (_cycles?.IsWaiting ?? false) _status = "Paused";
			else
			{
				_status = _samples < 0 ? e.StatusText : ""; // "Updating Engine" : "";
				if(!_cycles.CancelRender) SignalRedraw();
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

		public override bool DrawOpenGl()
		{
			if (_samples < 0) return false;
			_cycles.DrawOpenGl();
			return true;
		}

		public override bool OnRenderSizeChanged(int width, int height)
		{
			_startTime = DateTime.UtcNow;
			_available = false;
			_frameAvailable = false;
			_sameView = false;

			return true;
		}

		public override void ShutdownRenderer()
		{
			_available = false;
			_cycles?.StopRendering();
			_cycles?.Dispose();
		}

		public override bool IsRendererStarted()
		{
			return _started;
		}

		public override bool IsCompleted()
		{
			var rc = _available && _cycles.State == State.Rendering && _frameAvailable && _samples==_maxSamples;
			return rc;
		}

    public override string HudProductName()
    {
      return Localization.LocalizeString("Cycles", 9);
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
			return _samples;
		}
		public override int HudLastRenderedPass()
		{
			return _samples;
		}

		public override bool HudRendererPaused()
		{
			var st = _cycles?.State ?? State.Stopped;
			return st==State.Waiting || _status.Equals("Idle");
		}

		public override bool HudRendererLocked()
		{
			return Locked;
		}

		public override bool HudShowMaxPasses()
		{
			return RcCore.It.EngineSettings.ShowMaxPasses;
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
	}
}
