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

using System.Drawing;
using System.Runtime.InteropServices;
using Rhino.DocObjects;
using Rhino.Render;

namespace RhinoCyclesCore.Materials
{
	[Guid("8B544B3E-D86F-4BCD-8494-FB660CF15E1C")]
	[CustomRenderContent(IsPrivate=true)]
	public class XmlMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Xml";

		public override string TypeDescription => "Cycles Xml (grasshopper)";

		public float Gamma { get; set; }

		public CyclesShader.CyclesMaterial MaterialType => CyclesShader.CyclesMaterial.Xml;

		private string Xml { get; set; }

		public XmlMaterial()
		{
			Xml = "<diffuse_bsdf color=\"0 1 0\" name=\"diff\"/>" +
								"<connect from=\"diff bsdf\" to=\"output surface\" />";
			Fields.Add("xmlcode", Xml, "XML");
		}

		public void BakeParameters()
		{
			string xml;
			if (Fields.TryGetValue("xmlcode", out xml))
			{
				Xml = xml;
			}
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			simulatedMaterial.DiffuseColor = Color.HotPink;

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
				return Xml;
			}
		}
	}
}
