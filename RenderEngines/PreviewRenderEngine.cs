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

using ccl;
using Rhino.Render;
using RhinoCyclesCore.Core;
using System;
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
			// TODO CSycles.log_to_stdout(false);
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
			lock (RcCore.It.PreviewRendererLock)
			{
				var cyclesEngine = (PreviewRenderEngine)oPipe;
				cyclesEngine.Success = false;

				var size = cyclesEngine.RenderDimension;
				cyclesEngine.PreviewSamples = Math.Max(1, RcCore.It.AllSettings.PreviewSamples);
				cyclesEngine.MaxSamples = cyclesEngine.PreviewSamples;

				#region pick a render device
				(bool isReady, Device renderDevice) = RcCore.It.IsDeviceReady(RcCore.It.AllSettings.RenderDevice);
				cyclesEngine.IsFallbackRenderDevice = !isReady;
				#endregion

				if (cyclesEngine.CancelRender)
				{
					RcCore.It.AddLogStringIfVerbose("Preview render cancelled. Exit before rendering, 1");
					return;
				}

				var gpusize = TileSize(renderDevice);
				uint threads = renderDevice.IsGpu ? 0u : (uint)RcCore.It.AllSettings.Threads;

				int pixelSize = 1; // Don't use pixel size for now, see  below on SetRenderOutputRect. Math.Max(1, RcCore.It.AllSettings.PixelSize);

				/* HUOM disable SetRenderOutputRect usage for now since this doesn't seem to be
				 * working properly in previews
				 *
				cyclesEngine.PixelSize = pixelSize;
				cyclesEngine.RenderWindow.SetRenderOutputRect(
					new Rectangle(0, 0, size.Width / pixelSize, size.Height / pixelSize)
				);
				*/

				#region set up session parameters

				var (idPtr, sessionParams, sceneParams, bufferParams) = Session.PrepareForSession();
				sessionParams.Device = renderDevice;

				sessionParams.Experimental = false;
				sessionParams.Samples = cyclesEngine.MaxSamples;
				sessionParams.TileSize = gpusize;
				sessionParams.Threads = (int)threads;
				sessionParams.Shadingsystem = ShadingSystem.SHADINGSYSTEM_SVM;
				sessionParams.Background = false;
				sessionParams.PixelSize = pixelSize;
				sessionParams.UseResolutionDivider = false;
				sceneParams.Shadingsystem = sessionParams.Shadingsystem;
				#endregion

				if (cyclesEngine.CancelRender)
				{
					RcCore.It.AddLogStringIfVerbose("Preview render cancelled. Exit before rendering, 2");
					return;
				}

				#region create session for scene
				cyclesEngine.Session = RcCore.It.CreateSession(idPtr, sessionParams, sceneParams);
				cyclesEngine.CreateSimpShader();
				#endregion

				Pass pass = cyclesEngine.Session.Scene.AddPass();
				pass.ins.Name.Value = NameForPassType(PassType.PASS_COMBINED);
				pass.ins.Type.Value = PassType.PASS_COMBINED;
				cyclesEngine.Session.AddPass(pass);


				// main render loop
				cyclesEngine.Database.Flush();
				cyclesEngine.Session.Scene.WaitUntilLocked();
				cyclesEngine.UploadData();
				cyclesEngine.Session.Scene.Unlock();

				cyclesEngine.Session.Scene.Integrator.ins.UseAdaptiveSampling.Value = true;
				cyclesEngine.Session.Scene.Integrator.ins.AdaptiveMinSamples.Value = 3;
				cyclesEngine.Session.Scene.Integrator.ins.AdaptiveThreshold.Value = 0.3f;

				bool renderSuccess = true;

				bufferParams.ins.Width.Value = size.Width;
				bufferParams.ins.Height.Value = size.Height;
				bufferParams.ins.FullX.Value = 0;
				bufferParams.ins.FullY.Value = 0;
				bufferParams.ins.FullWidth.Value = size.Width;
				bufferParams.ins.FullHeight.Value = size.Height;
				bufferParams.ins.Samples.Value = cyclesEngine.MaxSamples;

				cyclesEngine.Session.Reset(sessionParams, bufferParams);
				cyclesEngine.Session.Start();

				while (!cyclesEngine.Finished)
				{
					if (!cyclesEngine.ShouldBreak)
					{
						cyclesEngine.UpdateCallback(cyclesEngine.Session.Ptr);

						if (cyclesEngine.RenderedSamples == -13)
						{
							cyclesEngine.Success = false;
							renderSuccess = false;
							cyclesEngine.Finished = true;
						}

						cyclesEngine.UpdatePreview();

						Thread.Sleep(50);
					}
					if (cyclesEngine.PreviewEventArgs.Cancel)
					{
						RcCore.It.AddLogStringIfVerbose("PreviewRenderEngine.Renderer: cancel signalled during rendering");
						cyclesEngine.State = State.Stopping;
						cyclesEngine.CancelRender = true;
						cyclesEngine.Finished = true;
					}
				}
				if (renderSuccess)
				{
					RcCore.It.AddLogStringIfVerbose("PreviewRenderEngine.Renderer: rendering done, UpdatePreview start");
					cyclesEngine.UpdatePreview();
					RcCore.It.AddLogStringIfVerbose("PreviewRenderEngine.Renderer: rendering done, UpdatePreview start");
				}

				cyclesEngine.StopTheRenderer();

				cyclesEngine?.Database.ResetChangeQueue();

				cyclesEngine.Success = renderSuccess;

				RcCore.It.AddLogString("PreviewRenderEngine.Renderer releasing session start");
				RcCore.It.ReleaseSession(cyclesEngine.Session);
				RcCore.It.AddLogString("PreviewRenderEngine.Renderer releasing session done");

				// we're done now, so lets clean up our session.
				cyclesEngine.Dispose();
			}
		}

		public void UpdatePreview()
		{
			RcCore.It.AddLogStringIfVerbose("UpdatePreview: entry");
			BlitPixelsToRenderWindowChannel();
			RenderWindow.Invalidate();
			PreviewEventArgs.PreviewNotifier.NotifyIntermediateUpdate(RenderWindow);
			RcCore.It.AddLogStringIfVerbose("UpdatePreview: exit");
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
