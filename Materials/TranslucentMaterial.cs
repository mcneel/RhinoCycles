/**
Copyright 2014-2024 Robert McNeel and Associates

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

using ccl.ShaderNodes.Sockets;
using Rhino.Display;
using Rhino.Render;
using RhinoCyclesCore.ExtensionMethods;
using System;
using System.Runtime.InteropServices;

namespace RhinoCyclesCore.Materials
{
	[Guid("E64050E9-521F-44F3-BFDA-EFEAFA73625E")]
	[CustomRenderContent(IsPrivate = true)]
	public class TranslucentMaterial : RenderMaterial, ICyclesMaterial
	{
		private static readonly string _DiffuseColor = "diffuse_color";
		public override string TypeName => "Translucent Material (DEV)";
		public override string TypeDescription => "Translucent Material (DEV)";

		public float Gamma { get; set; }

		TexturedColor Diffuse = new TexturedColor(_DiffuseColor, Color4f.White, false, 0.0f);
		CyclesTextureImage DiffuseTexture = new CyclesTextureImage();

		public TranslucentMaterial()
		{
			Utilities.TexturedSlot(this, _DiffuseColor, Color4f.White, "Diffuse Color");
			ModifyRenderContentStyles(RenderContentStyles.None, RenderContentStyles.TextureSummary);
		}

		public void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			HandleTexturedValue(_DiffuseColor, Diffuse);
			Utilities.HandleRenderTexture(Diffuse.Texture, DiffuseTexture, false, false, bitmapConverter, docsrn, Gamma, false, true);
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Rhino.DocObjects.Material simulatedMaterial, RenderTexture.TextureGeneration tg)
		{
			base.SimulateMaterial(ref simulatedMaterial, tg);
			BakeParameters(BitmapConverter, 0);


			simulatedMaterial.DiffuseColor = Diffuse.Value.AsSystemColor();
			if(Diffuse.On && Diffuse.Texture!=null && DiffuseTexture.HasTextureImage) {
				simulatedMaterial.SetTexture(Diffuse.Texture.SimulatedTexture(RenderTexture.TextureGeneration.Disallow).Texture(), Rhino.DocObjects.TextureType.Bitmap);
			}
		}

		public string MaterialXml => throw new InvalidOperationException("Cycles Translucent is not an XMl-based material");

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;
		ClosureSocket outsocket = null;

		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			ccl.ShaderNodes.TranslucentBsdfNode translucent = new ccl.ShaderNodes.TranslucentBsdfNode(sh, "translucent");

			Utilities.PbrGraphForSlot(sh, Diffuse, DiffuseTexture, translucent.ins.Color.ToList(), false, Gamma, false);

			translucent.outs.BSDF.Connect(sh.Output.ins.Surface);
			outsocket = translucent.outs.BSDF;

			if (finalize) sh.WriteDataToNodes();
			return true;
		}
		public ClosureSocket GetClosureSocket(ccl.Shader sh)
		{
			return outsocket ?? sh.Output.ins.Surface.ConnectionFrom as ClosureSocket;
		}

		public Converters.BitmapConverter BitmapConverter { get; set; }

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				Diffuse.Texture?.Dispose();
				Diffuse.Texture = null;
				DiffuseTexture?.Dispose();
				DiffuseTexture = null;
			}
		}
	}
}
