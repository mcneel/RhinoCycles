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

using ccl;
using ccl.ShaderNodes;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoFullNxt : RhinoShader
	{
		public RhinoFullNxt(Client client, CyclesShader intermediate) : this(client, intermediate, null, intermediate.Front.Name)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing) : this(client, intermediate, existing, intermediate.Front.Name)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing, string name) : base(client, intermediate)
		{
			if (existing != null)
			{
				m_shader = existing;
				m_shader.Recreate();
			}
			else
			{
				m_shader = new Shader(m_client, Shader.ShaderType.Material)
				{
					UseMis = true,
					UseTransparentShadow = true,
					HeterogeneousVolume = false,
					Name = name
				};
			}
		}

		public override Shader GetShader()
		{
			if (m_original.DisplayMaterial)
			{
				var front = GetShaderPart(m_original.Front);
				var back = GetShaderPart(m_original.Back);

				var backfacing=  new GeometryInfoNode("backfacepicker");
				var flipper = new MixClosureNode("front or back");
				var lp = new LightPathNode("lp for bf");
				var mlt = new MathNode("toggle bf only when camera ray") {Operation = MathNode.Operations.Multiply};

				m_shader.AddNode(backfacing);
				m_shader.AddNode(flipper);
				m_shader.AddNode(lp);
				m_shader.AddNode(mlt);

				lp.outs.IsCameraRay.Connect(mlt.ins.Value1);
				backfacing.outs.Backfacing.Connect(mlt.ins.Value2);

				mlt.outs.Value.Connect(flipper.ins.Fac);

				var frontclosure = front.GetClosureSocket();
				var backclosure = back.GetClosureSocket();

				frontclosure.Connect(flipper.ins.Closure1);
				backclosure.Connect(flipper.ins.Closure2);

				flipper.outs.Closure.Connect(m_shader.Output.ins.Surface);
			}
			else
			{
				var last = GetShaderPart(m_original.Front);
				var lastclosure = last.GetClosureSocket();

				lastclosure.Connect(m_shader.Output.ins.Surface);
			}


			m_shader.FinalizeGraph();

			return m_shader;
		}

		private ShaderNode GetShaderPart(ShaderBody part)
		{


			////////////////////////////
			var texcoord323 = new TextureCoordinateNode("texcoord");

			var diffuse_texture324 = new ImageTextureNode("diffuse_texture");
			diffuse_texture324.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture324.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture324.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture324.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
			diffuse_texture324.Interpolation = InterpolationType.Linear;
			diffuse_texture324.UseAlpha = true;
			diffuse_texture324.IsLinear = false;

			var invert_alpha347 = new MathNode("invert_alpha");
			invert_alpha347.ins.Value1.Value = 1f;
			invert_alpha347.ins.Value2.Value = 0f;
			invert_alpha347.Operation = MathNode.Operations.Subtract;
			invert_alpha347.UseClamp = false;

			var honor_texture_repeat348 = new MathNode("honor_texture_repeat");
			honor_texture_repeat348.ins.Value1.Value = 1f;
			honor_texture_repeat348.ins.Value2.Value = part.DiffuseTexture.InvertRepeatAsFloat;
			honor_texture_repeat348.Operation = MathNode.Operations.Multiply;
			honor_texture_repeat348.UseClamp = false;

			var invert_transparency350 = new MathNode("invert_transparency");
			invert_transparency350.ins.Value1.Value = 1f;
			invert_transparency350.ins.Value2.Value = part.Transparency;
			invert_transparency350.Operation = MathNode.Operations.Subtract;
			invert_transparency350.UseClamp = false;

			var repeat_mixer346 = new MixNode("repeat_mixer");
			repeat_mixer346.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			repeat_mixer346.ins.Color2.Value = part.BaseColor;
			repeat_mixer346.ins.Fac.Value = 0f;
			repeat_mixer346.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			repeat_mixer346.UseClamp = false;

			var weight_diffuse_amount_by_transparency_inv349 = new MathNode("weight_diffuse_amount_by_transparency_inv");
			weight_diffuse_amount_by_transparency_inv349.ins.Value1.Value = part.DiffuseTexture.Amount;
			weight_diffuse_amount_by_transparency_inv349.ins.Value2.Value = 0f;
			weight_diffuse_amount_by_transparency_inv349.Operation = MathNode.Operations.Multiply;
			weight_diffuse_amount_by_transparency_inv349.UseClamp = false;

			var diffuse_texture_amount328 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount328.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount328.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount328.ins.Fac.Value = 0f;
			diffuse_texture_amount328.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount328.UseClamp = false;

			var invert_diffuse_color_amount331 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount331.ins.Value1.Value = 1f;
			invert_diffuse_color_amount331.ins.Value2.Value = 0f;
			invert_diffuse_color_amount331.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount331.UseClamp = false;

			var diffuse_col_amount330 = new MixNode("diffuse_col_amount");
			diffuse_col_amount330.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount330.ins.Color2.Value = part.BaseColor;
			diffuse_col_amount330.ins.Fac.Value = 1f;
			diffuse_col_amount330.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_col_amount330.UseClamp = false;

			var separate_diffuse_texture_color340 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color340.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color341 = new SeparateRgbNode("separate_base_color");
			separate_base_color341.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r342 = new MathNode("add_base_color_r");
			add_base_color_r342.ins.Value1.Value = 0f;
			add_base_color_r342.ins.Value2.Value = 0f;
			add_base_color_r342.Operation = MathNode.Operations.Add;
			add_base_color_r342.UseClamp = true;

			var add_base_color_g343 = new MathNode("add_base_color_g");
			add_base_color_g343.ins.Value1.Value = 0f;
			add_base_color_g343.ins.Value2.Value = 0f;
			add_base_color_g343.Operation = MathNode.Operations.Add;
			add_base_color_g343.UseClamp = true;

			var add_base_color_b344 = new MathNode("add_base_color_b");
			add_base_color_b344.ins.Value1.Value = 0f;
			add_base_color_b344.ins.Value2.Value = 0f;
			add_base_color_b344.Operation = MathNode.Operations.Add;
			add_base_color_b344.UseClamp = true;

			var bump_texture334 = new ImageTextureNode("bump_texture");
			bump_texture334.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture334.Projection = TextureNode.TextureProjection.Flat;
			bump_texture334.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture334.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture334.Interpolation = InterpolationType.Linear;
			bump_texture334.UseAlpha = true;
			bump_texture334.IsLinear = false;

			var bump_texture_to_bw335 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw335.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount336 = new MathNode("bump_amount");
			bump_amount336.ins.Value1.Value = 4.66f;
			bump_amount336.ins.Value2.Value = part.BumpTexture.Amount;
			bump_amount336.Operation = MathNode.Operations.Multiply;
			bump_amount336.UseClamp = false;

			var final_base_color345 = new CombineRgbNode("final_base_color");
			final_base_color345.ins.R.Value = 0f;
			final_base_color345.ins.G.Value = 0f;
			final_base_color345.ins.B.Value = 0f;

			var bump333 = new BumpNode("bump");
			bump333.ins.Height.Value = 0f;
			bump333.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump333.ins.Strength.Value = 0f;
			bump333.ins.Distance.Value = 0.1f;

			var transparency_texture325 = new ImageTextureNode("transparency_texture");
			transparency_texture325.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture325.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture325.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture325.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture325.Interpolation = InterpolationType.Linear;
			transparency_texture325.UseAlpha = true;
			transparency_texture325.IsLinear = false;

			var transpluminance326 = new RgbToLuminanceNode("transpluminance");
			transpluminance326.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence327 = new MathNode("invert_luminence");
			invert_luminence327.ins.Value1.Value = 1f;
			invert_luminence327.ins.Value2.Value = 0f;
			invert_luminence327.Operation = MathNode.Operations.Subtract;
			invert_luminence327.UseClamp = false;

			var transparency_texture_amount332 = new MathNode("transparency_texture_amount");
			transparency_texture_amount332.ins.Value1.Value = 1f;
			transparency_texture_amount332.ins.Value2.Value = part.TransparencyTexture.Amount;
			transparency_texture_amount332.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount332.UseClamp = false;

			var diffuse298 = new DiffuseBsdfNode("diffuse");
			diffuse298.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse298.ins.Roughness.Value = 0f;
			diffuse298.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent391 = new TransparentBsdfNode("transparent");
			transparent391.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var toggle_transparency_texture329 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture329.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
			toggle_transparency_texture329.ins.Value2.Value = 1f;
			toggle_transparency_texture329.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture329.UseClamp = false;

			var diffuse_alpha_cutter397 = new MixClosureNode("diffuse_alpha_cutter");
			diffuse_alpha_cutter397.ins.Fac.Value = 0f;

			var roughness_times_roughness339 = new MathNode("roughness_times_roughness");
			roughness_times_roughness339.ins.Value1.Value = part.Roughness;
			roughness_times_roughness339.ins.Value2.Value = part.Roughness;
			roughness_times_roughness339.Operation = MathNode.Operations.Multiply;
			roughness_times_roughness339.UseClamp = false;

			var principled_metal299 = new UberBsdfNode("principled_metal");
			principled_metal299.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal299.ins.SpecularColor.Value = part.SpecularColor;
			principled_metal299.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal299.ins.Metallic.Value = part.Reflectivity;
			principled_metal299.ins.Subsurface.Value = 0f;
			principled_metal299.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal299.ins.Specular.Value = part.Reflectivity;
			principled_metal299.ins.Roughness.Value = 0.7225f;
			principled_metal299.ins.SpecularTint.Value = part.Reflectivity;
			principled_metal299.ins.Anisotropic.Value = 0f;
			principled_metal299.ins.Sheen.Value = 0f;
			principled_metal299.ins.SheenTint.Value = 0f;
			principled_metal299.ins.Clearcoat.Value = 0f;
			principled_metal299.ins.ClearcoatGloss.Value = 0f;
			principled_metal299.ins.IOR.Value = 0f;
			principled_metal299.ins.Transparency.Value = 0f;
			principled_metal299.ins.RefractionRoughness.Value = 0f;
			principled_metal299.ins.AnisotropicRotation.Value = 0f;
			principled_metal299.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal299.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal299.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var metal_alpha_cutter398 = new MixClosureNode("metal_alpha_cutter");
			metal_alpha_cutter398.ins.Fac.Value = 0f;

			var paint_roughness_compression402 = new MathNode("paint_roughness_compression");
			paint_roughness_compression402.ins.Value1.Value = 0.7225f;
			paint_roughness_compression402.ins.Value2.Value = 0.9f;
			paint_roughness_compression402.Operation = MathNode.Operations.Divide;
			paint_roughness_compression402.UseClamp = false;

			var paint_roughness_fixer403 = new MathNode("paint_roughness_fixer");
			paint_roughness_fixer403.ins.Value1.Value = 0.8027778f;
			paint_roughness_fixer403.ins.Value2.Value = 0.1f;
			paint_roughness_fixer403.Operation = MathNode.Operations.Add;
			paint_roughness_fixer403.UseClamp = true;

			var paint_shine_fixer404 = new MathNode("paint_shine_fixer");
			paint_shine_fixer404.ins.Value1.Value = 1f;
			paint_shine_fixer404.ins.Value2.Value = 0.9027778f;
			paint_shine_fixer404.Operation = MathNode.Operations.Subtract;
			paint_shine_fixer404.UseClamp = false;

			var principled_paint302 = new UberBsdfNode("principled_paint");
			principled_paint302.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint302.ins.SpecularColor.Value = part.SpecularColor;
			principled_paint302.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint302.ins.Metallic.Value = 0f;
			principled_paint302.ins.Subsurface.Value = 0f;
			principled_paint302.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint302.ins.Specular.Value = 0.09722222f;
			principled_paint302.ins.Roughness.Value = 0.9027778f;
			principled_paint302.ins.SpecularTint.Value = 0f;
			principled_paint302.ins.Anisotropic.Value = 0f;
			principled_paint302.ins.Sheen.Value = 0.09722222f;
			principled_paint302.ins.SheenTint.Value = 0f;
			principled_paint302.ins.Clearcoat.Value = 0f;
			principled_paint302.ins.ClearcoatGloss.Value = 0f;
			principled_paint302.ins.IOR.Value = 0f;
			principled_paint302.ins.Transparency.Value = 0f;
			principled_paint302.ins.RefractionRoughness.Value = 0f;
			principled_paint302.ins.AnisotropicRotation.Value = 0f;
			principled_paint302.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint302.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint302.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var paint_alpha_cutter399 = new MixClosureNode("paint_alpha_cutter");
			paint_alpha_cutter399.ins.Fac.Value = 0f;

			var invert_roughness310 = new MathNode("invert_roughness");
			invert_roughness310.ins.Value1.Value = 1f;
			invert_roughness310.ins.Value2.Value = 0.7225f;
			invert_roughness310.Operation = MathNode.Operations.Subtract;
			invert_roughness310.UseClamp = false;

			var transparency_factor_for_roughness311 = new MathNode("transparency_factor_for_roughness");
			transparency_factor_for_roughness311.ins.Value1.Value = 0.2775f;
			transparency_factor_for_roughness311.ins.Value2.Value = part.Transparency;
			transparency_factor_for_roughness311.Operation = MathNode.Operations.Multiply;
			transparency_factor_for_roughness311.UseClamp = false;

			var light_path322 = new LightPathNode("light_path");

			var toggle_when_shadow312 = new MathNode("toggle_when_shadow");
			toggle_when_shadow312.ins.Value1.Value = 0.2775f;
			toggle_when_shadow312.ins.Value2.Value = 0f;
			toggle_when_shadow312.Operation = MathNode.Operations.Multiply;
			toggle_when_shadow312.UseClamp = false;

			var layer_weight314 = new LayerWeightNode("layer_weight");
			layer_weight314.ins.Blend.Value = 0.87f;
			layer_weight314.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_glass_and_gem304 = new UberBsdfNode("principled_glass_and_gem");
			principled_glass_and_gem304.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem304.ins.SpecularColor.Value = part.SpecularColor;
			principled_glass_and_gem304.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem304.ins.Metallic.Value = 0f;
			principled_glass_and_gem304.ins.Subsurface.Value = 0f;
			principled_glass_and_gem304.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem304.ins.Specular.Value = 0f;
			principled_glass_and_gem304.ins.Roughness.Value = 0.7225f;
			principled_glass_and_gem304.ins.SpecularTint.Value = 0f;
			principled_glass_and_gem304.ins.Anisotropic.Value = 0f;
			principled_glass_and_gem304.ins.Sheen.Value = 0f;
			principled_glass_and_gem304.ins.SheenTint.Value = 0f;
			principled_glass_and_gem304.ins.Clearcoat.Value = 0f;
			principled_glass_and_gem304.ins.ClearcoatGloss.Value = 0f;
			principled_glass_and_gem304.ins.IOR.Value = part.IOR;
			principled_glass_and_gem304.ins.Transparency.Value = part.Transparency;
			principled_glass_and_gem304.ins.RefractionRoughness.Value = 0f;
			principled_glass_and_gem304.ins.AnisotropicRotation.Value = 0f;
			principled_glass_and_gem304.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem304.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem304.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent307 = new TransparentBsdfNode("transparent");
			transparent307.ins.Color.Value = part.TransparencyColor;

			var transparency_layer_weight313 = new MathNode("transparency_layer_weight");
			transparency_layer_weight313.ins.Value1.Value = 0f;
			transparency_layer_weight313.ins.Value2.Value = 0f;
			transparency_layer_weight313.Operation = MathNode.Operations.Multiply;
			transparency_layer_weight313.UseClamp = false;

			var glass_and_gem308 = new MixClosureNode("glass_and_gem");
			glass_and_gem308.ins.Fac.Value = 0f;

			var gem_and_glass_alpha_cutter400 = new MixClosureNode("gem_and_glass_alpha_cutter");
			gem_and_glass_alpha_cutter400.ins.Fac.Value = 0f;

			var principled306 = new UberBsdfNode("principled");
			principled306.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled306.ins.SpecularColor.Value = part.SpecularColor;
			principled306.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled306.ins.Metallic.Value = part.Metalic;
			principled306.ins.Subsurface.Value = 0f;
			principled306.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled306.ins.Specular.Value = part.Shine;
			principled306.ins.Roughness.Value = 0.9027778f;
			principled306.ins.SpecularTint.Value = part.Gloss;
			principled306.ins.Anisotropic.Value = 0f;
			principled306.ins.Sheen.Value = 0f;
			principled306.ins.SheenTint.Value = part.Gloss;
			principled306.ins.Clearcoat.Value = 0f;
			principled306.ins.ClearcoatGloss.Value = part.Gloss;
			principled306.ins.IOR.Value = part.IOR;
			principled306.ins.Transparency.Value = part.Transparency;
			principled306.ins.RefractionRoughness.Value = part.RefractionRoughness;
			principled306.ins.AnisotropicRotation.Value = 0f;
			principled306.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled306.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled306.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf337 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf337.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf337.ins.Strength.Value = 1f;

			var invert_roughness316 = new MathNode("invert_roughness");
			invert_roughness316.ins.Value1.Value = 1f;
			invert_roughness316.ins.Value2.Value = 0.9027778f;
			invert_roughness316.Operation = MathNode.Operations.Subtract;
			invert_roughness316.UseClamp = false;

			var multiply_transparency317 = new MathNode("multiply_transparency");
			multiply_transparency317.ins.Value1.Value = 0.09722222f;
			multiply_transparency317.ins.Value2.Value = part.Transparency;
			multiply_transparency317.Operation = MathNode.Operations.Multiply;
			multiply_transparency317.UseClamp = false;

			var light_path309 = new LightPathNode("light_path");

			var multiply_with_shadowray318 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray318.ins.Value1.Value = 0.09722222f;
			multiply_with_shadowray318.ins.Value2.Value = 0f;
			multiply_with_shadowray318.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray318.UseClamp = false;

			var layer_weight320 = new LayerWeightNode("layer_weight");
			layer_weight320.ins.Blend.Value = 0.89f;
			layer_weight320.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless338 = new MixClosureNode("shadeless");
			shadeless338.ins.Fac.Value = part.ShadelessAsFloat;

			var coloured_shadow_trans_color315 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color315.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow319 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow319.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow319.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow319.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow319.UseClamp = false;

			var coloured_shadow_mix321 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix321.ins.Fac.Value = 0f;

			var plastic_alpha_cutter401 = new MixClosureNode("plastic_alpha_cutter");
			plastic_alpha_cutter401.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord323);
			m_shader.AddNode(diffuse_texture324);
			m_shader.AddNode(invert_alpha347);
			m_shader.AddNode(honor_texture_repeat348);
			m_shader.AddNode(invert_transparency350);
			m_shader.AddNode(repeat_mixer346);
			m_shader.AddNode(weight_diffuse_amount_by_transparency_inv349);
			m_shader.AddNode(diffuse_texture_amount328);
			m_shader.AddNode(invert_diffuse_color_amount331);
			m_shader.AddNode(diffuse_col_amount330);
			m_shader.AddNode(separate_diffuse_texture_color340);
			m_shader.AddNode(separate_base_color341);
			m_shader.AddNode(add_base_color_r342);
			m_shader.AddNode(add_base_color_g343);
			m_shader.AddNode(add_base_color_b344);
			m_shader.AddNode(bump_texture334);
			m_shader.AddNode(bump_texture_to_bw335);
			m_shader.AddNode(bump_amount336);
			m_shader.AddNode(final_base_color345);
			m_shader.AddNode(bump333);
			m_shader.AddNode(transparency_texture325);
			m_shader.AddNode(transpluminance326);
			m_shader.AddNode(invert_luminence327);
			m_shader.AddNode(transparency_texture_amount332);
			m_shader.AddNode(diffuse298);
			m_shader.AddNode(transparent391);
			m_shader.AddNode(toggle_transparency_texture329);
			m_shader.AddNode(diffuse_alpha_cutter397);
			m_shader.AddNode(roughness_times_roughness339);
			m_shader.AddNode(principled_metal299);
			m_shader.AddNode(metal_alpha_cutter398);
			m_shader.AddNode(paint_roughness_compression402);
			m_shader.AddNode(paint_roughness_fixer403);
			m_shader.AddNode(paint_shine_fixer404);
			m_shader.AddNode(principled_paint302);
			m_shader.AddNode(paint_alpha_cutter399);
			m_shader.AddNode(invert_roughness310);
			m_shader.AddNode(transparency_factor_for_roughness311);
			m_shader.AddNode(light_path322);
			m_shader.AddNode(toggle_when_shadow312);
			m_shader.AddNode(layer_weight314);
			m_shader.AddNode(principled_glass_and_gem304);
			m_shader.AddNode(transparent307);
			m_shader.AddNode(transparency_layer_weight313);
			m_shader.AddNode(glass_and_gem308);
			m_shader.AddNode(gem_and_glass_alpha_cutter400);
			m_shader.AddNode(principled306);
			m_shader.AddNode(shadeless_bsdf337);
			m_shader.AddNode(invert_roughness316);
			m_shader.AddNode(multiply_transparency317);
			m_shader.AddNode(light_path309);
			m_shader.AddNode(multiply_with_shadowray318);
			m_shader.AddNode(layer_weight320);
			m_shader.AddNode(shadeless338);
			m_shader.AddNode(coloured_shadow_trans_color315);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow319);
			m_shader.AddNode(coloured_shadow_mix321);
			m_shader.AddNode(plastic_alpha_cutter401);


			texcoord323.outs.UV.Connect(diffuse_texture324.ins.Vector);
			diffuse_texture324.outs.Alpha.Connect(invert_alpha347.ins.Value2);
			invert_alpha347.outs.Value.Connect(honor_texture_repeat348.ins.Value1);
			diffuse_texture324.outs.Color.Connect(repeat_mixer346.ins.Color1);
			honor_texture_repeat348.outs.Value.Connect(repeat_mixer346.ins.Fac);
			invert_transparency350.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv349.ins.Value2);
			repeat_mixer346.outs.Color.Connect(diffuse_texture_amount328.ins.Color2);
			weight_diffuse_amount_by_transparency_inv349.outs.Value.Connect(diffuse_texture_amount328.ins.Fac);
			weight_diffuse_amount_by_transparency_inv349.outs.Value.Connect(invert_diffuse_color_amount331.ins.Value2);
			invert_diffuse_color_amount331.outs.Value.Connect(diffuse_col_amount330.ins.Fac);
			diffuse_texture_amount328.outs.Color.Connect(separate_diffuse_texture_color340.ins.Image);
			diffuse_col_amount330.outs.Color.Connect(separate_base_color341.ins.Image);
			separate_diffuse_texture_color340.outs.R.Connect(add_base_color_r342.ins.Value1);
			separate_base_color341.outs.R.Connect(add_base_color_r342.ins.Value2);
			separate_diffuse_texture_color340.outs.G.Connect(add_base_color_g343.ins.Value1);
			separate_base_color341.outs.G.Connect(add_base_color_g343.ins.Value2);
			separate_diffuse_texture_color340.outs.B.Connect(add_base_color_b344.ins.Value1);
			separate_base_color341.outs.B.Connect(add_base_color_b344.ins.Value2);
			texcoord323.outs.UV.Connect(bump_texture334.ins.Vector);
			bump_texture334.outs.Color.Connect(bump_texture_to_bw335.ins.Color);
			add_base_color_r342.outs.Value.Connect(final_base_color345.ins.R);
			add_base_color_g343.outs.Value.Connect(final_base_color345.ins.G);
			add_base_color_b344.outs.Value.Connect(final_base_color345.ins.B);
			bump_texture_to_bw335.outs.Val.Connect(bump333.ins.Height);
			bump_amount336.outs.Value.Connect(bump333.ins.Strength);
			texcoord323.outs.UV.Connect(transparency_texture325.ins.Vector);
			transparency_texture325.outs.Color.Connect(transpluminance326.ins.Color);
			transpluminance326.outs.Val.Connect(invert_luminence327.ins.Value2);
			invert_luminence327.outs.Value.Connect(transparency_texture_amount332.ins.Value1);
			final_base_color345.outs.Image.Connect(diffuse298.ins.Color);
			bump333.outs.Normal.Connect(diffuse298.ins.Normal);
			transparency_texture_amount332.outs.Value.Connect(toggle_transparency_texture329.ins.Value2);
			diffuse298.outs.BSDF.Connect(diffuse_alpha_cutter397.ins.Closure1);
			transparent391.outs.BSDF.Connect(diffuse_alpha_cutter397.ins.Closure2);
			toggle_transparency_texture329.outs.Value.Connect(diffuse_alpha_cutter397.ins.Fac);
			final_base_color345.outs.Image.Connect(principled_metal299.ins.BaseColor);
			roughness_times_roughness339.outs.Value.Connect(principled_metal299.ins.Roughness);
			bump333.outs.Normal.Connect(principled_metal299.ins.Normal);
			principled_metal299.outs.BSDF.Connect(metal_alpha_cutter398.ins.Closure1);
			transparent391.outs.BSDF.Connect(metal_alpha_cutter398.ins.Closure2);
			toggle_transparency_texture329.outs.Value.Connect(metal_alpha_cutter398.ins.Fac);
			roughness_times_roughness339.outs.Value.Connect(paint_roughness_compression402.ins.Value1);
			paint_roughness_compression402.outs.Value.Connect(paint_roughness_fixer403.ins.Value1);
			paint_roughness_fixer403.outs.Value.Connect(paint_shine_fixer404.ins.Value2);
			final_base_color345.outs.Image.Connect(principled_paint302.ins.BaseColor);
			paint_shine_fixer404.outs.Value.Connect(principled_paint302.ins.Specular);
			paint_roughness_fixer403.outs.Value.Connect(principled_paint302.ins.Roughness);
			paint_shine_fixer404.outs.Value.Connect(principled_paint302.ins.Sheen);
			bump333.outs.Normal.Connect(principled_paint302.ins.Normal);
			principled_paint302.outs.BSDF.Connect(paint_alpha_cutter399.ins.Closure1);
			transparent391.outs.BSDF.Connect(paint_alpha_cutter399.ins.Closure2);
			toggle_transparency_texture329.outs.Value.Connect(paint_alpha_cutter399.ins.Fac);
			roughness_times_roughness339.outs.Value.Connect(invert_roughness310.ins.Value2);
			invert_roughness310.outs.Value.Connect(transparency_factor_for_roughness311.ins.Value1);
			transparency_factor_for_roughness311.outs.Value.Connect(toggle_when_shadow312.ins.Value1);
			light_path322.outs.IsShadowRay.Connect(toggle_when_shadow312.ins.Value2);
			final_base_color345.outs.Image.Connect(principled_glass_and_gem304.ins.BaseColor);
			roughness_times_roughness339.outs.Value.Connect(principled_glass_and_gem304.ins.Roughness);
			bump333.outs.Normal.Connect(principled_glass_and_gem304.ins.Normal);
			toggle_when_shadow312.outs.Value.Connect(transparency_layer_weight313.ins.Value1);
			layer_weight314.outs.Facing.Connect(transparency_layer_weight313.ins.Value2);
			principled_glass_and_gem304.outs.BSDF.Connect(glass_and_gem308.ins.Closure1);
			transparent307.outs.BSDF.Connect(glass_and_gem308.ins.Closure2);
			transparency_layer_weight313.outs.Value.Connect(glass_and_gem308.ins.Fac);
			glass_and_gem308.outs.Closure.Connect(gem_and_glass_alpha_cutter400.ins.Closure1);
			transparent391.outs.BSDF.Connect(gem_and_glass_alpha_cutter400.ins.Closure2);
			toggle_transparency_texture329.outs.Value.Connect(gem_and_glass_alpha_cutter400.ins.Fac);
			final_base_color345.outs.Image.Connect(principled306.ins.BaseColor);
			paint_roughness_fixer403.outs.Value.Connect(principled306.ins.Roughness);
			bump333.outs.Normal.Connect(principled306.ins.Normal);
			final_base_color345.outs.Image.Connect(shadeless_bsdf337.ins.Color);
			paint_roughness_fixer403.outs.Value.Connect(invert_roughness316.ins.Value2);
			invert_roughness316.outs.Value.Connect(multiply_transparency317.ins.Value1);
			multiply_transparency317.outs.Value.Connect(multiply_with_shadowray318.ins.Value1);
			light_path309.outs.IsShadowRay.Connect(multiply_with_shadowray318.ins.Value2);
			principled306.outs.BSDF.Connect(shadeless338.ins.Closure1);
			shadeless_bsdf337.outs.Emission.Connect(shadeless338.ins.Closure2);
			final_base_color345.outs.Image.Connect(coloured_shadow_trans_color315.ins.Color);
			multiply_with_shadowray318.outs.Value.Connect(weight_for_shadowray_coloured_shadow319.ins.Value1);
			layer_weight320.outs.Facing.Connect(weight_for_shadowray_coloured_shadow319.ins.Value2);
			shadeless338.outs.Closure.Connect(coloured_shadow_mix321.ins.Closure1);
			coloured_shadow_trans_color315.outs.BSDF.Connect(coloured_shadow_mix321.ins.Closure2);
			weight_for_shadowray_coloured_shadow319.outs.Value.Connect(coloured_shadow_mix321.ins.Fac);
			coloured_shadow_mix321.outs.Closure.Connect(plastic_alpha_cutter401.ins.Closure1);
			transparent391.outs.BSDF.Connect(plastic_alpha_cutter401.ins.Closure2);
			toggle_transparency_texture329.outs.Value.Connect(plastic_alpha_cutter401.ins.Fac);


			diffuse_alpha_cutter397.outs.Closure.Connect(m_shader.Output.ins.Surface);
			metal_alpha_cutter398.outs.Closure.Connect(m_shader.Output.ins.Surface);
			paint_alpha_cutter399.outs.Closure.Connect(m_shader.Output.ins.Surface);
			gem_and_glass_alpha_cutter400.outs.Closure.Connect(m_shader.Output.ins.Surface);
			plastic_alpha_cutter401.outs.Closure.Connect(m_shader.Output.ins.Surface);

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture324, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture324, texcoord323);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture334, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture334, texcoord323);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture325, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture325, texcoord323);
			}

			switch (part.CyclesMaterialType)
			{
				case ShaderBody.CyclesMaterial.Diffuse:
					return diffuse_alpha_cutter397;
				case ShaderBody.CyclesMaterial.SimpleMetal:
					return metal_alpha_cutter398;
				case ShaderBody.CyclesMaterial.Paint:
					return paint_alpha_cutter399;
				case ShaderBody.CyclesMaterial.Glass:
					return gem_and_glass_alpha_cutter400;
				default:
					return plastic_alpha_cutter401;
			}

		}

	}
}
