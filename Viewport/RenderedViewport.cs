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
using ssd = System.Diagnostics.Debug;

namespace RhinoCycles.Viewport
{
	/// <summary>
	/// Our class information implementation with which we can register our
	/// RealtimeDisplayMode implementation RenderedViewport with
	/// RhinoCommon.
	/// </summary>
	public class RenderedViewportClassInfo : RealtimeDisplayModeClassInfo
	{
		public override string ClassName => LOC.STR("Raytraced");

		public override string FullName => LOC.STR("Raytraced");

		public override Guid GUID => new Guid("69E0C7A5-1C6A-46C8-B98B-8779686CD181");

		public override Type RealtimeDisplayModeType => typeof(RenderedViewport);
	}

	public class RenderedViewport : RealtimeDisplayMode
	{
		private static int _runningSerial;
		private readonly int _serial;

		private bool _started;
		private bool _available;
		private bool _frameAvailable;

		private bool _locked;

		public bool IsSynchronizing { get; private set; }

		private ViewportRenderEngine _cycles;
		private ModalRenderEngine _modal;

		private DateTime _startTime;
		private int _samples = -1;
		private int _maxSamples;
		private string _status = "";

		//private DisplayPipelineAttributes _displayPipelineAttributes;

		public RenderedViewport()
		{
			_runningSerial ++;
			_serial = _runningSerial;
			ssd.WriteLine($"Initialising a RenderedViewport {_serial}");
			Plugin.InitialiseCSycles();
			_available = true;

			HudPlayButtonPressed += RenderedViewport_HudPlayButtonPressed;
			HudPauseButtonPressed += RenderedViewport_HudPauseButtonPressed;
			HudLockButtonPressed += RenderedViewport_HudLockButtonPressed;
			HudUnlockButtonPressed += RenderedViewport_HudUnlockButtonPressed;
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
			_locked = false;
		}

		private void RenderedViewport_HudLockButtonPressed(object sender, EventArgs e)
		{
			_locked = true;
		}

		private void RenderedViewport_HudPauseButtonPressed(object sender, EventArgs e)
		{
			_cycles?.Pause();
		}

		private void RenderedViewport_HudPlayButtonPressed(object sender, EventArgs e)
		{
			_cycles?.Continue();
		}

		public override void CreateWorld(RhinoDoc doc, ViewInfo viewInfo, DisplayPipelineAttributes displayPipelineAttributes)
		{
			lock (_locker)
			{
				if (!_alreadyCreated)
				{
					_alreadyCreated = true;
					//_displayPipelineAttributes = displayPipelineAttributes;
					ssd.WriteLine($"CreateWorld {_serial}");
				}
			}
		}

		private Thread _modalThread;

		private readonly object _locker = new object();
		private bool _alreadyStarted;
		private bool _alreadyCreated;
		public override bool StartRenderer(int w, int h, RhinoDoc doc, ViewInfo rhinoView, ViewportInfo viewportInfo, bool forCapture, RenderWindow renderWindow)
		{
			lock (_locker)
			{
				if (!_alreadyStarted)
				{
					_alreadyStarted = true;
					if (forCapture)
					{
						ModalRenderEngine mre = new ModalRenderEngine(doc, PlugIn.IdFromName("RhinoCycles"), rhinoView, viewportInfo);
						_cycles = null;
						_modal = mre;

						mre.Settings = RcCore.It.EngineSettings;
						mre.Settings.UseInteractiveRenderer = false;
						mre.Settings.SetQuality(doc.RenderSettings.AntialiasLevel);

						var rs = new Size((int)w, (int)h);

						mre.RenderWindow = renderWindow;

						mre.RenderDimension = rs;
						mre.Database.RenderDimension = rs;

						mre.Settings.Verbose = true;

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

					ssd.WriteLine($"StartRender {_serial}");
					_available = false; // the renderer hasn't started yet. It'll tell us when it has.
					_frameAvailable = false;

					_cycles = new ViewportRenderEngine(doc.RuntimeSerialNumber, PlugIn.IdFromName("RhinoCycles"), rhinoView);

					_cycles.StatusTextUpdated += CyclesStatusTextUpdated; // render engine tells us status texts for the hud
					_cycles.RenderStarted += CyclesRenderStarted; // render engine tells us when it actually is rendering
					_cycles.StartSynchronizing += CyclesStartSynchronizing;
					_cycles.Synchronized += CyclesSynchronized;
					_cycles.PassRendered += CyclesPassRendered;
					_cycles.Database.LinearWorkflowChanged += DatabaseLinearWorkflowChanged;
					_cycles.SamplesChanged += CyclesSamplesChanged;

					_cycles.Settings = RcCore.It.EngineSettings;
					_cycles.Settings.SetQuality(doc.RenderSettings.AntialiasLevel);

					var renderSize = Rhino.Render.RenderPipeline.RenderSize(doc);

					_cycles.RenderWindow = renderWindow;
					_cycles.RenderDimension = renderSize;

					_cycles.Settings.Verbose = true;

					_maxSamples = _cycles.Settings.Samples;

					_cycles.SetFloatTextureAsByteTexture(false); // m_cycles.Settings.RenderDeviceIsOpenCl);

					_cycles.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

					_startTime = DateTime.UtcNow;

					_cycles.StartRenderThread(_cycles.Renderer, $"A cool Cycles viewport rendering thread {_serial}");
				}

			}
			return true;
		}

		private void CyclesSamplesChanged(object sender, RenderEngine.SamplesChangedEventArgs e)
		{
			ChangeSamples(e.Count);
		}

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
				//SetCRC(mre.ViewCrc);
				mre.SaveRenderedBuffer(0);
				_started = true; // we started (and are also ready, though)
				_available = true;
				_frameAvailable = true;
			}
		}

