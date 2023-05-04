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

using ccl.ShaderNodes.Sockets;
using Rhino.Display;
using Rhino.Render;
using System;

namespace RhinoCyclesCore.Materials
{
	[System.Runtime.InteropServices.Guid("4D4E8BF5-E4DD-4AC4-A1F3-4801A923FE32")]
	[CustomRenderContent(IsPrivate=true)]
	public class SimpleBrickMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Brick Material (DEV)";
		public override string TypeDescription => "Cycles Brick Material (DEV)";

		public float Gamma { get; set; }

		private Color4f Color1 { get; set; }
		private Color4f Color2 { get; set; }
		private Color4f Mortar { get; set; }
		private float Offset { get; set; }

		public SimpleBrickMaterial()
		{
			Color1 = Color2 = Color4f.White;
			Mortar = Color4f.Black;
			Offset = 0.33f;
			Fields.Add("color1", Color1, "Color 1");
			Fields.Add("color2", Color2, "Color 2");
			Fields.Add("mortar", Mortar, "Mortar Color");
			Fields.Add("offset", Offset, "Offset");
		}

		public void BakeParameters(Converters.BitmapConverter bitmapConverter)
		{
			Color4f col;
			if (Fields.TryGetValue("color1", out col))
			{
				Color1 = col;
			}
			if (Fields.TryGetValue("color2", out col))
			{
				Color2 = col;
			}
			if (Fields.TryGetValue("mortar", out col))
			{
				Mortar = col;
			}
			float val;
			if (Fields.TryGetValue("offset", out val))
			{
				Offset = val;
			}

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



		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.Brick;


		public string MaterialXml
		{
			get
			{
				var name = "brick";
				var color1 = Color4f.ApplyGamma(Color1, Gamma);
				var color2 = Color4f.ApplyGamma(Color2, Gamma);

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
					Mortar.R, Mortar.G, Mortar.B,
					Offset
					);
				return nodegraph;
			}
		}
		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			try
			{
				ccl.Shader.ShaderFromXml(sh, MaterialXml, finalize);
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}
		public ClosureSocket GetClosureSocket(ccl.Shader sh)
		{
			return sh.Output.ins.Surface.ConnectionFrom as ClosureSocket;
		}

		public Converters.BitmapConverter BitmapConverter { get; set; }
	}
}
