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
using ccl;
using Rhino.Render;
using RhinoCyclesCore.Core;
using sdd = System.Diagnostics.Debug;
using System.Threading;

namespace RhinoCyclesCore.RenderEngines
{
	public class PreviewRenderEngine : RenderEngine
	{
		int m_sample_count = -1;

		/// <summary>
		/// Construct a render engine for preview rendering
		/// </summary>
		/// <param name="createPreviewEventArgs"></param>
		/// <param name="pluginId">Id of the plugin for which the render engine is created</param>
		public PreviewRenderEngine(CreatePreviewEventArgs createPreviewEventArgs, Guid pluginId) : base (pluginId, createPreviewEventArgs, false)
		{
			Client = new Client();
			State = State.Rendering;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = PreviewRendererUpdateRenderTileCallback;
			m_write_render_tile_callback = PreviewRendererWriteRenderTileCallback;
			m_test_cancel_callback = TestCancel;

			CSycles.log_to_stdout(false);
#endregion
		}

		public void PreviewRendererUpdateRenderTileCallback(uint sessionId, uint x, uint y, uint w, uint h, uint sample, uint depth, PassType passtype, float[] pixels, int pixlen)
		{
		}

		public void PreviewRendererWriteRenderTileCallback(uint sessionId, uint x, uint y, uint w, uint h, uint sample, uint depth, PassType passtype, float[] pixels, int pixlen)
		{
			if (IsStopped) return;
			
		  DisplayBuffer(sessionId, x, y, w, h, passtype, ref pixels, pixlen, (int)depth);
	  }

		public void SignalUpdate(int sample)
		{
			if (sample > 10 && sample % 50 != 0 && (sample + 2) < PreviewSamples) 
				return;

			if (sample > 1 && m_sample_count != sample)
			{
				PreviewEventArgs.PreviewNotifier.NotifyIntermediateUpdate(RenderWindow);
				m_sample_count = sample;
			}
		}


		public int PreviewSamples { get; set; }
		/// <summary>
		/// Renderer entry point for preview rendering
		/// </summary>
		/// <param name="oPipe"></param>
		public static void Renderer(object oPipe)
		{
			var cyclesEngine = (PreviewRenderEngine)oPipe;

			var client = cyclesEngine.Client;
			var threads = (uint)Math.Max(1, Environment.ProcessorCount - 1);

			var size = cyclesEngine.RenderDimension;
			var samples = RcCore.It.EngineSettings.PreviewSamples;
			cyclesEngine.PreviewSamples = samples;

			#region pick a render device
			var renderDevice = Device.Default;

			if (RcCore.It.EngineSettings.Verbose) sdd.WriteLine(
				$"Using device {renderDevice.Name + " " + renderDevice.Description}");
#endregion

			if (cyclesEngine.CancelRender) return;

			#region set up session parameters
			var sessionParams = new SessionParameters(client, renderDevice)
			{
				Experimental = false,
				Samples = samples,
				TileSize = new Size(16, 16),
				TileOrder = TileOrder.HilbertSpiral,
				Threads = threads,
				ShadingSystem = ShadingSystem.SVM,
				SkipLinearToSrgbConversion = true,
				DisplayBufferLinear = true,
				Background = true,
				ProgressiveRefine = true,
				Progressive = true,
				PixelSize = 1,
			};
#endregion

			if (cyclesEngine.CancelRender) return;

#region create session for scene
			cyclesEngine.Session = new Session(client, sessionParams);
#endregion

			CreateScene(client, cyclesEngine.Session, renderDevice, cyclesEngine, RcCore.It.EngineSettings);

			// register callbacks before starting any rendering
			cyclesEngine.SetCallbacks();

			// main render loop
			cyclesEngine.Database.Flush();
			cyclesEngine.UploadData();

			cyclesEngine.Session.PrepareRun();

			// lets first reset session
			cyclesEngine.Session.Reset(size.Width, size.Height, samples, 0, 0, size.Width, size.Height);
			// then reset scene
			cyclesEngine.Session.Scene.Reset();
			// and actually start
			bool stillrendering = true;
			var throttle = Math.Max(0, RcCore.It.EngineSettings.ThrottleMs);
			while (stillrendering)
			{
				if (cyclesEngine.IsRendering)
				{
					var sample = cyclesEngine.Session.Sample();
					stillrendering = sample > -1;
					cyclesEngine.SignalUpdate(sample);
					Thread.Sleep(throttle);
				}
				else
				{
					break;
				}
				if (cyclesEngine.IsStopped) break;
				if (cyclesEngine.CancelRender) break;
			}

			cyclesEngine.Session.EndRun();
			// we're done now, so lets clean up our session.
			cyclesEngine.Session.Destroy();

			// get rid of our change queue
			cyclesEngine.Database?.Dispose();
			cyclesEngine.Database = null;
		}

	}

}
