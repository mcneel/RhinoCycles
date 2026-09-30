/**
Copyright 2014-2026 Robert McNeel and Associates

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

using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoCycles.Viewport;
using RhinoCyclesCore.Converters;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("fdf3078f-f9c6-402c-b1e2-6c6934a33ff8")]
	[CommandStyle(Style.Hidden)]
	public class TestSetBumpVirtualTextureSize : Command
	{
		static TestSetBumpVirtualTextureSize _instance;
		public TestSetBumpVirtualTextureSize()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the TestSetBumpVirtualTextureSize command.</summary>
		public static TestSetBumpVirtualTextureSize Instance => _instance;

		public override string EnglishName => "TestSetBumpVirtualTextureSize";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getNumber = new GetNumber();
			getNumber.SetDefaultNumber(Procedural.BumpProceduralResolution);
			getNumber.SetLowerLimit(0.0, true);
			getNumber.SetCommandPrompt("Virtual texture size for procedural bump, in texels per texture unit");
			var getRc = getNumber.Get();
			if (getNumber.CommandResult() != Result.Success) return getNumber.CommandResult();
			if (getRc != GetResult.Number) return Result.Nothing;

			Procedural.BumpProceduralResolution = (float)getNumber.Number();
			RhinoApp.WriteLine($"Procedural bump virtual texture size: {Procedural.BumpProceduralResolution}");

			// The texel size is baked into the shaders. A frame drawn in a non-realtime mode drops
			// the view's Raytraced session, so switching back starts a new one with new shaders.
			var wireframe = DisplayModeDescription.GetDisplayMode(DisplayModeDescription.WireframeId);
			var restarted = 0;
			foreach (var view in doc.Views)
			{
				if (!(view.RealtimeDisplayMode is RenderedViewport)) continue;

				var raytraced = view.ActiveViewport.DisplayMode;
				view.ActiveViewport.DisplayMode = wireframe;
				view.Redraw();
				view.ActiveViewport.DisplayMode = raytraced;
				view.Redraw();
				restarted++;
			}
			RhinoApp.WriteLine($"Restarted {restarted} Raytraced viewport(s)");

			return Result.Success;
		}
	}
}
