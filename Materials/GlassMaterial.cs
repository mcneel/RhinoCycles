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
using System;
using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using ccl.ShaderNodes.Sockets;
using RhinoCyclesCore.ExtensionMethods;

namespace RhinoCyclesCore.Materials
{
	[Guid("3CEC0E39-8A13-4E73-8D0C-F1F1DF730C35")]
	[CustomRenderContent(IsPrivate=true)]
	public class GlassMaterial : RenderMaterial, ICyclesMaterial
	{
		private static readonly string _Color = "glass_color";
		private static readonly string _Frost = "frost-amount";
		private static readonly string _Ior = "ior";
		public override string TypeName => "Cycles Glass";

		public override string TypeDescription => "Cycles Glass Material";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;

		TexturedColor Color = new TexturedColor(_Color, Color4f.White, false, 0.0f);
		CyclesTextureImage ColorTexture = new CyclesTextureImage();
		TexturedFloat Frost = new TexturedFloat(_Frost, 0.0f, false, 0.0f);
		CyclesTextureImage FrostTexture = new CyclesTextureImage();
		TexturedFloat Ior = new TexturedFloat(_Ior, 1.45f, false, 0.0f);
		CyclesTextureImage IorTexture = new CyclesTextureImage();


		public GlassMaterial()
		{
			Utilities.TexturedSlot(this, _Color, Color4f.White, "Color");
			Utilities.TexturedSlot(this, _Frost, 0.0f, "Frost");
			Utilities.TexturedSlot(this, _Ior, 1.45f, "IOR");
			ModifyRenderContentStyles(RenderContentStyles.None, RenderContentStyles.TextureSummary);
		}

		public void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			HandleTexturedValue(_Color, Color);
			Utilities.HandleRenderTexture(Color.Texture, ColorTexture, false, false, bitmapConverter, docsrn, Gamma);
			HandleTexturedValue(_Frost, Frost);
			Utilities.HandleRenderTexture(Frost.Texture, FrostTexture, false, false, bitmapConverter, docsrn, Gamma);
			HandleTexturedValue(_Ior, Ior);
			Utilities.HandleRenderTexture(Color.Texture, ColorTexture, false, false, bitmapConverter, docsrn, Gamma);
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, RenderTexture.TextureGeneration tg)
		{
			base.SimulateMaterial(ref simulatedMaterial, tg);

			BakeParameters(BitmapConverter, 0);

			simulatedMaterial.Reflectivity = 1.0;
			simulatedMaterial.Transparency = 1.0;
			simulatedMaterial.FresnelReflections = true;
			simulatedMaterial.DiffuseColor = System.Drawing.Color.Black;

			simulatedMaterial.TransparentColor = Color.Value.AsSystemColor();

			simulatedMaterial.ReflectionGlossiness = Frost.Value;
			simulatedMaterial.RefractionGlossiness = Frost.Value;
			simulatedMaterial.IndexOfRefraction = Ior.Value;
			simulatedMaterial.Name = Name;
		}

		public string MaterialXml => throw new InvalidOperationException("Cycles Glass is not an XML-material");
		ClosureSocket outsocket = null;
		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			ccl.ShaderNodes.TransparentBsdfNode transp = new ccl.ShaderNodes.TransparentBsdfNode(sh, "transp");
			ccl.ShaderNodes.PrincipledBsdfNode glass = new ccl.ShaderNodes.PrincipledBsdfNode(sh, "glass");
			ccl.ShaderNodes.LightPathNode lp = new ccl.ShaderNodes.LightPathNode(sh, "lp");
			ccl.ShaderNodes.MathMaximum max = new ccl.ShaderNodes.MathMaximum(sh, "max");
			ccl.ShaderNodes.MixClosureNode mix = new ccl.ShaderNodes.MixClosureNode(sh, "mix");

			sh.AddNode(transp);
			sh.AddNode(glass);
			sh.AddNode(lp);
			sh.AddNode(max);
			sh.AddNode(mix);

			glass.ins.Transmission.Value = 1.0f;

			Utilities.PbrGraphForSlot(sh, Color, ColorTexture, glass.ins.BaseColor.ToList(), false);
			Utilities.PbrGraphForSlot(sh, Color, ColorTexture, transp.ins.Color.ToList(), false);
			Utilities.PbrGraphForSlot(sh, Frost, FrostTexture, glass.ins.TransmissionRoughness.ToList(), false);
			Utilities.PbrGraphForSlot(sh, Frost, FrostTexture, glass.ins.IOR.ToList(), false);

			transp.outs.BSDF.Connect(mix.ins.Closure2);
			glass.outs.BSDF.Connect(mix.ins.Closure1);
			lp.outs.IsShadowRay.Connect(max.ins.Value1);
			lp.outs.IsReflectionRay.Connect(max.ins.Value2);
			max.outs.Value.Connect(mix.ins.Fac);
			mix.outs.Closure.Connect(sh.Output.ins.Surface);

			outsocket = mix.outs.Closure;

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

				Color.Texture?.Dispose();
				Color.Texture = null;
				ColorTexture?.Dispose();
				ColorTexture= null;
				Frost.Texture?.Dispose();
				Frost.Texture = null;
				FrostTexture?.Dispose();
				FrostTexture= null;
				Ior.Texture?.Dispose();
				Ior.Texture = null;
				IorTexture?.Dispose();
				IorTexture = null;
			}
		}
	}
}
