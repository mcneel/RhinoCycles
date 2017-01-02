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
using Rhino.Render;

namespace RhinoCyclesCore.Materials
{
	[Guid("E64050E9-521F-44F3-BFDA-EFEAFA73625E")]
	[CustomRenderContent(IsPrivate=true)]
	public class TranslucentMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Translucent Material (DEV)";
		public override string TypeDescription => "Translucent Material (DEV)";

		public float Gamma { get; set; }

		private Color4f Diffuse { get; set; }

		public TranslucentMaterial()
		{
			Diffuse = Color4f.White;
			Fields.Add("diffuse_color", Diffuse, "Diffuse Color");
		}

		public void BakeParameters()
		{
			Color4f col;
			if (Fields.TryGetValue("diffuse_color", out col))
			{
				Diffuse = col;
			}
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Rhino.DocObjects.Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			Color4f color;
			if (Fields.TryGetValue("diffuse_color", out color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();
		}

		public override Rhino.DocObjects.Material SimulateMaterial(bool isForDataOnly)
		{
			var m = base.SimulateMaterial(isForDataOnly);

			SimulateMaterial(ref m, isForDataOnly);

			return m;
		}


		public string MaterialXml
		{
			get
			{
				Color4f color = Color4f.ApplyGamma(Diffuse, Gamma);

				return string.Format(
					ccl.Utilities.Instance.NumberFormatInfo,
					"<diffuse_bsdf color=\"{0} {1} {2}\" name=\"diff\" />" +
					"<translucent_bsdf color=\"{0} {1} {2}\" name=\"translucent\" />" +
					"<mix_closure name=\"mix\" fac=\"0.5\" />" +
					"<connect from=\"diff bsdf\" to=\"mix closure1\" />" +
					"<connect from=\"translucent bsdf\" to=\"mix closure2\" />" +
					"<connect from=\"mix closure\" to=\"output surface\" />" +
			             " ", color.R, color.G, color.B);
			}
		}

		public CyclesShader.CyclesMaterial MaterialType => CyclesShader.CyclesMaterial.Translucent;
	}
}