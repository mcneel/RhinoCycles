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
			var texcoord305 = new TextureCoordinateNode("texcoord");
			var diffuse_texture306 = new ImageTextureNode("diffuse_texture");
			diffuse_texture306.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture306.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture306.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture306.Extension = TextureNode.TextureExtension.Repeat;
			diffuse_texture306.Interpolation = InterpolationType.Linear;
			diffuse_texture306.UseAlpha = true;
			diffuse_texture306.IsLinear = false;
			var invert_diffuse_color_amount318 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount318.ins.Value1.Value = 1f;
			invert_diffuse_color_amount318.ins.Value2.Value = m_original.DiffuseTexture.Amount;
			invert_diffuse_color_amount318.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount318.UseClamp = false;
			var diffuse_texture_amount315 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount315.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount315.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount315.ins.Fac.Value = m_original.DiffuseTexture.Amount;
			var diffuse_col_amount317 = new MixNode("diffuse_col_amount");
			diffuse_col_amount317.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount317.ins.Color2.Value = m_original.DiffuseColor ^ m_original.Gamma;
			diffuse_col_amount317.ins.Fac.Value = 1f;
			var bump_texture322 = new ImageTextureNode("bump_texture");
			bump_texture322.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture322.Projection = TextureNode.TextureProjection.Flat;
			bump_texture322.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture322.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture322.Interpolation = InterpolationType.Linear;
			bump_texture322.UseAlpha = true;
			bump_texture322.IsLinear = false;
			var bump_texture_to_bw323 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw323.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			var bump_amount324 = new MathNode("bump_amount");
			bump_amount324.ins.Value1.Value = 10f;
			bump_amount324.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount324.Operation = MathNode.Operations.Multiply;
			bump_amount324.UseClamp = false;
			var combine_diffuse_color_and_texture319 = new MixNode("combine_diffuse_color_and_texture");
			combine_diffuse_color_and_texture319.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			combine_diffuse_color_and_texture319.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			combine_diffuse_color_and_texture319.ins.Fac.Value = 0.5f;
			var roughness_power_two336 = new MathNode("roughness_power_two");
			roughness_power_two336.ins.Value1.Value = m_original.Roughness;
			roughness_power_two336.ins.Value2.Value = m_original.Roughness;
			roughness_power_two336.Operation = MathNode.Operations.Multiply;
			roughness_power_two336.UseClamp = false;
			var bump321 = new BumpNode("bump");
			bump321.ins.Height.Value = 0f;
			bump321.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump321.ins.Strength.Value = 0f;
			bump321.ins.Distance.Value = 0.1f;
			var glossy332 = new GlossyBsdfNode("glossy");
			glossy332.ins.Color.Value = m_original.ReflectionColor ^ m_original.Gamma;
			glossy332.ins.Roughness.Value = 0f;
			glossy332.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var glass338 = new GlassBsdfNode("glass");
			glass338.ins.Color.Value = m_original.ReflectionColor ^ m_original.Gamma;
			glass338.ins.Roughness.Value = 0f;
			glass338.ins.IOR.Value = m_original.IOR;
			glass338.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var multiply335 = new MathNode("multiply");
			multiply335.ins.Value1.Value = m_original.Reflectivity;
			multiply335.ins.Value2.Value = m_original.FresnelReflectionsAsFloat;
			multiply335.Operation = MathNode.Operations.Multiply;
			multiply335.UseClamp = false;
			var reflectivity_fresnel_weight334 = new MathNode("reflectivity_fresnel_weight");
			reflectivity_fresnel_weight334.ins.Value1.Value = 0.35f;
			reflectivity_fresnel_weight334.ins.Value2.Value = 0.5f;
			reflectivity_fresnel_weight334.Operation = MathNode.Operations.Multiply;
			reflectivity_fresnel_weight334.UseClamp = false;
			var principled278 = new UberBsdfNode("principled");
			principled278.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled278.ins.SpecularColor.Value = m_original.SpecularColor ^ m_original.Gamma;
			principled278.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled278.ins.Metallic.Value = m_original.Metalic;
			principled278.ins.Subsurface.Value = 0f;
			principled278.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled278.ins.Specular.Value = m_original.Shine;
			principled278.ins.Roughness.Value = 0f;
			principled278.ins.SpecularTint.Value = m_original.Gloss;
			principled278.ins.Anisotropic.Value = 0f;
			principled278.ins.Sheen.Value = 0f;
			principled278.ins.SheenTint.Value = m_original.Gloss;
			principled278.ins.Clearcoat.Value = 0f;
			principled278.ins.ClearcoatGloss.Value = m_original.Gloss;
			principled278.ins.IOR.Value = m_original.IOR;
			principled278.ins.Transparency.Value = m_original.Transparency;
			principled278.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled278.ins.AnisotropicRotation.Value = 0f;
			principled278.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled278.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled278.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var blend337 = new MixClosureNode("blend");
			blend337.ins.Fac.Value = m_original.Transparency;
			var layerweight_for_reflectivity_blend333 = new LayerWeightNode("layerweight_for_reflectivity_blend");
			layerweight_for_reflectivity_blend333.ins.Blend.Value = 0.175f;
			layerweight_for_reflectivity_blend333.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var reflectivity_blend331 = new MixClosureNode("reflectivity_blend");
			reflectivity_blend331.ins.Fac.Value = 0f;
			var shadeless_bsdf325 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf325.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf325.ins.Strength.Value = 1f;
			var invert_roughness288 = new MathNode("invert_roughness");
			invert_roughness288.ins.Value1.Value = 1f;
			invert_roughness288.ins.Value2.Value = 0f;
			invert_roughness288.Operation = MathNode.Operations.Subtract;
			invert_roughness288.UseClamp = false;
			var multiply_transparency289 = new MathNode("multiply_transparency");
			multiply_transparency289.ins.Value1.Value = 1f;
			multiply_transparency289.ins.Value2.Value = m_original.Transparency;
			multiply_transparency289.Operation = MathNode.Operations.Multiply;
			multiply_transparency289.UseClamp = false;
			var light_path281 = new LightPathNode("light_path");
			var multiply_with_shadowray290 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray290.ins.Value1.Value = 1f;
			multiply_with_shadowray290.ins.Value2.Value = 0f;
			multiply_with_shadowray290.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray290.UseClamp = false;
			var layer_weight292 = new LayerWeightNode("layer_weight");
			layer_weight292.ins.Blend.Value = 0.89f;
			layer_weight292.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var shadeless326 = new MixClosureNode("shadeless");
			shadeless326.ins.Fac.Value = m_original.ShadelessAsFloat;
			var coloured_shadow_trans_color287 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color287.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			var weight_for_shadowray_coloured_shadow291 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow291.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow291.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow291.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow291.UseClamp = false;
			var invert_alpha310 = new MathNode("invert_alpha");
			invert_alpha310.ins.Value1.Value = 1f;
			invert_alpha310.ins.Value2.Value = 0f;
			invert_alpha310.Operation = MathNode.Operations.Subtract;
			invert_alpha310.UseClamp = false;
			var coloured_shadow_mix293 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix293.ins.Fac.Value = 0f;
			var alphacutter_transparent307 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent307.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);
			var toggle_image_alpha309 = new MathNode("toggle_image_alpha");
			toggle_image_alpha309.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha309.ins.Value2.Value = 1f;
			toggle_image_alpha309.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha309.UseClamp = false;
			var transparency_texture311 = new ImageTextureNode("transparency_texture");
			transparency_texture311.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture311.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture311.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture311.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture311.Interpolation = InterpolationType.Linear;
			transparency_texture311.UseAlpha = true;
			transparency_texture311.IsLinear = false;
			var color___luminance312 = new RgbToLuminanceNode("color___luminance");
			color___luminance312.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			var invert_luminence314 = new MathNode("invert_luminence");
			invert_luminence314.ins.Value1.Value = 1f;
			invert_luminence314.ins.Value2.Value = 0f;
			invert_luminence314.Operation = MathNode.Operations.Subtract;
			invert_luminence314.UseClamp = false;
			var transparency_texture_amount320 = new MathNode("transparency_texture_amount");
			transparency_texture_amount320.ins.Value1.Value = 1f;
			transparency_texture_amount320.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount320.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount320.UseClamp = false;
			var alpha_cutter_mix308 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix308.ins.Fac.Value = 0f;
			var toggle_transparency_texture316 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture316.ins.Value1.Value = m_original.HasTransparencyTexture ? 1.0f : 0.0f;
			toggle_transparency_texture316.ins.Value2.Value = 0f;
			toggle_transparency_texture316.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture316.UseClamp = false;
			var emission_value328 = new RgbToBwNode("emission_value");
			emission_value328.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			var transparency_alpha_cutter313 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter313.ins.Fac.Value = 0f;
			var emissive330 = new EmissionNode("emissive");
			emissive330.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			emissive330.ins.Strength.Value = 0f;
			var blend327 = new MixClosureNode("blend");
			blend327.ins.Fac.Value = 0f;
			m_shader.AddNode(texcoord305);
			m_shader.AddNode(diffuse_texture306);
			m_shader.AddNode(invert_diffuse_color_amount318);
			m_shader.AddNode(diffuse_texture_amount315);
			m_shader.AddNode(diffuse_col_amount317);
			m_shader.AddNode(bump_texture322);
			m_shader.AddNode(bump_texture_to_bw323);
			m_shader.AddNode(bump_amount324);
			m_shader.AddNode(combine_diffuse_color_and_texture319);
			m_shader.AddNode(roughness_power_two336);
			m_shader.AddNode(bump321);
			m_shader.AddNode(glossy332);
			m_shader.AddNode(glass338);
			m_shader.AddNode(multiply335);
			m_shader.AddNode(reflectivity_fresnel_weight334);
			m_shader.AddNode(principled278);
			m_shader.AddNode(blend337);
			m_shader.AddNode(layerweight_for_reflectivity_blend333);
			m_shader.AddNode(reflectivity_blend331);
			m_shader.AddNode(shadeless_bsdf325);
			m_shader.AddNode(invert_roughness288);
			m_shader.AddNode(multiply_transparency289);
			m_shader.AddNode(light_path281);
			m_shader.AddNode(multiply_with_shadowray290);
			m_shader.AddNode(layer_weight292);
			m_shader.AddNode(shadeless326);
			m_shader.AddNode(coloured_shadow_trans_color287);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow291);
			m_shader.AddNode(invert_alpha310);
			m_shader.AddNode(coloured_shadow_mix293);
			m_shader.AddNode(alphacutter_transparent307);
			m_shader.AddNode(toggle_image_alpha309);
			m_shader.AddNode(transparency_texture311);
			m_shader.AddNode(color___luminance312);
			m_shader.AddNode(invert_luminence314);
			m_shader.AddNode(transparency_texture_amount320);
			m_shader.AddNode(alpha_cutter_mix308);
			m_shader.AddNode(toggle_transparency_texture316);
			m_shader.AddNode(emission_value328);
			m_shader.AddNode(transparency_alpha_cutter313);
			m_shader.AddNode(emissive330);
			m_shader.AddNode(blend327);

			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture306, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture306, texcoord305);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture322, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture322, texcoord305);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture311, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture311, texcoord305);
			}
			diffuse_texture306.outs.Color.Connect(diffuse_texture_amount315.ins.Color2);
			invert_diffuse_color_amount318.outs.Value.Connect(diffuse_col_amount317.ins.Fac);
			bump_texture322.outs.Color.Connect(bump_texture_to_bw323.ins.Color);
			diffuse_texture_amount315.outs.Color.Connect(combine_diffuse_color_and_texture319.ins.Color1);
			diffuse_col_amount317.outs.Color.Connect(combine_diffuse_color_and_texture319.ins.Color2);
			bump_texture_to_bw323.outs.Val.Connect(bump321.ins.Height);
			bump_amount324.outs.Value.Connect(bump321.ins.Strength);
			roughness_power_two336.outs.Value.Connect(glossy332.ins.Roughness);
			roughness_power_two336.outs.Value.Connect(glass338.ins.Roughness);
			multiply335.outs.Value.Connect(reflectivity_fresnel_weight334.ins.Value1);
			combine_diffuse_color_and_texture319.outs.Color.Connect(principled278.ins.BaseColor);
			roughness_power_two336.outs.Value.Connect(principled278.ins.Roughness);
			bump321.outs.Normal.Connect(principled278.ins.Normal);
			glossy332.outs.BSDF.Connect(blend337.ins.Closure1);
			glass338.outs.BSDF.Connect(blend337.ins.Closure2);
			reflectivity_fresnel_weight334.outs.Value.Connect(layerweight_for_reflectivity_blend333.ins.Blend);
			principled278.outs.BSDF.Connect(reflectivity_blend331.ins.Closure1);
			blend337.outs.Closure.Connect(reflectivity_blend331.ins.Closure2);
			layerweight_for_reflectivity_blend333.outs.Fresnel.Connect(reflectivity_blend331.ins.Fac);
			combine_diffuse_color_and_texture319.outs.Color.Connect(shadeless_bsdf325.ins.Color);
			roughness_power_two336.outs.Value.Connect(invert_roughness288.ins.Value2);
			invert_roughness288.outs.Value.Connect(multiply_transparency289.ins.Value1);
			multiply_transparency289.outs.Value.Connect(multiply_with_shadowray290.ins.Value1);
			light_path281.outs.IsShadowRay.Connect(multiply_with_shadowray290.ins.Value2);
			reflectivity_blend331.outs.Closure.Connect(shadeless326.ins.Closure1);
			shadeless_bsdf325.outs.Emission.Connect(shadeless326.ins.Closure2);
			combine_diffuse_color_and_texture319.outs.Color.Connect(coloured_shadow_trans_color287.ins.Color);
			multiply_with_shadowray290.outs.Value.Connect(weight_for_shadowray_coloured_shadow291.ins.Value1);
			layer_weight292.outs.Facing.Connect(weight_for_shadowray_coloured_shadow291.ins.Value2);
			diffuse_texture306.outs.Alpha.Connect(invert_alpha310.ins.Value2);
			shadeless326.outs.Closure.Connect(coloured_shadow_mix293.ins.Closure1);
			coloured_shadow_trans_color287.outs.BSDF.Connect(coloured_shadow_mix293.ins.Closure2);
			weight_for_shadowray_coloured_shadow291.outs.Value.Connect(coloured_shadow_mix293.ins.Fac);
			invert_alpha310.outs.Value.Connect(toggle_image_alpha309.ins.Value2);
			transparency_texture311.outs.Color.Connect(color___luminance312.ins.Color);
			color___luminance312.outs.Val.Connect(invert_luminence314.ins.Value2);
			invert_luminence314.outs.Value.Connect(transparency_texture_amount320.ins.Value1);
			coloured_shadow_mix293.outs.Closure.Connect(alpha_cutter_mix308.ins.Closure1);
			alphacutter_transparent307.outs.BSDF.Connect(alpha_cutter_mix308.ins.Closure2);
			toggle_image_alpha309.outs.Value.Connect(alpha_cutter_mix308.ins.Fac);
			transparency_texture_amount320.outs.Value.Connect(toggle_transparency_texture316.ins.Value2);
			alpha_cutter_mix308.outs.Closure.Connect(transparency_alpha_cutter313.ins.Closure1);
			alphacutter_transparent307.outs.BSDF.Connect(transparency_alpha_cutter313.ins.Closure2);
			toggle_transparency_texture316.outs.Value.Connect(transparency_alpha_cutter313.ins.Fac);
			emission_value328.outs.Val.Connect(emissive330.ins.Strength);
			transparency_alpha_cutter313.outs.Closure.Connect(blend327.ins.Closure1);
			emissive330.outs.Emission.Connect(blend327.ins.Closure2);
			emission_value328.outs.Val.Connect(blend327.ins.Fac);
			blend327.outs.Closure.Connect(m_shader.Output.ins.Surface);
			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
