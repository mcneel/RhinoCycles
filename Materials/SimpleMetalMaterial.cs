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
using System.Drawing;
using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using ccl.ShaderNodes.Sockets;

namespace RhinoCyclesCore.Materials
{
	[Guid("0507F137-D483-4098-891A-DD58741C2C8D")]
	[CustomRenderContent(IsPrivate=true)]
	public class SimpleMetalMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Metal";

		public override string TypeDescription => "Cycles Metal Material";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.SimpleMetal;

		private Color4f Color { get; set; }
		private float Roughness { get; set; }

		public SimpleMetalMaterial()
		{
			Color = Color4f.White;
			Roughness = 0.0f;
			Fields.Add("metal-color", Color4f.White, "Metal Color");
			Fields.Add("metal-polish", 0.0f, "Roughness");
		}

		public void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			Color4f col;
			if (Fields.TryGetValue("metal-color", out col))
			{
				Color = col;
			}
			float val;
			if (Fields.TryGetValue("metal-polish", out val))
			{
				Roughness = val;
			}

		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, RenderTexture.TextureGeneration tg)
		{
			base.SimulateMaterial(ref simulatedMaterial, tg);

			simulatedMaterial.Reflectivity = 1.0;
			simulatedMaterial.Transparency = 0.0;
			simulatedMaterial.FresnelReflections = false;
			simulatedMaterial.DiffuseColor = System.Drawing.Color.Black;

			Color4f color;
			if (Fields.TryGetValue("metal-color", out color))
				simulatedMaterial.ReflectionColor = color.AsSystemColor();

			float f;
			if (Fields.TryGetValue("metal-polish", out f))
			{
				simulatedMaterial.ReflectionGlossiness = 1.0 - f;
			}

			simulatedMaterial.Name = Name;
		}



		public string MaterialXml
		{
			get
			{
				var polish = (float)Math.Pow(Roughness, 2);
				var color = Color4f.ApplyGamma(Color, Gamma);

				return string.Format(
					ccl.Utilities.Instance.NumberFormatInfo,
					"<glossy_bsdf color=\"{0} {1} {2}\" roughness=\"{3}\" name=\"metal\" />" +

					"<connect from=\"metal bsdf\" to=\"output surface\" />" +
					"",

					color.R, color.G, color.B,
					polish
					);
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
