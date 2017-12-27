/**
Copyright 2014-2017 Robert McNeel and Associates

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
using ccl.ShaderNodes.Sockets;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoFullNxt : RhinoShader
	{
		public RhinoFullNxt(Client client, CyclesShader intermediate) : this(client, intermediate, null, intermediate.Front.Name, true)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing, bool recreate) : this(client, intermediate, existing, intermediate.Front.Name, recreate)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing) : this(client, intermediate, existing, intermediate.Front.Name, true)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing, string name, bool recreate) : base(client, intermediate, name, existing, recreate)
		{
		}

		public ClosureSocket GetClosureSocket()
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

				//flipper.outs.Closure.Connect(m_shader.Output.ins.Surface);
				return flipper.GetClosureSocket();
			}
			else
			{
				var last = GetShaderPart(m_original.Front);
				var lastclosure = last.GetClosureSocket();

				//lastclosure.Connect(m_shader.Output.ins.Surface);
				return lastclosure;
			}

		}

		public override Shader GetShader()
		{
			if (RcCore.It.EngineSettings.DebugSimpleShaders)
			{
				ccl.ShaderNodes.DiffuseBsdfNode diff = new DiffuseBsdfNode("x");
				diff.ins.Color.Value = new float4(0.8f, 0.6f, 0.5f, 1.0f);
				m_shader.AddNode(diff);
				diff.outs.BSDF.Connect(m_shader.Output.ins.Surface);
			}
			else
			{
				var lc = GetClosureSocket();
				lc.Connect(m_shader.Output.ins.Surface);
			}
			m_shader.FinalizeGraph();
			return m_shader;
		}

		private ShaderNode GetShaderPart(ShaderBody part)
		{
			var texcoord209 = new TextureCoordinateNode("texcoord");

			var invert_transparency192 = new MathSubtract("invert_transparency");
				invert_transparency192.ins.Value1.Value = 1f;
				invert_transparency192.ins.Value2.Value = part.Transparency;
				invert_transparency192.Operation = MathNode.Operations.Subtract;
				invert_transparency192.UseClamp = false;

			var weight_diffuse_amount_by_transparency_inv193 = new MathMultiply("weight_diffuse_amount_by_transparency_inv");
				weight_diffuse_amount_by_transparency_inv193.ins.Value1.Value = part.DiffuseTexture.Amount;
				weight_diffuse_amount_by_transparency_inv193.Operation = MathNode.Operations.Multiply;
				weight_diffuse_amount_by_transparency_inv193.UseClamp = false;

			var diff_tex_amount_multiplied_with_inv_transparency309 = new MathMultiply("diff_tex_amount_multiplied_with_inv_transparency");
				diff_tex_amount_multiplied_with_inv_transparency309.Operation = MathNode.Operations.Multiply;
				diff_tex_amount_multiplied_with_inv_transparency309.UseClamp = false;

			var diffuse_texture210 = new ImageTextureNode("diffuse_texture");
				diffuse_texture210.Projection = TextureNode.TextureProjection.Flat;
				diffuse_texture210.ColorSpace = TextureNode.TextureColorSpace.None;
				diffuse_texture210.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
				diffuse_texture210.Interpolation = InterpolationType.Smart;
				diffuse_texture210.UseAlpha = true;
				diffuse_texture210.IsLinear = false;

			var diff_tex_weighted_alpha_for_basecol_mix310 = new MathMultiply("diff_tex_weighted_alpha_for_basecol_mix");
				diff_tex_weighted_alpha_for_basecol_mix310.Operation = MathNode.Operations.Multiply;
				diff_tex_weighted_alpha_for_basecol_mix310.UseClamp = false;

			var diffuse_base_color_through_alpha308 = new MixNode("diffuse_base_color_through_alpha");
				diffuse_base_color_through_alpha308.ins.Color1.Value = part.BaseColor;
				diffuse_base_color_through_alpha308.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
				diffuse_base_color_through_alpha308.UseClamp = false;

			var use_alpha_weighted_with_modded_amount195 = new MathMultiply("use_alpha_weighted_with_modded_amount");
				use_alpha_weighted_with_modded_amount195.ins.Value1.Value = part.DiffuseTexture.UseAlphaAsFloat;
				use_alpha_weighted_with_modded_amount195.Operation = MathNode.Operations.Multiply;
				use_alpha_weighted_with_modded_amount195.UseClamp = false;

			var bump_texture211 = new ImageTextureNode("bump_texture");
				bump_texture211.Projection = TextureNode.TextureProjection.Flat;
				bump_texture211.ColorSpace = TextureNode.TextureColorSpace.None;
				bump_texture211.Extension = TextureNode.TextureExtension.Repeat;
				bump_texture211.Interpolation = InterpolationType.Smart;
				bump_texture211.UseAlpha = true;
				bump_texture211.IsLinear = false;

			var bump_texture_to_bw212 = new RgbToBwNode("bump_texture_to_bw");

			var bump_amount196 = new MathMultiply("bump_amount");
				bump_amount196.ins.Value1.Value = 4.66f;
				bump_amount196.ins.Value2.Value = part.BumpTexture.Amount;
				bump_amount196.Operation = MathNode.Operations.Multiply;
				bump_amount196.UseClamp = false;

			var diffuse_base_color_through_alpha246 = new MixNode("diffuse_base_color_through_alpha");
				diffuse_base_color_through_alpha246.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
				diffuse_base_color_through_alpha246.UseClamp = false;

			var bump213 = new BumpNode("bump");
				bump213.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				bump213.ins.Distance.Value = 0.1f;

			var light_path234 = new LightPathNode("light_path");

			var final_diffuse214 = new DiffuseBsdfNode("final_diffuse");
				final_diffuse214.ins.Roughness.Value = 0f;

			var shadeless_bsdf215 = new EmissionNode("shadeless_bsdf");
				shadeless_bsdf215.ins.Strength.Value = 1f;

			var shadeless_on_cameraray248 = new MathMultiply("shadeless_on_cameraray");
				shadeless_on_cameraray248.ins.Value2.Value = part.ShadelessAsFloat;
				shadeless_on_cameraray248.Operation = MathNode.Operations.Multiply;
				shadeless_on_cameraray248.UseClamp = false;

			var attenuated_reflection_color216 = new MixNode("attenuated_reflection_color");
				attenuated_reflection_color216.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_reflection_color216.ins.Color2.Value = part.ReflectionColorGamma;
				attenuated_reflection_color216.ins.Fac.Value = part.Reflectivity;
				attenuated_reflection_color216.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
				attenuated_reflection_color216.UseClamp = false;

			var fresnel_based_on_constant217 = new FresnelNode("fresnel_based_on_constant");
				fresnel_based_on_constant217.ins.IOR.Value = part.FresnelIOR;

			var simple_reflection218 = new CombineRgbNode("simple_reflection");
				simple_reflection218.ins.R.Value = part.Reflectivity;
				simple_reflection218.ins.G.Value = 0f;
				simple_reflection218.ins.B.Value = 0f;

			var fresnel_reflection219 = new CombineRgbNode("fresnel_reflection");
				fresnel_reflection219.ins.G.Value = 0f;
				fresnel_reflection219.ins.B.Value = 0f;

			var fresnel_reflection_if_reflection_used197 = new MathMultiply("fresnel_reflection_if_reflection_used");
				fresnel_reflection_if_reflection_used197.ins.Value1.Value = part.Reflectivity;
				fresnel_reflection_if_reflection_used197.ins.Value2.Value = part.FresnelReflectionsAsFloat;
				fresnel_reflection_if_reflection_used197.Operation = MathNode.Operations.Multiply;
				fresnel_reflection_if_reflection_used197.UseClamp = false;

			var select_reflection_or_fresnel_reflection220 = new MixNode("select_reflection_or_fresnel_reflection");
				select_reflection_or_fresnel_reflection220.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
				select_reflection_or_fresnel_reflection220.UseClamp = false;

			var shadeless221 = new MixClosureNode("shadeless");

			var glossy222 = new GlossyBsdfNode("glossy");
				glossy222.ins.Roughness.Value = part.ReflectionRoughnessPow2;

			var reflection_factor223 = new SeparateRgbNode("reflection_factor");

			var attennuated_refraction_color224 = new MixNode("attennuated_refraction_color");
				attennuated_refraction_color224.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attennuated_refraction_color224.ins.Color2.Value = part.TransparencyColorGamma;
				attennuated_refraction_color224.ins.Fac.Value = part.Transparency;
				attennuated_refraction_color224.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
				attennuated_refraction_color224.UseClamp = false;

			var refraction225 = new RefractionBsdfNode("refraction");
				refraction225.ins.Roughness.Value = part.RefractionRoughnessPow2;
				refraction225.ins.IOR.Value = part.IOR;
				refraction225.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;

			var diffuse_plus_glossy226 = new MixClosureNode("diffuse_plus_glossy");

			var blend_in_transparency227 = new MixClosureNode("blend_in_transparency");
				blend_in_transparency227.ins.Fac.Value = part.Transparency;

			var separate_envmap_texco228 = new SeparateXyzNode("separate_envmap_texco");

			var flip_sign_envmap_texco_y198 = new MathMultiply("flip_sign_envmap_texco_y");
				flip_sign_envmap_texco_y198.ins.Value2.Value = -1f;
				flip_sign_envmap_texco_y198.Operation = MathNode.Operations.Multiply;
				flip_sign_envmap_texco_y198.UseClamp = false;

			var recombine_envmap_texco229 = new CombineXyzNode("recombine_envmap_texco");

			var environment_texture230 = new ImageTextureNode("environment_texture");
				environment_texture230.Projection = TextureNode.TextureProjection.Flat;
				environment_texture230.ColorSpace = TextureNode.TextureColorSpace.None;
				environment_texture230.Extension = TextureNode.TextureExtension.Repeat;
				environment_texture230.Interpolation = InterpolationType.Smart;
				environment_texture230.UseAlpha = true;
				environment_texture230.IsLinear = false;

			var attenuated_environment_color231 = new MixNode("attenuated_environment_color");
				attenuated_environment_color231.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_environment_color231.ins.Fac.Value = part.EnvironmentTexture.Amount;
				attenuated_environment_color231.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
				attenuated_environment_color231.UseClamp = false;

			var diffuse_glossy_and_refraction232 = new MixClosureNode("diffuse_glossy_and_refraction");
				diffuse_glossy_and_refraction232.ins.Fac.Value = part.Transparency;

			var environment_map_diffuse233 = new DiffuseBsdfNode("environment_map_diffuse");
				environment_map_diffuse233.ins.Roughness.Value = 0f;
				environment_map_diffuse233.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness199 = new MathSubtract("invert_roughness");
				invert_roughness199.ins.Value1.Value = 1f;
				invert_roughness199.ins.Value2.Value = part.RefractionRoughnessPow2;
				invert_roughness199.Operation = MathNode.Operations.Subtract;
				invert_roughness199.UseClamp = false;

			var multiply_transparency200 = new MathMultiply("multiply_transparency");
				multiply_transparency200.ins.Value2.Value = part.Transparency;
				multiply_transparency200.Operation = MathNode.Operations.Multiply;
				multiply_transparency200.UseClamp = false;

			var multiply_with_shadowray201 = new MathMultiply("multiply_with_shadowray");
				multiply_with_shadowray201.Operation = MathNode.Operations.Multiply;
				multiply_with_shadowray201.UseClamp = false;

			var custom_environment_blend235 = new MixClosureNode("custom_environment_blend");
				custom_environment_blend235.ins.Fac.Value = part.EnvironmentTexture.Amount;

			var coloured_shadow_trans_color236 = new TransparentBsdfNode("coloured_shadow_trans_color");

			var weight_for_shadowray_coloured_shadow202 = new MathMultiply("weight_for_shadowray_coloured_shadow");
				weight_for_shadowray_coloured_shadow202.ins.Value2.Value = 1f;
				weight_for_shadowray_coloured_shadow202.Operation = MathNode.Operations.Multiply;
				weight_for_shadowray_coloured_shadow202.UseClamp = false;

			var diffuse_from_emission_color249 = new DiffuseBsdfNode("diffuse_from_emission_color");
				diffuse_from_emission_color249.ins.Color.Value = part.EmissionColorGamma;
				diffuse_from_emission_color249.ins.Roughness.Value = 0f;
				diffuse_from_emission_color249.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_emission251 = new EmissionNode("shadeless_emission");
				shadeless_emission251.ins.Color.Value = part.EmissionColorGamma;
				shadeless_emission251.ins.Strength.Value = 1f;

			var coloured_shadow_mix_custom239 = new MixClosureNode("coloured_shadow_mix_custom");

			var diffuse_or_shadeless_emission252 = new MixClosureNode("diffuse_or_shadeless_emission");

			var one_if_usealphatransp_turned_off306 = new MathLess_Than("one_if_usealphatransp_turned_off");
				one_if_usealphatransp_turned_off306.ins.Value1.Value = part.DiffuseTexture.UseAlphaAsFloat;
				one_if_usealphatransp_turned_off306.ins.Value2.Value = 1f;
				one_if_usealphatransp_turned_off306.Operation = MathNode.Operations.Less_Than;
				one_if_usealphatransp_turned_off306.UseClamp = false;

			var max_of_texalpha_or_usealpha307 = new MathMaximum("max_of_texalpha_or_usealpha");
				max_of_texalpha_or_usealpha307.Operation = MathNode.Operations.Maximum;
				max_of_texalpha_or_usealpha307.UseClamp = false;

			var invert_alpha194 = new MathSubtract("invert_alpha");
				invert_alpha194.ins.Value1.Value = 1f;
				invert_alpha194.Operation = MathNode.Operations.Subtract;
				invert_alpha194.UseClamp = false;

			var transparency_texture237 = new ImageTextureNode("transparency_texture");
				transparency_texture237.Projection = TextureNode.TextureProjection.Flat;
				transparency_texture237.ColorSpace = TextureNode.TextureColorSpace.None;
				transparency_texture237.Extension = TextureNode.TextureExtension.Repeat;
				transparency_texture237.Interpolation = InterpolationType.Smart;
				transparency_texture237.UseAlpha = true;
				transparency_texture237.IsLinear = false;

			var transpluminance238 = new RgbToLuminanceNode("transpluminance");

			var invert_luminence203 = new MathSubtract("invert_luminence");
				invert_luminence203.ins.Value1.Value = 1f;
				invert_luminence203.Operation = MathNode.Operations.Subtract;
				invert_luminence203.UseClamp = false;

			var transparency_texture_amount204 = new MathMultiply("transparency_texture_amount");
				transparency_texture_amount204.ins.Value2.Value = part.TransparencyTexture.Amount;
				transparency_texture_amount204.Operation = MathNode.Operations.Multiply;
				transparency_texture_amount204.UseClamp = false;

			var toggle_diffuse_texture_alpha_usage205 = new MathMultiply("toggle_diffuse_texture_alpha_usage");
				toggle_diffuse_texture_alpha_usage205.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
				toggle_diffuse_texture_alpha_usage205.Operation = MathNode.Operations.Multiply;
				toggle_diffuse_texture_alpha_usage205.UseClamp = false;

			var toggle_transparency_texture206 = new MathMultiply("toggle_transparency_texture");
				toggle_transparency_texture206.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
				toggle_transparency_texture206.Operation = MathNode.Operations.Multiply;
				toggle_transparency_texture206.UseClamp = false;

			var add_emission_to_final250 = new AddClosureNode("add_emission_to_final");

			var transparent240 = new TransparentBsdfNode("transparent");
				transparent240.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var add_diffuse_texture_alpha207 = new MathAdd("add_diffuse_texture_alpha");
				add_diffuse_texture_alpha207.Operation = MathNode.Operations.Add;
				add_diffuse_texture_alpha207.UseClamp = false;

			var custom_alpha_cutter241 = new MixClosureNode("custom_alpha_cutter");

			var principledbsdf242 = new PrincipledBsdfNode("principledbsdf");
				principledbsdf242.Distribution = PrincipledBsdfNode.Distributions.Multiscatter_GGX;
				principledbsdf242.ins.BaseColor.Value = part.BaseColor;
				principledbsdf242.ins.Subsurface.Value = 0f;
				principledbsdf242.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf242.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				principledbsdf242.ins.Metallic.Value = part.Metallic;
				principledbsdf242.ins.Specular.Value = part.Specular;
				principledbsdf242.ins.SpecularTint.Value = part.SpecularTint;
				principledbsdf242.ins.Roughness.Value = part.ReflectionRoughness;
				principledbsdf242.ins.Anisotropic.Value = 0f;
				principledbsdf242.ins.AnisotropicRotation.Value = 0f;
				principledbsdf242.ins.Sheen.Value = part.Sheen;
				principledbsdf242.ins.SheenTint.Value = part.SheenTint;
				principledbsdf242.ins.Clearcoat.Value = part.ClearCoat;
				principledbsdf242.ins.ClearcoatGloss.Value = part.ClearCoatGloss;
				principledbsdf242.ins.IOR.Value = part.IOR;
				principledbsdf242.ins.Transmission.Value = part.Transparency;
				principledbsdf242.ins.TransmissionRoughness.Value = part.RefractionRoughness;
				principledbsdf242.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var coloured_shadow_mix_glass_principled243 = new MixClosureNode("coloured_shadow_mix_glass_principled");
			

			m_shader.AddNode(texcoord209);
			m_shader.AddNode(invert_transparency192);
			m_shader.AddNode(weight_diffuse_amount_by_transparency_inv193);
			m_shader.AddNode(diff_tex_amount_multiplied_with_inv_transparency309);
			m_shader.AddNode(diffuse_texture210);
			m_shader.AddNode(diff_tex_weighted_alpha_for_basecol_mix310);
			m_shader.AddNode(diffuse_base_color_through_alpha308);
			m_shader.AddNode(use_alpha_weighted_with_modded_amount195);
			m_shader.AddNode(bump_texture211);
			m_shader.AddNode(bump_texture_to_bw212);
			m_shader.AddNode(bump_amount196);
			m_shader.AddNode(diffuse_base_color_through_alpha246);
			m_shader.AddNode(bump213);
			m_shader.AddNode(light_path234);
			m_shader.AddNode(final_diffuse214);
			m_shader.AddNode(shadeless_bsdf215);
			m_shader.AddNode(shadeless_on_cameraray248);
			m_shader.AddNode(attenuated_reflection_color216);
			m_shader.AddNode(fresnel_based_on_constant217);
			m_shader.AddNode(simple_reflection218);
			m_shader.AddNode(fresnel_reflection219);
			m_shader.AddNode(fresnel_reflection_if_reflection_used197);
			m_shader.AddNode(select_reflection_or_fresnel_reflection220);
			m_shader.AddNode(shadeless221);
			m_shader.AddNode(glossy222);
			m_shader.AddNode(reflection_factor223);
			m_shader.AddNode(attennuated_refraction_color224);
			m_shader.AddNode(refraction225);
			m_shader.AddNode(diffuse_plus_glossy226);
			m_shader.AddNode(blend_in_transparency227);
			m_shader.AddNode(separate_envmap_texco228);
			m_shader.AddNode(flip_sign_envmap_texco_y198);
			m_shader.AddNode(recombine_envmap_texco229);
			m_shader.AddNode(environment_texture230);
			m_shader.AddNode(attenuated_environment_color231);
			m_shader.AddNode(diffuse_glossy_and_refraction232);
			m_shader.AddNode(environment_map_diffuse233);
			m_shader.AddNode(invert_roughness199);
			m_shader.AddNode(multiply_transparency200);
			m_shader.AddNode(multiply_with_shadowray201);
			m_shader.AddNode(custom_environment_blend235);
			m_shader.AddNode(coloured_shadow_trans_color236);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow202);
			m_shader.AddNode(diffuse_from_emission_color249);
			m_shader.AddNode(shadeless_emission251);
			m_shader.AddNode(coloured_shadow_mix_custom239);
			m_shader.AddNode(diffuse_or_shadeless_emission252);
			m_shader.AddNode(one_if_usealphatransp_turned_off306);
			m_shader.AddNode(max_of_texalpha_or_usealpha307);
			m_shader.AddNode(invert_alpha194);
			m_shader.AddNode(transparency_texture237);
			m_shader.AddNode(transpluminance238);
			m_shader.AddNode(invert_luminence203);
			m_shader.AddNode(transparency_texture_amount204);
			m_shader.AddNode(toggle_diffuse_texture_alpha_usage205);
			m_shader.AddNode(toggle_transparency_texture206);
			m_shader.AddNode(add_emission_to_final250);
			m_shader.AddNode(transparent240);
			m_shader.AddNode(add_diffuse_texture_alpha207);
			m_shader.AddNode(custom_alpha_cutter241);
			m_shader.AddNode(principledbsdf242);
			m_shader.AddNode(coloured_shadow_mix_glass_principled243);
			

			invert_transparency192.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv193.ins.Value2);
			weight_diffuse_amount_by_transparency_inv193.outs.Value.Connect(diff_tex_amount_multiplied_with_inv_transparency309.ins.Value1);
			invert_transparency192.outs.Value.Connect(diff_tex_amount_multiplied_with_inv_transparency309.ins.Value2);
			texcoord209.outs.UV.Connect(diffuse_texture210.ins.Vector);
			diff_tex_amount_multiplied_with_inv_transparency309.outs.Value.Connect(diff_tex_weighted_alpha_for_basecol_mix310.ins.Value1);
			diffuse_texture210.outs.Alpha.Connect(diff_tex_weighted_alpha_for_basecol_mix310.ins.Value2);
			diffuse_texture210.outs.Color.Connect(diffuse_base_color_through_alpha308.ins.Color2);
			diff_tex_weighted_alpha_for_basecol_mix310.outs.Value.Connect(diffuse_base_color_through_alpha308.ins.Fac);
			weight_diffuse_amount_by_transparency_inv193.outs.Value.Connect(use_alpha_weighted_with_modded_amount195.ins.Value2);
			texcoord209.outs.UV.Connect(bump_texture211.ins.Vector);
			bump_texture211.outs.Color.Connect(bump_texture_to_bw212.ins.Color);
			diffuse_base_color_through_alpha308.outs.Color.Connect(diffuse_base_color_through_alpha246.ins.Color1);
			diffuse_texture210.outs.Color.Connect(diffuse_base_color_through_alpha246.ins.Color2);
			use_alpha_weighted_with_modded_amount195.outs.Value.Connect(diffuse_base_color_through_alpha246.ins.Fac);
			bump_texture_to_bw212.outs.Val.Connect(bump213.ins.Height);
			bump_amount196.outs.Value.Connect(bump213.ins.Strength);
			diffuse_base_color_through_alpha246.outs.Color.Connect(final_diffuse214.ins.Color);
			bump213.outs.Normal.Connect(final_diffuse214.ins.Normal);
			diffuse_base_color_through_alpha246.outs.Color.Connect(shadeless_bsdf215.ins.Color);
			light_path234.outs.IsCameraRay.Connect(shadeless_on_cameraray248.ins.Value1);
			bump213.outs.Normal.Connect(fresnel_based_on_constant217.ins.Normal);
			fresnel_based_on_constant217.outs.Fac.Connect(fresnel_reflection219.ins.R);
			simple_reflection218.outs.Image.Connect(select_reflection_or_fresnel_reflection220.ins.Color1);
			fresnel_reflection219.outs.Image.Connect(select_reflection_or_fresnel_reflection220.ins.Color2);
			fresnel_reflection_if_reflection_used197.outs.Value.Connect(select_reflection_or_fresnel_reflection220.ins.Fac);
			final_diffuse214.outs.BSDF.Connect(shadeless221.ins.Closure1);
			shadeless_bsdf215.outs.Emission.Connect(shadeless221.ins.Closure2);
			shadeless_on_cameraray248.outs.Value.Connect(shadeless221.ins.Fac);
			attenuated_reflection_color216.outs.Color.Connect(glossy222.ins.Color);
			bump213.outs.Normal.Connect(glossy222.ins.Normal);
			select_reflection_or_fresnel_reflection220.outs.Color.Connect(reflection_factor223.ins.Image);
			attennuated_refraction_color224.outs.Color.Connect(refraction225.ins.Color);
			bump213.outs.Normal.Connect(refraction225.ins.Normal);
			shadeless221.outs.Closure.Connect(diffuse_plus_glossy226.ins.Closure1);
			glossy222.outs.BSDF.Connect(diffuse_plus_glossy226.ins.Closure2);
			reflection_factor223.outs.R.Connect(diffuse_plus_glossy226.ins.Fac);
			shadeless221.outs.Closure.Connect(blend_in_transparency227.ins.Closure1);
			refraction225.outs.BSDF.Connect(blend_in_transparency227.ins.Closure2);
			texcoord209.outs.EnvEmap.Connect(separate_envmap_texco228.ins.Vector);
			separate_envmap_texco228.outs.Y.Connect(flip_sign_envmap_texco_y198.ins.Value1);
			separate_envmap_texco228.outs.X.Connect(recombine_envmap_texco229.ins.X);
			flip_sign_envmap_texco_y198.outs.Value.Connect(recombine_envmap_texco229.ins.Y);
			separate_envmap_texco228.outs.Z.Connect(recombine_envmap_texco229.ins.Z);
			recombine_envmap_texco229.outs.Vector.Connect(environment_texture230.ins.Vector);
			environment_texture230.outs.Color.Connect(attenuated_environment_color231.ins.Color2);
			diffuse_plus_glossy226.outs.Closure.Connect(diffuse_glossy_and_refraction232.ins.Closure1);
			blend_in_transparency227.outs.Closure.Connect(diffuse_glossy_and_refraction232.ins.Closure2);
			attenuated_environment_color231.outs.Color.Connect(environment_map_diffuse233.ins.Color);
			invert_roughness199.outs.Value.Connect(multiply_transparency200.ins.Value1);
			multiply_transparency200.outs.Value.Connect(multiply_with_shadowray201.ins.Value1);
			light_path234.outs.IsShadowRay.Connect(multiply_with_shadowray201.ins.Value2);
			diffuse_glossy_and_refraction232.outs.Closure.Connect(custom_environment_blend235.ins.Closure1);
			environment_map_diffuse233.outs.BSDF.Connect(custom_environment_blend235.ins.Closure2);
			diffuse_base_color_through_alpha246.outs.Color.Connect(coloured_shadow_trans_color236.ins.Color);
			multiply_with_shadowray201.outs.Value.Connect(weight_for_shadowray_coloured_shadow202.ins.Value1);
			custom_environment_blend235.outs.Closure.Connect(coloured_shadow_mix_custom239.ins.Closure1);
			coloured_shadow_trans_color236.outs.BSDF.Connect(coloured_shadow_mix_custom239.ins.Closure2);
			weight_for_shadowray_coloured_shadow202.outs.Value.Connect(coloured_shadow_mix_custom239.ins.Fac);
			diffuse_from_emission_color249.outs.BSDF.Connect(diffuse_or_shadeless_emission252.ins.Closure1);
			shadeless_emission251.outs.Emission.Connect(diffuse_or_shadeless_emission252.ins.Closure2);
			shadeless_on_cameraray248.outs.Value.Connect(diffuse_or_shadeless_emission252.ins.Fac);
			diffuse_texture210.outs.Alpha.Connect(max_of_texalpha_or_usealpha307.ins.Value1);
			one_if_usealphatransp_turned_off306.outs.Value.Connect(max_of_texalpha_or_usealpha307.ins.Value2);
			max_of_texalpha_or_usealpha307.outs.Value.Connect(invert_alpha194.ins.Value2);
			texcoord209.outs.UV.Connect(transparency_texture237.ins.Vector);
			transparency_texture237.outs.Color.Connect(transpluminance238.ins.Color);
			transpluminance238.outs.Val.Connect(invert_luminence203.ins.Value2);
			invert_luminence203.outs.Value.Connect(transparency_texture_amount204.ins.Value1);
			invert_alpha194.outs.Value.Connect(toggle_diffuse_texture_alpha_usage205.ins.Value1);
			transparency_texture_amount204.outs.Value.Connect(toggle_transparency_texture206.ins.Value2);
			coloured_shadow_mix_custom239.outs.Closure.Connect(add_emission_to_final250.ins.Closure1);
			diffuse_or_shadeless_emission252.outs.Closure.Connect(add_emission_to_final250.ins.Closure2);
			toggle_diffuse_texture_alpha_usage205.outs.Value.Connect(add_diffuse_texture_alpha207.ins.Value1);
			toggle_transparency_texture206.outs.Value.Connect(add_diffuse_texture_alpha207.ins.Value2);
			add_emission_to_final250.outs.Closure.Connect(custom_alpha_cutter241.ins.Closure1);
			transparent240.outs.BSDF.Connect(custom_alpha_cutter241.ins.Closure2);
			add_diffuse_texture_alpha207.outs.Value.Connect(custom_alpha_cutter241.ins.Fac);
			bump213.outs.Normal.Connect(principledbsdf242.ins.Normal);
			bump213.outs.Normal.Connect(principledbsdf242.ins.ClearcoatNormal);
			principledbsdf242.outs.BSDF.Connect(coloured_shadow_mix_glass_principled243.ins.Closure1);
			coloured_shadow_trans_color236.outs.BSDF.Connect(coloured_shadow_mix_glass_principled243.ins.Closure2);
			weight_for_shadowray_coloured_shadow202.outs.Value.Connect(coloured_shadow_mix_glass_principled243.ins.Fac);

			/* extra code */

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture210, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture210, texcoord209);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture211, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture211, texcoord209);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture237, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture237, texcoord209);
			}

			if (part.HasEnvironmentTexture)
			{
				RenderEngine.SetTextureImage(environment_texture230, part.EnvironmentTexture);
				RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture230, texcoord209);
			}

			if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass
				|| part.CyclesMaterialType == ShaderBody.CyclesMaterial.SimplePlastic
				|| part.CyclesMaterialType == ShaderBody.CyclesMaterial.SimpleMetal) return coloured_shadow_mix_glass_principled243;
			return custom_alpha_cutter241;
		}

	}
}
