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
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.Render.PostEffects;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Database;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace RhinoCyclesCore
{
	public class CyclesPostEffectExecutionControl : PostEffectExecutionControl
	{
		public int CurrentSample { get; set; } = 0;
		public int TriggerSample { get; set; } = int.MaxValue;

		public override bool ReadyToExecutePostEffect(Guid pep_id)
		{
			return CurrentSample >= TriggerSample;
		}
	}

	public enum State
	{
		Unset,
		Waiting,
		Uploading,
		Rendering,
		Stopping,
		Stopped
	}

	/// <summary>
	/// The actual render engine, ready for asynchronous work in Rhino.
	/// </summary>
	public partial class RenderEngine : IDisposable
	{
		protected CreatePreviewEventArgs PreviewEventArgs { get; set; }

		public RenderWindow RenderWindow { get; set; }

		public Session Session { get; set; } = null;

		/// <summary>
		/// True when State.Rendering
		/// </summary>
		public bool IsRendering => State == State.Rendering;

		/// <summary>
		/// True when State.Uploading
		/// </summary>
		public bool IsUploading => State == State.Uploading;

		/// <summary>
		/// True when State.Waiting
		/// </summary>
		public bool IsWaiting => State == State.Waiting;

		/// <summary>
		/// True when State.IsStopped
		/// </summary>
		public bool IsStopped => State == State.Stopped;

		/// <summary>
		/// Current render engine state.
		/// </summary>
		public State State { get; set; }

		/// <summary>
		/// Reference to the bitmap we're rendering into.
		///
		/// This is used when rendering material previews.
		/// </summary>
		public Bitmap RenderBitmap { get; set; }

		/// <summary>
		/// Set to true when the render session should be cancelled - used for preview job cancellation
		/// </summary>
		public bool CancelRender { get; set; }

		public bool ShouldBreak => CancelRender || State == State.Stopped || State == State.Stopping;

		public int RenderedSamples { get; set; }
		public int RenderedTiles { get; set; }
		public bool Finished { get; set; } = false;

		public string TimeString;

		protected CSycles.LoggerCallback m_logger_callback;

		protected bool m_flush;
		/// <summary>
		/// Flag set to true when a flush on the changequeue is needed.
		///
		/// Setting of Flush is protected with a lock. Getting is not.
		/// </summary>
		public bool Flush
		{
			get
			{
				return m_flush;
			}
			set
			{
				m_flush = value;
			}
		}

		public void TriggerBeginChangesNotified()
		{
			BeginChangesNotified?.Invoke(this, EventArgs.Empty);
		}
		public event EventHandler<EventArgs> BeginChangesNotified;



		/// <summary>
		/// Our instance of the change queue. This is our access point for all
		/// data. The ChangeQueue mechanism will push data to it, record it
		/// with all necessary book keeping to track the data relations between
		/// Rhino and Cycles.
		/// </summary>
		public ChangeDatabase Database { get; set; }

		/// <summary>
		/// Return true if any change has been received through the changequeue
		/// </summary>
		/// <returns>true if any changes have been received.</returns>
		protected bool HasSceneChanges()
		{
			return Database.HasChanges();
		}


		protected readonly uint m_doc_serialnumber;
		private readonly bool m_interactive;

		public RhinoDoc Doc => RhinoDoc.FromRuntimeSerialNumber(m_doc_serialnumber);

		/// <summary>
		/// Render engine implementations that need to keep track of views
		/// for instance to signal when a frame is ready for that particular
		/// view.
		///
		/// Generally such engines want to register an event handler to
		/// Database.ViewChanged to record the new ViewInfo here.
		/// </summary>
		public ViewInfo View { get; set; }

		public bool ViewSet => View != null;

		public Rectangle BufferRectangle { get; set; }
		public Size FullSize { get; set; }

		public CyclesPostEffectExecutionControl PEEController { get; set; }


#region CONSTRUCTORS

		private void RegisterEventHandler()
		{
			Database.MaterialShaderChanged += Database_MaterialShaderChanged;
			Database.LightShaderChanged += Database_LightShaderChanged;
			Database.FilmUpdateTagged += Database_FilmUpdateTagged;
		}

		protected Converters.BitmapConverter _bitmapConverter = new Converters.BitmapConverter();
		public DisplayPipelineAttributes Attributes => Database?.DisplayPipelineAttributes ?? null;
		public RenderEngine(Guid pluginId, uint docRuntimeSerialnumber, ViewInfo view, ViewportInfo vp, DisplayPipelineAttributes attributes, bool interactive)
		{
			RcCore.It.AddLogString("RenderEngine constructor entry");
			m_doc_serialnumber = docRuntimeSerialnumber;
			View = view;
			m_interactive = interactive;
			var doc = RhinoDoc.FromRuntimeSerialNumber(m_doc_serialnumber);
			RcCore.It.AddLogString("RenderEngine create ChangeDatabase");
			Database = new ChangeDatabase(pluginId, this, m_doc_serialnumber, View, attributes, !m_interactive, _bitmapConverter)
			{
				ModelAbsoluteTolerance = doc.ModelAbsoluteTolerance,
				ModelAngleToleranceRadians = doc.ModelAngleToleranceRadians,
				ModelUnitSystem = doc.ModelUnitSystem
			};
			RcCore.It.AddLogString("RenderEngine ChangeDatabase created");
			RegisterEventHandler();
			PEEController = new CyclesPostEffectExecutionControl()
			{
				CurrentSample = 0,
				TriggerSample = RcCore.It.AllSettings.TriggerPostEffectsSample
			};
			RcCore.It.AddLogString("RenderEngine constructor exit");
		}

		public RenderEngine(Guid pluginId, CreatePreviewEventArgs previewEventArgs, bool interactive, uint docsrn)
		{
			PEEController = new CyclesPostEffectExecutionControl()
			{
				CurrentSample = 0,
				TriggerSample = int.MaxValue
			};
			PreviewEventArgs = previewEventArgs;
			m_doc_serialnumber = docsrn;
			Database = new ChangeDatabase(pluginId, this, PreviewEventArgs, _bitmapConverter, docsrn);
			RegisterEventHandler();
		}

		public ccl.Shader _Shader { get; set; } = null;
		// Create a placeholder shader for new meshes to use before they get assigned
		// their actual shaders
		protected void CreateSimpShader()
		{
			var _sh = _Shader;
			if(_sh == null)
			{
				_Shader = new ccl.Shader(Session.Scene);
			}
		}

#endregion

		/// <summary>
		/// Tell our changequeue instance to initialise world.
		/// </summary>
		public void CreateWorld()
		{
			Database.CreateWorld(RcCore.It.AllSettings.FlushAtEndOfCreateWorld);
		}

		/// <summary>
		/// True if rendering for preview
		/// </summary>
		/// <returns></returns>
		public bool IsPreview()
		{
			return Database.IsPreview;
		}

		public void TestCancel(IntPtr sid)
		{
			if (IsStopped) return;

			if (PreviewEventArgs != null)
			{
				if (PreviewEventArgs.Cancel)
				{
					Session.QuickCancel();
					State = State.Stopping;
					CancelRender = true;
				}
			}
		}

		public class StatusTextEventArgs
		{
			public StatusTextEventArgs(string s, float progress, int samples, bool finished)
			{
				StatusText = s;
				Progress = progress;
				Samples = samples;
				Finished = finished;
			}

			public string StatusText { get; private set; }
			public float Progress { get; private set; }
			public int Samples { get; private set; }
			public bool Finished { get; private set; }
		}

		public event EventHandler<StatusTextEventArgs> StatusTextUpdated;

		/// <summary>
		/// Tell engine to fire StatusTextEvent with given arguments
		/// </summary>
		/// <param name="e"></param>
		public void TriggerStatusTextUpdated(StatusTextEventArgs e)
		{
			StatusTextUpdated?.Invoke(this, e);
		}

		/// <summary>
		/// Handle status updates
		/// </summary>
		/// <param name="sid"></param>
		string _previouStatusMessage = "";
		public void UpdateCallback(IntPtr sid)
		{
			if (IsStopped) return;

			RcCore.It.AddLogStringIfVerbose($"RenderEngine.UpdateCallback (ptr {sid}) entry");

			var status = CSycles.progress_get_status(sid);
			var substatus = CSycles.progress_get_substatus(sid);
			RenderedSamples = CSycles.progress_get_sample(sid);
			RenderedTiles = CSycles.progress_get_rendered_tiles(sid);

			//Debug.WriteLine("Current sample: {0}", RenderedSamples);

			float progress;
			double total_time, sample_time;
			CSycles.progress_get_time(sid, out total_time, out sample_time);
			CSycles.progress_get_progress(sid, out progress);
			int hr = ((int)total_time) / (60 * 60);
			int min = (((int)total_time) / 60) % 60;
			int sec = ((int)total_time) % 60;
			int hun = ((int)(total_time * 100.0)) % 100;

			if (!substatus.Equals(string.Empty)) status = status + ": " + substatus;

			bool finished = status.Contains("Finished") || status.Contains("Rendering Done");
			if (finished) RenderedSamples = MaxSamples;

			if(!Finished && !status.Equals(_previouStatusMessage)) RcCore.It.AddLogStringIfVerbose($"RenderEngine.UpdateCallback {status}");
			_previouStatusMessage = status;

			TimeString = $"{hr}h {min}m {sec}.{hun}s";
			status = $"{status} {TimeString}";


			Finished = finished;

			// don't set full 100% progress here yet, because that signals the renderwindow the end of async render
			if (progress >= 0.9999f) progress = 1.0f;

			if (MaxSamples == int.MaxValue) progress = -1.0f;
			RenderWindow?.SetProgress(status, progress);
			TriggerStatusTextUpdated(new StatusTextEventArgs(status, progress, RenderedSamples, finished));
			RcCore.It.AddLogStringIfVerbose($"RenderEngine.UpdateCallback (ptr {sid}) exit");
		}

		/// <summary>
		///  Clamp color so we get valid values for system bitmap
		/// </summary>
		/// <param name="ch"></param>
		/// <returns></returns>
		public static int ColorClamp(int ch)
		{
			if (ch < 0) return 0;
			return ch > 255 ? 255 : ch;
		}

		/// <summary>
		/// Return resolution size divided by PixelSize
		/// </summary>
		/// <returns>width / pixelsize, height / pixelsize</returns>
		protected Size CalculateNativeRenderSize()
		{
			Size size = RenderWindow.Size();
			return new Size(
				width: size.Width / PixelSize,
				height: size.Height / PixelSize
			);
		}

		public void BlitPixelsToRenderWindowChannel()
		{
			RcCore.It.AddLogStringIfVerbose("BlitPixelsToRenderWindowChannel entry");
			var renderWindowSize = RenderWindow.Size();
			int width = renderWindowSize.Width;
			int height = renderWindowSize.Height;
			var rect = new Rectangle(0, 0, width, height);
			foreach (var pass in Session.Passes)
			{
				IntPtr pixel_buffer = IntPtr.Zero;
				int pixelSizeFromCycles = 1;
				var channel = StandardChannelForPassType(pass);

				RcCore.It.AddLogStringIfVerbose($"BlitPixelsToRenderWindowChannel RetainPixelBuffer {pass} start");
				Session.RetainPixelBuffer(pass, width, height, ref pixel_buffer, ref pixelSizeFromCycles);
				RcCore.It.AddLogStringIfVerbose($"BlitPixelsToRenderWindowChannel RetainPixelBuffer {pass} end");
				if (pixel_buffer != IntPtr.Zero && pixelSizeFromCycles > 0)
				{
					RenderWindow.SetRenderOutputRect(
						new Rectangle(
							x: 0,
							y: 0,
							width: width / pixelSizeFromCycles,
							height: height / pixelSizeFromCycles
						)
					);
					using (var rgba = RenderWindow.OpenChannel(channel))
					{
						PixelBuffer pb = new PixelBuffer(pixel_buffer);
						RcCore.It.AddLogStringIfVerbose($"BlitPixelsToRenderWindowChannel rgba.SetValues {pass} start");
						rgba?.SetValues(rect, rect.Size, pb);
						RcCore.It.AddLogStringIfVerbose($"BlitPixelsToRenderWindowChannel rgba.SetValues {pass} end");
					}

					Session.ReleasePixelBuffer(pass);
				}

			}
			RcCore.It.AddLogStringIfVerbose("BlitPixelsToRenderWindowChannel exit");
		}

		/// <summary>
		/// Callback for debug logging facility. Will be called only for Debug builds of ccycles.dll
		/// </summary>
		/// <param name="msg"></param>
		public static void LoggerCallback(string msg)
		{
			RcCore.It.AddLogStringIfVerbose($"Cycles: {msg}");
		}

		/// <summary>
		/// Call to stop render thread.
		/// </summary>
		public void StopRendering()
		{
			StopTheRenderer();

			RcCore.It.AddLogStringIfVerbose($"C# Cycles render thread join start\n");
			RenderThread?.Join();
			RcCore.It.AddLogStringIfVerbose($"C# Cycles render thread join end\n");
			RenderThread = null;
		}

		public void Pause()
		{
			State = State.Waiting;
		}

		public void Continue()
		{
			State = State.Rendering;
		}


		/// <summary>
		/// This device is set for the modal render engine case when the session
		/// is used to compile the OpenCL kernels off the main thread
		/// when we do so we should just destroy the session without cancelling
		/// the session
		/// </summary>
		public Thread RenderThread { get; set; } = null;
		public bool StartRenderThread(ThreadStart threadStart, string threadName)
		{
		  RenderThread = new Thread(threadStart)
		  {
			Name = threadName
		  };
		  RenderThread.Start();
		  return true;
		}

		// Object to lock while uploading data to the session
		// This is needed to ensure we don't pull out the session
		// while still uploading the data. So this is used in data upload
		// and is tried to be acquired in StopTheRenderer.
		protected object UploadDataLock = new();

		/// <summary>
		/// Call to cancel rendering, stop and destroy the session, and change state to Stopped. At the start
		/// state is changed to Stopping
		/// </summary>
		public void StopTheRenderer()
		{
			RcCore.It.AddLogStringIfVerbose("StopTheRenderer entry");
			State = State.Stopping;

			RcCore.It.AddLogStringIfVerbose("StopTheRenderer, acquire UploadData lock...");
			// Try to get the lock. If we can't get it, we're still uploading data, so wait
			// for the upload to finish. Once we can get the lock, we know that no other actor
			// is uploading data, so we can just unlock and continue with the teardown.
			lock(UploadDataLock)
			{
				RcCore.It.AddLogStringIfVerbose("StopTheRenderer, UploadData lock acquired");
			}

			// try to get scene lock. Necessary since UploadData might still be writing to
			// the session. Wait for it to react to state being set to Stopping.
			// Once we can lock we know there is no other actor accessing the session, so
			// we can just unlock and continue with the teardown.
			Debug.Assert(Session != null);
			if (Session != null)
			{
				RcCore.It.AddLogStringIfVerbose("Session Wait, lock and unlock start");
				Session.WaitUntilLocked();
				Session.Unlock();
				RcCore.It.AddLogStringIfVerbose("Session Wait, lock and unlock end");
				RcCore.It.AddLogStringIfVerbose("Session QuickCancel start");
				Session.QuickCancel();
				RcCore.It.AddLogStringIfVerbose("Session QuickCancel end");
				Session.Cancel("StopTheRenderer");
				Thread.Sleep(500);
				RcCore.It.AddLogStringIfVerbose("Session Dispose start");
				Session.Dispose();
				RcCore.It.AddLogStringIfVerbose("Session Dispose end");
			}
			CancelRender = true;
			State = State.Stopped;
			RcCore.It.AddLogStringIfVerbose("StopTheRenderer exit");
		}

		/// <summary>
		/// Set progress to HUD if exists. Also set to RenderWindow, if it is not null.
		/// </summary>
		/// <param name="rw"></param>
		/// <param name="msg"></param>
		/// <param name="progress"></param>
		public void SetProgress(RenderWindow rw, string msg, float progress)
		{
			TriggerStatusTextUpdated(new StatusTextEventArgs(msg, progress, progress < 0 ? -1 : 0, false));
			rw?.SetProgress(msg, progress);
		}

		// handle material shader updates
		protected void Database_MaterialShaderChanged(object sender, MaterialShaderUpdatedEventArgs e)
		{
			RecreateMaterialShader(e.RcShader, e.CclShader);
			e.CclShader.Tag();
		}

		// handle light shader updates
		protected void Database_LightShaderChanged(object sender, LightShaderUpdatedEventArgs e)
		{
			ReCreateSimpleEmissionShader(e.RcLightShader, e.CclShader);
			e.CclShader.Tag();
		}

		protected void Database_FilmUpdateTagged(object sender, EventArgs e)
		{
			Session.Scene.Film.Update();
		}

		protected void Database_LinearWorkflowChanged(object sender, LinearWorkflowChangedEventArgs e)
		{
		}

		private bool disposedValue = false;

		public virtual void Dispose() {
			Dispose(true);
		}

		public virtual void Dispose(bool isDisposing) {
			if(!disposedValue)
			{
				if(isDisposing)
				{
					_bitmapConverter?.Dispose();
					Database?.Dispose();
				}
			}
		}

	}

}
