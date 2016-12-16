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
			var texcoord79 = new TextureCoordinateNode("texcoord");

			var diffuse_texture80 = new ImageTextureNode("diffuse_texture");
			diffuse_texture80.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture80.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture80.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture80.Extension = TextureNode.TextureExtension.Repeat;
			diffuse_texture80.Interpolation = InterpolationType.Linear;
			diffuse_texture80.UseAlpha = true;
			diffuse_texture80.IsLinear = false;

			var invert_diffuse_color_amount92 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount92.ins.Value1.Value = 1f;
			invert_diffuse_color_amount92.ins.Value2.Value = m_original.DiffuseTexture.Amount;
			invert_diffuse_color_amount92.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount92.UseClamp = false;

			var diffuse_texture_amount89 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount89.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount89.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount89.ins.Fac.Value = m_original.DiffuseTexture.Amount;

			var diffuse_col_amount91 = new MixNode("diffuse_col_amount");
			diffuse_col_amount91.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount91.ins.Color2.Value = m_original.DiffuseColor ^ m_original.Gamma;
			diffuse_col_amount91.ins.Fac.Value = 0f;

			var bump_texture96 = new ImageTextureNode("bump_texture");
			bump_texture96.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture96.Projection = TextureNode.TextureProjection.Flat;
			bump_texture96.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture96.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture96.Interpolation = InterpolationType.Linear;
			bump_texture96.UseAlpha = true;
			bump_texture96.IsLinear = false;

			var bump_texture_to_bw97 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw97.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount98 = new MathNode("bump_amount");
			bump_amount98.ins.Value1.Value = 10f;
			bump_amount98.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount98.Operation = MathNode.Operations.Multiply;
			bump_amount98.UseClamp = false;

			var combine_diffuse_color_and_texture93 = new MixNode("combine_diffuse_color_and_texture");
			combine_diffuse_color_and_texture93.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			combine_diffuse_color_and_texture93.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			combine_diffuse_color_and_texture93.ins.Fac.Value = 0.5f;

			var bump95 = new BumpNode("bump");
			bump95.ins.Height.Value = 0f;
			bump95.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump95.ins.Strength.Value = 6.4f;
			bump95.ins.Distance.Value = 0.1f;

			var diffuse54 = new DiffuseBsdfNode("diffuse");
			diffuse54.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse54.ins.Roughness.Value = 0f;
			diffuse54.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var roughness_times_roughness111 = new MathNode("roughness_times_roughness");
			roughness_times_roughness111.ins.Value1.Value = m_original.Roughness;
			roughness_times_roughness111.ins.Value2.Value = m_original.Roughness;
			roughness_times_roughness111.Operation = MathNode.Operations.Multiply;
			roughness_times_roughness111.UseClamp = false;

			var principled_metal55 = new UberBsdfNode("principled_metal");
			principled_metal55.ins.BaseColor.Value = m_original.ReflectionColor;
			principled_metal55.ins.SpecularColor.Value = m_original.ReflectionColor;
			principled_metal55.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_metal55.ins.Metallic.Value = m_original.Reflectivity;
			principled_metal55.ins.Subsurface.Value = 0f;
			principled_metal55.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal55.ins.Specular.Value = m_original.Reflectivity;
			principled_metal55.ins.Roughness.Value = 0f;
			principled_metal55.ins.SpecularTint.Value = m_original.Reflectivity;
			principled_metal55.ins.Anisotropic.Value = 0f;
			principled_metal55.ins.Sheen.Value = 0f;
			principled_metal55.ins.SheenTint.Value = 0f;
			principled_metal55.ins.Clearcoat.Value = 0f;
			principled_metal55.ins.ClearcoatGloss.Value = 0f;
			principled_metal55.ins.IOR.Value = 0f;
			principled_metal55.ins.Transparency.Value = 0f;
			principled_metal55.ins.RefractionRoughness.Value = 0f;
			principled_metal55.ins.AnisotropicRotation.Value = 0f;
			principled_metal55.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal55.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_metal55.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_paint58 = new UberBsdfNode("principled_paint");
			principled_paint58.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint58.ins.SpecularColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint58.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_paint58.ins.Metallic.Value = 0f;
			principled_paint58.ins.Subsurface.Value = 0f;
			principled_paint58.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint58.ins.Specular.Value = m_original.Gloss;
			principled_paint58.ins.Roughness.Value = 0f;
			/*principled_paint58.ins.SpecularTint.Value = m_original.SpecularTint;
			principled_paint58.ins.Anisotropic.Value = 0f;
			principled_paint58.ins.Sheen.Value = m_original.SpecularTint;
			principled_paint58.ins.SheenTint.Value = m_original.SpecularTint;
			principled_paint58.ins.Clearcoat.Value = m_original.SpecularTint;
			principled_paint58.ins.ClearcoatGloss.Value = m_original.SpecularTint;*/
			principled_paint58.ins.IOR.Value = 0f;
			principled_paint58.ins.Transparency.Value = 0f;
			principled_paint58.ins.RefractionRoughness.Value = 0f;
			principled_paint58.ins.AnisotropicRotation.Value = 0f;
			principled_paint58.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint58.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_paint58.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness66 = new MathNode("invert_roughness");
			invert_roughness66.ins.Value1.Value = 1f;
			invert_roughness66.ins.Value2.Value = 0f;
			invert_roughness66.Operation = MathNode.Operations.Subtract;
			invert_roughness66.UseClamp = false;

			var transparency_factor_for_roughness67 = new MathNode("transparency_factor_for_roughness");
			transparency_factor_for_roughness67.ins.Value1.Value = 1f;
			transparency_factor_for_roughness67.ins.Value2.Value = m_original.Transparency;
			transparency_factor_for_roughness67.Operation = MathNode.Operations.Multiply;
			transparency_factor_for_roughness67.UseClamp = false;

			var light_path78 = new LightPathNode("light_path");

			var toggle_when_shadow68 = new MathNode("toggle_when_shadow");
			toggle_when_shadow68.ins.Value1.Value = 0f;
			toggle_when_shadow68.ins.Value2.Value = 0f;
			toggle_when_shadow68.Operation = MathNode.Operations.Multiply;
			toggle_when_shadow68.UseClamp = false;

			var layer_weight70 = new LayerWeightNode("layer_weight");
			layer_weight70.ins.Blend.Value = 0.89f;
			layer_weight70.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var principled_glass_and_gem60 = new UberBsdfNode("principled_glass_and_gem");
			principled_glass_and_gem60.ins.BaseColor.Value = m_original.TransparencyColor;
			principled_glass_and_gem60.ins.SpecularColor.Value = m_original.TransparencyColor;
			principled_glass_and_gem60.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled_glass_and_gem60.ins.Metallic.Value = 0f;
			principled_glass_and_gem60.ins.Subsurface.Value = 0f;
			principled_glass_and_gem60.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem60.ins.Specular.Value = m_original.Shine;
			principled_glass_and_gem60.ins.Roughness.Value = 0f;
			principled_glass_and_gem60.ins.SpecularTint.Value = m_original.Shine;
			principled_glass_and_gem60.ins.Anisotropic.Value = 0f;
			principled_glass_and_gem60.ins.Sheen.Value = m_original.Shine;
			principled_glass_and_gem60.ins.SheenTint.Value = m_original.Shine;
			principled_glass_and_gem60.ins.Clearcoat.Value = m_original.Shine;
			principled_glass_and_gem60.ins.ClearcoatGloss.Value = m_original.Shine;
			principled_glass_and_gem60.ins.IOR.Value = m_original.IOR;
			principled_glass_and_gem60.ins.Transparency.Value = m_original.Transparency;
			principled_glass_and_gem60.ins.RefractionRoughness.Value = 0f;
			principled_glass_and_gem60.ins.AnisotropicRotation.Value = 0f;
			principled_glass_and_gem60.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem60.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled_glass_and_gem60.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var transparent63 = new TransparentBsdfNode("transparent");
			transparent63.ins.Color.Value = m_original.TransparencyColor;

			var transparency_layer_weight69 = new MathNode("transparency_layer_weight");
			transparency_layer_weight69.ins.Value1.Value = 0f;
			transparency_layer_weight69.ins.Value2.Value = 0f;
			transparency_layer_weight69.Operation = MathNode.Operations.Multiply;
			transparency_layer_weight69.UseClamp = false;

			var transparency_blend_for_shadow64 = new MixClosureNode("transparency_blend_for_shadow");
			transparency_blend_for_shadow64.ins.Fac.Value = 0f;

			var glossy105 = new GlossyBsdfNode("glossy");
			glossy105.ins.Color.Value = m_original.ReflectionColor;
			glossy105.ins.Roughness.Value = 0f;
			glossy105.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var glass110 = new GlassBsdfNode("glass");
			glass110.ins.Color.Value = m_original.ReflectionColor;
			glass110.ins.Roughness.Value = 0f;
			glass110.ins.IOR.Value = m_original.IOR;
			glass110.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var multiply108 = new MathNode("multiply");
			multiply108.ins.Value1.Value = m_original.Reflectivity;
			multiply108.ins.Value2.Value = m_original.FresnelReflectionsAsFloat;
			multiply108.Operation = MathNode.Operations.Multiply;
			multiply108.UseClamp = false;

			var reflectivity_fresnel_weight107 = new MathNode("reflectivity_fresnel_weight");
			reflectivity_fresnel_weight107.ins.Value1.Value = 0.35f;
			reflectivity_fresnel_weight107.ins.Value2.Value = 0.5f;
			reflectivity_fresnel_weight107.Operation = MathNode.Operations.Multiply;
			reflectivity_fresnel_weight107.UseClamp = false;

			var principled62 = new UberBsdfNode("principled");
			principled62.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled62.ins.SpecularColor.Value = m_original.SpecularColor;
			principled62.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled62.ins.Metallic.Value = m_original.Metalic;
			principled62.ins.Subsurface.Value = 0f;
			principled62.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled62.ins.Specular.Value = m_original.Shine;
			principled62.ins.Roughness.Value = 0f;
			principled62.ins.SpecularTint.Value = m_original.Gloss;
			principled62.ins.Anisotropic.Value = 0f;
			principled62.ins.Sheen.Value = 0f;
			principled62.ins.SheenTint.Value = m_original.Gloss;
			principled62.ins.Clearcoat.Value = 0f;
			principled62.ins.ClearcoatGloss.Value = m_original.Gloss;
			principled62.ins.IOR.Value = m_original.IOR;
			principled62.ins.Transparency.Value = m_original.Transparency;
			principled62.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled62.ins.AnisotropicRotation.Value = 0f;
			principled62.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled62.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled62.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var blend109 = new MixClosureNode("blend");
			blend109.ins.Fac.Value = m_original.Transparency;

			var layerweight_for_reflectivity_blend106 = new LayerWeightNode("layerweight_for_reflectivity_blend");
			layerweight_for_reflectivity_blend106.ins.Blend.Value = 0.175f;
			layerweight_for_reflectivity_blend106.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var reflectivity_blend104 = new MixClosureNode("reflectivity_blend");
			reflectivity_blend104.ins.Fac.Value = 0f;

			var shadeless_bsdf99 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf99.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf99.ins.Strength.Value = 1f;

			var invert_roughness72 = new MathNode("invert_roughness");
			invert_roughness72.ins.Value1.Value = 1f;
			invert_roughness72.ins.Value2.Value = 0f;
			invert_roughness72.Operation = MathNode.Operations.Subtract;
			invert_roughness72.UseClamp = false;

			var multiply_transparency73 = new MathNode("multiply_transparency");
			multiply_transparency73.ins.Value1.Value = 1f;
			multiply_transparency73.ins.Value2.Value = m_original.Transparency;
			multiply_transparency73.Operation = MathNode.Operations.Multiply;
			multiply_transparency73.UseClamp = false;

			var light_path65 = new LightPathNode("light_path");

			var multiply_with_shadowray74 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray74.ins.Value1.Value = 0f;
			multiply_with_shadowray74.ins.Value2.Value = 0f;
			multiply_with_shadowray74.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray74.UseClamp = false;

			var layer_weight76 = new LayerWeightNode("layer_weight");
			layer_weight76.ins.Blend.Value = 0.89f;
			layer_weight76.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless100 = new MixClosureNode("shadeless");
			shadeless100.ins.Fac.Value = m_original.ShadelessAsFloat;

			var coloured_shadow_trans_color71 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color71.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow75 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow75.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow75.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow75.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow75.UseClamp = false;

			var invert_alpha84 = new MathNode("invert_alpha");
			invert_alpha84.ins.Value1.Value = 1f;
			invert_alpha84.ins.Value2.Value = 0f;
			invert_alpha84.Operation = MathNode.Operations.Subtract;
			invert_alpha84.UseClamp = false;

			var coloured_shadow_mix77 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix77.ins.Fac.Value = 0f;

			var alphacutter_transparent81 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent81.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var toggle_image_alpha83 = new MathNode("toggle_image_alpha");
			toggle_image_alpha83.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha83.ins.Value2.Value = 1f;
			toggle_image_alpha83.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha83.UseClamp = false;

			var transparency_texture85 = new ImageTextureNode("transparency_texture");
			transparency_texture85.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture85.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture85.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture85.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture85.Interpolation = InterpolationType.Linear;
			transparency_texture85.UseAlpha = true;
			transparency_texture85.IsLinear = false;

			var color___luminance86 = new RgbToLuminanceNode("color___luminance");
			color___luminance86.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence88 = new MathNode("invert_luminence");
			invert_luminence88.ins.Value1.Value = 1f;
			invert_luminence88.ins.Value2.Value = 0f;
			invert_luminence88.Operation = MathNode.Operations.Subtract;
			invert_luminence88.UseClamp = false;

			var transparency_texture_amount94 = new MathNode("transparency_texture_amount");
			transparency_texture_amount94.ins.Value1.Value = 1f;
			transparency_texture_amount94.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount94.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount94.UseClamp = false;

			var alpha_cutter_mix82 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix82.ins.Fac.Value = 0f;

			var toggle_transparency_texture90 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture90.ins.Value1.Value = m_original.HasTransparencyTextureAsFloat;
			toggle_transparency_texture90.ins.Value2.Value = 0f;
			toggle_transparency_texture90.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture90.UseClamp = false;

			var emission_value102 = new RgbToBwNode("emission_value");
			emission_value102.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;

			var transparency_alpha_cutter87 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter87.ins.Fac.Value = 0f;

			var emissive103 = new EmissionNode("emissive");
			emissive103.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			emissive103.ins.Strength.Value = 0f;

			var custom_emission101 = new MixClosureNode("custom_emission");
			custom_emission101.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord79);
			m_shader.AddNode(diffuse_texture80);
			m_shader.AddNode(invert_diffuse_color_amount92);
			m_shader.AddNode(diffuse_texture_amount89);
			m_shader.AddNode(diffuse_col_amount91);
			m_shader.AddNode(bump_texture96);
			m_shader.AddNode(bump_texture_to_bw97);
			m_shader.AddNode(bump_amount98);
			m_shader.AddNode(combine_diffuse_color_and_texture93);
			m_shader.AddNode(bump95);
			m_shader.AddNode(diffuse54);
			m_shader.AddNode(roughness_times_roughness111);
			m_shader.AddNode(principled_metal55);
			m_shader.AddNode(principled_paint58);
			m_shader.AddNode(invert_roughness66);
			m_shader.AddNode(transparency_factor_for_roughness67);
			m_shader.AddNode(light_path78);
			m_shader.AddNode(toggle_when_shadow68);
			m_shader.AddNode(layer_weight70);
			m_shader.AddNode(principled_glass_and_gem60);
			m_shader.AddNode(transparent63);
			m_shader.AddNode(transparency_layer_weight69);
			m_shader.AddNode(transparency_blend_for_shadow64);
			m_shader.AddNode(glossy105);
			m_shader.AddNode(glass110);
			m_shader.AddNode(multiply108);
			m_shader.AddNode(reflectivity_fresnel_weight107);
			m_shader.AddNode(principled62);
			m_shader.AddNode(blend109);
			m_shader.AddNode(layerweight_for_reflectivity_blend106);
			m_shader.AddNode(reflectivity_blend104);
			m_shader.AddNode(shadeless_bsdf99);
			m_shader.AddNode(invert_roughness72);
			m_shader.AddNode(multiply_transparency73);
			m_shader.AddNode(light_path65);
			m_shader.AddNode(multiply_with_shadowray74);
			m_shader.AddNode(layer_weight76);
			m_shader.AddNode(shadeless100);
			m_shader.AddNode(coloured_shadow_trans_color71);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow75);
			m_shader.AddNode(invert_alpha84);
			m_shader.AddNode(coloured_shadow_mix77);
			m_shader.AddNode(alphacutter_transparent81);
			m_shader.AddNode(toggle_image_alpha83);
			m_shader.AddNode(transparency_texture85);
			m_shader.AddNode(color___luminance86);
			m_shader.AddNode(invert_luminence88);
			m_shader.AddNode(transparency_texture_amount94);
			m_shader.AddNode(alpha_cutter_mix82);
			m_shader.AddNode(toggle_transparency_texture90);
			m_shader.AddNode(emission_value102);
			m_shader.AddNode(transparency_alpha_cutter87);
			m_shader.AddNode(emissive103);
			m_shader.AddNode(custom_emission101);


			texcoord79.outs.UV.Connect(diffuse_texture80.ins.Vector);
			diffuse_texture80.outs.Color.Connect(diffuse_texture_amount89.ins.Color2);
			invert_diffuse_color_amount92.outs.Value.Connect(diffuse_col_amount91.ins.Fac);
			texcoord79.outs.UV.Connect(bump_texture96.ins.Vector);
			bump_texture96.outs.Color.Connect(bump_texture_to_bw97.ins.Color);
			diffuse_texture_amount89.outs.Color.Connect(combine_diffuse_color_and_texture93.ins.Color1);
			diffuse_col_amount91.outs.Color.Connect(combine_diffuse_color_and_texture93.ins.Color2);
			bump_texture_to_bw97.outs.Val.Connect(bump95.ins.Height);
			bump_amount98.outs.Value.Connect(bump95.ins.Strength);
			combine_diffuse_color_and_texture93.outs.Color.Connect(diffuse54.ins.Color);
			bump95.outs.Normal.Connect(diffuse54.ins.Normal);
			roughness_times_roughness111.outs.Value.Connect(principled_metal55.ins.Roughness);
			bump95.outs.Normal.Connect(principled_metal55.ins.Normal);
			combine_diffuse_color_and_texture93.outs.Color.Connect(principled_paint58.ins.BaseColor);
			combine_diffuse_color_and_texture93.outs.Color.Connect(principled_paint58.ins.SpecularColor);
			roughness_times_roughness111.outs.Value.Connect(principled_paint58.ins.Roughness);
			bump95.outs.Normal.Connect(principled_paint58.ins.Normal);
			roughness_times_roughness111.outs.Value.Connect(invert_roughness66.ins.Value2);
			invert_roughness66.outs.Value.Connect(transparency_factor_for_roughness67.ins.Value1);
			transparency_factor_for_roughness67.outs.Value.Connect(toggle_when_shadow68.ins.Value1);
			light_path78.outs.IsShadowRay.Connect(toggle_when_shadow68.ins.Value2);
			roughness_times_roughness111.outs.Value.Connect(principled_glass_and_gem60.ins.Roughness);
			bump95.outs.Normal.Connect(principled_glass_and_gem60.ins.Normal);
			toggle_when_shadow68.outs.Value.Connect(transparency_layer_weight69.ins.Value1);
			layer_weight70.outs.Facing.Connect(transparency_layer_weight69.ins.Value2);
			principled_glass_and_gem60.outs.BSDF.Connect(transparency_blend_for_shadow64.ins.Closure1);
			transparent63.outs.BSDF.Connect(transparency_blend_for_shadow64.ins.Closure2);
			transparency_layer_weight69.outs.Value.Connect(transparency_blend_for_shadow64.ins.Fac);
			roughness_times_roughness111.outs.Value.Connect(glossy105.ins.Roughness);
			roughness_times_roughness111.outs.Value.Connect(glass110.ins.Roughness);
			multiply108.outs.Value.Connect(reflectivity_fresnel_weight107.ins.Value1);
			combine_diffuse_color_and_texture93.outs.Color.Connect(principled62.ins.BaseColor);
			roughness_times_roughness111.outs.Value.Connect(principled62.ins.Roughness);
			bump95.outs.Normal.Connect(principled62.ins.Normal);
			glossy105.outs.BSDF.Connect(blend109.ins.Closure1);
			glass110.outs.BSDF.Connect(blend109.ins.Closure2);
			reflectivity_fresnel_weight107.outs.Value.Connect(layerweight_for_reflectivity_blend106.ins.Blend);
			principled62.outs.BSDF.Connect(reflectivity_blend104.ins.Closure1);
			blend109.outs.Closure.Connect(reflectivity_blend104.ins.Closure2);
			layerweight_for_reflectivity_blend106.outs.Fresnel.Connect(reflectivity_blend104.ins.Fac);
			combine_diffuse_color_and_texture93.outs.Color.Connect(shadeless_bsdf99.ins.Color);
			roughness_times_roughness111.outs.Value.Connect(invert_roughness72.ins.Value2);
			invert_roughness72.outs.Value.Connect(multiply_transparency73.ins.Value1);
			multiply_transparency73.outs.Value.Connect(multiply_with_shadowray74.ins.Value1);
			light_path65.outs.IsShadowRay.Connect(multiply_with_shadowray74.ins.Value2);
			reflectivity_blend104.outs.Closure.Connect(shadeless100.ins.Closure1);
			shadeless_bsdf99.outs.Emission.Connect(shadeless100.ins.Closure2);
			combine_diffuse_color_and_texture93.outs.Color.Connect(coloured_shadow_trans_color71.ins.Color);
			multiply_with_shadowray74.outs.Value.Connect(weight_for_shadowray_coloured_shadow75.ins.Value1);
			layer_weight76.outs.Facing.Connect(weight_for_shadowray_coloured_shadow75.ins.Value2);
			diffuse_texture80.outs.Alpha.Connect(invert_alpha84.ins.Value2);
			shadeless100.outs.Closure.Connect(coloured_shadow_mix77.ins.Closure1);
			coloured_shadow_trans_color71.outs.BSDF.Connect(coloured_shadow_mix77.ins.Closure2);
			weight_for_shadowray_coloured_shadow75.outs.Value.Connect(coloured_shadow_mix77.ins.Fac);
			invert_alpha84.outs.Value.Connect(toggle_image_alpha83.ins.Value2);
			texcoord79.outs.UV.Connect(transparency_texture85.ins.Vector);
			transparency_texture85.outs.Color.Connect(color___luminance86.ins.Color);
			color___luminance86.outs.Val.Connect(invert_luminence88.ins.Value2);
			invert_luminence88.outs.Value.Connect(transparency_texture_amount94.ins.Value1);
			coloured_shadow_mix77.outs.Closure.Connect(alpha_cutter_mix82.ins.Closure1);
			alphacutter_transparent81.outs.BSDF.Connect(alpha_cutter_mix82.ins.Closure2);
			toggle_image_alpha83.outs.Value.Connect(alpha_cutter_mix82.ins.Fac);
			transparency_texture_amount94.outs.Value.Connect(toggle_transparency_texture90.ins.Value2);
			alpha_cutter_mix82.outs.Closure.Connect(transparency_alpha_cutter87.ins.Closure1);
			alphacutter_transparent81.outs.BSDF.Connect(transparency_alpha_cutter87.ins.Closure2);
			toggle_transparency_texture90.outs.Value.Connect(transparency_alpha_cutter87.ins.Fac);
			emission_value102.outs.Val.Connect(emissive103.ins.Strength);
			transparency_alpha_cutter87.outs.Closure.Connect(custom_emission101.ins.Closure1);
			emissive103.outs.Emission.Connect(custom_emission101.ins.Closure2);
			emission_value102.outs.Val.Connect(custom_emission101.ins.Fac);

			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture80, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture80, texcoord79);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture96, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture96, texcoord79);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture85, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture85, texcoord79);
			}


			switch (m_original.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.Diffuse:
					diffuse54.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.SimpleMetal:
					principled_metal55.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Glass:
					transparency_blend_for_shadow64.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
				case CyclesShader.CyclesMaterial.Paint:
					principled_paint58.outs.BSDF.Connect(m_shader.Output.ins.Surface);
					break;
				default:
					custom_emission101.outs.Closure.Connect(m_shader.Output.ins.Surface);
					break;
			}

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
