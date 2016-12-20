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
			var texcoord1126 = new TextureCoordinateNode("texcoord");

			var diffuse_texture1127 = new ImageTextureNode("diffuse_texture");
			diffuse_texture1127.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture1127.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture1127.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture1127.Extension = TextureNode.TextureExtension.Repeat;
			diffuse_texture1127.Interpolation = InterpolationType.Linear;
			diffuse_texture1127.UseAlpha = true;
			diffuse_texture1127.IsLinear = false;

			var diffuse_texture_amount1136 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount1136.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount1136.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount1136.ins.Fac.Value = m_original.DiffuseTexture.Amount;
			diffuse_texture_amount1136.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount1136.UseClamp = false;

			var invert_diffuse_color_amount1139 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount1139.ins.Value1.Value = 1f;
			invert_diffuse_color_amount1139.ins.Value2.Value = m_original.DiffuseTexture.Amount;
			invert_diffuse_color_amount1139.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount1139.UseClamp = false;

			var diffuse_col_amount1138 = new MixNode("diffuse_col_amount");
			diffuse_col_amount1138.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount1138.ins.Color2.Value = m_original.BaseColor;
			diffuse_col_amount1138.ins.Fac.Value = 1f;
			diffuse_col_amount1138.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_col_amount1138.UseClamp = false;

			var separate_diffuse_texture_color1151 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color1151.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color1152 = new SeparateRgbNode("separate_base_color");
			separate_base_color1152.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r1153 = new MathNode("add_base_color_r");
			add_base_color_r1153.ins.Value1.Value = 0f;
			add_base_color_r1153.ins.Value2.Value = 0f;
			add_base_color_r1153.Operation = MathNode.Operations.Add;
			add_base_color_r1153.UseClamp = true;

			var add_base_color_g1154 = new MathNode("add_base_color_g");
			add_base_color_g1154.ins.Value1.Value = 0f;
			add_base_color_g1154.ins.Value2.Value = 0f;
			add_base_color_g1154.Operation = MathNode.Operations.Add;
			add_base_color_g1154.UseClamp = true;

			var add_base_color_b1155 = new MathNode("add_base_color_b");
			add_base_color_b1155.ins.Value1.Value = 0f;
			add_base_color_b1155.ins.Value2.Value = 0f;
			add_base_color_b1155.Operation = MathNode.Operations.Add;
			add_base_color_b1155.UseClamp = true;

			var bump_texture1142 = new ImageTextureNode("bump_texture");
			bump_texture1142.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture1142.Projection = TextureNode.TextureProjection.Flat;
			bump_texture1142.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture1142.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture1142.Interpolation = InterpolationType.Linear;
			bump_texture1142.UseAlpha = true;
			bump_texture1142.IsLinear = false;

			var bump_texture_to_bw1143 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw1143.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount1144 = new MathNode("bump_amount");
			bump_amount1144.ins.Value1.Value = 10f;
			bump_amount1144.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount1144.Operation = MathNode.Operations.Multiply;
			bump_amount1144.UseClamp = false;

			var final_base_color1156 = new CombineRgbNode("final_base_color");
			final_base_color1156.ins.R.Value = 0f;
			final_base_color1156.ins.G.Value = 0f;
			final_base_color1156.ins.B.Value = 0f;

			var bump1141 = new BumpNode("bump");
			bump1141.ins.Height.Value = 0f;
			bump1141.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump1141.ins.Strength.Value = 0f;
			bump1141.ins.Distance.Value = 0.1f;

			var diffuse1101 = new DiffuseBsdfNode("diffuse");
			diffuse1101.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse1101.ins.Roughness.Value = 0f;
			diffuse1101.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var roughness_times_roughness1150 = new MathNode("roughness_times_roughness");
			roughness_times_roughness1150.ins.Value1.Value = m_original.Roughness;
			roughness_times_roughness1150.ins.Value2.Value = m_original.Roughness;
			roughness_times_roughness1150.Operation = MathNode.Operations.Multiply;
			roughness_times_roughness1150.UseClamp = false;

			var principled_metal1102 = new UberBsdfNode("principled_metal");
			principled_metal1102.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal1102.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_metal1102.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal1102.ins.Metallic.Value = m_original.Reflectivity;
			principled_metal1102.ins.Subsurface.Value = 0f;
			principled_metal1102.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal1102.ins.Specular.Value = m_original.Reflectivity;
			principled_metal1102.ins.Roughness.Value = 0f;
			principled_metal1102.ins.SpecularTint.Value = m_original.Reflectivity;
			principled_metal1102.ins.Anisotropic.Value = 0f;
			principled_metal1102.ins.Sheen.Value = 0f;
			principled_metal1102.ins.SheenTint.Value = 0f;
			principled_metal1102.ins.Clearcoat.Value = 0f;
			principled_metal1102.ins.ClearcoatGloss.Value = 0f;
			principled_metal1102.ins.IOR.Value = 0f;
			principled_metal1102.ins.Transparency.Value = 0f;
			principled_metal1102.ins.RefractionRoughness.Value = 0f;
			principled_metal1102.ins.AnisotropicRotation.Value = 0f;
			principled_metal1102.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal1102.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal1102.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_paint1105 = new UberBsdfNode("principled_paint");
			principled_paint1105.ins.SpecularColor.Value = m_original.SpecularColor ^ m_original.Gamma;
			principled_paint1105.ins.Metallic.Value = 0f;
			principled_paint1105.ins.Subsurface.Value = 0f;
			principled_paint1105.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint1105.ins.Specular.Value = m_original.Shine;
			principled_paint1105.ins.SpecularTint.Value = 0f;
			principled_paint1105.ins.Anisotropic.Value = 0f;
			principled_paint1105.ins.Sheen.Value = m_original.Shine;
			principled_paint1105.ins.SheenTint.Value = 0f;
			principled_paint1105.ins.Clearcoat.Value = 0f;
			principled_paint1105.ins.ClearcoatGloss.Value = 0f;
			principled_paint1105.ins.IOR.Value = 0f;
			principled_paint1105.ins.Transparency.Value = 0f;
			principled_paint1105.ins.RefractionRoughness.Value = 0f;
			principled_paint1105.ins.AnisotropicRotation.Value = 0f;
			principled_paint1105.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint1105.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint1105.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness1113 = new MathNode("invert_roughness");
			invert_roughness1113.ins.Value1.Value = 1f;
			invert_roughness1113.ins.Value2.Value = 0f;
			invert_roughness1113.Operation = MathNode.Operations.Subtract;
			invert_roughness1113.UseClamp = false;

			var transparency_factor_for_roughness1114 = new MathNode("transparency_factor_for_roughness");
			transparency_factor_for_roughness1114.ins.Value1.Value = 1f;
			transparency_factor_for_roughness1114.ins.Value2.Value = m_original.Transparency;
			transparency_factor_for_roughness1114.Operation = MathNode.Operations.Multiply;
			transparency_factor_for_roughness1114.UseClamp = false;

			var light_path1125 = new LightPathNode("light_path");

			var toggle_when_shadow1115 = new MathNode("toggle_when_shadow");
			toggle_when_shadow1115.ins.Value1.Value = 1f;
			toggle_when_shadow1115.ins.Value2.Value = 0f;
			toggle_when_shadow1115.Operation = MathNode.Operations.Multiply;
			toggle_when_shadow1115.UseClamp = false;

			var layer_weight1117 = new LayerWeightNode("layer_weight");
			layer_weight1117.ins.Blend.Value = 0.87f;
			layer_weight1117.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_glass_and_gem1107 = new UberBsdfNode("principled_glass_and_gem");
			principled_glass_and_gem1107.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem1107.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_glass_and_gem1107.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem1107.ins.Metallic.Value = 0f;
			principled_glass_and_gem1107.ins.Subsurface.Value = 0f;
			principled_glass_and_gem1107.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem1107.ins.Specular.Value = 0f;
			principled_glass_and_gem1107.ins.Roughness.Value = 0f;
			principled_glass_and_gem1107.ins.SpecularTint.Value = 0f;
			principled_glass_and_gem1107.ins.Anisotropic.Value = 0f;
			principled_glass_and_gem1107.ins.Sheen.Value = 0f;
			principled_glass_and_gem1107.ins.SheenTint.Value = 0f;
			principled_glass_and_gem1107.ins.Clearcoat.Value = 0f;
			principled_glass_and_gem1107.ins.ClearcoatGloss.Value = 0f;
			principled_glass_and_gem1107.ins.IOR.Value = m_original.IOR;
			principled_glass_and_gem1107.ins.Transparency.Value = m_original.Transparency;
			principled_glass_and_gem1107.ins.RefractionRoughness.Value = 0f;
			principled_glass_and_gem1107.ins.AnisotropicRotation.Value = 0f;
			principled_glass_and_gem1107.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem1107.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem1107.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent1110 = new TransparentBsdfNode("transparent");
			transparent1110.ins.Color.Value = m_original.TransparencyColor;

			var transparency_layer_weight1116 = new MathNode("transparency_layer_weight");
			transparency_layer_weight1116.ins.Value1.Value = 0f;
			transparency_layer_weight1116.ins.Value2.Value = 0f;
			transparency_layer_weight1116.Operation = MathNode.Operations.Multiply;
			transparency_layer_weight1116.UseClamp = false;

			var transparency_blend_for_shadow_with_gem_and_glass1111 = new MixClosureNode("transparency_blend_for_shadow_with_gem_and_glass");
			transparency_blend_for_shadow_with_gem_and_glass1111.ins.Fac.Value = 0f;

			var principled1109 = new UberBsdfNode("principled");
			principled1109.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled1109.ins.SpecularColor.Value = m_original.SpecularColor;
			principled1109.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled1109.ins.Metallic.Value = m_original.Metalic;
			principled1109.ins.Subsurface.Value = 0f;
			principled1109.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled1109.ins.Specular.Value = m_original.Shine;
			principled1109.ins.Roughness.Value = 0f;
			principled1109.ins.SpecularTint.Value = m_original.Gloss;
			principled1109.ins.Anisotropic.Value = 0f;
			principled1109.ins.Sheen.Value = 0f;
			principled1109.ins.SheenTint.Value = m_original.Gloss;
			principled1109.ins.Clearcoat.Value = 0f;
			principled1109.ins.ClearcoatGloss.Value = m_original.Gloss;
			principled1109.ins.IOR.Value = m_original.IOR;
			principled1109.ins.Transparency.Value = m_original.Transparency;
			principled1109.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled1109.ins.AnisotropicRotation.Value = 0f;
			principled1109.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled1109.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled1109.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf1145 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf1145.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf1145.ins.Strength.Value = 1f;

			var invert_roughness1119 = new MathNode("invert_roughness");
			invert_roughness1119.ins.Value1.Value = 1f;
			invert_roughness1119.ins.Value2.Value = 0f;
			invert_roughness1119.Operation = MathNode.Operations.Subtract;
			invert_roughness1119.UseClamp = false;

			var multiply_transparency1120 = new MathNode("multiply_transparency");
			multiply_transparency1120.ins.Value1.Value = 1f;
			multiply_transparency1120.ins.Value2.Value = m_original.Transparency;
			multiply_transparency1120.Operation = MathNode.Operations.Multiply;
			multiply_transparency1120.UseClamp = false;

			var light_path1112 = new LightPathNode("light_path");

			var multiply_with_shadowray1121 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray1121.ins.Value1.Value = 0f;
			multiply_with_shadowray1121.ins.Value2.Value = 0f;
			multiply_with_shadowray1121.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray1121.UseClamp = false;

			var layer_weight1123 = new LayerWeightNode("layer_weight");
			layer_weight1123.ins.Blend.Value = 0.89f;
			layer_weight1123.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless1146 = new MixClosureNode("shadeless");
			shadeless1146.ins.Fac.Value = m_original.ShadelessAsFloat;

			var coloured_shadow_trans_color1118 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color1118.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow1122 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow1122.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow1122.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow1122.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow1122.UseClamp = false;

			var invert_alpha1131 = new MathNode("invert_alpha");
			invert_alpha1131.ins.Value1.Value = 1f;
			invert_alpha1131.ins.Value2.Value = 0f;
			invert_alpha1131.Operation = MathNode.Operations.Subtract;
			invert_alpha1131.UseClamp = false;

			var coloured_shadow_mix1124 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix1124.ins.Fac.Value = 0f;

			var alphacutter_transparent1128 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent1128.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var toggle_image_alpha1130 = new MathNode("toggle_image_alpha");
			toggle_image_alpha1130.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha1130.ins.Value2.Value = 1f;
			toggle_image_alpha1130.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha1130.UseClamp = false;

			var transparency_texture1132 = new ImageTextureNode("transparency_texture");
			transparency_texture1132.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture1132.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture1132.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture1132.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture1132.Interpolation = InterpolationType.Linear;
			transparency_texture1132.UseAlpha = true;
			transparency_texture1132.IsLinear = false;

			var color___luminance1133 = new RgbToLuminanceNode("color___luminance");
			color___luminance1133.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence1135 = new MathNode("invert_luminence");
			invert_luminence1135.ins.Value1.Value = 1f;
			invert_luminence1135.ins.Value2.Value = 0f;
			invert_luminence1135.Operation = MathNode.Operations.Subtract;
			invert_luminence1135.UseClamp = false;

			var transparency_texture_amount1140 = new MathNode("transparency_texture_amount");
			transparency_texture_amount1140.ins.Value1.Value = 1f;
			transparency_texture_amount1140.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount1140.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount1140.UseClamp = false;

			var alpha_cutter_mix1129 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix1129.ins.Fac.Value = 0f;

			var toggle_transparency_texture1137 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture1137.ins.Value1.Value = m_original.HasTransparencyTextureAsFloat;
			toggle_transparency_texture1137.ins.Value2.Value = 0f;
			toggle_transparency_texture1137.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture1137.UseClamp = false;

			var emission_value1148 = new RgbToBwNode("emission_value");
			emission_value1148.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;

			var transparency_alpha_cutter1134 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter1134.ins.Fac.Value = 0f;

			var emissive1149 = new EmissionNode("emissive");
			emissive1149.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			emissive1149.ins.Strength.Value = 0f;

			var custom_emission1147 = new MixClosureNode("custom_emission");
			custom_emission1147.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord1126);
			m_shader.AddNode(diffuse_texture1127);
			m_shader.AddNode(diffuse_texture_amount1136);
			m_shader.AddNode(invert_diffuse_color_amount1139);
			m_shader.AddNode(diffuse_col_amount1138);
			m_shader.AddNode(separate_diffuse_texture_color1151);
			m_shader.AddNode(separate_base_color1152);
			m_shader.AddNode(add_base_color_r1153);
			m_shader.AddNode(add_base_color_g1154);
			m_shader.AddNode(add_base_color_b1155);
			m_shader.AddNode(bump_texture1142);
			m_shader.AddNode(bump_texture_to_bw1143);
			m_shader.AddNode(bump_amount1144);
			m_shader.AddNode(final_base_color1156);
			m_shader.AddNode(bump1141);
			m_shader.AddNode(diffuse1101);
			m_shader.AddNode(roughness_times_roughness1150);
			m_shader.AddNode(principled_metal1102);
			m_shader.AddNode(principled_paint1105);
			m_shader.AddNode(invert_roughness1113);
			m_shader.AddNode(transparency_factor_for_roughness1114);
			m_shader.AddNode(light_path1125);
			m_shader.AddNode(toggle_when_shadow1115);
			m_shader.AddNode(layer_weight1117);
			m_shader.AddNode(principled_glass_and_gem1107);
			m_shader.AddNode(transparent1110);
			m_shader.AddNode(transparency_layer_weight1116);
			m_shader.AddNode(transparency_blend_for_shadow_with_gem_and_glass1111);
			m_shader.AddNode(principled1109);
			m_shader.AddNode(shadeless_bsdf1145);
			m_shader.AddNode(invert_roughness1119);
			m_shader.AddNode(multiply_transparency1120);
			m_shader.AddNode(light_path1112);
			m_shader.AddNode(multiply_with_shadowray1121);
			m_shader.AddNode(layer_weight1123);
			m_shader.AddNode(shadeless1146);
			m_shader.AddNode(coloured_shadow_trans_color1118);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow1122);
			m_shader.AddNode(invert_alpha1131);
			m_shader.AddNode(coloured_shadow_mix1124);
			m_shader.AddNode(alphacutter_transparent1128);
			m_shader.AddNode(toggle_image_alpha1130);
			m_shader.AddNode(transparency_texture1132);
			m_shader.AddNode(color___luminance1133);
			m_shader.AddNode(invert_luminence1135);
			m_shader.AddNode(transparency_texture_amount1140);
			m_shader.AddNode(alpha_cutter_mix1129);
			m_shader.AddNode(toggle_transparency_texture1137);
			m_shader.AddNode(emission_value1148);
			m_shader.AddNode(transparency_alpha_cutter1134);
			m_shader.AddNode(emissive1149);
			m_shader.AddNode(custom_emission1147);


			texcoord1126.outs.UV.Connect(diffuse_texture1127.ins.Vector);
			diffuse_texture1127.outs.Color.Connect(diffuse_texture_amount1136.ins.Color2);
			invert_diffuse_color_amount1139.outs.Value.Connect(diffuse_col_amount1138.ins.Fac);
			diffuse_texture_amount1136.outs.Color.Connect(separate_diffuse_texture_color1151.ins.Image);
			diffuse_col_amount1138.outs.Color.Connect(separate_base_color1152.ins.Image);
			separate_diffuse_texture_color1151.outs.R.Connect(add_base_color_r1153.ins.Value1);
			separate_base_color1152.outs.R.Connect(add_base_color_r1153.ins.Value2);
			separate_diffuse_texture_color1151.outs.G.Connect(add_base_color_g1154.ins.Value1);
			separate_base_color1152.outs.G.Connect(add_base_color_g1154.ins.Value2);
			separate_diffuse_texture_color1151.outs.B.Connect(add_base_color_b1155.ins.Value1);
			separate_base_color1152.outs.B.Connect(add_base_color_b1155.ins.Value2);
			texcoord1126.outs.UV.Connect(bump_texture1142.ins.Vector);
			bump_texture1142.outs.Color.Connect(bump_texture_to_bw1143.ins.Color);
			add_base_color_r1153.outs.Value.Connect(final_base_color1156.ins.R);
			add_base_color_g1154.outs.Value.Connect(final_base_color1156.ins.G);
			add_base_color_b1155.outs.Value.Connect(final_base_color1156.ins.B);
			bump_texture_to_bw1143.outs.Val.Connect(bump1141.ins.Height);
			bump_amount1144.outs.Value.Connect(bump1141.ins.Strength);
			final_base_color1156.outs.Image.Connect(diffuse1101.ins.Color);
			bump1141.outs.Normal.Connect(diffuse1101.ins.Normal);
			final_base_color1156.outs.Image.Connect(principled_metal1102.ins.BaseColor);
			roughness_times_roughness1150.outs.Value.Connect(principled_metal1102.ins.Roughness);
			bump1141.outs.Normal.Connect(principled_metal1102.ins.Normal);
			final_base_color1156.outs.Image.Connect(principled_paint1105.ins.BaseColor);
			roughness_times_roughness1150.outs.Value.Connect(principled_paint1105.ins.Roughness);
			bump1141.outs.Normal.Connect(principled_paint1105.ins.Normal);
			roughness_times_roughness1150.outs.Value.Connect(invert_roughness1113.ins.Value2);
			invert_roughness1113.outs.Value.Connect(transparency_factor_for_roughness1114.ins.Value1);
			transparency_factor_for_roughness1114.outs.Value.Connect(toggle_when_shadow1115.ins.Value1);
			light_path1125.outs.IsShadowRay.Connect(toggle_when_shadow1115.ins.Value2);
			final_base_color1156.outs.Image.Connect(principled_glass_and_gem1107.ins.BaseColor);
			roughness_times_roughness1150.outs.Value.Connect(principled_glass_and_gem1107.ins.Roughness);
			bump1141.outs.Normal.Connect(principled_glass_and_gem1107.ins.Normal);
			toggle_when_shadow1115.outs.Value.Connect(transparency_layer_weight1116.ins.Value1);
			layer_weight1117.outs.Facing.Connect(transparency_layer_weight1116.ins.Value2);
			principled_glass_and_gem1107.outs.BSDF.Connect(transparency_blend_for_shadow_with_gem_and_glass1111.ins.Closure1);
			transparent1110.outs.BSDF.Connect(transparency_blend_for_shadow_with_gem_and_glass1111.ins.Closure2);
			transparency_layer_weight1116.outs.Value.Connect(transparency_blend_for_shadow_with_gem_and_glass1111.ins.Fac);
			final_base_color1156.outs.Image.Connect(principled1109.ins.BaseColor);
			roughness_times_roughness1150.outs.Value.Connect(principled1109.ins.Roughness);
			bump1141.outs.Normal.Connect(principled1109.ins.Normal);
			final_base_color1156.outs.Image.Connect(shadeless_bsdf1145.ins.Color);
			roughness_times_roughness1150.outs.Value.Connect(invert_roughness1119.ins.Value2);
			invert_roughness1119.outs.Value.Connect(multiply_transparency1120.ins.Value1);
			multiply_transparency1120.outs.Value.Connect(multiply_with_shadowray1121.ins.Value1);
			light_path1112.outs.IsShadowRay.Connect(multiply_with_shadowray1121.ins.Value2);
			principled1109.outs.BSDF.Connect(shadeless1146.ins.Closure1);
			shadeless_bsdf1145.outs.Emission.Connect(shadeless1146.ins.Closure2);
			final_base_color1156.outs.Image.Connect(coloured_shadow_trans_color1118.ins.Color);
			multiply_with_shadowray1121.outs.Value.Connect(weight_for_shadowray_coloured_shadow1122.ins.Value1);
			layer_weight1123.outs.Facing.Connect(weight_for_shadowray_coloured_shadow1122.ins.Value2);
			diffuse_texture1127.outs.Alpha.Connect(invert_alpha1131.ins.Value2);
			shadeless1146.outs.Closure.Connect(coloured_shadow_mix1124.ins.Closure1);
			coloured_shadow_trans_color1118.outs.BSDF.Connect(coloured_shadow_mix1124.ins.Closure2);
			weight_for_shadowray_coloured_shadow1122.outs.Value.Connect(coloured_shadow_mix1124.ins.Fac);
			invert_alpha1131.outs.Value.Connect(toggle_image_alpha1130.ins.Value2);
			texcoord1126.outs.UV.Connect(transparency_texture1132.ins.Vector);
			transparency_texture1132.outs.Color.Connect(color___luminance1133.ins.Color);
			color___luminance1133.outs.Val.Connect(invert_luminence1135.ins.Value2);
			invert_luminence1135.outs.Value.Connect(transparency_texture_amount1140.ins.Value1);
			coloured_shadow_mix1124.outs.Closure.Connect(alpha_cutter_mix1129.ins.Closure1);
			alphacutter_transparent1128.outs.BSDF.Connect(alpha_cutter_mix1129.ins.Closure2);
			toggle_image_alpha1130.outs.Value.Connect(alpha_cutter_mix1129.ins.Fac);
			transparency_texture_amount1140.outs.Value.Connect(toggle_transparency_texture1137.ins.Value2);
			alpha_cutter_mix1129.outs.Closure.Connect(transparency_alpha_cutter1134.ins.Closure1);
			alphacutter_transparent1128.outs.BSDF.Connect(transparency_alpha_cutter1134.ins.Closure2);
			toggle_transparency_texture1137.outs.Value.Connect(transparency_alpha_cutter1134.ins.Fac);
			emission_value1148.outs.Val.Connect(emissive1149.ins.Strength);
			transparency_alpha_cutter1134.outs.Closure.Connect(custom_emission1147.ins.Closure1);
			emissive1149.outs.Emission.Connect(custom_emission1147.ins.Closure2);
			emission_value1148.outs.Val.Connect(custom_emission1147.ins.Fac);

			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture1127, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture1127, texcoord1126);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture1142, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture1142, texcoord1126);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture1132, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture1132, texcoord1126);
			}

			switch (m_original.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.Diffuse:
					diffuse1101.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.SimpleMetal:
					principled_metal1102.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Glass:
					transparency_blend_for_shadow_with_gem_and_glass1111.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Paint:
					principled_paint1105.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				default:
					custom_emission1147.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
			}

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
