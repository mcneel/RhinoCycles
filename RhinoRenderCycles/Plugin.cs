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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.PlugIns;
using Rhino.Render;
using Rhino.UI;
using Rhino.UI.Controls;
using RhinoCyclesCore.RenderEngines;
using RhinoCyclesCore.Settings;
using static Rhino.Render.RenderWindow;

namespace CyclesForRhino.CyclesForRhino
{
  public class Plugin : RenderPlugIn
  {
    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    /// <summary>
    /// Overrideing to get the localized name to appear in the current
    /// render plug-in menu
    /// </summary>
    protected override string LocalPlugInName => Rhino.UI.Localization.LocalizeString("Rhino Render", this, 1);

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
      Rectangle fullSize = new Rectangle(new Point(0, 0), RenderPipeline.RenderSize(doc, true));
      return RenderWithCycles(doc, mode, fullSize, true, fastPreview);
    }

    protected override Result RenderWindow(RhinoDoc doc, RunMode mode, bool fastPreview, RhinoView view, Rectangle rect, bool inWindow, bool blowup)
    {
      return RenderWithCycles(doc, mode, rect, inWindow, fastPreview);
    }

    private Result RenderWithCycles(RhinoDoc doc, RunMode mode, Rectangle rect, bool inWindow, bool fastPreview)
    {
      var rc = RenderPipeline.RenderReturnCode.InternalError;
      using (var rsv = new RenderSourceView(doc))
      {
        ViewInfo vi = rsv.GetViewInfo();
        if (vi == null)
          return Result.Failure;

        {
          foreach (var vw in doc.Views)
          {
            try
            {
              if (vw != null && vw.RealtimeDisplayMode != null)
              {
                vw.RealtimeDisplayMode.Paused = true;
              }
            }
            catch (Exception)
            {
              // pass
            }
          }
        }

        Size vpSize = vi.Viewport.GetScreenPort().Size;

        bool partial = rect.Top > 0 || rect.Left > 0;

        var renderSize = new Size(rect.Width, rect.Height);
        var fullSize = inWindow && !partial ? renderSize : vpSize;
        ModalRenderEngine engine = new ModalRenderEngine(doc, Id, vi, true)
        {
          BufferRectangle = rect,
          FullSize = fullSize,
          FastPreview = fastPreview,
        };
        var pipe = new RhinoCycles.RenderPipeline(doc, mode, this, renderSize, engine);

        engine.RenderWindow = pipe.GetRenderWindow(vi.Viewport, false, rect);
        engine.RenderWindow.SetSize(renderSize);

        var requestedChannels = engine.RenderWindow.GetRequestedRenderChannelsAsStandardChannels();

        List<StandardChannels> wireframes = new List<StandardChannels>();
        wireframes.Add(StandardChannels.WireframeAnnotationsRGBA);
        wireframes.Add(StandardChannels.WireframeCurvesRGBA);
        wireframes.Add(StandardChannels.WireframeIsocurvesRGBA);
        wireframes.Add(StandardChannels.WireframePointsRGBA);

        List<RenderWindow.StandardChannels> reqChanList = requestedChannels
            .Distinct()
            .Where(chan =>
                chan != StandardChannels.AlbedoRGB &&
                chan != StandardChannels.WireframeAnnotationsRGBA &&
                chan != StandardChannels.WireframeCurvesRGBA &&
                chan != StandardChannels.WireframeIsocurvesRGBA &&
                chan != StandardChannels.WireframePointsRGBA
            )
            .ToList();

        var needWireframeChannels = requestedChannels
          .Intersect(wireframes)
          .Any();

        foreach(var reqChan in reqChanList) {
          engine.RenderWindow.AddChannel(reqChan);
        }

        engine.RenderDimension = renderSize;
        engine.Database.RenderDimension = renderSize;

        EngineDocumentSettings eds = new EngineDocumentSettings(doc.RuntimeSerialNumber);
        if (fastPreview)
        {
            engine._textureBakeQuality = 0;
        }
        else
        {
            engine._textureBakeQuality = eds.TextureBakeQuality;
        }

        engine.CreateWorld(); // has to be done on main thread, so lets do this just before starting render session

        if (inWindow)
          rc = pipe.Render();
        else
          rc = pipe.RenderWindow(doc.Views.ActiveView, rect, inWindow);

        pipe.Dispose();
      }

      if (Rhino.Render.RenderPipeline.RenderReturnCode.Ok != rc)
      {
        RhinoApp.WriteLine(Localization.LocalizeString("Render setup failed:", 2) + RenderPipeline.LocalizeRenderReturnCode(rc));
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

      if (scene.Quality == PreviewSceneQuality.Low)
      {
        scene.PreviewImage = null;
        return;
      }

      var active_doc = RhinoDoc.ActiveDoc;
      if (active_doc == null)
        return;

      var engine = new PreviewRenderEngine(scene, Id, active_doc.RuntimeSerialNumber)
      {
        BufferRectangle = new Rectangle(new Point(0, 0), scene.PreviewImageSize),
        FullSize = scene.PreviewImageSize
      };

      engine.RenderDimension = scene.PreviewImageSize;
      /* create a window-less, non-document controlled render window */
      engine.RenderWindow = Rhino.Render.RenderWindow.Create(scene.PreviewImageSize);
      engine.RenderWindow.SetSize(scene.PreviewImageSize);
      engine.Database.RenderDimension = engine.RenderDimension;
      engine.RenderWindow.AddChannel(Rhino.Render.RenderWindow.StandardChannels.RGBA);

      engine.CreateWorld();

      /* render the preview scene */
      PreviewRenderEngine.Renderer(engine);

      /* set final preview bitmap, or null if cancelled */
      scene.PreviewImage = engine.Success ? engine.RenderWindow.GetBitmap() : null;

      engine.RenderWindow.Dispose();
    }

    public override void RenderSettingsCustomSections(List<ICollapsibleSection> sections) {
      sections.Add(new AdvancedSettingsSection(Id));
    }
  }
}
