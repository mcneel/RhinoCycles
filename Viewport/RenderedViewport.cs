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
using RhinoCyclesCore.Database;
using ssd = System.Diagnostics.Debug;

namespace RhinoCycles
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
		private static int g_running_serial;
		private readonly int m_serial;

		private bool m_started;
		private bool m_available;
		private bool m_frame_available = false;

		private bool m_locked = false;

		private bool m_synchronizing;

		private ViewportRenderEngine m_cycles;
		private ModalRenderEngine m_modal;

		private DateTime m_starttime;
		private int m_samples = -1;
		private int m_maxsamples;
		private string m_status = "";

		private DisplayPipelineAttributes m_displaypipelineattributes;

		public RenderedViewport()
		{
			g_running_serial ++;
			m_serial = g_running_serial;
			ssd.WriteLine($"Initialising a RenderedViewport {m_serial}");
			Plugin.InitialiseCSycles();
			m_available = true;

			HudPlayButtonPressed += RenderedViewport_HudPlayButtonPressed;
			HudPauseButtonPressed += RenderedViewport_HudPauseButtonPressed;
			HudLockButtonPressed += RenderedViewport_HudLockButtonPressed;
			HudUnlockButtonPressed += RenderedViewport_HudUnlockButtonPressed;
			MaxPassesChanged += RenderedViewport_MaxPassesChanged;
		}

		private void RenderedViewport_MaxPassesChanged(object sender, HudMaxPassesChangedEventArgs e)
		{
			m_cycles?.ChangeSamples(e.MaxPasses);
		}

		private void RenderedViewport_HudUnlockButtonPressed(object sender, EventArgs e)
		{
			m_locked = false;
		}

		private void RenderedViewport_HudLockButtonPressed(object sender, EventArgs e)
		{
			m_locked = true;
		}

		private void RenderedViewport_HudPauseButtonPressed(object sender, EventArgs e)
		{
			m_cycles?.PauseRendering();
		}

		private void RenderedViewport_HudPlayButtonPressed(object sender, EventArgs e)
		{
			m_cycles?.ContinueRendering();
		}

		public override void CreateWorld(RhinoDoc doc, ViewInfo viewInfo, DisplayPipelineAttributes displayPipelineAttributes)
		{
			lock (locker)
			{
				if (!alreadyCreated)
				{
					alreadyCreated = true;
					m_displaypipelineattributes = displayPipelineAttributes;
					ssd.WriteLine($"CreateWorld {m_serial}");
				}
			}
		}

		private Thread ModalThread;

		private readonly object locker = new object();
		private bool alreadyStarted;
		private bool alreadyCreated;
		public override bool StartRenderer(uint w, uint h, RhinoDoc doc, ViewInfo rhinoView, ViewportInfo viewportInfo, bool forCapture, RenderWindow renderWindow)
		{
			lock (locker)
			{
				if (!alreadyStarted)
				{
					alreadyStarted = true;
					if (forCapture)
					{
						ModalRenderEngine mre = new ModalRenderEngine(doc, PlugIn.IdFromName("RhinoCycles"), rhinoView, viewportInfo);
						m_cycles = null;
						m_modal = mre;

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

						ModalThread = new Thread(this.RenderOffscreen)
						{
							Name = $"Cycles offscreen viewport rendering with ModalRenderEngine {m_serial}"
						};
						ModalThread.Start(mre);

						return true;
					}

					ssd.WriteLine($"StartRender {m_serial}");
					m_available = false; // the renderer hasn't started yet. It'll tell us when it has.
					m_frame_available = false;

					m_cycles = new ViewportRenderEngine(doc.RuntimeSerialNumber, PlugIn.IdFromName("RhinoCycles"), rhinoView);

					m_cycles.StatusTextUpdated += CyclesStatusTextUpdated; // render engine tells us status texts for the hud
					m_cycles.RenderStarted += m_cycles_RenderStarted; // render engine tells us when it actually is rendering
					m_cycles.StartSynchronizing += m_cycles_StartSynchronizing;
					m_cycles.Synchronized += m_cycles_Synchronized;
					m_cycles.PassRendered += m_cycles_PassRendered;
					m_cycles.Database.LinearWorkflowChanged += DatabaseLinearWorkflowChanged;
					m_cycles.SamplesChanged += M_cycles_SamplesChanged;

					m_cycles.Settings = RcCore.It.EngineSettings;
					m_cycles.Settings.SetQuality(doc.RenderSettings.AntialiasLevel);

					var renderSize = Rhino.Render.RenderPipeline.RenderSize(doc);

					m_cycles.RenderWindow = renderWindow;
					m_cycles.RenderDimension = renderSize;

					m_cycles.Settings.Verbose = true;

					m_maxsamples = m_cycles.Settings.Samples;

					m_cycles.SetFloatTextureAsByteTexture(false); // m_cycles.Settings.RenderDeviceIsOpenCl);

					m_cycles.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

					m_starttime = DateTime.UtcNow;

					m_cycles.StartRenderThread(m_cycles.Renderer, $"A cool Cycles viewport rendering thread {m_serial}");
				}

			}
			return true;
		}

		private void M_cycles_SamplesChanged(object sender, RenderEngine.SamplesChangedEventArgs e)
		{
			ChangeSamples(e.Count);
		}

		private double m_progress = 0.0;
		private void Mre_StatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			m_progress = e.Progress;
		}

		public void RenderOffscreen(object o)
		{
			var mre = o as ModalRenderEngine;
			if (mre != null)
			{
				mre.Renderer();
				//SetCRC(mre.ViewCrc);
				mre.SaveRenderedBuffer(0);
				m_started = true; // we started (and are also ready, though)
				m_available = true;
				m_frame_available = true;
			}
		}

		public override bool IsFrameBufferAvailable(ViewInfo view)
		{
			if (m_cycles != null)
			{
				return m_cycles.Database.AreViewsEqual(GetView(), view);
			}
			else if (m_modal != null)
			{
				return m_frame_available;
			}

			return false;
		}

		void m_cycles_PassRendered(object sender, ViewportRenderEngine.PassRenderedEventArgs e)
		{
			m_frame_available = true;
			m_available = true;
			m_started = true;
			if (m_cycles?.IsRendering ?? false)
			{
				if (e.Sample <= 1) SetView(e.View);
				SignalRedraw();
			}
		}

		void m_cycles_StartSynchronizing(object sender, EventArgs e)
		{
			m_synchronizing = true;
		}

		void m_cycles_Synchronized(object sender, EventArgs e)
		{
			m_starttime = DateTime.UtcNow;
			m_frame_available = false;
			m_samples = -1;
			m_synchronizing = false;
		}

		void DatabaseLinearWorkflowChanged(object sender, LinearWorkflowChangedEventArgs e)
		{
			ssd.WriteLine($"Setting Gamma {e.Gamma} and ApplyGammaCorrection {e.Lwf.Active} ({m_serial})");
			SetUseLinearWorkflowGamma(e.Lwf.Active);
			SetGamma(e.Gamma);
			if (m_cycles != null)
			{
				var imageadjust = m_cycles.RenderWindow.GetAdjust();
				imageadjust.Gamma = e.Gamma;
				m_cycles.RenderWindow.SetAdjust(imageadjust);
			} else if(m_modal!= null)
			{
				var imageadjust = m_modal.RenderWindow.GetAdjust();
				imageadjust.Gamma = e.Gamma;
				m_modal.RenderWindow.SetAdjust(imageadjust);
			}
		}

		void m_cycles_RenderStarted(object sender, ViewportRenderEngine.RenderStartedEventArgs e)
		{
			m_available = false;
		}

		public void ChangeSamples(int samples)
		{
			m_starttime = DateTime.UtcNow; 
			m_maxsamples = samples;
			m_cycles?.ChangeSamples(samples);
		}

		void CyclesStatusTextUpdated(object sender, RenderEngine.StatusTextEventArgs e)
		{
			m_samples = e.Samples;

			m_status = m_samples < 0 ? "Updating Engine" : "";
		}

		public override bool ShowCaptureProgress()
		{
			return true;
		}

		public override double CaptureProgress()
		{
			return m_progress;
		}

		public override void GetRenderSize(out int width, out int height)
		{
			if (m_cycles != null)
			{
				width = m_cycles.RenderDimension.Width;
				height = m_cycles.RenderDimension.Height;
			} else
			{
				width = m_modal.RenderDimension.Width;
				height = m_modal.RenderDimension.Height;
			}
		}

		public override bool OnRenderSizeChanged(int width, int height)
		{
			ssd.WriteLine($"RestartRender {m_serial}");
			SetGamma(m_cycles.Database.Gamma);
			m_starttime = DateTime.UtcNow;
			m_available = false;

			return true;
		}

		public override void ShutdownRenderer()
		{
			m_available = false;
			m_started = false;
			ssd.WriteLine($"!!! === ShutdownRender {m_serial} === !!!");
			m_cycles?.StopRendering();
			m_cycles?.Dispose();
		}

		public override bool IsRendererStarted()
		{
			return m_started;
		}

		public override bool IsCompleted()
		{
			//SetGamma(m_cycles.Database.Gamma);
			var rc = m_available && m_cycles.State == State.Rendering && m_frame_available && m_samples==m_maxsamples;
			return rc;
		}

		public override string HudProductName()
		{
			return "Raytraced";
		}

		public override string HudStatusText()
		{
			return m_status;
		}

		public override int HudMaximumPasses()
		{
			return m_maxsamples;
		}

		public override int LastRenderedPass()
		{
			return m_samples;
		}
		public override int HudLastRenderedPass()
		{
			return m_samples;
		}

		public override bool HudRendererPaused()
		{
			var st = m_cycles?.State ?? State.Stopped;
			return st==State.Waiting || m_status.Equals("Idle");
		}

		public override bool HudRendererLocked()
		{
			return m_locked;
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
			return m_starttime;
		}
	}
}