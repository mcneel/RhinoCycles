/**
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

using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;

namespace RhinoCycles.Materials
{
	[Guid("9418BFA6-B894-4E41-9AC0-9217B714A4D5")]
	public class PhongTestMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName { get { return "Phong Test Material (DEV)"; } }
		public override string TypeDescription { get { return "Phong Test Material (DEV)"; } }

		public float Gamma { get; set; }

		public PhongTestMaterial()
		{
			Fields.Add("diffuse_color", Color4f.White, "Diffuse Color");
			Fields.Add("specular_color", Color4f.White, "Specular Color");
			Fields.Add("roughness", 0.17f, "Roughness");
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			Color4f color;
			if (Fields.TryGetValue("diffuse_color", out color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();
			if (Fields.TryGetValue("specular_color", out color))
				simulatedMaterial.SpecularColor = color.AsSystemColor();
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
				Color4f spec;
				float roughness;

				Fields.TryGetValue("diffuse_color", out color);
				Fields.TryGetValue("specular_color", out spec);
				Fields.TryGetValue("roughness", out roughness);
				color = Color4f.ApplyGamma(color, Gamma);
				spec = Color4f.ApplyGamma(spec, Gamma);

				var xml = string.Format("<glossy_bsdf color=\"{0} {1} {2}\" name=\"phong\" distribution=\"phong\" roughness=\"{3}\" />" +
					"<diffuse_bsdf name=\"diff\" color=\"{4} {5} {6}\" />" +
					"<add_closure name=\"add\" />" +
					"<connect from=\"diff bsdf\" to=\"add closure1\" />" +
					"<connect from=\"phong bsdf\" to=\"add closure2\" />" +
					"<connect from=\"add closure\" to=\"output surface\" />",
					spec.R, spec.G, spec.B,
					roughness,
					color.R, color.G, color.B);
				return xml;
			}
		}

		public CyclesShader.CyclesMaterial MaterialType
		{
			get { return CyclesShader.CyclesMaterial.PhongTest; }
		}
	}
}