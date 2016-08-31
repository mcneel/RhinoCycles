/**
Copyright 2014-2016 Robert McNeel and Associates

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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.PlugIns;
using Rhino.Commands;
using Rhino;
using Rhino.Display;
using System.Drawing;
using Rhino.Render;
using RhinoCyclesCore;

namespace CyclesForRhino
{
	public class Plugin : RenderPlugIn
	{
		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			RhinoApp.WriteLine("Cycles for Rhino ready.");
			return LoadReturnCode.Success;
		}
		protected override bool SupportsFeature(RenderFeature feature)
		{
			if (feature == RenderFeature.CustomDecalProperties)
				return false;

			return true;
		}

		protected override Result RenderWindow(RhinoDoc doc, RunMode modes, bool fastPreview, RhinoView view, Rectangle rect, bool inWindow)
		{
			return Result.Failure;
		}

		protected override PreviewRenderTypes PreviewRenderType()
		{
			return PreviewRenderTypes.Progressive;
		}

		/// <summary>
		/// Implement the render entry point.
		/// 
		/// Rhino data is prepared for further conversion in RenderEngine.
		/// </summary>
		/// <param name="doc">Rhino document for which the render command was given</param>
		/// <param name="mode">mode</param>
		/// <param name="fastPreview">True for fast preview.</param>
		/// <returns></returns>
		protected override Result Render(RhinoDoc doc, RunMode mode, bool fastPreview)
		{
			//AsyncRenderContext a_rc = new RhinoCycles.ModalRenderEngine(doc, Id);
			var engine = new RhinoCycles.ModalRenderEngine(doc, Id) {Settings = RcCore.It.EngineSettings};

			engine.Settings.UseInteractiveRenderer = false;
			engine.Settings.SetQuality(doc.RenderSettings.AntialiasLevel);

			/* render only 3 samples if we are told to generate a fast preview. */
			if (fastPreview)
			{
				engine.Settings.Samples = 3;
			}
			// for now when using interactive renderer render indefinitely
			if (engine.Settings.UseInteractiveRenderer) engine.Settings.Samples = ushort.MaxValue;
			var renderSize = Rhino.Render.RenderPipeline.RenderSize(doc);

			var pipe = new RhinoCycles.RenderPipeline(doc, mode, this, engine);

			engine.RenderWindow = pipe.GetRenderWindow(true);
			engine.RenderDimension = renderSize;
			engine.Database.RenderDimension = renderSize;

			engine.Settings.Verbose = true;
			engine.SetFloatTextureAsByteTexture(false); // engine.Settings.RenderDeviceIsOpenCl);

			engine.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

			/* since we're an asynchronous renderer plugin we start the render process
			 * here, but, apart from data conversion and pumping, we fall right through
			 * without a complete render result.
			 */
			var rc = pipe.Render();

			if (Rhino.Render.RenderPipeline.RenderReturnCode.Ok != rc)
			{
				RhinoApp.WriteLine("Rendering failed:" + rc.ToString());
				return Result.Failure;
			}

			return Result.Success;
		}

		/// <summary>
		/// Handler for rendering preview thumbnails.
		/// 
		/// The CreatePreviewEventArgs parameter contains a simple
		/// scene description to be rendered. It contains a set of meshes
		/// and lights. Meshes have RenderMaterials attached to them.
		/// </summary>
		/// <param name="scene">The scene description to render, along with the requested quality setting</param>
		protected override void CreatePreview(CreatePreviewEventArgs scene)
		{
			scene.SkipInitialisation();

			//InitialiseCSycles();

			if (scene.Quality == PreviewSceneQuality.RealtimeQuick)
			{
				scene.PreviewImage = null;
				return;
			}

			AsyncRenderContext a_rc = new RhinoCycles.PreviewRenderEngine(scene, Id);
			var engine = (RhinoCycles.PreviewRenderEngine)a_rc;
			engine.Settings = RcCore.It.EngineSettings;
			engine.Settings.IgnoreQualityChanges = true;
			engine.Settings.SetQuality(PreviewSceneQuality.RefineThirdPass);

			engine.RenderDimension = scene.PreviewImageSize;
			/* create a window-less, non-document controlled render window */
			engine.RenderWindow = Rhino.Render.RenderWindow.Create(scene.PreviewImageSize);
			engine.Database.RenderDimension = engine.RenderDimension;

			engine.SetFloatTextureAsByteTexture(false); // engine.Settings.RenderDeviceIsOpenCl);

			engine.CreateWorld();

			/* render the preview scene */
			RhinoCycles.PreviewRenderEngine.Renderer(engine);

			/* set final preview bitmap, or null if cancelled */
			scene.PreviewImage = engine.RenderWindow.GetBitmap();

#if DEBUGX
			var prev = string.Format("{0}\\{1}.jpg", System.Environment.GetEnvironmentVariable("TEMP"), "previmg");
			scene.PreviewImage.Save(prev, ImageFormat.Jpeg);
#endif
		}
	}
}
