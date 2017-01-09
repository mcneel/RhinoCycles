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


			var texcoord193 = new TextureCoordinateNode("texcoord");

			var diffuse_texture194 = new ImageTextureNode("diffuse_texture");
			diffuse_texture194.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture194.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture194.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture194.Extension = m_original.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
			diffuse_texture194.Interpolation = InterpolationType.Linear;
			diffuse_texture194.UseAlpha = true;
			diffuse_texture194.IsLinear = false;

			var invert_alpha230 = new MathNode("invert_alpha");
			invert_alpha230.ins.Value1.Value = 1f;
			invert_alpha230.ins.Value2.Value = 0f;
			invert_alpha230.Operation = MathNode.Operations.Subtract;
			invert_alpha230.UseClamp = false;

			var honor_texture_repeat231 = new MathNode("honor_texture_repeat");
			honor_texture_repeat231.ins.Value1.Value = 1f;
			honor_texture_repeat231.ins.Value2.Value = m_original.DiffuseTexture.Repeat ? 0.0f : 1.0f;
			honor_texture_repeat231.Operation = MathNode.Operations.Multiply;
			honor_texture_repeat231.UseClamp = false;

			var subtract233 = new MathNode("subtract");
			subtract233.ins.Value1.Value = 1f;
			subtract233.ins.Value2.Value = m_original.Transparency;
			subtract233.Operation = MathNode.Operations.Subtract;
			subtract233.UseClamp = false;

			var repeat_mixer229 = new MixNode("repeat_mixer");
			repeat_mixer229.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			repeat_mixer229.ins.Color2.Value = m_original.BaseColor;
			repeat_mixer229.ins.Fac.Value = 0f;
			repeat_mixer229.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			repeat_mixer229.UseClamp = false;

			var multiply232 = new MathNode("multiply");
			multiply232.ins.Value1.Value = m_original.DiffuseTexture.Amount;
			multiply232.ins.Value2.Value = 0f;
			multiply232.Operation = MathNode.Operations.Multiply;
			multiply232.UseClamp = false;

			var diffuse_texture_amount203 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount203.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount203.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount203.ins.Fac.Value = 0f;
			diffuse_texture_amount203.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount203.UseClamp = false;

			var invert_diffuse_color_amount206 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount206.ins.Value1.Value = 1f;
			invert_diffuse_color_amount206.ins.Value2.Value = 0f;
			invert_diffuse_color_amount206.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount206.UseClamp = false;

			var diffuse_col_amount193 = new MixNode("diffuse_col_amount");
			diffuse_col_amount193.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount193.ins.Color2.Value = m_original.BaseColor;
			diffuse_col_amount193.ins.Fac.Value = 1f;
			diffuse_col_amount193.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_col_amount193.UseClamp = false;

			var separate_diffuse_texture_color218 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color218.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color219 = new SeparateRgbNode("separate_base_color");
			separate_base_color219.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r220 = new MathNode("add_base_color_r");
			add_base_color_r220.ins.Value1.Value = 0f;
			add_base_color_r220.ins.Value2.Value = 0f;
			add_base_color_r220.Operation = MathNode.Operations.Add;
			add_base_color_r220.UseClamp = true;

			var add_base_color_g221 = new MathNode("add_base_color_g");
			add_base_color_g221.ins.Value1.Value = 0f;
			add_base_color_g221.ins.Value2.Value = 0f;
			add_base_color_g221.Operation = MathNode.Operations.Add;
			add_base_color_g221.UseClamp = true;

			var add_base_color_b222 = new MathNode("add_base_color_b");
			add_base_color_b222.ins.Value1.Value = 0f;
			add_base_color_b222.ins.Value2.Value = 0f;
			add_base_color_b222.Operation = MathNode.Operations.Add;
			add_base_color_b222.UseClamp = true;

			var bump_texture209 = new ImageTextureNode("bump_texture");
			bump_texture209.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture209.Projection = TextureNode.TextureProjection.Flat;
			bump_texture209.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture209.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture209.Interpolation = InterpolationType.Linear;
			bump_texture209.UseAlpha = true;
			bump_texture209.IsLinear = false;

			var bump_texture_to_bw210 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw210.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount211 = new MathNode("bump_amount");
			bump_amount211.ins.Value1.Value = 10f;
			bump_amount211.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount211.Operation = MathNode.Operations.Multiply;
			bump_amount211.UseClamp = false;

			var final_base_color223 = new CombineRgbNode("final_base_color");
			final_base_color223.ins.R.Value = 0f;
			final_base_color223.ins.G.Value = 0f;
			final_base_color223.ins.B.Value = 0f;

			var bump208 = new BumpNode("bump");
			bump208.ins.Height.Value = 0f;
			bump208.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump208.ins.Strength.Value = 0f;
			bump208.ins.Distance.Value = 0.1f;

			var diffuse168 = new DiffuseBsdfNode("diffuse");
			diffuse168.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse168.ins.Roughness.Value = 0f;
			diffuse168.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var roughness_times_roughness217 = new MathNode("roughness_times_roughness");
			roughness_times_roughness217.ins.Value1.Value = m_original.Roughness;
			roughness_times_roughness217.ins.Value2.Value = m_original.Roughness;
			roughness_times_roughness217.Operation = MathNode.Operations.Multiply;
			roughness_times_roughness217.UseClamp = false;

			var principled_metal169 = new UberBsdfNode("principled_metal");
			principled_metal169.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal169.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_metal169.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal169.ins.Metallic.Value = m_original.Reflectivity;
			principled_metal169.ins.Subsurface.Value = 0f;
			principled_metal169.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal169.ins.Specular.Value = m_original.Reflectivity;
			principled_metal169.ins.Roughness.Value = 0f;
			principled_metal169.ins.SpecularTint.Value = m_original.Reflectivity;
			principled_metal169.ins.Anisotropic.Value = 0f;
			principled_metal169.ins.Sheen.Value = 0f;
			principled_metal169.ins.SheenTint.Value = 0f;
			principled_metal169.ins.Clearcoat.Value = 0f;
			principled_metal169.ins.ClearcoatGloss.Value = 0f;
			principled_metal169.ins.IOR.Value = 0f;
			principled_metal169.ins.Transparency.Value = 0f;
			principled_metal169.ins.RefractionRoughness.Value = 0f;
			principled_metal169.ins.AnisotropicRotation.Value = 0f;
			principled_metal169.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal169.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal169.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_paint172 = new UberBsdfNode("principled_paint");
			principled_paint172.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint172.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_paint172.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint172.ins.Metallic.Value = 0f;
			principled_paint172.ins.Subsurface.Value = 0f;
			principled_paint172.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint172.ins.Specular.Value = m_original.Shine;
			principled_paint172.ins.Roughness.Value = 0f;
			principled_paint172.ins.SpecularTint.Value = 0f;
			principled_paint172.ins.Anisotropic.Value = 0f;
			principled_paint172.ins.Sheen.Value = m_original.Shine;
			principled_paint172.ins.SheenTint.Value = 0f;
			principled_paint172.ins.Clearcoat.Value = 0f;
			principled_paint172.ins.ClearcoatGloss.Value = 0f;
			principled_paint172.ins.IOR.Value = 0f;
			principled_paint172.ins.Transparency.Value = 0f;
			principled_paint172.ins.RefractionRoughness.Value = 0f;
			principled_paint172.ins.AnisotropicRotation.Value = 0f;
			principled_paint172.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint172.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint172.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness180 = new MathNode("invert_roughness");
			invert_roughness180.ins.Value1.Value = 1f;
			invert_roughness180.ins.Value2.Value = 0f;
			invert_roughness180.Operation = MathNode.Operations.Subtract;
			invert_roughness180.UseClamp = false;

			var transparency_factor_for_roughness181 = new MathNode("transparency_factor_for_roughness");
			transparency_factor_for_roughness181.ins.Value1.Value = 1f;
			transparency_factor_for_roughness181.ins.Value2.Value = m_original.Transparency;
			transparency_factor_for_roughness181.Operation = MathNode.Operations.Multiply;
			transparency_factor_for_roughness181.UseClamp = false;

			var light_path192 = new LightPathNode("light_path");

			var toggle_when_shadow182 = new MathNode("toggle_when_shadow");
			toggle_when_shadow182.ins.Value1.Value = 1f;
			toggle_when_shadow182.ins.Value2.Value = 0f;
			toggle_when_shadow182.Operation = MathNode.Operations.Multiply;
			toggle_when_shadow182.UseClamp = false;

			var layer_weight184 = new LayerWeightNode("layer_weight");
			layer_weight184.ins.Blend.Value = 0.87f;
			layer_weight184.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_glass_and_gem174 = new UberBsdfNode("principled_glass_and_gem");
			principled_glass_and_gem174.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem174.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_glass_and_gem174.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem174.ins.Metallic.Value = 0f;
			principled_glass_and_gem174.ins.Subsurface.Value = 0f;
			principled_glass_and_gem174.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem174.ins.Specular.Value = 0f;
			principled_glass_and_gem174.ins.Roughness.Value = 0f;
			principled_glass_and_gem174.ins.SpecularTint.Value = 0f;
			principled_glass_and_gem174.ins.Anisotropic.Value = 0f;
			principled_glass_and_gem174.ins.Sheen.Value = 0f;
			principled_glass_and_gem174.ins.SheenTint.Value = 0f;
			principled_glass_and_gem174.ins.Clearcoat.Value = 0f;
			principled_glass_and_gem174.ins.ClearcoatGloss.Value = 0f;
			principled_glass_and_gem174.ins.IOR.Value = m_original.IOR;
			principled_glass_and_gem174.ins.Transparency.Value = m_original.Transparency;
			principled_glass_and_gem174.ins.RefractionRoughness.Value = 0f;
			principled_glass_and_gem174.ins.AnisotropicRotation.Value = 0f;
			principled_glass_and_gem174.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem174.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem174.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent177 = new TransparentBsdfNode("transparent");
			transparent177.ins.Color.Value = m_original.TransparencyColor;

			var transparency_layer_weight183 = new MathNode("transparency_layer_weight");
			transparency_layer_weight183.ins.Value1.Value = 0f;
			transparency_layer_weight183.ins.Value2.Value = 0f;
			transparency_layer_weight183.Operation = MathNode.Operations.Multiply;
			transparency_layer_weight183.UseClamp = false;

			var transparency_blend_for_shadow_with_gem_and_glass178 = new MixClosureNode("transparency_blend_for_shadow_with_gem_and_glass");
			transparency_blend_for_shadow_with_gem_and_glass178.ins.Fac.Value = 0f;

			var principled176 = new UberBsdfNode("principled");
			principled176.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled176.ins.SpecularColor.Value = m_original.SpecularColor;
			principled176.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled176.ins.Metallic.Value = m_original.Metalic;
			principled176.ins.Subsurface.Value = 0f;
			principled176.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled176.ins.Specular.Value = m_original.Shine;
			principled176.ins.Roughness.Value = 0f;
			principled176.ins.SpecularTint.Value = m_original.Gloss;
			principled176.ins.Anisotropic.Value = 0f;
			principled176.ins.Sheen.Value = 0f;
			principled176.ins.SheenTint.Value = m_original.Gloss;
			principled176.ins.Clearcoat.Value = 0f;
			principled176.ins.ClearcoatGloss.Value = m_original.Gloss;
			principled176.ins.IOR.Value = m_original.IOR;
			principled176.ins.Transparency.Value = m_original.Transparency;
			principled176.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled176.ins.AnisotropicRotation.Value = 0f;
			principled176.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled176.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled176.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf212 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf212.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf212.ins.Strength.Value = 1f;

			var invert_roughness186 = new MathNode("invert_roughness");
			invert_roughness186.ins.Value1.Value = 1f;
			invert_roughness186.ins.Value2.Value = 0f;
			invert_roughness186.Operation = MathNode.Operations.Subtract;
			invert_roughness186.UseClamp = false;

			var multiply_transparency187 = new MathNode("multiply_transparency");
			multiply_transparency187.ins.Value1.Value = 1f;
			multiply_transparency187.ins.Value2.Value = m_original.Transparency;
			multiply_transparency187.Operation = MathNode.Operations.Multiply;
			multiply_transparency187.UseClamp = false;

			var light_path179 = new LightPathNode("light_path");

			var multiply_with_shadowray188 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray188.ins.Value1.Value = 1f;
			multiply_with_shadowray188.ins.Value2.Value = 0f;
			multiply_with_shadowray188.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray188.UseClamp = false;

			var layer_weight190 = new LayerWeightNode("layer_weight");
			layer_weight190.ins.Blend.Value = 0.89f;
			layer_weight190.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless213 = new MixClosureNode("shadeless");
			shadeless213.ins.Fac.Value = m_original.ShadelessAsFloat;

			var coloured_shadow_trans_color185 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color185.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow189 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow189.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow189.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow189.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow189.UseClamp = false;

			var invert_alpha198 = new MathNode("invert_alpha");
			invert_alpha198.ins.Value1.Value = 1f;
			invert_alpha198.ins.Value2.Value = 0f;
			invert_alpha198.Operation = MathNode.Operations.Subtract;
			invert_alpha198.UseClamp = false;

			var coloured_shadow_mix191 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix191.ins.Fac.Value = 0f;

			var alphacutter_transparent195 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent195.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var toggle_image_alpha197 = new MathNode("toggle_image_alpha");
			toggle_image_alpha197.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha197.ins.Value2.Value = 1f;
			toggle_image_alpha197.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha197.UseClamp = false;

			var transparency_texture199 = new ImageTextureNode("transparency_texture");
			transparency_texture199.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture199.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture199.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture199.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture199.Interpolation = InterpolationType.Linear;
			transparency_texture199.UseAlpha = true;
			transparency_texture199.IsLinear = false;

			var color___luminance200 = new RgbToLuminanceNode("color___luminance");
			color___luminance200.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence202 = new MathNode("invert_luminence");
			invert_luminence202.ins.Value1.Value = 1f;
			invert_luminence202.ins.Value2.Value = 0f;
			invert_luminence202.Operation = MathNode.Operations.Subtract;
			invert_luminence202.UseClamp = false;

			var transparency_texture_amount207 = new MathNode("transparency_texture_amount");
			transparency_texture_amount207.ins.Value1.Value = 1f;
			transparency_texture_amount207.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount207.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount207.UseClamp = false;

			var alpha_cutter_mix196 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix196.ins.Fac.Value = 0f;

			var toggle_transparency_texture204 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture204.ins.Value1.Value = m_original.HasTransparencyTextureAsFloat;
			toggle_transparency_texture204.ins.Value2.Value = 0f;
			toggle_transparency_texture204.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture204.UseClamp = false;

			var emission_value215 = new RgbToBwNode("emission_value");
			emission_value215.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;

			var transparency_alpha_cutter201 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter201.ins.Fac.Value = 0f;

			var emissive216 = new EmissionNode("emissive");
			emissive216.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			emissive216.ins.Strength.Value = 0f;

			var custom_emission214 = new MixClosureNode("custom_emission");
			custom_emission214.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord193);
			m_shader.AddNode(diffuse_texture194);
			m_shader.AddNode(invert_alpha230);
			m_shader.AddNode(honor_texture_repeat231);
			m_shader.AddNode(subtract233);
			m_shader.AddNode(repeat_mixer229);
			m_shader.AddNode(multiply232);
			m_shader.AddNode(diffuse_texture_amount203);
			m_shader.AddNode(invert_diffuse_color_amount206);
			m_shader.AddNode(diffuse_col_amount193);
			m_shader.AddNode(separate_diffuse_texture_color218);
			m_shader.AddNode(separate_base_color219);
			m_shader.AddNode(add_base_color_r220);
			m_shader.AddNode(add_base_color_g221);
			m_shader.AddNode(add_base_color_b222);
			m_shader.AddNode(bump_texture209);
			m_shader.AddNode(bump_texture_to_bw210);
			m_shader.AddNode(bump_amount211);
			m_shader.AddNode(final_base_color223);
			m_shader.AddNode(bump208);
			m_shader.AddNode(diffuse168);
			m_shader.AddNode(roughness_times_roughness217);
			m_shader.AddNode(principled_metal169);
			m_shader.AddNode(principled_paint172);
			m_shader.AddNode(invert_roughness180);
			m_shader.AddNode(transparency_factor_for_roughness181);
			m_shader.AddNode(light_path192);
			m_shader.AddNode(toggle_when_shadow182);
			m_shader.AddNode(layer_weight184);
			m_shader.AddNode(principled_glass_and_gem174);
			m_shader.AddNode(transparent177);
			m_shader.AddNode(transparency_layer_weight183);
			m_shader.AddNode(transparency_blend_for_shadow_with_gem_and_glass178);
			m_shader.AddNode(principled176);
			m_shader.AddNode(shadeless_bsdf212);
			m_shader.AddNode(invert_roughness186);
			m_shader.AddNode(multiply_transparency187);
			m_shader.AddNode(light_path179);
			m_shader.AddNode(multiply_with_shadowray188);
			m_shader.AddNode(layer_weight190);
			m_shader.AddNode(shadeless213);
			m_shader.AddNode(coloured_shadow_trans_color185);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow189);
			m_shader.AddNode(invert_alpha198);
			m_shader.AddNode(coloured_shadow_mix191);
			m_shader.AddNode(alphacutter_transparent195);
			m_shader.AddNode(toggle_image_alpha197);
			m_shader.AddNode(transparency_texture199);
			m_shader.AddNode(color___luminance200);
			m_shader.AddNode(invert_luminence202);
			m_shader.AddNode(transparency_texture_amount207);
			m_shader.AddNode(alpha_cutter_mix196);
			m_shader.AddNode(toggle_transparency_texture204);
			m_shader.AddNode(emission_value215);
			m_shader.AddNode(transparency_alpha_cutter201);
			m_shader.AddNode(emissive216);
			m_shader.AddNode(custom_emission214);


			texcoord193.outs.UV.Connect(diffuse_texture194.ins.Vector);
			diffuse_texture194.outs.Alpha.Connect(invert_alpha230.ins.Value2);
			invert_alpha230.outs.Value.Connect(honor_texture_repeat231.ins.Value1);
			diffuse_texture194.outs.Color.Connect(repeat_mixer229.ins.Color1);
			honor_texture_repeat231.outs.Value.Connect(repeat_mixer229.ins.Fac);
			subtract233.outs.Value.Connect(multiply232.ins.Value2);
			repeat_mixer229.outs.Color.Connect(diffuse_texture_amount203.ins.Color2);
			multiply232.outs.Value.Connect(diffuse_texture_amount203.ins.Fac);
			multiply232.outs.Value.Connect(invert_diffuse_color_amount206.ins.Value2);
			invert_diffuse_color_amount206.outs.Value.Connect(diffuse_col_amount193.ins.Fac);
			diffuse_texture_amount203.outs.Color.Connect(separate_diffuse_texture_color218.ins.Image);
			diffuse_col_amount193.outs.Color.Connect(separate_base_color219.ins.Image);
			separate_diffuse_texture_color218.outs.R.Connect(add_base_color_r220.ins.Value1);
			separate_base_color219.outs.R.Connect(add_base_color_r220.ins.Value2);
			separate_diffuse_texture_color218.outs.G.Connect(add_base_color_g221.ins.Value1);
			separate_base_color219.outs.G.Connect(add_base_color_g221.ins.Value2);
			separate_diffuse_texture_color218.outs.B.Connect(add_base_color_b222.ins.Value1);
			separate_base_color219.outs.B.Connect(add_base_color_b222.ins.Value2);
			texcoord193.outs.UV.Connect(bump_texture209.ins.Vector);
			bump_texture209.outs.Color.Connect(bump_texture_to_bw210.ins.Color);
			add_base_color_r220.outs.Value.Connect(final_base_color223.ins.R);
			add_base_color_g221.outs.Value.Connect(final_base_color223.ins.G);
			add_base_color_b222.outs.Value.Connect(final_base_color223.ins.B);
			bump_texture_to_bw210.outs.Val.Connect(bump208.ins.Height);
			bump_amount211.outs.Value.Connect(bump208.ins.Strength);
			final_base_color223.outs.Image.Connect(diffuse168.ins.Color);
			bump208.outs.Normal.Connect(diffuse168.ins.Normal);
			final_base_color223.outs.Image.Connect(principled_metal169.ins.BaseColor);
			roughness_times_roughness217.outs.Value.Connect(principled_metal169.ins.Roughness);
			bump208.outs.Normal.Connect(principled_metal169.ins.Normal);
			final_base_color223.outs.Image.Connect(principled_paint172.ins.BaseColor);
			roughness_times_roughness217.outs.Value.Connect(principled_paint172.ins.Roughness);
			bump208.outs.Normal.Connect(principled_paint172.ins.Normal);
			roughness_times_roughness217.outs.Value.Connect(invert_roughness180.ins.Value2);
			invert_roughness180.outs.Value.Connect(transparency_factor_for_roughness181.ins.Value1);
			transparency_factor_for_roughness181.outs.Value.Connect(toggle_when_shadow182.ins.Value1);
			light_path192.outs.IsShadowRay.Connect(toggle_when_shadow182.ins.Value2);
			final_base_color223.outs.Image.Connect(principled_glass_and_gem174.ins.BaseColor);
			roughness_times_roughness217.outs.Value.Connect(principled_glass_and_gem174.ins.Roughness);
			bump208.outs.Normal.Connect(principled_glass_and_gem174.ins.Normal);
			toggle_when_shadow182.outs.Value.Connect(transparency_layer_weight183.ins.Value1);
			layer_weight184.outs.Facing.Connect(transparency_layer_weight183.ins.Value2);
			principled_glass_and_gem174.outs.BSDF.Connect(transparency_blend_for_shadow_with_gem_and_glass178.ins.Closure1);
			transparent177.outs.BSDF.Connect(transparency_blend_for_shadow_with_gem_and_glass178.ins.Closure2);
			transparency_layer_weight183.outs.Value.Connect(transparency_blend_for_shadow_with_gem_and_glass178.ins.Fac);
			final_base_color223.outs.Image.Connect(principled176.ins.BaseColor);
			roughness_times_roughness217.outs.Value.Connect(principled176.ins.Roughness);
			bump208.outs.Normal.Connect(principled176.ins.Normal);
			final_base_color223.outs.Image.Connect(shadeless_bsdf212.ins.Color);
			roughness_times_roughness217.outs.Value.Connect(invert_roughness186.ins.Value2);
			invert_roughness186.outs.Value.Connect(multiply_transparency187.ins.Value1);
			multiply_transparency187.outs.Value.Connect(multiply_with_shadowray188.ins.Value1);
			light_path179.outs.IsShadowRay.Connect(multiply_with_shadowray188.ins.Value2);
			principled176.outs.BSDF.Connect(shadeless213.ins.Closure1);
			shadeless_bsdf212.outs.Emission.Connect(shadeless213.ins.Closure2);
			final_base_color223.outs.Image.Connect(coloured_shadow_trans_color185.ins.Color);
			multiply_with_shadowray188.outs.Value.Connect(weight_for_shadowray_coloured_shadow189.ins.Value1);
			layer_weight190.outs.Facing.Connect(weight_for_shadowray_coloured_shadow189.ins.Value2);
			diffuse_texture194.outs.Alpha.Connect(invert_alpha198.ins.Value2);
			shadeless213.outs.Closure.Connect(coloured_shadow_mix191.ins.Closure1);
			coloured_shadow_trans_color185.outs.BSDF.Connect(coloured_shadow_mix191.ins.Closure2);
			weight_for_shadowray_coloured_shadow189.outs.Value.Connect(coloured_shadow_mix191.ins.Fac);
			invert_alpha198.outs.Value.Connect(toggle_image_alpha197.ins.Value2);
			texcoord193.outs.UV.Connect(transparency_texture199.ins.Vector);
			transparency_texture199.outs.Color.Connect(color___luminance200.ins.Color);
			color___luminance200.outs.Val.Connect(invert_luminence202.ins.Value2);
			invert_luminence202.outs.Value.Connect(transparency_texture_amount207.ins.Value1);
			coloured_shadow_mix191.outs.Closure.Connect(alpha_cutter_mix196.ins.Closure1);
			alphacutter_transparent195.outs.BSDF.Connect(alpha_cutter_mix196.ins.Closure2);
			toggle_image_alpha197.outs.Value.Connect(alpha_cutter_mix196.ins.Fac);
			transparency_texture_amount207.outs.Value.Connect(toggle_transparency_texture204.ins.Value2);
			alpha_cutter_mix196.outs.Closure.Connect(transparency_alpha_cutter201.ins.Closure1);
			alphacutter_transparent195.outs.BSDF.Connect(transparency_alpha_cutter201.ins.Closure2);
			toggle_transparency_texture204.outs.Value.Connect(transparency_alpha_cutter201.ins.Fac);
			emission_value215.outs.Val.Connect(emissive216.ins.Strength);
			transparency_alpha_cutter201.outs.Closure.Connect(custom_emission214.ins.Closure1);
			emissive216.outs.Emission.Connect(custom_emission214.ins.Closure2);
			emission_value215.outs.Val.Connect(custom_emission214.ins.Fac);

			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture194, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture194, texcoord193);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture209, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture209, texcoord193);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture199, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture199, texcoord193);
			}

			switch (m_original.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.Diffuse:
					diffuse168.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.SimpleMetal:
					principled_metal169.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Glass:
					transparency_blend_for_shadow_with_gem_and_glass178.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Paint:
					principled_paint172.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				default:
					custom_emission214.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
			}

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
