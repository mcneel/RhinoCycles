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
using ccl.ShaderNodes.Sockets;

namespace RhinoCyclesCore.Materials
{
	[Guid("F4C85EC1-C1CB-4633-A712-1BA2F020B954")]
	[CustomRenderContent(IsPrivate=true)]
	public class DiffuseMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Diffuse";

		public override string TypeDescription => "Cycles Diffuse color only material (plaster)";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.Diffuse;

		private Color4f Diffuse { get; set; }

		public DiffuseMaterial()
		{
			Fields.Add("diffuse", Color4f.White, "Color");
		}

		public void BakeParameters()
		{
			Color4f color;
			Diffuse = Fields.TryGetValue("diffuse", out color) ? color : Color4f.White;
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);


			Color4f color;
			if (Fields.TryGetValue("diffuse", out color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();

			simulatedMaterial.Name = Name;
		}



		public string MaterialXml
		{
			get
			{
				Color4f color = Color4f.ApplyGamma(Diffuse, Gamma);

				return string.Format(
					ccl.Utilities.Instance.NumberFormatInfo,
					"<diffuse_bsdf color=\"{0} {1} {2}\" name=\"diff\"/>" +
					"<connect from=\"diff bsdf\" to=\"output surface\" />" +
					"",
					color.R, color.G, color.B);
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
	}
}
