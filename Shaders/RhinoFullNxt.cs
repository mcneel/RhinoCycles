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
			var texcoord147 = new TextureCoordinateNode("texcoord");
			var diffuse_texture148 = new ImageTextureNode("diffuse_texture");
			diffuse_texture148.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture148.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture148.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture148.Extension = TextureNode.TextureExtension.Repeat;
			diffuse_texture148.Interpolation = InterpolationType.Linear;
			diffuse_texture148.IsLinear = false;
			if (m_original.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture148, m_original.DiffuseTexture);
				diffuse_texture148.UseAlpha = m_original.DiffuseTexture.UseAlpha;
				RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture148, texcoord147);
			}
			var invert_diffuse_color_amount161 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount161.ins.Value1.Value = 1f;
			invert_diffuse_color_amount161.ins.Value2.Value = m_original.DiffuseTexture.Amount;
			invert_diffuse_color_amount161.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount161.UseClamp = false;
			var diffuse_texture_amount158 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount158.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount158.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount158.ins.Fac.Value = m_original.DiffuseTexture.Amount;
			var diffuse_col_amount160 = new MixNode("diffuse_col_amount");
			diffuse_col_amount160.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount160.ins.Color2.Value = m_original.DiffuseColor;
			diffuse_col_amount160.ins.Fac.Value = 0.49f;
			var bump_texture165 = new ImageTextureNode("bump_texture");
			bump_texture165.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture165.Projection = TextureNode.TextureProjection.Flat;
			bump_texture165.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture165.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture165.Interpolation = InterpolationType.Linear;
			bump_texture165.UseAlpha = true;
			bump_texture165.IsLinear = false;

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture165, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture165, texcoord147);
			}

			var bump_texture_to_bw199 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw199.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			var bump_amount200 = new MathNode("bump_amount");
			bump_amount200.ins.Value1.Value = 10f;
			bump_amount200.ins.Value2.Value = m_original.BumpTexture.Amount;
			bump_amount200.Operation = MathNode.Operations.Multiply;
			bump_amount200.UseClamp = false;
			var combine_diffuse_color_and_texture162 = new MixNode("combine_diffuse_color_and_texture");
			combine_diffuse_color_and_texture162.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			combine_diffuse_color_and_texture162.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			combine_diffuse_color_and_texture162.ins.Fac.Value = 0.5f;
			var bump164 = new BumpNode("bump");
			bump164.ins.Height.Value = 0f;
			bump164.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump164.ins.Strength.Value = 0f;
			bump164.ins.Distance.Value = 0.1f;
			var invert_roughness130 = new MathNode("invert_roughness");
			invert_roughness130.ins.Value1.Value = 1f;
			invert_roughness130.ins.Value2.Value = m_original.Roughness;
			invert_roughness130.Operation = MathNode.Operations.Subtract;
			invert_roughness130.UseClamp = false;
			var multiply_transparency131 = new MathNode("multiply_transparency");
			multiply_transparency131.ins.Value1.Value = 1f;
			multiply_transparency131.ins.Value2.Value = m_original.Transparency;
			multiply_transparency131.Operation = MathNode.Operations.Multiply;
			multiply_transparency131.UseClamp = false;
			var light_path123 = new LightPathNode("light_path");
			var multiply_with_shadowray132 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray132.ins.Value1.Value = 0f;
			multiply_with_shadowray132.ins.Value2.Value = 0f;
			multiply_with_shadowray132.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray132.UseClamp = false;
			var layer_weight134 = new LayerWeightNode("layer_weight");
			layer_weight134.ins.Blend.Value = 0.89f;
			layer_weight134.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var principled120 = new UberBsdfNode("principled");
			principled120.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled120.ins.SpecularColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled120.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principled120.ins.Metallic.Value = m_original.Metalic;
			principled120.ins.Subsurface.Value = 0f;
			principled120.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled120.ins.Specular.Value = m_original.Shine;
			principled120.ins.Roughness.Value = m_original.Roughness;
			principled120.ins.SpecularTint.Value = 0f;
			principled120.ins.Anisotropic.Value = 0f;
			principled120.ins.Sheen.Value = 0f;
			principled120.ins.SheenTint.Value = 0f;
			principled120.ins.Clearcoat.Value = 0f;
			principled120.ins.ClearcoatGloss.Value = 0f;
			principled120.ins.IOR.Value = m_original.IOR;
			principled120.ins.Transparency.Value = m_original.Transparency;
			principled120.ins.RefractionRoughness.Value = m_original.RefractionRoughness;
			principled120.ins.AnisotropicRotation.Value = 0f;
			principled120.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled120.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principled120.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);
			var coloured_shadow_trans_color129 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color129.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			var weight_for_shadowray_coloured_shadow133 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow133.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow133.ins.Value2.Value = 0f;
			weight_for_shadowray_coloured_shadow133.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow133.UseClamp = false;
			var invert_alpha153 = new MathNode("invert_alpha");
			invert_alpha153.ins.Value1.Value = 1f;
			invert_alpha153.ins.Value2.Value = 0f;
			invert_alpha153.Operation = MathNode.Operations.Subtract;
			invert_alpha153.UseClamp = false;
			var coloured_shadow_mix135 = new MixClosureNode("coloured_shadow_mix");
			coloured_shadow_mix135.ins.Fac.Value = 0f;
			var alphacutter_transparent149 = new TransparentBsdfNode("alphacutter_transparent");
			alphacutter_transparent149.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);
			var toggle_image_alpha151 = new MathNode("toggle_image_alpha");
			toggle_image_alpha151.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			toggle_image_alpha151.ins.Value2.Value = 1f;
			toggle_image_alpha151.Operation = MathNode.Operations.Multiply;
			toggle_image_alpha151.UseClamp = false;
			var transparency_texture154 = new ImageTextureNode("transparency_texture");
			transparency_texture154.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture154.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture154.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture154.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture154.Interpolation = InterpolationType.Linear;
			transparency_texture154.UseAlpha = true;
			transparency_texture154.IsLinear = false;
			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture154, m_original.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture154, texcoord147);
			}
			var color___luminance155 = new RgbToLuminanceNode("color___luminance");
			color___luminance155.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			var invert_luminence157 = new MathNode("invert_luminence");
			invert_luminence157.ins.Value1.Value = 1f;
			invert_luminence157.ins.Value2.Value = 0f;
			invert_luminence157.Operation = MathNode.Operations.Subtract;
			invert_luminence157.UseClamp = false;
			var transparency_texture_amount163 = new MathNode("transparency_texture_amount");
			transparency_texture_amount163.ins.Value1.Value = 1f;
			transparency_texture_amount163.ins.Value2.Value = m_original.TransparencyTexture.Amount;
			transparency_texture_amount163.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount163.UseClamp = false;
			var alpha_cutter_mix150 = new MixClosureNode("alpha_cutter_mix");
			alpha_cutter_mix150.ins.Fac.Value = 1f;
			var toggle_transparency_texture159 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture159.ins.Value1.Value = m_original.HasTransparencyTexture ? 1.0f : 0.0f;
			toggle_transparency_texture159.ins.Value2.Value = 1f;
			toggle_transparency_texture159.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture159.UseClamp = false;
			var transparency_alpha_cutter156 = new MixClosureNode("transparency_alpha_cutter");
			transparency_alpha_cutter156.ins.Fac.Value = 1f;
			m_shader.AddNode(texcoord147);
			m_shader.AddNode(diffuse_texture148);
			m_shader.AddNode(invert_diffuse_color_amount161);
			m_shader.AddNode(diffuse_texture_amount158);
			m_shader.AddNode(diffuse_col_amount160);
			m_shader.AddNode(bump_texture165);
			m_shader.AddNode(bump_texture_to_bw199);
			m_shader.AddNode(bump_amount200);
			m_shader.AddNode(combine_diffuse_color_and_texture162);
			m_shader.AddNode(bump164);
			m_shader.AddNode(invert_roughness130);
			m_shader.AddNode(multiply_transparency131);
			m_shader.AddNode(light_path123);
			m_shader.AddNode(multiply_with_shadowray132);
			m_shader.AddNode(layer_weight134);
			m_shader.AddNode(principled120);
			m_shader.AddNode(coloured_shadow_trans_color129);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow133);
			m_shader.AddNode(invert_alpha153);
			m_shader.AddNode(coloured_shadow_mix135);
			m_shader.AddNode(alphacutter_transparent149);
			m_shader.AddNode(toggle_image_alpha151);
			m_shader.AddNode(transparency_texture154);
			m_shader.AddNode(color___luminance155);
			m_shader.AddNode(invert_luminence157);
			m_shader.AddNode(transparency_texture_amount163);
			m_shader.AddNode(alpha_cutter_mix150);
			m_shader.AddNode(toggle_transparency_texture159);
			m_shader.AddNode(transparency_alpha_cutter156);
			//texcoord147.outs.UV.Connect(diffuse_texture148.ins.Vector);
			diffuse_texture148.outs.Color.Connect(diffuse_texture_amount158.ins.Color2);
			invert_diffuse_color_amount161.outs.Value.Connect(diffuse_col_amount160.ins.Fac);
			//texcoord147.outs.UV.Connect(bump_texture165.ins.Vector);
			bump_texture165.outs.Color.Connect(bump_texture_to_bw199.ins.Color);
			diffuse_texture_amount158.outs.Color.Connect(combine_diffuse_color_and_texture162.ins.Color1);
			diffuse_col_amount160.outs.Color.Connect(combine_diffuse_color_and_texture162.ins.Color2);
			bump_texture_to_bw199.outs.Val.Connect(bump164.ins.Height);
			bump_amount200.outs.Value.Connect(bump164.ins.Strength);
			invert_roughness130.outs.Value.Connect(multiply_transparency131.ins.Value1);
			multiply_transparency131.outs.Value.Connect(multiply_with_shadowray132.ins.Value1);
			light_path123.outs.IsShadowRay.Connect(multiply_with_shadowray132.ins.Value2);
			combine_diffuse_color_and_texture162.outs.Color.Connect(principled120.ins.BaseColor);
			combine_diffuse_color_and_texture162.outs.Color.Connect(principled120.ins.SpecularColor);
			bump164.outs.Normal.Connect(principled120.ins.Normal);
			diffuse_texture148.outs.Color.Connect(coloured_shadow_trans_color129.ins.Color);
			multiply_with_shadowray132.outs.Value.Connect(weight_for_shadowray_coloured_shadow133.ins.Value1);
			layer_weight134.outs.Facing.Connect(weight_for_shadowray_coloured_shadow133.ins.Value2);
			diffuse_texture148.outs.Alpha.Connect(invert_alpha153.ins.Value2);
			principled120.outs.BSDF.Connect(coloured_shadow_mix135.ins.Closure1);
			coloured_shadow_trans_color129.outs.BSDF.Connect(coloured_shadow_mix135.ins.Closure2);
			weight_for_shadowray_coloured_shadow133.outs.Value.Connect(coloured_shadow_mix135.ins.Fac);
			invert_alpha153.outs.Value.Connect(toggle_image_alpha151.ins.Value2);
			//texcoord147.outs.UV.Connect(transparency_texture154.ins.Vector);
			transparency_texture154.outs.Color.Connect(color___luminance155.ins.Color);
			color___luminance155.outs.Val.Connect(invert_luminence157.ins.Value2);
			invert_luminence157.outs.Value.Connect(transparency_texture_amount163.ins.Value1);
			coloured_shadow_mix135.outs.Closure.Connect(alpha_cutter_mix150.ins.Closure1);
			alphacutter_transparent149.outs.BSDF.Connect(alpha_cutter_mix150.ins.Closure2);
			toggle_image_alpha151.outs.Value.Connect(alpha_cutter_mix150.ins.Fac);
			transparency_texture_amount163.outs.Value.Connect(toggle_transparency_texture159.ins.Value2);
			alpha_cutter_mix150.outs.Closure.Connect(transparency_alpha_cutter156.ins.Closure1);
			alphacutter_transparent149.outs.BSDF.Connect(transparency_alpha_cutter156.ins.Closure2);
			toggle_transparency_texture159.outs.Value.Connect(transparency_alpha_cutter156.ins.Fac);
			transparency_alpha_cutter156.outs.Closure.Connect(m_shader.Output.ins.Surface);
			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
