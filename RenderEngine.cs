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
using ccl;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using sdd = System.Diagnostics.Debug;
using CclLight = ccl.Light;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;

namespace RhinoCycles
{

	public enum State
	{
		Waiting,
		Uploading,
		Rendering,
		Stopped
	}

	/// <summary>
	/// The actual render engine, ready for asynchronous work in Rhino.
	/// </summary>
	public partial class RenderEngine : AsyncRenderContext
	{
		private readonly object m_flushlock = new object();

		protected CreatePreviewEventArgs m_preview_event_args;

		protected Guid m_plugin_id = Guid.Empty;

		/// <summary>
		/// Reference to the client representation of this render engine instance.
		/// </summary>
		public Client Client { get; set; }

		/// <summary>
		/// Current render engine state.
		/// </summary>
		public State State { get; set; }

		/// <summary>
		/// Reference to the session of this render engine instance.
		/// </summary>
		public Session Session = null;

		/// <summary>
		/// Reference to the thread in which this render engine session lives.
		/// </summary>
		public Thread RenderThread { get; set; }

		/// <summary>
		/// Reference to the RenderWindow into which we're rendering.
		/// 
		/// Can be null, for instance in the case of material preview rendering
		/// </summary>
		public RenderWindow RenderWindow { get; set; }

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

		public int RenderedSamples;

		public string TimeString;

		protected CSycles.UpdateCallback m_update_callback;
		protected CSycles.RenderTileCallback m_update_render_tile_callback;
		protected CSycles.RenderTileCallback m_write_render_tile_callback;
		protected CSycles.TestCancelCallback m_test_cancel_callback;

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
				lock (m_flushlock)
				{
					m_flush = value;
				}
			}
		}

		/// <summary>
		/// Our instance of the change queue. This is our access point for all
		/// data. The ChangeQueue mechanism will push data to it, record it
		/// with all necessary book keeping to track the data relations between
		/// Rhino and Cycles.
		/// </summary>
		public ChangeDatabase Database { get; set; }



		/// <summary>
		/// a approx avg measurement device.
		/// </summary>
		protected readonly Measurement m_measurements = new Measurement();

		/// <summary>
		/// Return true if any change has been received through the changequeue
		/// </summary>
		/// <returns>true if any changes have been received.</returns>
		private bool HasSceneChanges()
		{
			return Database.HasChanges();
		}

		/// <summary>
		/// Check if we should change render engine status. If the changequeue
		/// has notified us of any changes Flush will be true. If we're rendering
		/// then move to State.Halted and cancel our current render progress.
		/// </summary>
		private void CheckFlushQueue()
		{
			// not rendering, nor flush needed, bail
			if (State != State.Rendering || Database == null || !Flush) return;

			// We've been told we need to flush, so cancel current render
			//State = State.Halted;
			// acquire lock while flushing queue and uploading any data
			lock (m_flushlock)
			{
				// flush the queue
				Database.Flush();

				// reset flush flag directly, since we already have lock.
				m_flush = false;

				// if we've got actually changes we care about
				// lets upload that
				if (HasSceneChanges())
				{
					State = State.Uploading;
					if (Session != null) Session.Cancel("Scene changes detected.\n");
				}
			}
		}

		protected uint m_doc_serialnumber;
		protected RhinoView m_view;

		public RhinoDoc Doc
		{
			get { return RhinoDoc.FromRuntimeSerialNumber(m_doc_serialnumber); }
		}

		public ViewportInfo ViewportInfo
		{
			get { return new ViewportInfo(m_view.ActiveViewport); }
		}

#region CONSTRUCTORS

		public RenderEngine() { }

