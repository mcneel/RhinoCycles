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

using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using Utilities = ccl.Utilities;

namespace RhinoCyclesCore.Materials
{
	[Guid("5C717BA9-C033-48D1-A03A-CC2E8A49E540")]
	public class FlakedCarPaintMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Flaked Car Paint Material (DEV)";
		public override string TypeDescription => "Cycles Flaked Car Paint Material (DEV)";

		public float Gamma { get; set; }

		private Color4f FlakeColor1 { get; set; }
		private Color4f CoatColor1 { get; set; }
		private Color4f FlakeColor2 { get; set; }
		private Color4f CoatColor2 { get; set; }
		private float FlakeSize { get; set; }
		private float Fresnel { get; set; }
		private float MixFactor { get; set; }

		public FlakedCarPaintMaterial()
		{
			var flake1 = new Color4f(0.002f, 0.502f, 0.002f, 1.0f);
			FlakeColor1 = flake1;
			var shine1 = new Color4f(0.0f, 0.048f, 0.5f, 1.0f);
			CoatColor1 = shine1;
			var flake2 = new Color4f(0.098f, 0.503f, 0.102f, 1.0f);
			FlakeColor2 = flake2;
			var shine2 = new Color4f(0.503f, 0.0f, 0.001f, 1.0f);
			CoatColor2 = shine2;
			FlakeSize = 100.0f;
			Fresnel = 1.56f;
			MixFactor = 0.5f;
			Fields.Add("flake1", flake1, "Flake Color 1");
			Fields.Add("shine1", shine1, "Coat Shine 1");
			Fields.Add("flake2", flake2, "Flake Color 2");
			Fields.Add("shine2", shine2, "Coat Shine 2");
			Fields.Add("flakesize", FlakeSize, "Flake Size");
			Fields.Add("fresnel", Fresnel, "Fresnel");
			Fields.Add("gamma", MixFactor, "Flake and Shine Mix Factor");
		}

		public void BakeParameters()
		{
			Color4f color;
			if (Fields.TryGetValue("flake1", out color))
			{
				FlakeColor1 = color;
			}
			if (Fields.TryGetValue("flake2", out color))
			{
				FlakeColor2 = color;
			}
			if (Fields.TryGetValue("shine1", out color))
			{
				CoatColor1 = color;
			}
			if (Fields.TryGetValue("shine2", out color))
			{
				CoatColor2 = color;
			}
			float val;
			if (Fields.TryGetValue("flakesize", out val))
			{
				FlakeSize = val;
			}
			if (Fields.TryGetValue("fresnel", out val))
			{
				Fresnel = val;
			}
			if (Fields.TryGetValue("gamma", out val))
			{
				MixFactor = val;
			}
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			Color4f color;
			if (Fields.TryGetValue("flake1", out color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();
			if (Fields.TryGetValue("shine1", out color))
				simulatedMaterial.ReflectionColor = color.AsSystemColor();
		}

		public override Material SimulateMaterial(bool isForDataOnly)
		{
			var m = base.SimulateMaterial(isForDataOnly);

			SimulateMaterial(ref m, isForDataOnly);

			return m;
		}

		public CyclesShader.CyclesMaterial MaterialType => CyclesShader.CyclesMaterial.FlakedCarPaint;


		public string MaterialXml
		{
			get
			{
				var flake1 = Color4f.ApplyGamma(FlakeColor1, Gamma);

				var flake2 = Color4f.ApplyGamma(FlakeColor2, Gamma);

				var shine1 = Color4f.ApplyGamma(CoatColor1, Gamma);

				var shine2 = Color4f.ApplyGamma(CoatColor2, Gamma);

				var nodegraph = string.Format(
					Utilities.Instance.NumberFormatInfo,
					"<voronoi_texture name=\"voronoi\" coloring=\"Cells\" scale=\"{0}\" />" +
					"<layer_weight name=\"layer1\" blend=\"0.5\" />" +
					"<layer_weight name=\"layer2\" blend=\"0.5\" />" +
					"<gamma name=\"gamma\" gamma=\"{1}\" />" +
					"<fresnel name=\"userfresnel\" ior=\"{2}\" />" +
					"<fresnel name=\"fresnel\" ior=\"2.0\" />" +
					"<glossy_bsdf name=\"shine\" color=\"1.0 1.0 1.0\" />" +
					"<glossy_bsdf name=\"flake2glossy\" roughness=\"0.0\" />" +
					"<diffuse_bsdf name=\"flake1diff\" roughness=\"0.0\" />" +
					"<mix name=\"flake1\" color1=\"{3} {4} {5}\" color2=\"{6} {7} {8}\" />" +
					"<mix name=\"flake2\" color1=\"{9} {10} {11}\" color2=\"{12} {13} {14}\" />" +
					"<mix_closure name=\"mixflakes\" />" +
					"<mix_closure name=\"mixinglossiness\" />" +

					"<connect from=\"voronoi color\" to=\"gamma color\" />" +
					"<connect from=\"gamma color\" to=\"mixflakes fac\" />" +
					"<connect from=\"layer1 facing\" to=\"flake1 fac\" />" +
					"<connect from=\"layer2 facing\" to=\"flake2 fac\" />" +
					"<connect from=\"flake1 color\" to=\"flake1diff color\" />" +
					"<connect from=\"flake2 color\" to=\"flake2glossy color\" />" +
					"<connect from=\"fresnel fac\" to=\"flake2glossy roughness\" />" +
					"<connect from=\"flake1diff bsdf\" to=\"mixflakes closure1\" />" +
					"<connect from=\"flake2glossy bsdf\" to=\"mixflakes closure2\" />" +
					"<connect from=\"userfresnel fac\" to=\"mixinglossiness fac\" />" +
					"<connect from=\"mixflakes closure\" to=\"mixinglossiness closure1\" />" +
					"<connect from=\"shine bsdf\" to=\"mixinglossiness closure2\" />" +

					"<connect from=\"mixinglossiness closure\" to=\"output surface\" />",
					FlakeSize, MixFactor, Fresnel,
					flake1.R, flake1.G, flake1.B,
					shine1.R, shine1.G, shine1.B,
					flake2.R, flake2.G, flake2.B,
					shine2.R, shine2.G, shine2.B
					);
				return nodegraph;
			}
		}
	}
}
