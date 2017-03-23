/**
Copyright 2014-2017 Robert McNeel and Associates

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
	[Guid("A6B37849-F705-403A-AC3E-58E083BF3CD6")]
	[CustomRenderContent(IsPrivate=true)]
	public class EmissiveMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Emissive";

		public override string TypeDescription => "Cycles Emissive Material (no falloff)";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.Emissive;

		private float Strength { get; set; }
		private Color4f Emission { get; set; }

		public EmissiveMaterial()
		{
			Emission = Color4f.White;
			Fields.Add("emission_color", Color4f.White, "Emissive Color");
			Strength = 1.0f;
			Fields.Add("strength", 1.0f, "Strength");
		}

		public void BakeParameters()
		{
			Color4f color;
			if (Fields.TryGetValue("emission_color", out color))
			{
				Emission = color;
			}
			float strength;
			if (Fields.TryGetValue("strength", out strength))
			{
				Strength = strength;
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

			simulatedMaterial.Reflectivity = 0.0;
			simulatedMaterial.Transparency = 0.0;
			simulatedMaterial.FresnelReflections = false;
			if (Fields.TryGetValue("emission_color", out color))
				simulatedMaterial.EmissionColor = color.AsSystemColor();
				simulatedMaterial.DiffuseColor = color.AsSystemColor();


			float f;
			if (Fields.TryGetValue("strength", out f))
			{
				simulatedMaterial.RefractionGlossiness = f;
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
				Color4f color = Color4f.ApplyGamma(Emission, Gamma);

				return string.Format(
					Utilities.Instance.NumberFormatInfo,
					"<transparent_bsdf color=\"1 1 1\" name=\"transp\" />" +
					"<emission color=\"{0} {1} {2}\" name=\"emission\" />" +
					"<light_falloff name=\"lfo\" strength=\"{3}\"/>" +
					"<light_path name=\"lp\" />" +
					"<mix_closure name=\"mix\" />" +

					"<connect from=\"transp bsdf\" to=\"mix closure2\" />" +
					"<connect from=\"lfo constant\" to=\"emission strength\" />" +
					"<connect from=\"emission emission\" to=\"mix closure1\" />" +
					"<connect from=\"lp iscameraray\" to=\"mix fac\" />" +
					"<connect from=\"mix closure\" to=\"output surface\" />" +
					"",
					
					color.R, color.G, color.B,
					Strength);
			}
		}
	}
}
