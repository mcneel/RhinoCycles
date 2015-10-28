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

using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Eto.Forms;
using Rhino.Display;
using Rhino.Render;
using RhinoWindows.Controls;
using ef = Eto.Forms;


namespace RhinoCycles.Materials
{

	[Guid("17767410-7917-4523-90A3-FEBA810EA35D")]
	public class TestMaterialUiSection : ef.Panel, IWin32Window
	{
		public TestMaterialUiSection()
		{
			Content = new ef.Label { Text = "Hello there" };
		}

		private WpfElementHost m_host;

		public System.IntPtr Handle
		{
			get
			{
				if (m_host != null) return m_host.Handle;

				var hand = this.ToNative(true);
				if (hand != null)
				{
					m_host = new WpfElementHost(hand, null);
					if (m_host != null)
					{
						return m_host.Handle;
					}
				}

				return IntPtr.Zero;
			}
		}
	}

	[Guid("37D8ABBD-14BD-488C-B717-1FAA1F14EF62")]
	public class TestMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName { get { return "Test Material (DEV)"; } }
		public override string TypeDescription { get { return "Test Material (DEV)"; } }

		public float Gamma { get; set; }

		public TestMaterial()
		{
			Fields.Add("diffuse_color", Rhino.Display.Color4f.White, "Diffuse Color");
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);

			var tmus = typeof (TestMaterialUiSection);
			AddUserInterfaceSection(tmus, "Eto Panel Thingy", true, true);
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
			get {
				Color4f color;

				Fields.TryGetValue("diffuse_color", out color);

				color = Color4f.ApplyGamma(color, Gamma);

				return string.Format(
					"<diffuse_bsdf color=\"{0} {1} {2}\" name=\"diff\"/>" +
					"<connect from=\"diff bsdf\" to=\"output surface\" />" +
					"",
					color.R, color.G, color.B);
			}
		}

		public CyclesShader.CyclesMaterial MaterialType
		{
			get { return CyclesShader.CyclesMaterial.Test; }
		}
	}
}