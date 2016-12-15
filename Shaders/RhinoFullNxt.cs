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
			var texcoord89 = new TextureCoordinateNode("texcoord");
			var diffuse_texture90 = new ImageTextureNode("diffuse_texture");

			diffuse_texture90.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture90.Extension = TextureNode.TextureExtension.Repeat;
			diffuse_texture90.Interpolation = InterpolationType.Linear;
			diffuse_texture90.UseAlpha = true;
			diffuse_texture90.IsLinear = false;

			var invert_diffuse_color_amount103 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount103.ins.Value1.Value = 1f;
			invert_diffuse_color_amount103.ins.Value2.Value = m_original.DiffuseTexture.Amount;

			invert_diffuse_color_amount103.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount103.UseClamp = false;
			var diffuse_texture_amount100 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount100.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount100.ins.Fac.Value = m_original.DiffuseTexture.Amount;

			var diffuse_col_amount102 = new MixNode("diffuse_col_amount");
			diffuse_col_amount102.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount102.ins.Color2.Value = m_original.DiffuseColor ^ m_original.Gamma;
			diffuse_col_amount102.ins.Fac.Value = 0.49f;

			var bump_texture107 = new ImageTextureNode("bump_texture");

			bump_texture107.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture107.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture107.Interpolation = InterpolationType.Linear;
			bump_texture107.UseAlpha = true;
			bump_texture107.IsLinear = false;
			var bump_texture_to_bw108 = new RgbToBwNode("bump_texture_to_bw");

			var bump_amount109 = new MathNode("bump_amount");
			bump_amount109.ins.Value1.Value = 10f;
			bump_amount109.ins.Value2.Value = m_original.BumpTexture.Amount;

			bump_amount109.Operation = MathNode.Operations.Multiply;
			bump_amount109.UseClamp = false;
			var combine_diffuse_color_and_texture104 = new MixNode("combine_diffuse_color_and_texture");
			combine_diffuse_color_and_texture104.ins.Fac.Value = 0.5f;

			var bump106 = new BumpNode("bump");
			bump106.ins.Height.Value = 0f;
			bump106.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump106.ins.Strength.Value = 0f;
			bump106.ins.Distance.Value = 0.1f;
			var principled62 = new UberBsdfNode("principled");
			principled62.ins.Metallic.Value = m_original.Metalic;
			principled62.ins.Subsurface.Value = 0f;
			principled62.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled62.ins.Specular.Value = m_original.Shine;
			principled62.ins.Roughness.Value = m_original.Roughness;
			principled62.ins.SpecularTint.Value = 0f;
			principled62.ins.Anisotropic.Value = 0f;
			principled62.ins.Sheen.Value = 0f;
			principled62.ins.SheenTint.Value = 0f;
			principled62.ins.Clearcoat.Value = 0f;
			principled62.ins.ClearcoatGloss.Value = 0f;
			principled62.ins.IOR.Value = m_original.IOR;
			principled62.ins.Transparency.Value = m_original.Transparency;
			principled62.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled62.ins.AnisotropicRotation.Value = 0f;
			principled62.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled62.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled62.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var emission110 = new EmissionNode("emission");
			emission110.ins.Strength.Value = 1f;
			var invert_roughness72 = new MathNode("invert_roughness");
			invert_roughness72.ins.Value1.Value = 1f;
			invert_roughness72.ins.Value2.Value = m_original.Roughness;
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
			var shadeless111 = new MixClosureNode("shadeless");
			shadeless111.ins.Fac.Value = m_original.ShadelessAsFloat;
			var coloured_shadow_trans_color71 = new TransparentBsdfNode("coloured_shadow_trans_color");
			var weight_for_shadowray_coloured_shadow75 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow75.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow75.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow75.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow75.UseClamp = false;
			var invert_alpha95 = new MathNode("invert_alpha");
			invert_alpha95.ins.Value1.Value = 1f;
			invert_alpha95.ins.Value2.Value = 0f;
			invert_alpha95.Operation = MathNode.Operations.Subtract;
			invert_alpha95.UseClamp = false;
			var coloured_shadow_mix77 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix77.ins.Fac.Value = 0f;
			var alphacutter_transparent91 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent91.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);
			var toggle_image_alpha93 = new MathNode("toggle_image_alpha");
			toggle_image_alpha93.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha93.ins.Value2.Value = 1f;
			toggle_image_alpha93.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha93.UseClamp = false;
			var transparency_texture96 = new ImageTextureNode("transparency_texture");
			transparency_texture96.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture96.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture96.Interpolation = InterpolationType.Linear;
			transparency_texture96.UseAlpha = true;
			transparency_texture96.IsLinear = false;
			var color___luminance97 = new RgbToLuminanceNode("color___luminance");
			var invert_luminence99 = new MathNode("invert_luminence");
			invert_luminence99.ins.Value1.Value = 1f;
			invert_luminence99.ins.Value2.Value = 0f;
			invert_luminence99.Operation = MathNode.Operations.Subtract;
			invert_luminence99.UseClamp = false;
			var transparency_texture_amount105 = new MathNode("transparency_texture_amount");
			transparency_texture_amount105.ins.Value1.Value = 1f;
			transparency_texture_amount105.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount105.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount105.UseClamp = false;
			var alpha_cutter_mix92 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix92.ins.Fac.Value = 1f;
			var toggle_transparency_texture101 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture101.ins.Value1.Value = m_original.HasTransparencyTexture ? 1.0f : 0.0f;
			toggle_transparency_texture101.ins.Value2.Value = 1f;
			toggle_transparency_texture101.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture101.UseClamp = false;
			var emission_value113 = new RgbToBwNode("emission_value");
			emission_value113.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			var transparency_alpha_cutter98 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter98.ins.Fac.Value = 1f;
			var emissive115 = new EmissionNode("emissive");
			emissive115.ins.Color.Value = m_original.EmissionColor ^ m_original.Gamma;
			emissive115.ins.Strength.Value = 1f;
			var emission_enabled_check114 = new MathNode("emission_enabled_check");
			emission_enabled_check114.ins.Value1.Value = 0f;
			emission_enabled_check114.ins.Value2.Value = 0f;
			emission_enabled_check114.Operation = MathNode.Operations.Greater_Than;
			emission_enabled_check114.UseClamp = false;
			var blend112 = new MixClosureNode("blend");
			blend112.ins.Fac.Value = 0f;
			m_shader.AddNode(texcoord89);
			m_shader.AddNode(diffuse_texture90);
			m_shader.AddNode(invert_diffuse_color_amount103);
			m_shader.AddNode(diffuse_texture_amount100);
			m_shader.AddNode(diffuse_col_amount102);
			m_shader.AddNode(bump_texture107);
			m_shader.AddNode(bump_texture_to_bw108);
			m_shader.AddNode(bump_amount109);
			m_shader.AddNode(combine_diffuse_color_and_texture104);
			m_shader.AddNode(bump106);
			m_shader.AddNode(principled62);
			m_shader.AddNode(emission110);
			m_shader.AddNode(invert_roughness72);
			m_shader.AddNode(multiply_transparency73);
			m_shader.AddNode(light_path65);
			m_shader.AddNode(multiply_with_shadowray74);
			m_shader.AddNode(layer_weight76);
			m_shader.AddNode(shadeless111);
			m_shader.AddNode(coloured_shadow_trans_color71);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow75);
			m_shader.AddNode(invert_alpha95);
			m_shader.AddNode(coloured_shadow_mix77);
			m_shader.AddNode(alphacutter_transparent91);
			m_shader.AddNode(toggle_image_alpha93);
			m_shader.AddNode(transparency_texture96);
			m_shader.AddNode(color___luminance97);
			m_shader.AddNode(invert_luminence99);
			m_shader.AddNode(transparency_texture_amount105);
			m_shader.AddNode(alpha_cutter_mix92);
			m_shader.AddNode(toggle_transparency_texture101);
			m_shader.AddNode(emission_value113);
			m_shader.AddNode(transparency_alpha_cutter98);
			m_shader.AddNode(emissive115);
			m_shader.AddNode(emission_enabled_check114);
			m_shader.AddNode(blend112);

			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture90, m_original.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture90, texcoord89);
			}

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture107, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture107, texcoord89);
			}

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture96, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture96, texcoord89);
			}

			diffuse_texture90.outs.Color.Connect(diffuse_texture_amount100.ins.Color2);
			invert_diffuse_color_amount103.outs.Value.Connect(diffuse_col_amount102.ins.Fac);
			bump_texture107.outs.Color.Connect(bump_texture_to_bw108.ins.Color);
			diffuse_texture_amount100.outs.Color.Connect(combine_diffuse_color_and_texture104.ins.Color1);
			diffuse_col_amount102.outs.Color.Connect(combine_diffuse_color_and_texture104.ins.Color2);
			bump_texture_to_bw108.outs.Val.Connect(bump106.ins.Height);
			bump_amount109.outs.Value.Connect(bump106.ins.Strength);
			combine_diffuse_color_and_texture104.outs.Color.Connect(principled62.ins.BaseColor);
			combine_diffuse_color_and_texture104.outs.Color.Connect(principled62.ins.SpecularColor);
			bump106.outs.Normal.Connect(principled62.ins.Normal);
			combine_diffuse_color_and_texture104.outs.Color.Connect(emission110.ins.Color);
			invert_roughness72.outs.Value.Connect(multiply_transparency73.ins.Value1);
			multiply_transparency73.outs.Value.Connect(multiply_with_shadowray74.ins.Value1);
			light_path65.outs.IsShadowRay.Connect(multiply_with_shadowray74.ins.Value2);
			principled62.outs.BSDF.Connect(shadeless111.ins.Closure1);
			emission110.outs.Emission.Connect(shadeless111.ins.Closure2);
			combine_diffuse_color_and_texture104.outs.Color.Connect(coloured_shadow_trans_color71.ins.Color);
			multiply_with_shadowray74.outs.Value.Connect(weight_for_shadowray_coloured_shadow75.ins.Value1);
			layer_weight76.outs.Facing.Connect(weight_for_shadowray_coloured_shadow75.ins.Value2);
			diffuse_texture90.outs.Alpha.Connect(invert_alpha95.ins.Value2);
			shadeless111.outs.Closure.Connect(coloured_shadow_mix77.ins.Closure1);
			coloured_shadow_trans_color71.outs.BSDF.Connect(coloured_shadow_mix77.ins.Closure2);
			weight_for_shadowray_coloured_shadow75.outs.Value.Connect(coloured_shadow_mix77.ins.Fac);
			invert_alpha95.outs.Value.Connect(toggle_image_alpha93.ins.Value2);
			transparency_texture96.outs.Color.Connect(color___luminance97.ins.Color);
			color___luminance97.outs.Val.Connect(invert_luminence99.ins.Value2);
			invert_luminence99.outs.Value.Connect(transparency_texture_amount105.ins.Value1);
			coloured_shadow_mix77.outs.Closure.Connect(alpha_cutter_mix92.ins.Closure1);
			alphacutter_transparent91.outs.BSDF.Connect(alpha_cutter_mix92.ins.Closure2);
			toggle_image_alpha93.outs.Value.Connect(alpha_cutter_mix92.ins.Fac);
			transparency_texture_amount105.outs.Value.Connect(toggle_transparency_texture101.ins.Value2);
			alpha_cutter_mix92.outs.Closure.Connect(transparency_alpha_cutter98.ins.Closure1);
			alphacutter_transparent91.outs.BSDF.Connect(transparency_alpha_cutter98.ins.Closure2);
			toggle_transparency_texture101.outs.Value.Connect(transparency_alpha_cutter98.ins.Fac);
			emission_value113.outs.Val.Connect(emission_enabled_check114.ins.Value1);
			transparency_alpha_cutter98.outs.Closure.Connect(blend112.ins.Closure1);
			emissive115.outs.Emission.Connect(blend112.ins.Closure2);
			emission_enabled_check114.outs.Value.Connect(blend112.ins.Fac);
			blend112.outs.Closure.Connect(m_shader.Output.ins.Surface);

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
