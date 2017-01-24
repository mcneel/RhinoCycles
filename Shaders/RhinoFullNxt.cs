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
		public RhinoFullNxt(Client client, CyclesShader intermediate) : this(client, intermediate, null, intermediate.Name)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing) : this(client, intermediate, existing, intermediate.Name)
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
			var texcoord86 = new TextureCoordinateNode("texcoord");

			var diffuse_texture87 = new ImageTextureNode("diffuse_texture");
			diffuse_texture87.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture87.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture87.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture87.Extension = m_original.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
			diffuse_texture87.Interpolation = InterpolationType.Linear;
			diffuse_texture87.UseAlpha = true;
			diffuse_texture87.IsLinear = false;

			var invert_alpha110 = new MathNode("invert_alpha");
			invert_alpha110.ins.Value1.Value = 1f;
			invert_alpha110.ins.Value2.Value = 0f;
			invert_alpha110.Operation = MathNode.Operations.Subtract;
			invert_alpha110.UseClamp = false;

			var honor_texture_repeat111 = new MathNode("honor_texture_repeat");
			honor_texture_repeat111.ins.Value1.Value = 1f;
			honor_texture_repeat111.ins.Value2.Value = m_original.DiffuseTexture.InvertRepeatAsFloat;
			honor_texture_repeat111.Operation = MathNode.Operations.Multiply;
			honor_texture_repeat111.UseClamp = false;

			var invert_transparency113 = new MathNode("invert_transparency");
			invert_transparency113.ins.Value1.Value = 1f;
			invert_transparency113.ins.Value2.Value = m_original.Transparency;
			invert_transparency113.Operation = MathNode.Operations.Subtract;
			invert_transparency113.UseClamp = false;

			var repeat_mixer109 = new MixNode("repeat_mixer");
			repeat_mixer109.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			repeat_mixer109.ins.Color2.Value = m_original.BaseColor;
			repeat_mixer109.ins.Fac.Value = 0f;
			repeat_mixer109.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			repeat_mixer109.UseClamp = false;

			var weight_diffuse_amount_by_transparency_inv112 = new MathNode("weight_diffuse_amount_by_transparency_inv");
			weight_diffuse_amount_by_transparency_inv112.ins.Value1.Value = m_original.DiffuseTexture.Amount;
			weight_diffuse_amount_by_transparency_inv112.ins.Value2.Value = 0f;
			weight_diffuse_amount_by_transparency_inv112.Operation = MathNode.Operations.Multiply;
			weight_diffuse_amount_by_transparency_inv112.UseClamp = false;

			var diffuse_texture_amount91 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount91.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount91.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount91.ins.Fac.Value = 0f;
			diffuse_texture_amount91.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount91.UseClamp = false;

			var invert_diffuse_color_amount94 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount94.ins.Value1.Value = 1f;
			invert_diffuse_color_amount94.ins.Value2.Value = 0f;
			invert_diffuse_color_amount94.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount94.UseClamp = false;

			var diffuse_col_amount93 = new MixNode("diffuse_col_amount");
			diffuse_col_amount93.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount93.ins.Color2.Value = m_original.BaseColor;
			diffuse_col_amount93.ins.Fac.Value = 1f;
			diffuse_col_amount93.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_col_amount93.UseClamp = false;

			var separate_diffuse_texture_color103 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color103.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color104 = new SeparateRgbNode("separate_base_color");
			separate_base_color104.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r105 = new MathNode("add_base_color_r");
			add_base_color_r105.ins.Value1.Value = 0f;
			add_base_color_r105.ins.Value2.Value = 0f;
			add_base_color_r105.Operation = MathNode.Operations.Add;
			add_base_color_r105.UseClamp = true;

			var add_base_color_g106 = new MathNode("add_base_color_g");
			add_base_color_g106.ins.Value1.Value = 0f;
			add_base_color_g106.ins.Value2.Value = 0f;
			add_base_color_g106.Operation = MathNode.Operations.Add;
			add_base_color_g106.UseClamp = true;

			var add_base_color_b107 = new MathNode("add_base_color_b");
			add_base_color_b107.ins.Value1.Value = 0f;
			add_base_color_b107.ins.Value2.Value = 0f;
			add_base_color_b107.Operation = MathNode.Operations.Add;
			add_base_color_b107.UseClamp = true;

			var bump_texture97 = new ImageTextureNode("bump_texture");
			bump_texture97.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture97.Projection = TextureNode.TextureProjection.Flat;
			bump_texture97.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture97.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture97.Interpolation = InterpolationType.Linear;
			bump_texture97.UseAlpha = true;
			bump_texture97.IsLinear = false;

			var bump_texture_to_bw98 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw98.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount99 = new MathNode("bump_amount");
			bump_amount99.ins.Value1.Value = 4.66f;
			bump_amount99.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount99.Operation = MathNode.Operations.Multiply;
			bump_amount99.UseClamp = false;

			var final_base_color108 = new CombineRgbNode("final_base_color");
			final_base_color108.ins.R.Value = 0f;
			final_base_color108.ins.G.Value = 0f;
			final_base_color108.ins.B.Value = 0f;

			var bump96 = new BumpNode("bump");
			bump96.ins.Height.Value = 0f;
			bump96.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump96.ins.Strength.Value = 0f;
			bump96.ins.Distance.Value = 0.1f;

			var transparency_texture88 = new ImageTextureNode("transparency_texture");
			transparency_texture88.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture88.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture88.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture88.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture88.Interpolation = InterpolationType.Linear;
			transparency_texture88.UseAlpha = true;
			transparency_texture88.IsLinear = false;

			var transpluminance89 = new RgbToLuminanceNode("transpluminance");
			transpluminance89.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence90 = new MathNode("invert_luminence");
			invert_luminence90.ins.Value1.Value = 1f;
			invert_luminence90.ins.Value2.Value = 0f;
			invert_luminence90.Operation = MathNode.Operations.Subtract;
			invert_luminence90.UseClamp = false;

			var transparency_texture_amount95 = new MathNode("transparency_texture_amount");
			transparency_texture_amount95.ins.Value1.Value = 1f;
			transparency_texture_amount95.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount95.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount95.UseClamp = false;

			var diffuse61 = new DiffuseBsdfNode("diffuse");
			diffuse61.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse61.ins.Roughness.Value = 0f;
			diffuse61.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent154 = new TransparentBsdfNode("transparent");
			transparent154.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var toggle_transparency_texture92 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture92.ins.Value1.Value = m_original.HasTransparencyTextureAsFloat;
			toggle_transparency_texture92.ins.Value2.Value = 1f;
			toggle_transparency_texture92.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture92.UseClamp = false;

			var diffuse_alpha_cutter160 = new MixClosureNode("diffuse_alpha_cutter");
			diffuse_alpha_cutter160.ins.Fac.Value = 0f;

			var roughness_times_roughness102 = new MathNode("roughness_times_roughness");
			roughness_times_roughness102.ins.Value1.Value = m_original.Roughness;
			roughness_times_roughness102.ins.Value2.Value = m_original.Roughness;
			roughness_times_roughness102.Operation = MathNode.Operations.Multiply;
			roughness_times_roughness102.UseClamp = false;

			var principled_metal62 = new UberBsdfNode("principled_metal");
			principled_metal62.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal62.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_metal62.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal62.ins.Metallic.Value = m_original.Reflectivity;
			principled_metal62.ins.Subsurface.Value = 0f;
			principled_metal62.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal62.ins.Specular.Value = m_original.Reflectivity;
			principled_metal62.ins.Roughness.Value = 0f;
			principled_metal62.ins.SpecularTint.Value = m_original.Reflectivity;
			principled_metal62.ins.Anisotropic.Value = 0f;
			principled_metal62.ins.Sheen.Value = 0f;
			principled_metal62.ins.SheenTint.Value = 0f;
			principled_metal62.ins.Clearcoat.Value = 0f;
			principled_metal62.ins.ClearcoatGloss.Value = 0f;
			principled_metal62.ins.IOR.Value = 0f;
			principled_metal62.ins.Transparency.Value = 0f;
			principled_metal62.ins.RefractionRoughness.Value = 0f;
			principled_metal62.ins.AnisotropicRotation.Value = 0f;
			principled_metal62.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal62.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal62.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var metal_alpha_cutter161 = new MixClosureNode("metal_alpha_cutter");
			metal_alpha_cutter161.ins.Fac.Value = 0f;

			var principled_paint65 = new UberBsdfNode("principled_paint");
			principled_paint65.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint65.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_paint65.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint65.ins.Metallic.Value = 0f;
			principled_paint65.ins.Subsurface.Value = 0f;
			principled_paint65.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint65.ins.Specular.Value = m_original.Shine;
			principled_paint65.ins.Roughness.Value = 0f;
			principled_paint65.ins.SpecularTint.Value = 0f;
			principled_paint65.ins.Anisotropic.Value = 0f;
			principled_paint65.ins.Sheen.Value = m_original.Shine;
			principled_paint65.ins.SheenTint.Value = 0f;
			principled_paint65.ins.Clearcoat.Value = 0f;
			principled_paint65.ins.ClearcoatGloss.Value = 0f;
			principled_paint65.ins.IOR.Value = 0f;
			principled_paint65.ins.Transparency.Value = 0f;
			principled_paint65.ins.RefractionRoughness.Value = 0f;
			principled_paint65.ins.AnisotropicRotation.Value = 0f;
			principled_paint65.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint65.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint65.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var paint_alpha_cutter162 = new MixClosureNode("paint_alpha_cutter");
			paint_alpha_cutter162.ins.Fac.Value = 0f;

			var invert_roughness73 = new MathNode("invert_roughness");
			invert_roughness73.ins.Value1.Value = 1f;
			invert_roughness73.ins.Value2.Value = 0f;
			invert_roughness73.Operation = MathNode.Operations.Subtract;
			invert_roughness73.UseClamp = false;

			var transparency_factor_for_roughness74 = new MathNode("transparency_factor_for_roughness");
			transparency_factor_for_roughness74.ins.Value1.Value = 1f;
			transparency_factor_for_roughness74.ins.Value2.Value = m_original.Transparency;
			transparency_factor_for_roughness74.Operation = MathNode.Operations.Multiply;
			transparency_factor_for_roughness74.UseClamp = false;

			var light_path85 = new LightPathNode("light_path");

			var toggle_when_shadow75 = new MathNode("toggle_when_shadow");
			toggle_when_shadow75.ins.Value1.Value = 1f;
			toggle_when_shadow75.ins.Value2.Value = 0f;
			toggle_when_shadow75.Operation = MathNode.Operations.Multiply;
			toggle_when_shadow75.UseClamp = false;

			var layer_weight77 = new LayerWeightNode("layer_weight");
			layer_weight77.ins.Blend.Value = 0.87f;
			layer_weight77.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_glass_and_gem67 = new UberBsdfNode("principled_glass_and_gem");
			principled_glass_and_gem67.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem67.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_glass_and_gem67.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem67.ins.Metallic.Value = 0f;
			principled_glass_and_gem67.ins.Subsurface.Value = 0f;
			principled_glass_and_gem67.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem67.ins.Specular.Value = 0f;
			principled_glass_and_gem67.ins.Roughness.Value = 0f;
			principled_glass_and_gem67.ins.SpecularTint.Value = 0f;
			principled_glass_and_gem67.ins.Anisotropic.Value = 0f;
			principled_glass_and_gem67.ins.Sheen.Value = 0f;
			principled_glass_and_gem67.ins.SheenTint.Value = 0f;
			principled_glass_and_gem67.ins.Clearcoat.Value = 0f;
			principled_glass_and_gem67.ins.ClearcoatGloss.Value = 0f;
			principled_glass_and_gem67.ins.IOR.Value = m_original.IOR;
			principled_glass_and_gem67.ins.Transparency.Value = m_original.Transparency;
			principled_glass_and_gem67.ins.RefractionRoughness.Value = 0f;
			principled_glass_and_gem67.ins.AnisotropicRotation.Value = 0f;
			principled_glass_and_gem67.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem67.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem67.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent70 = new TransparentBsdfNode("transparent");
			transparent70.ins.Color.Value = m_original.TransparencyColor;

			var transparency_layer_weight76 = new MathNode("transparency_layer_weight");
			transparency_layer_weight76.ins.Value1.Value = 0f;
			transparency_layer_weight76.ins.Value2.Value = 0f;
			transparency_layer_weight76.Operation = MathNode.Operations.Multiply;
			transparency_layer_weight76.UseClamp = false;

			var glass_and_gem71 = new MixClosureNode("glass_and_gem");
			glass_and_gem71.ins.Fac.Value = 0f;

			var gem_and_glass_alpha_cutter163 = new MixClosureNode("gem_and_glass_alpha_cutter");
			gem_and_glass_alpha_cutter163.ins.Fac.Value = 0f;

			var principled69 = new UberBsdfNode("principled");
			principled69.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled69.ins.SpecularColor.Value = m_original.SpecularColor;
			principled69.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled69.ins.Metallic.Value = m_original.Metalic;
			principled69.ins.Subsurface.Value = 0f;
			principled69.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled69.ins.Specular.Value = m_original.Shine;
			principled69.ins.Roughness.Value = 0f;
			principled69.ins.SpecularTint.Value = m_original.Gloss;
			principled69.ins.Anisotropic.Value = 0f;
			principled69.ins.Sheen.Value = 0f;
			principled69.ins.SheenTint.Value = m_original.Gloss;
			principled69.ins.Clearcoat.Value = 0f;
			principled69.ins.ClearcoatGloss.Value = m_original.Gloss;
			principled69.ins.IOR.Value = m_original.IOR;
			principled69.ins.Transparency.Value = m_original.Transparency;
			principled69.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled69.ins.AnisotropicRotation.Value = 0f;
			principled69.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled69.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled69.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf100 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf100.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf100.ins.Strength.Value = 1f;

			var invert_roughness79 = new MathNode("invert_roughness");
			invert_roughness79.ins.Value1.Value = 1f;
			invert_roughness79.ins.Value2.Value = 0f;
			invert_roughness79.Operation = MathNode.Operations.Subtract;
			invert_roughness79.UseClamp = false;

			var multiply_transparency80 = new MathNode("multiply_transparency");
			multiply_transparency80.ins.Value1.Value = 1f;
			multiply_transparency80.ins.Value2.Value = m_original.Transparency;
			multiply_transparency80.Operation = MathNode.Operations.Multiply;
			multiply_transparency80.UseClamp = false;

			var light_path72 = new LightPathNode("light_path");

			var multiply_with_shadowray81 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray81.ins.Value1.Value = 1f;
			multiply_with_shadowray81.ins.Value2.Value = 0f;
			multiply_with_shadowray81.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray81.UseClamp = false;

			var layer_weight83 = new LayerWeightNode("layer_weight");
			layer_weight83.ins.Blend.Value = 0.89f;
			layer_weight83.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless101 = new MixClosureNode("shadeless");
			shadeless101.ins.Fac.Value = m_original.ShadelessAsFloat;

			var coloured_shadow_trans_color78 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color78.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow82 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow82.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow82.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow82.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow82.UseClamp = false;

			var coloured_shadow_mix84 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix84.ins.Fac.Value = 0f;

			var gem_and_glass_alpha_cutter164 = new MixClosureNode("gem_and_glass_alpha_cutter");
			gem_and_glass_alpha_cutter164.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord86);
			m_shader.AddNode(diffuse_texture87);
			m_shader.AddNode(invert_alpha110);
			m_shader.AddNode(honor_texture_repeat111);
			m_shader.AddNode(invert_transparency113);
			m_shader.AddNode(repeat_mixer109);
			m_shader.AddNode(weight_diffuse_amount_by_transparency_inv112);
			m_shader.AddNode(diffuse_texture_amount91);
			m_shader.AddNode(invert_diffuse_color_amount94);
			m_shader.AddNode(diffuse_col_amount93);
			m_shader.AddNode(separate_diffuse_texture_color103);
			m_shader.AddNode(separate_base_color104);
			m_shader.AddNode(add_base_color_r105);
			m_shader.AddNode(add_base_color_g106);
			m_shader.AddNode(add_base_color_b107);
			m_shader.AddNode(bump_texture97);
			m_shader.AddNode(bump_texture_to_bw98);
			m_shader.AddNode(bump_amount99);
			m_shader.AddNode(final_base_color108);
			m_shader.AddNode(bump96);
			m_shader.AddNode(transparency_texture88);
			m_shader.AddNode(transpluminance89);
			m_shader.AddNode(invert_luminence90);
			m_shader.AddNode(transparency_texture_amount95);
			m_shader.AddNode(diffuse61);
			m_shader.AddNode(transparent154);
			m_shader.AddNode(toggle_transparency_texture92);
			m_shader.AddNode(diffuse_alpha_cutter160);
			m_shader.AddNode(roughness_times_roughness102);
			m_shader.AddNode(principled_metal62);
			m_shader.AddNode(metal_alpha_cutter161);
			m_shader.AddNode(principled_paint65);
			m_shader.AddNode(paint_alpha_cutter162);
			m_shader.AddNode(invert_roughness73);
			m_shader.AddNode(transparency_factor_for_roughness74);
			m_shader.AddNode(light_path85);
			m_shader.AddNode(toggle_when_shadow75);
			m_shader.AddNode(layer_weight77);
			m_shader.AddNode(principled_glass_and_gem67);
			m_shader.AddNode(transparent70);
			m_shader.AddNode(transparency_layer_weight76);
			m_shader.AddNode(glass_and_gem71);
			m_shader.AddNode(gem_and_glass_alpha_cutter163);
			m_shader.AddNode(principled69);
			m_shader.AddNode(shadeless_bsdf100);
			m_shader.AddNode(invert_roughness79);
			m_shader.AddNode(multiply_transparency80);
			m_shader.AddNode(light_path72);
			m_shader.AddNode(multiply_with_shadowray81);
			m_shader.AddNode(layer_weight83);
			m_shader.AddNode(shadeless101);
			m_shader.AddNode(coloured_shadow_trans_color78);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow82);
			m_shader.AddNode(coloured_shadow_mix84);
			m_shader.AddNode(gem_and_glass_alpha_cutter164);


			texcoord86.outs.UV.Connect(diffuse_texture87.ins.Vector);
			diffuse_texture87.outs.Alpha.Connect(invert_alpha110.ins.Value2);
			invert_alpha110.outs.Value.Connect(honor_texture_repeat111.ins.Value1);
			diffuse_texture87.outs.Color.Connect(repeat_mixer109.ins.Color1);
			honor_texture_repeat111.outs.Value.Connect(repeat_mixer109.ins.Fac);
			invert_transparency113.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv112.ins.Value2);
			repeat_mixer109.outs.Color.Connect(diffuse_texture_amount91.ins.Color2);
			weight_diffuse_amount_by_transparency_inv112.outs.Value.Connect(diffuse_texture_amount91.ins.Fac);
			weight_diffuse_amount_by_transparency_inv112.outs.Value.Connect(invert_diffuse_color_amount94.ins.Value2);
			invert_diffuse_color_amount94.outs.Value.Connect(diffuse_col_amount93.ins.Fac);
			diffuse_texture_amount91.outs.Color.Connect(separate_diffuse_texture_color103.ins.Image);
			diffuse_col_amount93.outs.Color.Connect(separate_base_color104.ins.Image);
			separate_diffuse_texture_color103.outs.R.Connect(add_base_color_r105.ins.Value1);
			separate_base_color104.outs.R.Connect(add_base_color_r105.ins.Value2);
			separate_diffuse_texture_color103.outs.G.Connect(add_base_color_g106.ins.Value1);
			separate_base_color104.outs.G.Connect(add_base_color_g106.ins.Value2);
			separate_diffuse_texture_color103.outs.B.Connect(add_base_color_b107.ins.Value1);
			separate_base_color104.outs.B.Connect(add_base_color_b107.ins.Value2);
			texcoord86.outs.UV.Connect(bump_texture97.ins.Vector);
			bump_texture97.outs.Color.Connect(bump_texture_to_bw98.ins.Color);
			add_base_color_r105.outs.Value.Connect(final_base_color108.ins.R);
			add_base_color_g106.outs.Value.Connect(final_base_color108.ins.G);
			add_base_color_b107.outs.Value.Connect(final_base_color108.ins.B);
			bump_texture_to_bw98.outs.Val.Connect(bump96.ins.Height);
			bump_amount99.outs.Value.Connect(bump96.ins.Strength);
			texcoord86.outs.UV.Connect(transparency_texture88.ins.Vector);
			transparency_texture88.outs.Color.Connect(transpluminance89.ins.Color);
			transpluminance89.outs.Val.Connect(invert_luminence90.ins.Value2);
			invert_luminence90.outs.Value.Connect(transparency_texture_amount95.ins.Value1);
			final_base_color108.outs.Image.Connect(diffuse61.ins.Color);
			bump96.outs.Normal.Connect(diffuse61.ins.Normal);
			transparency_texture_amount95.outs.Value.Connect(toggle_transparency_texture92.ins.Value2);
			diffuse61.outs.BSDF.Connect(diffuse_alpha_cutter160.ins.Closure1);
			transparent154.outs.BSDF.Connect(diffuse_alpha_cutter160.ins.Closure2);
			toggle_transparency_texture92.outs.Value.Connect(diffuse_alpha_cutter160.ins.Fac);
			final_base_color108.outs.Image.Connect(principled_metal62.ins.BaseColor);
			roughness_times_roughness102.outs.Value.Connect(principled_metal62.ins.Roughness);
			bump96.outs.Normal.Connect(principled_metal62.ins.Normal);
			principled_metal62.outs.BSDF.Connect(metal_alpha_cutter161.ins.Closure1);
			transparent154.outs.BSDF.Connect(metal_alpha_cutter161.ins.Closure2);
			toggle_transparency_texture92.outs.Value.Connect(metal_alpha_cutter161.ins.Fac);
			final_base_color108.outs.Image.Connect(principled_paint65.ins.BaseColor);
			roughness_times_roughness102.outs.Value.Connect(principled_paint65.ins.Roughness);
			bump96.outs.Normal.Connect(principled_paint65.ins.Normal);
			principled_paint65.outs.BSDF.Connect(paint_alpha_cutter162.ins.Closure1);
			transparent154.outs.BSDF.Connect(paint_alpha_cutter162.ins.Closure2);
			toggle_transparency_texture92.outs.Value.Connect(paint_alpha_cutter162.ins.Fac);
			roughness_times_roughness102.outs.Value.Connect(invert_roughness73.ins.Value2);
			invert_roughness73.outs.Value.Connect(transparency_factor_for_roughness74.ins.Value1);
			transparency_factor_for_roughness74.outs.Value.Connect(toggle_when_shadow75.ins.Value1);
			light_path85.outs.IsShadowRay.Connect(toggle_when_shadow75.ins.Value2);
			final_base_color108.outs.Image.Connect(principled_glass_and_gem67.ins.BaseColor);
			roughness_times_roughness102.outs.Value.Connect(principled_glass_and_gem67.ins.Roughness);
			bump96.outs.Normal.Connect(principled_glass_and_gem67.ins.Normal);
			toggle_when_shadow75.outs.Value.Connect(transparency_layer_weight76.ins.Value1);
			layer_weight77.outs.Facing.Connect(transparency_layer_weight76.ins.Value2);
			principled_glass_and_gem67.outs.BSDF.Connect(glass_and_gem71.ins.Closure1);
			transparent70.outs.BSDF.Connect(glass_and_gem71.ins.Closure2);
			transparency_layer_weight76.outs.Value.Connect(glass_and_gem71.ins.Fac);
			glass_and_gem71.outs.Closure.Connect(gem_and_glass_alpha_cutter163.ins.Closure1);
			transparent154.outs.BSDF.Connect(gem_and_glass_alpha_cutter163.ins.Closure2);
			toggle_transparency_texture92.outs.Value.Connect(gem_and_glass_alpha_cutter163.ins.Fac);
			final_base_color108.outs.Image.Connect(principled69.ins.BaseColor);
			roughness_times_roughness102.outs.Value.Connect(principled69.ins.Roughness);
			bump96.outs.Normal.Connect(principled69.ins.Normal);
			final_base_color108.outs.Image.Connect(shadeless_bsdf100.ins.Color);
			roughness_times_roughness102.outs.Value.Connect(invert_roughness79.ins.Value2);
			invert_roughness79.outs.Value.Connect(multiply_transparency80.ins.Value1);
			multiply_transparency80.outs.Value.Connect(multiply_with_shadowray81.ins.Value1);
			light_path72.outs.IsShadowRay.Connect(multiply_with_shadowray81.ins.Value2);
			principled69.outs.BSDF.Connect(shadeless101.ins.Closure1);
			shadeless_bsdf100.outs.Emission.Connect(shadeless101.ins.Closure2);
			final_base_color108.outs.Image.Connect(coloured_shadow_trans_color78.ins.Color);
			multiply_with_shadowray81.outs.Value.Connect(weight_for_shadowray_coloured_shadow82.ins.Value1);
			layer_weight83.outs.Facing.Connect(weight_for_shadowray_coloured_shadow82.ins.Value2);
			shadeless101.outs.Closure.Connect(coloured_shadow_mix84.ins.Closure1);
			coloured_shadow_trans_color78.outs.BSDF.Connect(coloured_shadow_mix84.ins.Closure2);
			weight_for_shadowray_coloured_shadow82.outs.Value.Connect(coloured_shadow_mix84.ins.Fac);
			coloured_shadow_mix84.outs.Closure.Connect(gem_and_glass_alpha_cutter164.ins.Closure1);
			transparent154.outs.BSDF.Connect(gem_and_glass_alpha_cutter164.ins.Closure2);
			toggle_transparency_texture92.outs.Value.Connect(gem_and_glass_alpha_cutter164.ins.Fac);

			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture87, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture87, texcoord86);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture97, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture97, texcoord86);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture88, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture88, texcoord86);
			}

			switch (m_original.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.Diffuse:
					diffuse_alpha_cutter160.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.SimpleMetal:
					metal_alpha_cutter161.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Glass:
					gem_and_glass_alpha_cutter163.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Paint:
					paint_alpha_cutter162.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				default:
					gem_and_glass_alpha_cutter164.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
			}

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