		public override bool IsFrameBufferAvailable(ViewInfo view)
		{
			if (_cycles != null)
			{
				var equal = _cycles.Database.AreViewsEqual(GetView(), view);
				return equal;
			}
			if (_modal != null)
			{
				return _frameAvailable;
			}

			return false;
		}

		void CyclesPassRendered(object sender, ViewportRenderEngine.PassRenderedEventArgs e)
		{
			_frameAvailable = true;
			_available = true;
			_started = true;
			if (_cycles?.IsRendering ?? false)
			{
				if (e.Sample <= 1) SetView(e.View);
				SignalRedraw();
			}
		}

		void CyclesStartSynchronizing(object sender, EventArgs e)
		{
			IsSynchronizing = true;
		}

		void CyclesSynchronized(object sender, EventArgs e)
		{
			_startTime = DateTime.UtcNow;
			_frameAvailable = false;
			_samples = -1;
			IsSynchronizing = false;
		}

		void DatabaseLinearWorkflowChanged(object sender, LinearWorkflowChangedEventArgs e)
		{
			ssd.WriteLine($"Setting Gamma {e.Gamma} and ApplyGammaCorrection {e.Lwf.Active} ({_serial})");
			SetUseLinearWorkflowGamma(e.Lwf.Active);
			SetGamma(e.Gamma);
			if (_cycles != null)
			{
				var imageadjust = _cycles.RenderWindow.GetAdjust();
				imageadjust.Gamma = e.Gamma;
				_cycles.RenderWindow.SetAdjust(imageadjust);
			} else if(_modal!= null)
			{
				var imageadjust = _modal.RenderWindow.GetAdjust();
				imageadjust.Gamma = e.Gamma;
				_modal.RenderWindow.SetAdjust(imageadjust);
			}
		}

		void CyclesRenderStarted(object sender, ViewportRenderEngine.RenderStartedEventArgs e)
		{
			_available = false;
		}

		public void ChangeSamples(int samples)
		{
			_startTime = DateTime.UtcNow; 
			_maxSamples = samples;
			_cycles?.ChangeSamples(samples);
		}

		void CyclesStatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			_samples = e.Samples;

			_status = _samples < 0 ? "Updating Engine" : "";
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

		public override bool OnRenderSizeChanged(int width, int height)
		{
			ssd.WriteLine($"RestartRender {_serial}");
			SetGamma(_cycles.Database.Gamma);
			_startTime = DateTime.UtcNow;
			_available = false;

			return true;
		}

		public override void ShutdownRenderer()
		{
			_available = false;
			_started = false;
			ssd.WriteLine($"!!! === ShutdownRender {_serial} === !!!");
			_cycles?.StopRendering();
			_cycles?.Dispose();
		}

		public override bool IsRendererStarted()
		{
			return _started || _alreadyStarted;
		}

		public override bool IsCompleted()
		{
			//SetGamma(m_cycles.Database.Gamma);
			var rc = _available && _cycles.State == State.Rendering && _frameAvailable && _samples==_maxSamples;
			return rc;
		}

		public override string HudProductName()
		{
			return "Raytraced";
		}

		public override string HudStatusText()
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
			return _locked;
		}

		public override bool HudShowMaxPasses()
		{
			return false;
		}

		public override bool HudShowPasses()
		{
			return true;
		}

		public override bool HudShowStatusText()
		{
			return true;
		}

		public override DateTime HudStartTime()
		{
			return _startTime;
		}
	}
}