#endregion

		/// <summary>
		/// Tell our changequeue instance to initialise world.
		/// </summary>
		public void CreateWorld()
		{
			Database.CreateWorld();
		}

		/// <summary>
		/// True if rendering for preview
		/// </summary>
		/// <returns></returns>
		public bool IsPreview()
		{
			return Database.IsPreview;
		}

		/// <summary>
		/// Flush
		/// </summary>
		public void FlushIt()
		{
			Database.Flush();
		}

		public void TestCancel(uint sid)
		{
			if (State == State.Stopped) return;

			if (m_preview_event_args != null)
			{
				if (m_preview_event_args.Cancel)
				{
					CancelRender = true;
					Session.Cancel("Preview Cancelled");
				}
			}
		}

		public class StatusTextEventArgs
		{
			public StatusTextEventArgs(string s, float progress, int samples)
			{
				StatusText = s;
				Progress = progress;
				Samples = samples;
			}

			public string StatusText { get; private set; }
			public float Progress { get; private set; }
			public int Samples { get; private set; }
		}

		public delegate void StatusTextHandler(object sender, StatusTextEventArgs e);
		public event StatusTextHandler StatusTextEvent;

		/// <summary>
		/// Handle status updates
		/// </summary>
		/// <param name="sid"></param>
		public void UpdateCallback(uint sid)
		{
			if (State == State.Stopped) return;

			var status = CSycles.progress_get_status(Client.Id, sid);
			var substatus = CSycles.progress_get_substatus(Client.Id, sid);
			RenderedSamples = CSycles.progress_get_sample(Client.Id, sid);
			int tile;
			float progress;
			double total_time, render_time, tile_time;
			CSycles.progress_get_tile(Client.Id, sid, out tile, out total_time, out render_time, out tile_time);
			CSycles.progress_get_progress(Client.Id, sid, out progress, out total_time, out render_time, out tile_time);
			int hr = ((int)total_time) / (60 * 60);
			int min = (((int)total_time) / 60) % 60;
			int sec = ((int)total_time) % 60;
			int hun = ((int)(total_time * 100.0)) % 100;

			if (!substatus.Equals(string.Empty)) status = status + ": " + substatus;

			TimeString = String.Format("{0}h {1}m {2}.{3}s", hr, min, sec, hun);

			status = String.Format("{0} {1}", status, TimeString);

			// don't set full 100% progress here yet, because that signals the renderwindow the end of async render
			if (progress >= 0.9999f) progress = 0.9999f;
			if (Settings.Samples == ushort.MaxValue) progress = -1.0f;
			if (null != RenderWindow) RenderWindow.SetProgress(status, progress);

			if (StatusTextEvent != null)
			{
				StatusTextEvent(this, new StatusTextEventArgs(status, progress, RenderedSamples));
			}

			CheckFlushQueue();
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
		/// Update the RenderWindow or RenderBitmap with the updated tile from
		/// Cycles render progress.
		/// </summary>
		/// <param name="sessionId"></param>
		/// <param name="tx"></param>
		/// <param name="ty"></param>
		/// <param name="tw"></param>
		/// <param name="th"></param>
		public void DisplayBuffer(uint sessionId, uint tx, uint ty, uint tw, uint th)
		{
			if (State == State.Stopped) return;
			var start = DateTime.Now;
			var rg = RenderBitmap;
			if (RenderWindow != null)
			{
				using (var channel = RenderWindow.OpenChannel(RenderWindow.StandardChannels.RGBA))
				{
					if (channel != null)
					{
						var pixelbuffer = new PixelBuffer(CSycles.session_get_buffer(Client.Id, sessionId));
						var size = Client.Scene.Camera.Size;
						var rect = new Rectangle((int) tx, (int) ty, (int) tw, (int) th);
						channel.SetValues(rect, size, pixelbuffer);
						RenderWindow.InvalidateArea(rect);
					}
				}
			}
			else if (rg != null)
			{
				uint buffer_size;
				uint buffer_stride;
				var width = RenderDimension.Width;
				CSycles.session_get_buffer_info(Client.Id, sessionId, out buffer_size, out buffer_stride);
				var pixels = CSycles.session_copy_buffer(Client.Id, sessionId, buffer_size);
				for (var x = (int)tx; x < (int)(tx + tw); x++)
				{
					for (var y = (int)ty; y < (int)(ty + th); y++)
					{
						var i = y * width * 4 + x * 4;
						var r = pixels[i];
						var g = pixels[i + 1];
						var b = pixels[i + 2];
						var a = pixels[i + 3];

						if (float.IsNaN(r)) r = 0.0f;
						if (float.IsNaN(g)) g = 0.0f;
						if (float.IsNaN(b)) b = 0.0f;
						if (float.IsNaN(a)) a = 0.0f;
						r = Math.Min(Math.Abs(r), 1.0f);
						g = Math.Min(Math.Abs(g), 1.0f);
						b = Math.Min(Math.Abs(b), 1.0f);
						a = Math.Min(Math.Abs(a), 1.0f);

						var c4_f = new Color4f(r, g, b, a);
						rg.SetPixel(x, y, c4_f.AsSystemColor());
					}
				}
			}
			var diff = (DateTime.Now - start).TotalMilliseconds;
			m_measurements.Add(diff);
		}

		/// <summary>
		/// Callback for debug logging facility. Will be called only for Debug builds of ccycles.dll
		/// </summary>
		/// <param name="msg"></param>
		public static void LoggerCallback(string msg)
		{
#if DEBUG
			sdd.WriteLine(String.Format("DBG: {0}", msg));
#endif
		}

		/// <summary>
		/// Handle write render tile callback
		/// </summary>
		/// <param name="sessionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="w"></param>
		/// <param name="h"></param>
		/// <param name="depth"></param>
		public void WriteRenderTileCallback(uint sessionId, uint x, uint y, uint w, uint h, uint depth)
		{
			if (State == State.Stopped) return;
			DisplayBuffer(sessionId, x, y, w, h);
		}

		/// <summary>
		/// Handle update render tile callback
		/// </summary>
		/// <param name="sessionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="w"></param>
		/// <param name="h"></param>
		/// <param name="depth"></param>
		public void UpdateRenderTileCallback(uint sessionId, uint x, uint y, uint w, uint h, uint depth)
		{
			if (State == State.Stopped) return;
			DisplayBuffer(sessionId, x, y, w, h);
		}

		/// <summary>
		/// Called when user presses the stop render button.
		/// </summary>
		override public void StopRendering()
		{
			if (RenderThread == null) return;

			StopTheRenderer();

			// done, let everybody know it
			if(Settings.Verbose) sdd.WriteLine("Rendering stopped. The render window can be closed safely.");
		}

		private void StopTheRenderer()
		{
			// signal that we should stop rendering.
			CancelRender = true;

			// get rid of our change queue
			Database.Dispose();
			Database = null;

			// set state to stopped
			State = State.Stopped;

			// signal our cycles session to stop rendering.
			if (Session != null) Session.Cancel("Render stop called.\n");

			// let's get back into the thread.
			if (RenderThread != null)
			{
				RenderThread.Join();
				RenderThread = null;
			}
		}

		/// <summary>
		/// Set progress to RenderWindow, if it is not null.
		/// </summary>
		/// <param name="rw"></param>
		/// <param name="msg"></param>
		/// <param name="progress"></param>
		public void SetProgress(RenderWindow rw, string msg, float progress)
		{
			if (null != rw) rw.SetProgress(msg, progress);
		}

		/// <summary>
		/// Register the callbacks to the render engine session
		/// </summary>
		protected void SetCallbacks()
		{
			#region register callbacks with Cycles session

			Session.UpdateCallback = m_update_callback;
			Session.UpdateTileCallback = m_update_render_tile_callback;
			Session.WriteTileCallback = m_write_render_tile_callback;
			Session.TestCancelCallback = m_test_cancel_callback;

			#endregion
		}
	}

}
