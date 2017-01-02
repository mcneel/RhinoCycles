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

			var texcoord205 = new TextureCoordinateNode("texcoord");

			var diffuse_texture206 = new ImageTextureNode("diffuse_texture");
			diffuse_texture206.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture206.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture206.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture206.Extension = m_original.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
			diffuse_texture206.Interpolation = InterpolationType.Linear;
			diffuse_texture206.UseAlpha = true;
			diffuse_texture206.IsLinear = false;

			var invert_alpha1138 = new MathNode("invert_alpha");
			invert_alpha1138.ins.Value1.Value = 1f;
			invert_alpha1138.ins.Value2.Value = 0f;
			invert_alpha1138.Operation = MathNode.Operations.Subtract;
			invert_alpha1138.UseClamp = false;

			var honor_texture_repeat1283 = new MathNode("honor_texture_repeat");
			honor_texture_repeat1283.ins.Value1.Value = 1f;
			honor_texture_repeat1283.ins.Value2.Value = m_original.DiffuseTexture.Repeat ? 0.0f : 1.0f;
			honor_texture_repeat1283.Operation = MathNode.Operations.Multiply;
			honor_texture_repeat1283.UseClamp = false;

			var repeat_mixer294 = new MixNode("repeat_mixer");
			repeat_mixer294.ins.Color1.Value = m_original.BaseColor;
			repeat_mixer294.ins.Color2.Value = m_original.BaseColor;
			repeat_mixer294.ins.Fac.Value = 1f;
			repeat_mixer294.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			repeat_mixer294.UseClamp = false;

			var diffuse_texture_amount215 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount215.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount215.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount215.ins.Fac.Value = m_original.DiffuseTexture.Amount;
			diffuse_texture_amount215.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount215.UseClamp = false;

			var invert_diffuse_color_amount218 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount218.ins.Value1.Value = 1f;
			invert_diffuse_color_amount218.ins.Value2.Value = m_original.DiffuseTexture.Amount;
			invert_diffuse_color_amount218.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount218.UseClamp = false;

			var diffuse_col_amount217 = new MixNode("diffuse_col_amount");
			diffuse_col_amount217.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount217.ins.Color2.Value = m_original.BaseColor;
			diffuse_col_amount217.ins.Fac.Value = 0f;
			diffuse_col_amount217.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_col_amount217.UseClamp = false;

			var separate_diffuse_texture_color230 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color230.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color231 = new SeparateRgbNode("separate_base_color");
			separate_base_color231.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r232 = new MathNode("add_base_color_r");
			add_base_color_r232.ins.Value1.Value = 0f;
			add_base_color_r232.ins.Value2.Value = 0f;
			add_base_color_r232.Operation = MathNode.Operations.Add;
			add_base_color_r232.UseClamp = true;

			var add_base_color_g233 = new MathNode("add_base_color_g");
			add_base_color_g233.ins.Value1.Value = 0f;
			add_base_color_g233.ins.Value2.Value = 0f;
			add_base_color_g233.Operation = MathNode.Operations.Add;
			add_base_color_g233.UseClamp = true;

			var add_base_color_b234 = new MathNode("add_base_color_b");
			add_base_color_b234.ins.Value1.Value = 0f;
			add_base_color_b234.ins.Value2.Value = 0f;
			add_base_color_b234.Operation = MathNode.Operations.Add;
			add_base_color_b234.UseClamp = true;

			var bump_texture221 = new ImageTextureNode("bump_texture");
			bump_texture221.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture221.Projection = TextureNode.TextureProjection.Flat;
			bump_texture221.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture221.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture221.Interpolation = InterpolationType.Linear;
			bump_texture221.UseAlpha = true;
			bump_texture221.IsLinear = false;

			var bump_texture_to_bw222 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw222.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount223 = new MathNode("bump_amount");
			bump_amount223.ins.Value1.Value = 10f;
			bump_amount223.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount223.Operation = MathNode.Operations.Multiply;
			bump_amount223.UseClamp = false;

			var final_base_color235 = new CombineRgbNode("final_base_color");
			final_base_color235.ins.R.Value = 0f;
			final_base_color235.ins.G.Value = 0f;
			final_base_color235.ins.B.Value = 0f;

			var bump220 = new BumpNode("bump");
			bump220.ins.Height.Value = 0f;
			bump220.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump220.ins.Strength.Value = 0f;
			bump220.ins.Distance.Value = 0.1f;

			var diffuse180 = new DiffuseBsdfNode("diffuse");
			diffuse180.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse180.ins.Roughness.Value = 0f;
			diffuse180.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var roughness_times_roughness229 = new MathNode("roughness_times_roughness");
			roughness_times_roughness229.ins.Value1.Value = m_original.Roughness;
			roughness_times_roughness229.ins.Value2.Value = m_original.Roughness;
			roughness_times_roughness229.Operation = MathNode.Operations.Multiply;
			roughness_times_roughness229.UseClamp = false;

			var principled_metal181 = new UberBsdfNode("principled_metal");
			principled_metal181.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal181.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_metal181.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal181.ins.Metallic.Value = m_original.Reflectivity;
			principled_metal181.ins.Subsurface.Value = 0f;
			principled_metal181.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal181.ins.Specular.Value = m_original.Reflectivity;
			principled_metal181.ins.Roughness.Value = 0f;
			principled_metal181.ins.SpecularTint.Value = m_original.Reflectivity;
			principled_metal181.ins.Anisotropic.Value = 0f;
			principled_metal181.ins.Sheen.Value = 0f;
			principled_metal181.ins.SheenTint.Value = 0f;
			principled_metal181.ins.Clearcoat.Value = 0f;
			principled_metal181.ins.ClearcoatGloss.Value = 0f;
			principled_metal181.ins.IOR.Value = 0f;
			principled_metal181.ins.Transparency.Value = 0f;
			principled_metal181.ins.RefractionRoughness.Value = 0f;
			principled_metal181.ins.AnisotropicRotation.Value = 0f;
			principled_metal181.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal181.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal181.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_paint184 = new UberBsdfNode("principled_paint");
			principled_paint184.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint184.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_paint184.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint184.ins.Metallic.Value = 0f;
			principled_paint184.ins.Subsurface.Value = 0f;
			principled_paint184.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint184.ins.Specular.Value = m_original.Shine;
			principled_paint184.ins.Roughness.Value = 0f;
			principled_paint184.ins.SpecularTint.Value = 0f;
			principled_paint184.ins.Anisotropic.Value = 0f;
			principled_paint184.ins.Sheen.Value = m_original.Shine;
			principled_paint184.ins.SheenTint.Value = 0f;
			principled_paint184.ins.Clearcoat.Value = 0f;
			principled_paint184.ins.ClearcoatGloss.Value = 0f;
			principled_paint184.ins.IOR.Value = 0f;
			principled_paint184.ins.Transparency.Value = 0f;
			principled_paint184.ins.RefractionRoughness.Value = 0f;
			principled_paint184.ins.AnisotropicRotation.Value = 0f;
			principled_paint184.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint184.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint184.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness192 = new MathNode("invert_roughness");
			invert_roughness192.ins.Value1.Value = 1f;
			invert_roughness192.ins.Value2.Value = 0f;
			invert_roughness192.Operation = MathNode.Operations.Subtract;
			invert_roughness192.UseClamp = false;

			var transparency_factor_for_roughness193 = new MathNode("transparency_factor_for_roughness");
			transparency_factor_for_roughness193.ins.Value1.Value = 1f;
			transparency_factor_for_roughness193.ins.Value2.Value = m_original.Transparency;
			transparency_factor_for_roughness193.Operation = MathNode.Operations.Multiply;
			transparency_factor_for_roughness193.UseClamp = false;

			var light_path204 = new LightPathNode("light_path");

			var toggle_when_shadow194 = new MathNode("toggle_when_shadow");
			toggle_when_shadow194.ins.Value1.Value = 1f;
			toggle_when_shadow194.ins.Value2.Value = 0f;
			toggle_when_shadow194.Operation = MathNode.Operations.Multiply;
			toggle_when_shadow194.UseClamp = false;

			var layer_weight196 = new LayerWeightNode("layer_weight");
			layer_weight196.ins.Blend.Value = 0.87f;
			layer_weight196.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_glass_and_gem186 = new UberBsdfNode("principled_glass_and_gem");
			principled_glass_and_gem186.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem186.ins.SpecularColor.Value = m_original.SpecularColor;
			principled_glass_and_gem186.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem186.ins.Metallic.Value = 0f;
			principled_glass_and_gem186.ins.Subsurface.Value = 0f;
			principled_glass_and_gem186.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem186.ins.Specular.Value = 0f;
			principled_glass_and_gem186.ins.Roughness.Value = 0f;
			principled_glass_and_gem186.ins.SpecularTint.Value = 0f;
			principled_glass_and_gem186.ins.Anisotropic.Value = 0f;
			principled_glass_and_gem186.ins.Sheen.Value = 0f;
			principled_glass_and_gem186.ins.SheenTint.Value = 0f;
			principled_glass_and_gem186.ins.Clearcoat.Value = 0f;
			principled_glass_and_gem186.ins.ClearcoatGloss.Value = 0f;
			principled_glass_and_gem186.ins.IOR.Value = m_original.IOR;
			principled_glass_and_gem186.ins.Transparency.Value = m_original.Transparency;
			principled_glass_and_gem186.ins.RefractionRoughness.Value = 0f;
			principled_glass_and_gem186.ins.AnisotropicRotation.Value = 0f;
			principled_glass_and_gem186.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem186.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem186.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent189 = new TransparentBsdfNode("transparent");
			transparent189.ins.Color.Value = m_original.TransparencyColor;

			var transparency_layer_weight195 = new MathNode("transparency_layer_weight");
			transparency_layer_weight195.ins.Value1.Value = 0f;
			transparency_layer_weight195.ins.Value2.Value = 0f;
			transparency_layer_weight195.Operation = MathNode.Operations.Multiply;
			transparency_layer_weight195.UseClamp = false;

			var transparency_blend_for_shadow_with_gem_and_glass190 = new MixClosureNode("transparency_blend_for_shadow_with_gem_and_glass");
			transparency_blend_for_shadow_with_gem_and_glass190.ins.Fac.Value = 0f;

			var principled188 = new UberBsdfNode("principled");
			principled188.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled188.ins.SpecularColor.Value = m_original.SpecularColor;
			principled188.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled188.ins.Metallic.Value = m_original.Metalic;
			principled188.ins.Subsurface.Value = 0f;
			principled188.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled188.ins.Specular.Value = m_original.Shine;
			principled188.ins.Roughness.Value = 0f;
			principled188.ins.SpecularTint.Value = m_original.Gloss;
			principled188.ins.Anisotropic.Value = 0f;
			principled188.ins.Sheen.Value = 0f;
			principled188.ins.SheenTint.Value = m_original.Gloss;
			principled188.ins.Clearcoat.Value = 0f;
			principled188.ins.ClearcoatGloss.Value = m_original.Gloss;
			principled188.ins.IOR.Value = m_original.IOR;
			principled188.ins.Transparency.Value = m_original.Transparency;
			principled188.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled188.ins.AnisotropicRotation.Value = 0f;
			principled188.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled188.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled188.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf224 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf224.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf224.ins.Strength.Value = 1f;

			var invert_roughness198 = new MathNode("invert_roughness");
			invert_roughness198.ins.Value1.Value = 1f;
			invert_roughness198.ins.Value2.Value = 0f;
			invert_roughness198.Operation = MathNode.Operations.Subtract;
			invert_roughness198.UseClamp = false;

			var multiply_transparency199 = new MathNode("multiply_transparency");
			multiply_transparency199.ins.Value1.Value = 1f;
			multiply_transparency199.ins.Value2.Value = m_original.Transparency;
			multiply_transparency199.Operation = MathNode.Operations.Multiply;
			multiply_transparency199.UseClamp = false;

			var light_path191 = new LightPathNode("light_path");

			var multiply_with_shadowray200 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray200.ins.Value1.Value = 0f;
			multiply_with_shadowray200.ins.Value2.Value = 0f;
			multiply_with_shadowray200.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray200.UseClamp = false;

			var layer_weight202 = new LayerWeightNode("layer_weight");
			layer_weight202.ins.Blend.Value = 0.89f;
			layer_weight202.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless225 = new MixClosureNode("shadeless");
			shadeless225.ins.Fac.Value = m_original.ShadelessAsFloat;

			var coloured_shadow_trans_color197 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color197.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow201 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow201.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow201.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow201.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow201.UseClamp = false;

			var invert_alpha210 = new MathNode("invert_alpha");
			invert_alpha210.ins.Value1.Value = 1f;
			invert_alpha210.ins.Value2.Value = 0f;
			invert_alpha210.Operation = MathNode.Operations.Subtract;
			invert_alpha210.UseClamp = false;

			var coloured_shadow_mix203 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix203.ins.Fac.Value = 0f;

			var alphacutter_transparent207 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent207.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var toggle_image_alpha209 = new MathNode("toggle_image_alpha");
			toggle_image_alpha209.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha209.ins.Value2.Value = 1f;
			toggle_image_alpha209.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha209.UseClamp = false;

			var transparency_texture211 = new ImageTextureNode("transparency_texture");
			transparency_texture211.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture211.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture211.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture211.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture211.Interpolation = InterpolationType.Linear;
			transparency_texture211.UseAlpha = true;
			transparency_texture211.IsLinear = false;

			var color___luminance212 = new RgbToLuminanceNode("color___luminance");
			color___luminance212.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence214 = new MathNode("invert_luminence");
			invert_luminence214.ins.Value1.Value = 1f;
			invert_luminence214.ins.Value2.Value = 0f;
			invert_luminence214.Operation = MathNode.Operations.Subtract;
			invert_luminence214.UseClamp = false;

			var transparency_texture_amount219 = new MathNode("transparency_texture_amount");
			transparency_texture_amount219.ins.Value1.Value = 1f;
			transparency_texture_amount219.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount219.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount219.UseClamp = false;

			var alpha_cutter_mix208 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix208.ins.Fac.Value = 0f;

			var toggle_transparency_texture216 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture216.ins.Value1.Value = m_original.HasTransparencyTextureAsFloat;
			toggle_transparency_texture216.ins.Value2.Value = 0f;
			toggle_transparency_texture216.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture216.UseClamp = false;

			var emission_value227 = new RgbToBwNode("emission_value");
			emission_value227.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;

			var transparency_alpha_cutter213 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter213.ins.Fac.Value = 0f;

			var emissive228 = new EmissionNode("emissive");
			emissive228.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			emissive228.ins.Strength.Value = 0f;

			var custom_emission226 = new MixClosureNode("custom_emission");
			custom_emission226.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord205);
			m_shader.AddNode(diffuse_texture206);
			m_shader.AddNode(invert_alpha1138);
			m_shader.AddNode(honor_texture_repeat1283);
			m_shader.AddNode(repeat_mixer294);
			m_shader.AddNode(diffuse_texture_amount215);
			m_shader.AddNode(invert_diffuse_color_amount218);
			m_shader.AddNode(diffuse_col_amount217);
			m_shader.AddNode(separate_diffuse_texture_color230);
			m_shader.AddNode(separate_base_color231);
			m_shader.AddNode(add_base_color_r232);
			m_shader.AddNode(add_base_color_g233);
			m_shader.AddNode(add_base_color_b234);
			m_shader.AddNode(bump_texture221);
			m_shader.AddNode(bump_texture_to_bw222);
			m_shader.AddNode(bump_amount223);
			m_shader.AddNode(final_base_color235);
			m_shader.AddNode(bump220);
			m_shader.AddNode(diffuse180);
			m_shader.AddNode(roughness_times_roughness229);
			m_shader.AddNode(principled_metal181);
			m_shader.AddNode(principled_paint184);
			m_shader.AddNode(invert_roughness192);
			m_shader.AddNode(transparency_factor_for_roughness193);
			m_shader.AddNode(light_path204);
			m_shader.AddNode(toggle_when_shadow194);
			m_shader.AddNode(layer_weight196);
			m_shader.AddNode(principled_glass_and_gem186);
			m_shader.AddNode(transparent189);
			m_shader.AddNode(transparency_layer_weight195);
			m_shader.AddNode(transparency_blend_for_shadow_with_gem_and_glass190);
			m_shader.AddNode(principled188);
			m_shader.AddNode(shadeless_bsdf224);
			m_shader.AddNode(invert_roughness198);
			m_shader.AddNode(multiply_transparency199);
			m_shader.AddNode(light_path191);
			m_shader.AddNode(multiply_with_shadowray200);
			m_shader.AddNode(layer_weight202);
			m_shader.AddNode(shadeless225);
			m_shader.AddNode(coloured_shadow_trans_color197);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow201);
			m_shader.AddNode(invert_alpha210);
			m_shader.AddNode(coloured_shadow_mix203);
			m_shader.AddNode(alphacutter_transparent207);
			m_shader.AddNode(toggle_image_alpha209);
			m_shader.AddNode(transparency_texture211);
			m_shader.AddNode(color___luminance212);
			m_shader.AddNode(invert_luminence214);
			m_shader.AddNode(transparency_texture_amount219);
			m_shader.AddNode(alpha_cutter_mix208);
			m_shader.AddNode(toggle_transparency_texture216);
			m_shader.AddNode(emission_value227);
			m_shader.AddNode(transparency_alpha_cutter213);
			m_shader.AddNode(emissive228);
			m_shader.AddNode(custom_emission226);


			texcoord205.outs.UV.Connect(diffuse_texture206.ins.Vector);
			diffuse_texture206.outs.Alpha.Connect(invert_alpha1138.ins.Value2);
			invert_alpha1138.outs.Value.Connect(honor_texture_repeat1283.ins.Value1);
			diffuse_texture206.outs.Color.Connect(repeat_mixer294.ins.Color1);
			honor_texture_repeat1283.outs.Value.Connect(repeat_mixer294.ins.Fac);
			repeat_mixer294.outs.Color.Connect(diffuse_texture_amount215.ins.Color2);
			invert_diffuse_color_amount218.outs.Value.Connect(diffuse_col_amount217.ins.Fac);
			diffuse_texture_amount215.outs.Color.Connect(separate_diffuse_texture_color230.ins.Image);
			diffuse_col_amount217.outs.Color.Connect(separate_base_color231.ins.Image);
			separate_diffuse_texture_color230.outs.R.Connect(add_base_color_r232.ins.Value1);
			separate_base_color231.outs.R.Connect(add_base_color_r232.ins.Value2);
			separate_diffuse_texture_color230.outs.G.Connect(add_base_color_g233.ins.Value1);
			separate_base_color231.outs.G.Connect(add_base_color_g233.ins.Value2);
			separate_diffuse_texture_color230.outs.B.Connect(add_base_color_b234.ins.Value1);
			separate_base_color231.outs.B.Connect(add_base_color_b234.ins.Value2);
			texcoord205.outs.UV.Connect(bump_texture221.ins.Vector);
			bump_texture221.outs.Color.Connect(bump_texture_to_bw222.ins.Color);
			add_base_color_r232.outs.Value.Connect(final_base_color235.ins.R);
			add_base_color_g233.outs.Value.Connect(final_base_color235.ins.G);
			add_base_color_b234.outs.Value.Connect(final_base_color235.ins.B);
			bump_texture_to_bw222.outs.Val.Connect(bump220.ins.Height);
			bump_amount223.outs.Value.Connect(bump220.ins.Strength);
			final_base_color235.outs.Image.Connect(diffuse180.ins.Color);
			bump220.outs.Normal.Connect(diffuse180.ins.Normal);
			final_base_color235.outs.Image.Connect(principled_metal181.ins.BaseColor);
			roughness_times_roughness229.outs.Value.Connect(principled_metal181.ins.Roughness);
			bump220.outs.Normal.Connect(principled_metal181.ins.Normal);
			final_base_color235.outs.Image.Connect(principled_paint184.ins.BaseColor);
			roughness_times_roughness229.outs.Value.Connect(principled_paint184.ins.Roughness);
			bump220.outs.Normal.Connect(principled_paint184.ins.Normal);
			roughness_times_roughness229.outs.Value.Connect(invert_roughness192.ins.Value2);
			invert_roughness192.outs.Value.Connect(transparency_factor_for_roughness193.ins.Value1);
			transparency_factor_for_roughness193.outs.Value.Connect(toggle_when_shadow194.ins.Value1);
			light_path204.outs.IsShadowRay.Connect(toggle_when_shadow194.ins.Value2);
			final_base_color235.outs.Image.Connect(principled_glass_and_gem186.ins.BaseColor);
			roughness_times_roughness229.outs.Value.Connect(principled_glass_and_gem186.ins.Roughness);
			bump220.outs.Normal.Connect(principled_glass_and_gem186.ins.Normal);
			toggle_when_shadow194.outs.Value.Connect(transparency_layer_weight195.ins.Value1);
			layer_weight196.outs.Facing.Connect(transparency_layer_weight195.ins.Value2);
			principled_glass_and_gem186.outs.BSDF.Connect(transparency_blend_for_shadow_with_gem_and_glass190.ins.Closure1);
			transparent189.outs.BSDF.Connect(transparency_blend_for_shadow_with_gem_and_glass190.ins.Closure2);
			transparency_layer_weight195.outs.Value.Connect(transparency_blend_for_shadow_with_gem_and_glass190.ins.Fac);
			final_base_color235.outs.Image.Connect(principled188.ins.BaseColor);
			roughness_times_roughness229.outs.Value.Connect(principled188.ins.Roughness);
			bump220.outs.Normal.Connect(principled188.ins.Normal);
			final_base_color235.outs.Image.Connect(shadeless_bsdf224.ins.Color);
			roughness_times_roughness229.outs.Value.Connect(invert_roughness198.ins.Value2);
			invert_roughness198.outs.Value.Connect(multiply_transparency199.ins.Value1);
			multiply_transparency199.outs.Value.Connect(multiply_with_shadowray200.ins.Value1);
			light_path191.outs.IsShadowRay.Connect(multiply_with_shadowray200.ins.Value2);
			principled188.outs.BSDF.Connect(shadeless225.ins.Closure1);
			shadeless_bsdf224.outs.Emission.Connect(shadeless225.ins.Closure2);
			final_base_color235.outs.Image.Connect(coloured_shadow_trans_color197.ins.Color);
			multiply_with_shadowray200.outs.Value.Connect(weight_for_shadowray_coloured_shadow201.ins.Value1);
			layer_weight202.outs.Facing.Connect(weight_for_shadowray_coloured_shadow201.ins.Value2);
			diffuse_texture206.outs.Alpha.Connect(invert_alpha210.ins.Value2);
			shadeless225.outs.Closure.Connect(coloured_shadow_mix203.ins.Closure1);
			coloured_shadow_trans_color197.outs.BSDF.Connect(coloured_shadow_mix203.ins.Closure2);
			weight_for_shadowray_coloured_shadow201.outs.Value.Connect(coloured_shadow_mix203.ins.Fac);
			invert_alpha210.outs.Value.Connect(toggle_image_alpha209.ins.Value2);
			texcoord205.outs.UV.Connect(transparency_texture211.ins.Vector);
			transparency_texture211.outs.Color.Connect(color___luminance212.ins.Color);
			color___luminance212.outs.Val.Connect(invert_luminence214.ins.Value2);
			invert_luminence214.outs.Value.Connect(transparency_texture_amount219.ins.Value1);
			coloured_shadow_mix203.outs.Closure.Connect(alpha_cutter_mix208.ins.Closure1);
			alphacutter_transparent207.outs.BSDF.Connect(alpha_cutter_mix208.ins.Closure2);
			toggle_image_alpha209.outs.Value.Connect(alpha_cutter_mix208.ins.Fac);
			transparency_texture_amount219.outs.Value.Connect(toggle_transparency_texture216.ins.Value2);
			alpha_cutter_mix208.outs.Closure.Connect(transparency_alpha_cutter213.ins.Closure1);
			alphacutter_transparent207.outs.BSDF.Connect(transparency_alpha_cutter213.ins.Closure2);
			toggle_transparency_texture216.outs.Value.Connect(transparency_alpha_cutter213.ins.Fac);
			emission_value227.outs.Val.Connect(emissive228.ins.Strength);
			transparency_alpha_cutter213.outs.Closure.Connect(custom_emission226.ins.Closure1);
			emissive228.outs.Emission.Connect(custom_emission226.ins.Closure2);
			emission_value227.outs.Val.Connect(custom_emission226.ins.Fac);


			diffuse180.outs.BSDF.Connect(m_shader.Output.ins.Surface);
			principled_metal181.outs.BSDF.Connect(m_shader.Output.ins.Surface);
			principled_paint184.outs.BSDF.Connect(m_shader.Output.ins.Surface);
			transparency_blend_for_shadow_with_gem_and_glass190.outs.Closure.Connect(m_shader.Output.ins.Surface);
			custom_emission226.outs.Closure.Connect(m_shader.Output.ins.Surface);


			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture206, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture206, texcoord205);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture221, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture221, texcoord205);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture211, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture211, texcoord205);
			}

			switch (m_original.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.Diffuse:
					diffuse180.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.SimpleMetal:
					principled_metal181.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Glass:
					transparency_blend_for_shadow_with_gem_and_glass190.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Paint:
					principled_paint184.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				default:
					custom_emission226.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
			}

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
