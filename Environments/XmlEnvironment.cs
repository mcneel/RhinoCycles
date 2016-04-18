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
using Utilities = ccl.Utilities;

namespace RhinoCyclesCore.Materials
{
	[Guid("8D42AAEC-DB00-4EE3-81A1-54BBCD79E925")]
	public class XmlEnvironment: RenderEnvironment, ICyclesMaterial
	{
		public override string TypeName { get { return "Cycles XML Environment"; } }
		public override string TypeDescription { get { return "Grasshopper/XML environment"; } }

		public float Gamma { get; set; }

		public XmlEnvironment()
		{
			var xml = "<background color=\"0 1 0\" name=\"bg\" strength=\"1\" />" +
								"<connect from=\"bg bsdf\" to=\"output surface\" />";
			Fields.Add("xml", xml, "XML definition");
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public string MaterialXml
		{
			get {
				string xml = "";

				Fields.TryGetValue("xml", out xml);

				return xml;
			}
		}

		public CyclesShader.CyclesMaterial MaterialType
		{
			get { return CyclesShader.CyclesMaterial.XmlEnvironment; }
		}
	}
}