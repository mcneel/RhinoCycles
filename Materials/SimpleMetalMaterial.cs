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
using System.Drawing;
using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;

namespace RhinoCyclesCore.Materials
{
	[Guid("0507F137-D483-4098-891A-DD58741C2C8D")]
	public class SimpleMetalMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName
		{
			get { return "Cycles Metal"; }
		}

		public override string TypeDescription
		{
			get { return "Cycles Metal Material"; }
		}

		public float Gamma { get; set; }

		public CyclesShader.CyclesMaterial MaterialType
		{
			get { return CyclesShader.CyclesMaterial.SimpleMetal; }
		}

		public SimpleMetalMaterial()
		{
			Fields.Add("metal-color", Color4f.White, "Metal Color");
			Fields.Add("metal-polish", 0.0f, "Polish");
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			simulatedMaterial.Reflectivity = 1.0;
			simulatedMaterial.Transparency = 0.0;
			simulatedMaterial.FresnelReflections = false;
			simulatedMaterial.DiffuseColor = Color.Black;

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

		public override Material SimulateMaterial(bool isForDataOnly)
		{
			var m = base.SimulateMaterial(isForDataOnly);

			SimulateMaterial(ref m, isForDataOnly);

			return m;
		}


		public string MaterialXml
		{
			get
			{
				Color4f color;
				float polish;

				Fields.TryGetValue("metal-color", out color);
				Fields.TryGetValue("metal-polish", out polish);

				polish = (float)Math.Pow(polish, 2);

				color = Color4f.ApplyGamma(color, Gamma);

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
	}
}
