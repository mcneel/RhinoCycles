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
	public class RhinoFull : RhinoShader
	{
		/// <summary>
		/// Bump node to support bump textures
		/// </summary>
		private readonly BumpNode bump_normals = new BumpNode("bump_normals");
		/// <summary>
		/// Convert bump texture into value
		/// </summary>
		private readonly RgbToBwNode bump_rgb_to_bw = new RgbToBwNode("bump_rgb_to_bw");
		/// <summary>
		/// Image texture node holding bump texture
		/// </summary>
		private readonly ImageTextureNode bump_texture = new ImageTextureNode("bump_texture");

		private readonly AddClosureNode comp_diffuse = new AddClosureNode("comp_diffuse");

		private readonly AddClosureNode comp_emission = new AddClosureNode("comp_emission");

		private readonly AddClosureNode comp_reflection = new AddClosureNode("comp_reflection");

		private readonly AddClosureNode comp_transparency = new AddClosureNode("comp_transparency");

		private readonly MathNode diffuse_alpha_inv = new MathNode("diffuse_alpha_inv");

		private readonly MathNode diffuse_alpha_mult = new MathNode("diffuse_alpha_mult");

		private readonly DiffuseBsdfNode diffuse_bsdf = new DiffuseBsdfNode("diffuse_bsdf");

		private readonly ColorNode diffuse_color = new ColorNode("diffuse_color");

		private readonly MixNode diffuse_color_amount = new MixNode("diffuse_color_amount");

		private readonly MixNode diffuse_effective_color = new MixNode("diffuse_effective_color") { BlendType=MixNode.BlendTypes.Add};

		private readonly LightPathNode diffuse_light_path_for_shadeless = new LightPathNode("diffuse_light_path_for_shadeless");

		private readonly MixClosureNode diffuse_or_shadeless = new MixClosureNode("diffuse_or_shadeless");

		private readonly EmissionNode diffuse_shadeless = new EmissionNode("diffuse_shadeless");

		/// <summary>
		/// Transparent BSDF to help with lighting objects behind other objects with transp material
		/// </summary>
		private readonly TransparentBsdfNode transparent_bsdf = new TransparentBsdfNode("transparent for light transport");

		private readonly MixClosureNode transparent_mix_closure_node = new MixClosureNode("mix transparency based on lightpaths");

		/// <summary>
		/// max(shadow,reflection)
		/// </summary>
		private readonly MathNode transparent_lightpath_max = new MathNode("max(shadow,reflection)")
		{
			Operation = MathNode.Operations.Maximum
		};

		private readonly MathNode transparent_lightpath_fac = new MathNode("max * transp") {Operation=MathNode.Operations.Multiply};

		/// <summary>
		/// Diffuse texture input
		/// </summary>
		private readonly ImageTextureNode diffuse_texture = new ImageTextureNode("diffuse_texture");

		private readonly TransparentBsdfNode diffuse_texture_alpha = new TransparentBsdfNode("diffuse_texture_alpha");

		private readonly MixClosureNode diffuse_texture_alpha_mix = new MixClosureNode("diffuse_texture_alpha_mix");

		/// <summary>
		/// Driven by diffuse_texture_fac mixes black and texture input color. 0.0f
		/// means no texture color, 1.0f means full texture color
		/// </summary>
		private readonly MixNode diffuse_texture_amount = new MixNode("diffuse_texture_amount");

		private readonly MixNode diffuse_texture_or_color_blend = new MixNode("diffuse_texture_or_color_blend");

		/// <summary>
		/// diffuse_texture_fac controls the amount of diffuse texture over diffuse color.
		/// 
		/// 0.0f means only diffuse color and 1.0f means only diffuse texture.
		/// </summary>
		private readonly ValueNode diffuse_texture_fac = new ValueNode("diffuse_texture_fac");

		/// <summary>
		/// Math node used to determine factor for diffuse color
		/// </summary>
		private readonly MathNode diffuse_texture_fac_inv = new MathNode("diffuse_texture_fac_inv");

		private readonly MathNode diffuse_transparency_add = new MathNode("diffuse_transparency_add");

		private readonly MathNode diffuse_transparency_and_fresnel_final = new MathNode("diffuse_transparency_and_fresnel_final");

		private readonly MathNode diffuse_transparency_effective = new MathNode("diffuse_transparency_effective");

		private readonly MathNode diffuse_transparency_mult = new MathNode("diffuse_transparency_mult");

		private readonly MathNode diffuse_transparency_sub = new MathNode("diffuse_transparency_sub");

		private readonly MathNode diffuse_transparency_sub_not_zero = new MathNode("diffuse_transparency_sub_not_zero");

		private readonly MathNode diffuse_use_alpha = new MathNode("diffuse_use_alpha");

		private readonly EmissionNode emission_bsdf = new EmissionNode("emission_bsdf");

		private readonly ColorNode emission_color = new ColorNode("emission_color");

		private readonly ValueNode fresnel_input = new ValueNode("fresnel_input");

		private readonly MathNode fresnel_reflections = new MathNode("fresnel_reflections");

		private readonly FresnelNode fresnel_to_fac = new FresnelNode("fresnel_to_fac");

		private readonly ValueNode reflection_amount = new ValueNode("reflection_amount");

		private readonly GlossyBsdfNode reflection_bsdf = new GlossyBsdfNode("reflection_bsdf");

		private readonly ColorNode reflection_color = new ColorNode("reflection_color");

		private readonly MixNode reflection_effective_color = new MixNode("reflection_effective_color") {BlendType = MixNode.BlendTypes.Add};

		private readonly MathNode reflection_fresnel_inv = new MathNode("reflection_fresnel_inv");

		private readonly MathNode reflection_fresnel_inv_mult = new MathNode("reflection_fresnel_inv_mult");

		private readonly MixClosureNode reflection_fresnel_mod = new MixClosureNode("reflection_fresnel_mod");

		private readonly MathNode shadeless = new MathNode("shadeless");

		/// <summary>
		/// Texture coordinate input node for driving UV
		/// </summary>
		private readonly TextureCoordinateNode diff_texture_coord = new TextureCoordinateNode("diffuse texture_uv");
		private readonly TextureCoordinateNode bump_texture_coord = new TextureCoordinateNode("bump texture_uv");
		private readonly TextureCoordinateNode transp_texture_coord = new TextureCoordinateNode("transp texture_uv");

		private readonly ValueNode transparency_amount = new ValueNode("transparency_amount");

		private readonly ColorNode transparency_color = new ColorNode("transparency_color");

		private readonly MathNode transparency_inv = new MathNode("transparency_inv");

		private readonly RefractionBsdfNode transparency_refraction_bsdf = new RefractionBsdfNode("transparency_refraction_bsdf");

		private readonly MixNode transparency_refraction_effective_color = new MixNode("transparency_refraction_effective_color");

		private readonly MixClosureNode transparency_refraction_fresnel_mod = new MixClosureNode("transparency_refraction_fresnel_mod");

		private readonly MathNode transparency_refraction_fresnel_toggle_mult = new MathNode("transparency_refraction_fresnel_toggle_mult");

		private readonly ImageTextureNode transparency_texture = new ImageTextureNode("transparency texture");

		private readonly RgbToLuminanceNode transptex_to_bw = new RgbToLuminanceNode("transparency texture to luminance");

		private readonly MixClosureNode mix_transp_tex = new MixClosureNode("mix transp tex");

		private readonly TransparentBsdfNode transptex_transparent_bsdf_node = new TransparentBsdfNode("transp for transptex");


		public RhinoFull(Client client, CyclesShader intermediate) : this(client, intermediate, intermediate.Name)
		{
		}

		public RhinoFull(Client client, CyclesShader intermediate, string name) : base(client, intermediate)
		{
			m_shader = new Shader(m_client, Shader.ShaderType.Material)
			{
				UseMis = true,
				UseTransparentShadow = true,
				HeterogeneousVolume = false,
				Name = name
			};
		}

		public override Shader GetShader()
		{
			bump_normals.ins.Strength.Value = 0.0f;
			bump_normals.ins.Distance.Value = 0.0f;
			bump_normals.ins.Normal.Value = new float4(0.0f, 0.0f, 0.0f);

			if (m_original.HasBumpTexture)
			{
				bump_normals.ins.Strength.Value = 1.0f;
				bump_normals.ins.Distance.Value = 0.1f;
				RenderEngine.SetTextureImage(bump_texture, m_original.BumpTexture);
			}

			diffuse_alpha_inv.ins.Value1.Value = 1.0f;
			diffuse_alpha_inv.Operation = MathNode.Operations.Subtract;

			diffuse_alpha_mult.Operation = MathNode.Operations.Multiply;

			diffuse_bsdf.ins.Roughness.Value = 0.0f;

			diffuse_color.Value = m_original.DiffuseColor;

			diffuse_texture_alpha.ins.Color.Value = new float4(1.0f, 1.0f, 1.0f);


			diffuse_texture_amount.ins.Color1.Value = new float4(0.0f, 0.0f, 0.0f);


			diffuse_texture_fac.Value = 0.0f;
			if (m_original.HasDiffuseTexture)
			{
				diffuse_texture_fac.Value = m_original.DiffuseTexture.Amount;
				RenderEngine.SetTextureImage(diffuse_texture, m_original.DiffuseTexture);
			}

			diffuse_texture_fac_inv.ins.Value1.Value = 1.0f;
			diffuse_texture_fac_inv.Operation = MathNode.Operations.Subtract;

			diffuse_transparency_add.Operation = MathNode.Operations.Add;

			diffuse_transparency_and_fresnel_final.Operation = MathNode.Operations.Add;

			diffuse_transparency_effective.Operation = MathNode.Operations.Multiply;

			diffuse_transparency_mult.Operation = MathNode.Operations.Multiply;

			diffuse_transparency_sub.ins.Value1.Value = 1.0f;
			diffuse_transparency_sub.Operation = MathNode.Operations.Subtract;

			diffuse_transparency_sub_not_zero.ins.Value2.Value = 0.0f;
			diffuse_transparency_sub_not_zero.Operation = MathNode.Operations.Greater_Than;

			diffuse_use_alpha.ins.Value1.Value = m_original.DiffuseTexture.UseAlphaAsFloat;
			diffuse_use_alpha.ins.Value2.Value = 0.5f;
			diffuse_use_alpha.Operation = MathNode.Operations.Greater_Than;


			emission_color.Value = m_original.EmissionColor;

			fresnel_input.Value = m_original.FresnelReflections ? m_original.FresnelIOR : m_original.IOR;

			fresnel_reflections.ins.Value1.Value = m_original.FresnelReflections ? 1.0f : 0.0f;
			fresnel_reflections.ins.Value2.Value = 0.5f;
			fresnel_reflections.Operation = MathNode.Operations.Greater_Than;

			reflection_amount.Value = m_original.Reflectivity;

			reflection_bsdf.ins.Roughness.Value = m_original.ReflectionRoughness;
			reflection_bsdf.Distribution = m_original.ReflectionRoughness > 0.0f ? GlossyBsdfNode.GlossyDistribution.GGX : GlossyBsdfNode.GlossyDistribution.Sharp;

			reflection_color.Value = m_original.ReflectionColor;

			reflection_fresnel_inv.ins.Value1.Value = 1.0f;
			reflection_fresnel_inv.Operation = MathNode.Operations.Subtract;

			reflection_fresnel_inv_mult.Operation = MathNode.Operations.Multiply;

			shadeless.ins.Value1.Value = m_original.ShadelessAsFloat;
			shadeless.ins.Value2.Value = 0.5f;
			shadeless.Operation = MathNode.Operations.Greater_Than;

			transparency_amount.Value = m_original.Transparency;

			transparency_color.Value = m_original.TransparencyColor;

			transparency_inv.ins.Value1.Value = 1.0f;
			transparency_inv.Operation = MathNode.Operations.Subtract;

			transparency_refraction_bsdf.ins.Roughness.Value = m_original.RefractionRoughness; //0.0f;
			transparency_refraction_bsdf.Distribution = RefractionBsdfNode.RefractionDistribution.Sharp;

			transparency_refraction_fresnel_toggle_mult.Operation = MathNode.Operations.Multiply;

			transparent_bsdf.ins.Color.Value = m_original.TransparencyColor;

			m_shader.AddNode(bump_normals);
			m_shader.AddNode(bump_rgb_to_bw);
			m_shader.AddNode(bump_texture);
			m_shader.AddNode(comp_diffuse);
			m_shader.AddNode(comp_emission);
			m_shader.AddNode(comp_reflection);
			m_shader.AddNode(comp_transparency);
			m_shader.AddNode(diffuse_alpha_inv);
			m_shader.AddNode(diffuse_alpha_mult);
			m_shader.AddNode(diffuse_bsdf);
			m_shader.AddNode(diffuse_color);
			m_shader.AddNode(diffuse_color_amount);
			m_shader.AddNode(diffuse_effective_color);
			m_shader.AddNode(diffuse_light_path_for_shadeless);
			m_shader.AddNode(diffuse_or_shadeless);
			m_shader.AddNode(diffuse_shadeless);
			m_shader.AddNode(diffuse_texture);
			m_shader.AddNode(diffuse_texture_alpha);
			m_shader.AddNode(diffuse_texture_alpha_mix);
			m_shader.AddNode(diffuse_texture_amount);
			m_shader.AddNode(diffuse_texture_or_color_blend);
			m_shader.AddNode(diffuse_texture_fac);
			m_shader.AddNode(diffuse_texture_fac_inv);
			m_shader.AddNode(diffuse_transparency_add);
			m_shader.AddNode(diffuse_transparency_and_fresnel_final);
			m_shader.AddNode(diffuse_transparency_effective);
			m_shader.AddNode(diffuse_transparency_mult);
			m_shader.AddNode(diffuse_transparency_sub);
			m_shader.AddNode(diffuse_transparency_sub_not_zero);
			m_shader.AddNode(diffuse_use_alpha);
			m_shader.AddNode(emission_bsdf);
			m_shader.AddNode(emission_color);
			m_shader.AddNode(fresnel_input);
			m_shader.AddNode(fresnel_reflections);
			m_shader.AddNode(fresnel_to_fac);
			m_shader.AddNode(reflection_amount);
			m_shader.AddNode(reflection_bsdf);
			m_shader.AddNode(reflection_color);
			m_shader.AddNode(reflection_effective_color);
			m_shader.AddNode(reflection_fresnel_inv);
			m_shader.AddNode(reflection_fresnel_inv_mult);
			m_shader.AddNode(reflection_fresnel_mod);
			m_shader.AddNode(shadeless);
			m_shader.AddNode(diff_texture_coord);
			m_shader.AddNode(bump_texture_coord);
			m_shader.AddNode(transp_texture_coord);
			m_shader.AddNode(transparency_amount);
			m_shader.AddNode(transparency_color);
			m_shader.AddNode(transparency_inv);
			m_shader.AddNode(transparency_refraction_bsdf);
			m_shader.AddNode(transparency_refraction_effective_color);
			m_shader.AddNode(transparency_refraction_fresnel_mod);
			m_shader.AddNode(transparency_refraction_fresnel_toggle_mult);
			m_shader.AddNode(transptex_to_bw);
			m_shader.AddNode(transparency_texture);
			m_shader.AddNode(mix_transp_tex);
			m_shader.AddNode(transptex_transparent_bsdf_node);
			m_shader.AddNode(transparent_bsdf);
			m_shader.AddNode(transparent_mix_closure_node);
			m_shader.AddNode(transparent_lightpath_max);
			m_shader.AddNode(transparent_lightpath_fac);

			bump_normals.outs.Normal.Connect(diffuse_bsdf.ins.Normal);
			bump_normals.outs.Normal.Connect(reflection_bsdf.ins.Normal);
			bump_normals.outs.Normal.Connect(transparency_refraction_bsdf.ins.Normal);
			bump_rgb_to_bw.outs.Val.Connect(bump_normals.ins.Height);
			bump_texture.outs.Color.Connect(bump_rgb_to_bw.ins.Color);
			diffuse_alpha_inv.outs.Value.Connect(diffuse_alpha_mult.ins.Value1);
			diffuse_alpha_mult.outs.Value.Connect(diffuse_texture_alpha_mix.ins.Fac);
			diffuse_bsdf.outs.BSDF.Connect(diffuse_or_shadeless.ins.Closure1);
			diffuse_color.outs.Color.Connect(diffuse_color_amount.ins.Color2);
			diffuse_color_amount.outs.Color.Connect(diffuse_texture_or_color_blend.ins.Color2);
			diffuse_effective_color.outs.Color.Connect(diffuse_bsdf.ins.Color);
			diffuse_effective_color.outs.Color.Connect(diffuse_shadeless.ins.Color);
			diffuse_light_path_for_shadeless.outs.IsCameraRay.Connect(diffuse_shadeless.ins.Strength);
			diffuse_or_shadeless.outs.Closure.Connect(diffuse_texture_alpha_mix.ins.Closure1);
			diffuse_shadeless.outs.Emission.Connect(diffuse_or_shadeless.ins.Closure2);
			diffuse_texture.outs.Alpha.Connect(diffuse_alpha_inv.ins.Value2);
			diffuse_texture.outs.Color.Connect(diffuse_texture_amount.ins.Color2);
			diffuse_texture_alpha.outs.BSDF.Connect(diffuse_texture_alpha_mix.ins.Closure2);
			diffuse_texture_amount.outs.Color.Connect(diffuse_texture_or_color_blend.ins.Color1);
			diffuse_texture_or_color_blend.outs.Color.Connect(diffuse_effective_color.ins.Color2);
			diffuse_texture_fac.outs.Value.Connect(diffuse_texture_amount.ins.Fac);
			diffuse_texture_fac.outs.Value.Connect(diffuse_texture_fac_inv.ins.Value2);
			diffuse_texture_fac_inv.outs.Value.Connect(diffuse_color_amount.ins.Fac);
			diffuse_texture_fac_inv.outs.Value.Connect(diffuse_texture_or_color_blend.ins.Fac);
			diffuse_transparency_add.outs.Value.Connect(diffuse_transparency_sub.ins.Value2);
			diffuse_transparency_and_fresnel_final.outs.Value.Connect(diffuse_effective_color.ins.Fac);
			diffuse_transparency_effective.outs.Value.Connect(diffuse_transparency_and_fresnel_final.ins.Value1);
			diffuse_transparency_mult.outs.Value.Connect(diffuse_transparency_add.ins.Value2);
			diffuse_transparency_sub.outs.Value.Connect(diffuse_transparency_effective.ins.Value1);
			diffuse_transparency_sub.outs.Value.Connect(diffuse_transparency_sub_not_zero.ins.Value1);
			diffuse_transparency_sub_not_zero.outs.Value.Connect(diffuse_transparency_effective.ins.Value2);
			diffuse_use_alpha.outs.Value.Connect(diffuse_alpha_mult.ins.Value2);
			emission_color.outs.Color.Connect(emission_bsdf.ins.Color);
			fresnel_input.outs.Value.Connect(fresnel_to_fac.ins.IOR);
			fresnel_input.outs.Value.Connect(transparency_refraction_bsdf.ins.IOR);
			fresnel_reflections.outs.Value.Connect(diffuse_transparency_and_fresnel_final.ins.Value2);
			fresnel_reflections.outs.Value.Connect(reflection_fresnel_inv_mult.ins.Value2);
			fresnel_reflections.outs.Value.Connect(transparency_refraction_fresnel_toggle_mult.ins.Value2);
			fresnel_to_fac.outs.Fac.Connect(reflection_fresnel_inv.ins.Value2);
			fresnel_to_fac.outs.Fac.Connect(transparency_refraction_fresnel_toggle_mult.ins.Value1);
			reflection_amount.outs.Value.Connect(reflection_effective_color.ins.Fac);
			reflection_amount.outs.Value.Connect(diffuse_transparency_mult.ins.Value2);
			reflection_bsdf.outs.BSDF.Connect(reflection_fresnel_mod.ins.Closure1);
			reflection_color.outs.Color.Connect(reflection_effective_color.ins.Color2);
			reflection_effective_color.outs.Color.Connect(reflection_bsdf.ins.Color);
			reflection_fresnel_inv.outs.Value.Connect(reflection_fresnel_inv_mult.ins.Value1);
			reflection_fresnel_inv_mult.outs.Value.Connect(reflection_fresnel_mod.ins.Fac);
			shadeless.outs.Value.Connect(diffuse_or_shadeless.ins.Fac);

			RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, bump_texture, bump_texture_coord);
			RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, diffuse_texture, diff_texture_coord);
			RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, transparency_texture, transp_texture_coord);

			transparency_amount.outs.Value.Connect(transparency_refraction_effective_color.ins.Fac);
			transparency_amount.outs.Value.Connect(diffuse_transparency_add.ins.Value1);
			transparency_amount.outs.Value.Connect(transparency_inv.ins.Value2);
			transparency_color.outs.Color.Connect(transparency_refraction_effective_color.ins.Color2);
			transparency_inv.outs.Value.Connect(emission_bsdf.ins.Strength);
			transparency_inv.outs.Value.Connect(diffuse_transparency_mult.ins.Value1);
			transparency_refraction_bsdf.outs.BSDF.Connect(transparency_refraction_fresnel_mod.ins.Closure1);
			transparency_refraction_effective_color.outs.Color.Connect(transparency_refraction_bsdf.ins.Color);
			transparency_refraction_fresnel_toggle_mult.outs.Value.Connect(transparency_refraction_fresnel_mod.ins.Fac);

			diffuse_light_path_for_shadeless.outs.IsShadowRay.Connect(transparent_lightpath_max.ins.Value1);
			diffuse_light_path_for_shadeless.outs.IsReflectionRay.Connect(transparent_lightpath_max.ins.Value2);
			transparent_lightpath_max.outs.Value.Connect(transparent_lightpath_fac.ins.Value1);
			transparent_lightpath_fac.ins.Value2.Value=m_original.Transparency;
			transparent_lightpath_fac.outs.Value.Connect(transparent_mix_closure_node.ins.Fac);

			if (m_original.NoTransparency)
			{
				diffuse_texture_alpha_mix.outs.Closure.Connect(comp_diffuse.ins.Closure2);
			}
			else
			{
				diffuse_texture_alpha_mix.outs.Closure.Connect(transparent_mix_closure_node.ins.Closure1);
				transparent_mix_closure_node.outs.Closure.Connect(comp_diffuse.ins.Closure2);
			}

			comp_diffuse.outs.Closure.Connect(comp_transparency.ins.Closure1);
			transparency_refraction_fresnel_mod.outs.Closure.Connect(comp_transparency.ins.Closure2);

			comp_transparency.outs.Closure.Connect(comp_reflection.ins.Closure1);

			reflection_fresnel_mod.outs.Closure.Connect(comp_reflection.ins.Closure2);

			comp_reflection.outs.Closure.Connect(comp_emission.ins.Closure1);
			emission_bsdf.outs.Emission.Connect(comp_emission.ins.Closure2);

			transparent_bsdf.outs.BSDF.Connect(transparent_mix_closure_node.ins.Closure2);

			if (m_original.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture, m_original.TransparencyTexture);
				transparency_texture.outs.Color.Connect(transptex_to_bw.ins.Color);

				transptex_to_bw.outs.Val.Connect(mix_transp_tex.ins.Fac);
				comp_emission.outs.Closure.Connect(mix_transp_tex.ins.Closure2);
				transptex_transparent_bsdf_node.outs.BSDF.Connect(mix_transp_tex.ins.Closure1);

				mix_transp_tex.outs.Closure.Connect(m_shader.Output.ins.Surface);
			}
			else
			{
				comp_emission.outs.Closure.Connect(m_shader.Output.ins.Surface);
			}

			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
