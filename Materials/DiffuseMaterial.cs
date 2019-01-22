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
		private static readonly string _Diffuse = "diffuse";
		public override string TypeName => "Cycles Diffuse";

		public override string TypeDescription => "Cycles Diffuse color only material (plaster)";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;

		TexturedColor Diffuse = new TexturedColor(_Diffuse, Color4f.White, false, 0.0f);
		CyclesTextureImage DiffuseTexture = new CyclesTextureImage();

		public DiffuseMaterial()
		{
			Utilities.TexturedSlot(this, _Diffuse, Color4f.White, "Diffuse");
			ModifyRenderContentStyles(RenderContentStyles.None, RenderContentStyles.TextureSummary);
		}

		public void BakeParameters()
		{
			HandleTexturedValue(_Diffuse, Diffuse);
			Utilities.HandleRenderTexture(Diffuse.Texture, DiffuseTexture, Gamma);
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			BakeParameters();

			simulatedMaterial.DiffuseColor = Diffuse.Value.AsSystemColor();
			if(Diffuse.On && Diffuse.Texture!=null && DiffuseTexture.HasTextureImage) {
				simulatedMaterial.SetBitmapTexture(Diffuse.Texture.SimulatedTexture(RenderTexture.TextureGeneration.Disallow).Texture());
			}

			simulatedMaterial.Name = Name;
		}

		public string MaterialXml => throw new InvalidOperationException("Cycles Diffuse is not an XMl-based material");
		ClosureSocket outsocket = null;
		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			ccl.ShaderNodes.DiffuseBsdfNode diffuse = new ccl.ShaderNodes.DiffuseBsdfNode("diffuse");
			ccl.ShaderNodes.TextureCoordinateNode texco = new ccl.ShaderNodes.TextureCoordinateNode("texco");
			sh.AddNode(diffuse);
			sh.AddNode(texco);

			Utilities.PbrGraphForSlot(sh, Diffuse, DiffuseTexture, diffuse.ins.Color, texco);

			diffuse.outs.BSDF.Connect(sh.Output.ins.Surface);
			outsocket = diffuse.outs.BSDF;

			if (finalize) sh.FinalizeGraph();
			return true;
		}
		public ClosureSocket GetClosureSocket(ccl.Shader sh)
		{
			return outsocket ?? sh.Output.ins.Surface.ConnectionFrom as ClosureSocket;
		}
	}
}
