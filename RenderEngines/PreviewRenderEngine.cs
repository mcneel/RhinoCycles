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
using ccl;
using Rhino.Render;
using RhinoCyclesCore.Core;
using sdd = System.Diagnostics.Debug;
using System.Threading;

namespace RhinoCyclesCore.RenderEngines
{
	public class PreviewRenderEngine : RenderEngine
	{
		/// <summary>
		/// Construct a render engine for preview rendering
		/// </summary>
		/// <param name="createPreviewEventArgs"></param>
		/// <param name="pluginId">Id of the plugin for which the render engine is created</param>
		public PreviewRenderEngine(CreatePreviewEventArgs createPreviewEventArgs, Guid pluginId, uint docsrn) : base (pluginId, createPreviewEventArgs, false, docsrn)
		{
			State = State.Rendering;

#region create callbacks for Cycles
			CSycles.log_to_stdout(false);
#endregion
		}

		public void PreviewRendererUpdateRenderTileCallback(IntPtr sessionId, uint x, uint y, uint w, uint h, uint sample, uint depth, PassType passtype, float[] pixels, int pixlen)
		{
		}

		public void PreviewRendererWriteRenderTileCallback(IntPtr sessionId, uint x, uint y, uint w, uint h, uint sample, uint depth, PassType passtype, float[] pixels, int pixlen)
		{
	  }

		public bool Success { get; set; } = false;

		public int PreviewSamples { get; set; }

		/// <summary>
		/// Renderer entry point for preview rendering
		/// </summary>
		/// <param name="oPipe"></param>
		public static void Renderer(object oPipe)
		{
			var cyclesEngine = (PreviewRenderEngine)oPipe;
			cyclesEngine.Success = false;

			var size = cyclesEngine.RenderDimension;
			cyclesEngine.PreviewSamples = Math.Max(1, RcCore.It.AllSettings.PreviewSamples);
			cyclesEngine.MaxSamples = cyclesEngine.PreviewSamples;

#region pick a render device
			(bool isReady, Device renderDevice) = RcCore.It.IsDeviceReady(RcCore.It.AllSettings.RenderDevice);
			cyclesEngine.IsFallbackRenderDevice = !isReady;

			if (RcCore.It.AllSettings.Verbose) sdd.WriteLine(
				$"Using device {renderDevice.Name + " " + renderDevice.Description}");
#endregion

			if (cyclesEngine.CancelRender) return;

			var gpusize = TileSize(renderDevice);
			uint threads = renderDevice.IsGpu ? 0u : (uint)RcCore.It.AllSettings.Threads;

			var pixelSize = (int)(RcCore.It.AllSettings.DpiScale);

			#region set up session parameters
			var sessionParams = new SessionParameters(renderDevice)
			{
				Experimental = false,
				Samples = cyclesEngine.MaxSamples,
				TileSize = gpusize,
				Threads = threads,
				ShadingSystem = ShadingSystem.SVM,
				Background = false,
				PixelSize = pixelSize, //renderDevice.IsCpu ? pixelSize : 1,
			};
#endregion

			if (cyclesEngine.CancelRender) return;

#region create session for scene
			cyclesEngine.Session = RcCore.It.CreateSession(sessionParams);
#endregion

			// TODO: XXXX fix up scene creation InitializeSceneSettings(cyclesEngine.Session, renderDevice, cyclesEngine, RcCore.It.AllSettings);

			cyclesEngine.Session.AddPass(PassType.Combined);

			// main render loop
			cyclesEngine.Database.Flush();
			cyclesEngine.Session.WaitUntilLocked();
			cyclesEngine.UploadData();
			cyclesEngine.Session.Unlock();

			bool renderSuccess = true;

			cyclesEngine.Session.Reset(size.Width, size.Height, cyclesEngine.MaxSamples, 0, 0, size.Width, size.Height);
			cyclesEngine.Session.Start();

			while (!cyclesEngine.Finished)
			{
				if (!cyclesEngine.ShouldBreak)
				{
					cyclesEngine.UpdateCallback(cyclesEngine.Session.Id);

					if (cyclesEngine.RenderedSamples == -13)
					{
						cyclesEngine.Success = false;
						renderSuccess = false;
						cyclesEngine.Finished = true;
					}

					cyclesEngine.UpdatePreview();

					Thread.Sleep(50);
				}
				else
				{
					cyclesEngine.Session.QuickCancel();
					break;
				}
				if (cyclesEngine.PreviewEventArgs.Cancel)
				{
					cyclesEngine.State = State.Stopping;
					cyclesEngine.CancelRender = true;
				}
			}
			if (renderSuccess)
			{
				cyclesEngine.UpdatePreview();
			}

			cyclesEngine.StopTheRenderer();

			cyclesEngine?.Database.ResetChangeQueue();

			cyclesEngine.Success = renderSuccess;

			// we're done now, so lets clean up our session.
			cyclesEngine.Dispose();
		}

		public void UpdatePreview()
		{
			BlitPixelsToRenderWindowChannel();
			RenderWindow.Invalidate();
			PreviewEventArgs.PreviewNotifier.NotifyIntermediateUpdate(RenderWindow);
		}

		private bool _disposed;
		public override void Dispose(bool isDisposing)
		{
			if (_disposed) return;

			base.Dispose(isDisposing);

			Database?.Dispose();
			Database = null;
			// TODO: XXXX session disposal Session?.Dispose();
			_disposed = true;
		}

	}

}
