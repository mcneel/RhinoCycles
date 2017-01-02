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
using Rhino.Render;
using RhinoCyclesCore.Materials;
using Utilities = ccl.Utilities;

namespace RhinoCyclesCore.Environments
{
	[Guid("BFA4D96F-A1B5-40BB-9851-7A88D60E6F26")]
	[CustomRenderContent(IsPrivate=true)]
	public class SimpleNoiseEnvironment: RenderEnvironment, ICyclesMaterial
	{
		public override string TypeName => "Simple Noise Environment (DEV)";
		public override string TypeDescription => "Simple Noise Environment (DEV)";

		public float Gamma { get; set; }

		private float Scale { get; set; }
		private float Detail { get; set; }
		private float Distortion { get; set; }
		private float Strength { get; set; }

		public SimpleNoiseEnvironment()
		{
			Scale = 5.0f;
			Fields.Add("scale", Scale, "Scale");
			Detail = 2.0f;
			Fields.Add("detail", Detail, "Detail");
			Distortion = 0.0f;
			Fields.Add("distortion", Distortion, "Distortion");
			Strength = 1.0f;
			Fields.Add("strength", Strength, "Background Strength");
		}

		public void BakeParameters()
		{
			float val;
			if (Fields.TryGetValue("scale", out val))
			{
				Scale = val;
			}
			if (Fields.TryGetValue("detail", out val))
			{
				Detail = val;
			}
			if (Fields.TryGetValue("distortion", out val))
			{
				Distortion = val;
			}
			if (Fields.TryGetValue("strength", out val))
			{
				Strength = val;
			}

		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public string MaterialXml
		{
			get {
				var nodegraph = string.Format(Utilities.Instance.NumberFormatInfo,
					"<noise_texture name=\"nt\" scale=\"{0}\" detail=\"{1}\" distortion=\"{2}\" />" +
					"<background name=\"bg\" strength=\"{3}\" />" +
					"<connect from=\"nt color\" to=\"bg color\" />" +
					"<connect from=\"bg background\" to=\"output surface\" />",
					Scale, Detail, Distortion, Strength);

				return nodegraph;
			}
		}

		public CyclesShader.CyclesMaterial MaterialType => CyclesShader.CyclesMaterial.SimpleNoiseEnvironment;
	}
}