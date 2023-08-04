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

using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino;
using Rhino.UI;
using System;
using ccl.ShaderNodes.Sockets;

namespace RhinoCyclesCore.Materials
{
	[Guid("8B544B3E-D86F-4BCD-8494-FB660CF15E1C")]
	[CustomRenderContent(IsPrivate = true)]
	public class XmlMaterial : RenderMaterial, ICyclesMaterial
	{
		static private Dictionary<Size, Bitmap> material_icons = new Dictionary<Size, Bitmap>();
		public override bool Icon(Size size, out Bitmap bitmap)
		{
			if (false == material_icons.ContainsKey(size)) {
				Image img = Rhino.Resources.Assets.Rhino.SystemDrawing.Bitmaps.TryGet(Rhino.Resources.ResourceIds.Svg_CyclesMaterialSvg, new System.Drawing.Size(48, 48));
				material_icons.Add(size, new Bitmap(img));
			}

			Bitmap icon = material_icons[size];
			bitmap = icon;
			return icon != null;
		}

		public override bool VirtualIcon(Size size, out Bitmap bitmap)
		{
			return Icon(size, out bitmap);
		}

		public override string TypeName => "Cycles Xml";

		public override string TypeDescription => "Cycles Xml (grasshopper)";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.Xml;

		private string XmlString { get; set; }

		public XmlMaterial()
		{
			XmlString = "<diffuse_bsdf color=\"0 1 0\" name=\"diff\"/>" +
								"<connect from=\"diff bsdf\" to=\"output surface\" />";
			Fields.Add("xmlcode", XmlString, "XML");
		}

		public void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			string xml;
			if (Fields.TryGetValue("xmlcode", out xml))
			{
				XmlString = xml;
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

		public string MaterialXml
		{
			get
			{
				return XmlString;
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
