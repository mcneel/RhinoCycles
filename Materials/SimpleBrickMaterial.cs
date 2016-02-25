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

using Rhino.Render;

namespace RhinoCyclesCore.Materials
{
	[System.Runtime.InteropServices.Guid("4D4E8BF5-E4DD-4AC4-A1F3-4801A923FE32")]
	public class SimpleBrickMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName { get { return "Cycles Brick Material (DEV)"; } }
		public override string TypeDescription { get { return "Cycles Brick Material (DEV)"; } }

		public float Gamma { get; set; }

		public SimpleBrickMaterial()
		{
			Fields.Add("color1", Rhino.Display.Color4f.White, "Color 1");
			Fields.Add("color2", Rhino.Display.Color4f.White, "Color 2");
			Fields.Add("mortar", Rhino.Display.Color4f.Black, "Mortar Color");
			Fields.Add("offset", 0.0f, "Offset");
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Rhino.DocObjects.Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			Rhino.Display.Color4f color;
			if (Fields.TryGetValue("color1", out color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();
		}

		public override Rhino.DocObjects.Material SimulateMaterial(bool isForDataOnly)
		{
			var m = base.SimulateMaterial(isForDataOnly);

			SimulateMaterial(ref m, isForDataOnly);

			return m;
		}


		public CyclesShader.CyclesMaterial MaterialType
		{
			get { return CyclesShader.CyclesMaterial.Brick; }
		}


		public string MaterialXml
		{
			get
			{
				var name = Name.Replace(" ", "_");
				Rhino.Display.Color4f color1;
				Fields.TryGetValue("color1", out color1);
				color1 = Rhino.Display.Color4f.ApplyGamma(color1, Gamma);

				Rhino.Display.Color4f color2;
				Fields.TryGetValue("color2", out color2);
				color2 = Rhino.Display.Color4f.ApplyGamma(color2, Gamma);

				Rhino.Display.Color4f mortar;
				Fields.TryGetValue("mortar", out mortar);

				float offset;
				Fields.TryGetValue("offset", out offset);

				var nodegraph = string.Format(
					ccl.Utilities.Instance.NumberFormatInfo,
					"<texture_coordinate name=\"texcoord\" />"+
					"<brick_texture name=\"{0}\" " +
					"color1=\"{1} {2} {3}\" " +
					"color2=\"{4} {5} {6}\" " +
					"mortar=\"{7} {8} {9}\" " +
					"offset=\"{10}\" " +
					"scale=\"2.0\" " +
					" />" +
					"<diffuse_bsdf name=\"diff\" roughness=\"0.0\"/>" +
					"<connect from=\"texcoord uv\" to=\"{0} vector\" />" +
					"<connect from=\"{0} color\" to=\"diff color\" />" +
					"<connect from=\"diff bsdf\" to=\"output surface\" />",
					name,
					color1.R, color1.G, color1.B,
					color2.R, color2.G, color2.B,
					mortar.R, mortar.G, mortar.B,
					offset
					);
				return nodegraph;
			}
		}
	}
}
