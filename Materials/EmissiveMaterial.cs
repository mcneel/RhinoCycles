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

using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using System;
using ccl.ShaderNodes.Sockets;
using RhinoCyclesCore.ExtensionMethods;

namespace RhinoCyclesCore.Materials
{
	[Guid("A6B37849-F705-403A-AC3E-58E083BF3CD6")]
	[CustomRenderContent(IsPrivate=true)]
	public class EmissiveMaterial : RenderMaterial, ICyclesMaterial
	{

		public static readonly string _Emissive = "emission_color";
		public static readonly string _Strength = "strength";
		public static readonly string _Falloff = "falloff";
		private static readonly string _Smooth = "smooth";
		private static readonly string _Hide = "hide";


		public override string TypeName => "Cycles Emissive";

		public override string TypeDescription => "Cycles Emissive Material";

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;

		private float Strength { get; set; }
		//private Color4f Emission { get; set; }
		private int Falloff { get; set; }
		private float Smooth { get; set; }
		private bool Hide { get; set; }

		TexturedColor Emission = new TexturedColor(_Emissive, Color4f.White, false, 0.0f);
		CyclesTextureImage EmissionTexture = new CyclesTextureImage();

		public EmissiveMaterial()
		{
			/*Emission = Color4f.White;
			Fields.Add("emission_color", Color4f.White, "Emissive Color");*/
			Utilities.TexturedSlot(this, _Emissive, Color4f.White, "Emissive Color");
			Strength = 1.0f;
			Fields.Add(_Strength, 1.0f, "Strength");
			Falloff = 0;
			Fields.Add(_Falloff, 1, "Fall-off");
			Smooth = 0.0f;
			Fields.Add(_Smooth, 0.1f, "Smooth");
			Hide = false;
			Fields.Add(_Hide, true, "Hide");

			ModifyRenderContentStyles(RenderContentStyles.None, RenderContentStyles.TextureSummary);
		}

		public void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			HandleTexturedValue(_Emissive, Emission);
			Utilities.HandleRenderTexture(Emission.Texture, EmissionTexture, false, false, bitmapConverter, docsrn, 1.0f);
			if (Fields.TryGetValue(_Strength, out float strength))
			{
				Strength = strength;
			}
			if (Fields.TryGetValue(_Falloff, out int falloff))
			{
				Falloff = falloff;
			}
			if (Fields.TryGetValue(_Smooth, out float smooth))
			{
				Smooth = smooth;
			}
			if (Fields.TryGetValue(_Hide, out bool hide))
			{
				Hide = hide;
			}
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, RenderTexture.TextureGeneration tg)
		{
			BakeParameters(BitmapConverter, 0);
			base.SimulateMaterial(ref simulatedMaterial, tg);

			simulatedMaterial.Reflectivity = 0.0;
			simulatedMaterial.Transparency = 0.0;
			simulatedMaterial.FresnelReflections = false;
			simulatedMaterial.EmissionColor = Emission.Value.AsSystemColor();
			simulatedMaterial.DiffuseColor = Emission.Value.AsSystemColor();


			simulatedMaterial.RefractionGlossiness = Strength;

			simulatedMaterial.Name = Name;
		}

		public string MaterialXml => throw new InvalidOperationException("Cycles Emissive is not an XMl-based material");

		ClosureSocket outsocket = null;

		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			ccl.ShaderNodes.TransparentBsdfNode transp = new ccl.ShaderNodes.TransparentBsdfNode(sh, "transp");

			ccl.ShaderNodes.EmissionNode emission = new ccl.ShaderNodes.EmissionNode(sh, "emission");
			ccl.ShaderNodes.LightFalloffNode lfo = new ccl.ShaderNodes.LightFalloffNode(sh, "lfo");
			ccl.ShaderNodes.LightPathNode lp = new ccl.ShaderNodes.LightPathNode(sh, "lp");
			ccl.ShaderNodes.MixClosureNode mix = new ccl.ShaderNodes.MixClosureNode(sh, "mix");

			sh.AddNode(transp);
			sh.AddNode(emission);
			sh.AddNode(lfo);
			sh.AddNode(lp);
			sh.AddNode(mix);

			transp.ins.Color.Value = new ccl.float4(1.0f);

			Utilities.PbrGraphForSlot(sh, Emission, EmissionTexture, emission.ins.Color.ToList(), false);

			lfo.ins.Strength.Value = Strength;
			lfo.ins.Smooth.Value = Smooth;

			switch(Falloff) {
				case 0:
					lfo.outs.Constant.Connect(emission.ins.Strength);
					break;
				case 1:
					lfo.outs.Linear.Connect(emission.ins.Strength);
					break;
				default:
					lfo.outs.Quadratic.Connect(emission.ins.Strength);
					break;
			}

			transp.outs.BSDF.Connect(mix.ins.Closure2);

			if(Hide) {
				emission.outs.Emission.Connect(mix.ins.Closure1);
				lp.outs.IsCameraRay.Connect(mix.ins.Fac);
				mix.outs.Closure.Connect(sh.Output.ins.Surface);
				outsocket = mix.outs.Closure;
			}
			else {
				emission.outs.Emission.Connect(sh.Output.ins.Surface);
				outsocket = emission.outs.Emission;
			}

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
				Emission.Texture?.Dispose();
				Emission.Texture = null;
				EmissionTexture?.Dispose();
				EmissionTexture = null;
			}
		}
	}
}
