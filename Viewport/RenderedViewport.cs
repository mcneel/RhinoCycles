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
using System.Runtime.InteropServices;
using System.Threading;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using ssd = System.Diagnostics.Debug;
using Timer = System.Timers.Timer;

//using gl = OpenTK.Graphics.OpenGL;

namespace RhinoCycles
{
	[Guid("78B47310-A84B-445D-9991-A90625AC8837")]
	[RenderedDisplaymodeClassInfo("78B47310-A84B-445D-9991-A90625AC8837", "Cycles Rendered View", "A Cycles Rendered View")]
	public class RenderedViewport : RenderedDisplayMode
	{
		private static int g_running_serial;
		private readonly int m_serial;

		private bool m_started;
		private bool m_available;
		private bool m_need_rendersize_set;

		private ViewportRenderEngine m_cycles;
		private RhinoView m_view;

		private long m_starttime;
		private int m_samples;
		private int m_maxsamples;
		private float m_progress;
		private string m_status = "";

		public RenderedViewport()
		{
			g_running_serial ++;
			m_serial = g_running_serial;
			ssd.WriteLine("Initialising a RenderedViewport {0}", m_serial);
			Plugin.InitialiseCSycles();
			m_available = true;
		}

		public override void CreateWorld(RhinoDoc doc, ViewInfo viewInfo)
		{
			ssd.WriteLine("CreateWorld {0}", m_serial);
		}

		public override bool StartRender(uint w, uint h, RhinoDoc doc, RhinoView rhinoView, ViewportInfo viewportInfo, bool forCapture, RenderWindow renderWindow)
		{
			//gl.GL.GenTextures(1, out coltex);
			ssd.WriteLine("StartRender {0}", m_serial);
			m_started = true;
			m_available = true;
			m_view = rhinoView;

			AsyncRenderContext a_rc = new ViewportRenderEngine(doc, Plugin.IdFromName("RhinoCycles"), rhinoView);
			m_cycles = (ViewportRenderEngine)a_rc;

			m_cycles.RenderSizeUnset += m_cycles_RenderSizeUnset;
			m_cycles.StatusTextEvent += m_cycles_StatusTextEvent;

			m_cycles.Settings = Plugin.EngineSettings;
			m_cycles.Settings.SetQuality(doc.RenderSettings.AntialiasLevel);

			// for now when using interactive renderer render indefinitely
			//if(m_cycles.Settings.UseInteractiveRenderer) m_cycles.Settings.Samples = ushort.MaxValue;
			var renderSize = Rhino.Render.RenderPipeline.RenderSize(doc);

			m_cycles.RenderWindow = renderWindow;
			m_cycles.RenderDimension = renderSize;

			m_cycles.Settings.Verbose = true;
			SetGamma(m_cycles.Database.Gamma);
			SetApplyGammaCorrection(true);

			m_maxsamples = m_cycles.Settings.Samples;

			m_cycles.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

			m_cycles.UnsetRenderSize();

			m_cycles.RenderThread = new Thread(ViewportRenderEngine.Renderer)
			{
				Name = "A cool Cycles viewport rendering thread"
			};
			m_cycles.RenderThread.Start(m_cycles);
			m_cycles.SetRenderSize(renderSize.Width, renderSize.Height);

			m_starttime = GeCurrentTimeStamp();

			return true;
		}

		void m_cycles_RenderSizeUnset(object sender, EventArgs e)
		{
			m_need_rendersize_set = true;
		}

		static private long GeCurrentTimeStamp()
		{
			TimeSpan span = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0,DateTimeKind.Utc));
			return (long) span.TotalSeconds;
		}

		public override void UiUpdate()
		{
			if (m_started)
			{
				if (m_cycles.Flush)
				{
					m_cycles.CheckFlushQueue();
					m_cycles.Synchronize();
					m_starttime = GeCurrentTimeStamp();
				}
				else
				{
					SignalRedraw();
					if (m_need_rendersize_set)
					{
						var s = m_cycles.RenderDimension;
						m_cycles.SetRenderSize(s.Width, s.Height);
						m_need_rendersize_set = false;
					}
				}
			}
		}

		void m_cycles_StatusTextEvent(object sender, RenderEngine.StatusTextEventArgs e)
		{
			m_status = e.StatusText;
			m_progress = e.Progress;
			m_samples = e.Samples;
		}

		public override void GetRenderSize(out int width, out int height)
		{
			width = m_cycles.RenderDimension.Width;
			height = m_cycles.RenderDimension.Height;
		}

		public override bool RestartRender(bool tiled, int width, int height)
		{
			ssd.WriteLine("RestartRender {0}", m_serial);
			SetGamma(m_cycles.Database.Gamma);
			m_starttime = GeCurrentTimeStamp();

			return true;
		}

		public override void ShutdownRender()
		{
			m_available = false;
			m_started = false;
			ssd.WriteLine("!!! === ShutdownRender {0} === !!!", m_serial);
			if (m_cycles != null)
			{
				m_cycles.StopRendering();
			}
			// we're done now, so lets clean up our session.
			m_cycles.Session.Destroy();
		}

		public override bool IsRendererStarted()
		{
			//ssd.WriteLine("IsRendererStarted {0}: {1}", m_serial, m_started);
			return m_started;
		}

		public override bool IsRenderframeAvailable()
		{
			//ssd.WriteLine("IsRenderframeAvailable {0}: {1}", m_serial, m_available);
			SetGamma(m_cycles.Database.Gamma);
			//if(m_started) m_cycles.Session.Draw();
			return m_available && (m_cycles.State==State.Rendering || m_cycles.State==State.Waiting);
		}


		public override bool RenderEngineDraw()
		{
			if (m_cycles != null && m_cycles.Session != null)
			{
				m_cycles.Session.RhinoDraw(m_cycles.RenderDimension.Width, m_cycles.RenderDimension.Height);
			}

			return true;
		}


		public override string HudProductName()
		{
			return "RhinoCycles";
		}

		public override string HudStatusText()
		{
			return m_status;
		}

		public override int HudMaximumPasses()
		{
			return m_maxsamples;
		}

		public override int HudLastRenderedPass()
		{
			return m_samples;
		}

		public override bool HudRendererPaused()
		{
			return m_status.Equals("Idle");
		}

		public override bool HudRendererLocked()
		{
			return false;
		}

		public override bool HudShowMaxPasses()
		{
			return false;
		}

		public override bool HudShowPasses()
		{
			return false;
		}

		public override bool HudShowStatusText()
		{
			return true;
		}

		public override long HudStartTime()
		{
			return m_starttime;
		}
	}
}