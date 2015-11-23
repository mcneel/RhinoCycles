﻿/**
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
using ccl;
using sdd = System.Diagnostics.Debug;
using Rhino.Render;

namespace RhinoCycles
{
	public class PreviewRenderEngine : RenderEngine
	{

		/// <summary>
		/// Construct a render engine for preview rendering
		/// </summary>
		/// <param name="createPreviewEventArgs"></param>
		/// <param name="pluginId">Id of the plugin for which the render engine is created</param>
		public PreviewRenderEngine(CreatePreviewEventArgs createPreviewEventArgs, Guid pluginId) : base (pluginId, createPreviewEventArgs, false)
		{
			RenderThread = null;
			Client = new Client();
			State = State.Rendering;

			Database.MaterialShaderChanged += Database_MaterialShaderChanged;
			Database.LightShaderChanged += Database_LightShaderChanged;

#region create callbacks for Cycles
			m_update_callback = UpdateCallback;
			m_update_render_tile_callback = UpdateRenderTileCallback;
			m_write_render_tile_callback = WriteRenderTileCallback;
			m_test_cancel_callback = TestCancel;

			CSycles.log_to_stdout(false);
#endregion
		}
		/// <summary>
		/// Renderer entry point for preview rendering
		/// </summary>
		/// <param name="oPipe"></param>
		public static void Renderer(object oPipe)
		{
			var cycles_engine = (PreviewRenderEngine)oPipe;

			var client = cycles_engine.Client;

			var size = cycles_engine.RenderDimension;
			var samples = cycles_engine.Settings.Samples;

			cycles_engine.m_measurements.Reset();

#region pick a render device

			var render_device = cycles_engine.Settings.SelectedDevice == -1
				? Device.FirstCuda
				: Device.GetDevice(cycles_engine.Settings.SelectedDevice);

			if (cycles_engine.Settings.Verbose) sdd.WriteLine(String.Format("Using device {0}", render_device.Name + " " + render_device.Description));
#endregion

			if (cycles_engine.CancelRender) return;

			var scene = CreateScene(client, render_device, cycles_engine);

			#region set up session parameters
			var session_params = new SessionParameters(client, render_device)
			{
				Experimental = false,
				Samples = samples,
				TileSize = render_device.IsCuda ? new Size(256, 256) : new Size(32, 32),
				Threads = (uint)(render_device.IsCuda ? 0 : cycles_engine.Settings.Threads),
				ShadingSystem = ShadingSystem.SVM,
				Background = true,
				ProgressiveRefine = false,
				Progressive = false,
			};
#endregion

			if (cycles_engine.CancelRender) return;

#region create session for scene
			cycles_engine.Session = new Session(client, session_params, scene);
#endregion

			// register callbacks before starting any rendering
			cycles_engine.SetCallbacks();

			// main render loop, including restarts
			cycles_engine.Database.OneShot();
			cycles_engine.m_flush = false;
			cycles_engine.UploadData();

			// lets first reset session
			cycles_engine.Session.Reset((uint)size.Width, (uint)size.Height, (uint)samples);
			// then reset scene
			cycles_engine.Session.Scene.Reset();
			// and actually start
			// we're rendering again
			cycles_engine.Session.Start();
			// ... aaaaand we wait
			cycles_engine.Session.Wait();

			cycles_engine.CancelRender = true;

			// we're done now, so lets clean up our session.
			cycles_engine.Session.Destroy();
		}

	}

}
