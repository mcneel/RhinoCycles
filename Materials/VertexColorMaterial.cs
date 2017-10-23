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
using System;

namespace RhinoCyclesCore.Materials
{
	[Guid("8CBED696-CAE0-4C62-8714-F11286134CFF")]
	[CustomRenderContent(IsPrivate=true)]
	public class VertexColorMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles VertexColor";

		public override string TypeDescription => "Cycles VertexColor attr only material (plaster based on vertex attrs)";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.VertexColor;

		public VertexColorMaterial()
		{
			Fields.Add("attribute", "vertexcolor", "Attribute");
		}

		public void BakeParameters()
		{
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);
			simulatedMaterial.DiffuseColor = Color4f.White.AsSystemColor();

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
				return string.Format(
					ccl.Utilities.Instance.NumberFormatInfo,
					"<attribute name=\"attr\" Attribute=\"vertexcolor\" />" +
					"<diffuse_bsdf color=\"1 0.5 0.1\" name=\"diff\"/>" +
					"<connect from=\"attr color\" to=\"diff color\" />" +
					"<connect from=\"diff bsdf\" to=\"output surface\" />"
				);
			}
		}

		public bool GetShader(ccl.Shader sh) { throw new InvalidOperationException("Material is XML based"); }
	}
}
