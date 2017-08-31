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

namespace RhinoCyclesCore.Shaders
{
	public class RhinoFullNxt : RhinoShader
	{
		public RhinoFullNxt(Client client, CyclesShader intermediate) : this(client, intermediate, null, intermediate.Front.Name)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing) : this(client, intermediate, existing, intermediate.Front.Name)
		{
		}

		public RhinoFullNxt(Client client, CyclesShader intermediate, Shader existing, string name) : base(client, intermediate, name, existing)
		{
		}

		public override Shader GetShader()
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

				flipper.outs.Closure.Connect(m_shader.Output.ins.Surface);
			}
			else
			{
				var last = GetShaderPart(m_original.Front);
				var lastclosure = last.GetClosureSocket();

				lastclosure.Connect(m_shader.Output.ins.Surface);
			}


			m_shader.FinalizeGraph();

			return m_shader;
		}

		private ShaderNode GetShaderPart(ShaderBody part)
		{
			var texcoord211 = new TextureCoordinateNode("texcoord");

			var diffuse_texture212 = new ImageTextureNode("diffuse_texture");
				diffuse_texture212.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				diffuse_texture212.Projection = TextureNode.TextureProjection.Flat;
				diffuse_texture212.ColorSpace = TextureNode.TextureColorSpace.None;
				diffuse_texture212.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
				diffuse_texture212.Interpolation = InterpolationType.Smart;
				diffuse_texture212.UseAlpha = true;
				diffuse_texture212.IsLinear = false;

			var diffuse_texture_alpha_amount193 = new MathMultiply("diffuse_texture_alpha_amount");
				diffuse_texture_alpha_amount193.ins.Value1.Value = 0f;
				diffuse_texture_alpha_amount193.ins.Value2.Value = part.DiffuseTexture.Amount;
				diffuse_texture_alpha_amount193.Operation = MathNode.Operations.Multiply;
				diffuse_texture_alpha_amount193.UseClamp = false;

			var invert_transparency189 = new MathSubtract("invert_transparency");
				invert_transparency189.ins.Value1.Value = 1f;
				invert_transparency189.ins.Value2.Value = part.Transparency;
				invert_transparency189.Operation = MathNode.Operations.Subtract;
				invert_transparency189.UseClamp = false;

			var diff_tex_alpha_multiplied_with_inv_transparency194 = new MathMultiply("diff_tex_alpha_multiplied_with_inv_transparency");
				diff_tex_alpha_multiplied_with_inv_transparency194.ins.Value1.Value = 0f;
				diff_tex_alpha_multiplied_with_inv_transparency194.ins.Value2.Value = 1f;
				diff_tex_alpha_multiplied_with_inv_transparency194.Operation = MathNode.Operations.Multiply;
				diff_tex_alpha_multiplied_with_inv_transparency194.UseClamp = false;

			var bump_texture219 = new ImageTextureNode("bump_texture");
				bump_texture219.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				bump_texture219.Projection = TextureNode.TextureProjection.Flat;
				bump_texture219.ColorSpace = TextureNode.TextureColorSpace.None;
				bump_texture219.Extension = TextureNode.TextureExtension.Repeat;
				bump_texture219.Interpolation = InterpolationType.Smart;
				bump_texture219.UseAlpha = true;
				bump_texture219.IsLinear = false;

			var bump_texture_to_bw220 = new RgbToBwNode("bump_texture_to_bw");
				bump_texture_to_bw220.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount198 = new MathMultiply("bump_amount");
				bump_amount198.ins.Value1.Value = 4.66f;
				bump_amount198.ins.Value2.Value = part.BumpTexture.Amount;
				bump_amount198.Operation = MathNode.Operations.Multiply;
				bump_amount198.UseClamp = false;

			var diffuse_base_color_through_alphatmp648 = new MixNode("diffuse_base_color_through_alphatmp");
				diffuse_base_color_through_alphatmp648.ins.Color1.Value = part.BaseColor;
				diffuse_base_color_through_alphatmp648.ins.Color2.Value = part.BaseColor;
				diffuse_base_color_through_alphatmp648.ins.Fac.Value = 0f;
				diffuse_base_color_through_alphatmp648.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				diffuse_base_color_through_alphatmp648.UseClamp = false;

			var bump222 = new BumpNode("bump");
				bump222.ins.Height.Value = 0f;
				bump222.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				bump222.ins.Strength.Value = 4.66f;
				bump222.ins.Distance.Value = 0.1f;

			var final_diffuse223 = new DiffuseBsdfNode("final_diffuse");
				final_diffuse223.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				final_diffuse223.ins.Roughness.Value = 0f;
				final_diffuse223.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf224 = new EmissionNode("shadeless_bsdf");
				shadeless_bsdf224.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				shadeless_bsdf224.ins.Strength.Value = 1f;

			var attenuated_reflection_color225 = new MixNode("attenuated_reflection_color");
				attenuated_reflection_color225.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_reflection_color225.ins.Color2.Value = part.ReflectionColorGamma;
				attenuated_reflection_color225.ins.Fac.Value = part.Reflectivity;
				attenuated_reflection_color225.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attenuated_reflection_color225.UseClamp = false;

			var fresnel_based_on_constant226 = new FresnelNode("fresnel_based_on_constant");
				fresnel_based_on_constant226.ins.IOR.Value = part.FresnelIOR;
				fresnel_based_on_constant226.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var simple_reflection227 = new CombineRgbNode("simple_reflection");
				simple_reflection227.ins.R.Value = part.Reflectivity;
				simple_reflection227.ins.G.Value = 0f;
				simple_reflection227.ins.B.Value = 0f;

			var fresnel_reflection228 = new CombineRgbNode("fresnel_reflection");
				fresnel_reflection228.ins.R.Value = 0f;
				fresnel_reflection228.ins.G.Value = 0f;
				fresnel_reflection228.ins.B.Value = 0f;

			var fresnel_reflection_if_reflection_used199 = new MathMultiply("fresnel_reflection_if_reflection_used");
				fresnel_reflection_if_reflection_used199.ins.Value1.Value = part.Reflectivity;
				fresnel_reflection_if_reflection_used199.ins.Value2.Value = part.FresnelReflectionsAsFloat;
				fresnel_reflection_if_reflection_used199.Operation = MathNode.Operations.Multiply;
				fresnel_reflection_if_reflection_used199.UseClamp = false;

			var select_reflection_or_fresnel_reflection229 = new MixNode("select_reflection_or_fresnel_reflection");
				select_reflection_or_fresnel_reflection229.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				select_reflection_or_fresnel_reflection229.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				select_reflection_or_fresnel_reflection229.ins.Fac.Value = 0f;
				select_reflection_or_fresnel_reflection229.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				select_reflection_or_fresnel_reflection229.UseClamp = false;

			var shadeless230 = new MixClosureNode("shadeless");
				shadeless230.ins.Fac.Value = part.ShadelessAsFloat;

			var glossy231 = new GlossyBsdfNode("glossy");
				glossy231.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				glossy231.ins.Roughness.Value = part.ReflectionRoughnessPow2;
				glossy231.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var reflection_factor232 = new SeparateRgbNode("reflection_factor");
				reflection_factor232.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var attennuated_refraction_color233 = new MixNode("attennuated_refraction_color");
				attennuated_refraction_color233.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attennuated_refraction_color233.ins.Color2.Value = part.TransparencyColorGamma;
				attennuated_refraction_color233.ins.Fac.Value = part.Transparency;
				attennuated_refraction_color233.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attennuated_refraction_color233.UseClamp = false;

			var refraction234 = new RefractionBsdfNode("refraction");
				refraction234.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				refraction234.ins.Roughness.Value = part.RefractionRoughnessPow2;
				refraction234.ins.IOR.Value = part.IOR;
				refraction234.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				refraction234.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;

			var diffuse_plus_glossy235 = new MixClosureNode("diffuse_plus_glossy");
				diffuse_plus_glossy235.ins.Fac.Value = 0f;

			var blend_in_transparency236 = new MixClosureNode("blend_in_transparency");
				blend_in_transparency236.ins.Fac.Value = part.Transparency;

			var separate_envmap_texco237 = new SeparateXyzNode("separate_envmap_texco");
				separate_envmap_texco237.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var flip_sign_envmap_texco_y200 = new MathMultiply("flip_sign_envmap_texco_y");
				flip_sign_envmap_texco_y200.ins.Value1.Value = 0f;
				flip_sign_envmap_texco_y200.ins.Value2.Value = -1f;
				flip_sign_envmap_texco_y200.Operation = MathNode.Operations.Multiply;
				flip_sign_envmap_texco_y200.UseClamp = false;

			var recombine_envmap_texco238 = new CombineXyzNode("recombine_envmap_texco");
				recombine_envmap_texco238.ins.X.Value = 0f;
				recombine_envmap_texco238.ins.Y.Value = 0f;
				recombine_envmap_texco238.ins.Z.Value = 0f;

			var environment_texture239 = new ImageTextureNode("environment_texture");
				environment_texture239.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				environment_texture239.Projection = TextureNode.TextureProjection.Flat;
				environment_texture239.ColorSpace = TextureNode.TextureColorSpace.None;
				environment_texture239.Extension = TextureNode.TextureExtension.Repeat;
				environment_texture239.Interpolation = InterpolationType.Smart;
				environment_texture239.UseAlpha = true;
				environment_texture239.IsLinear = false;

			var attenuated_environment_color240 = new MixNode("attenuated_environment_color");
				attenuated_environment_color240.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_environment_color240.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				attenuated_environment_color240.ins.Fac.Value = part.EnvironmentTexture.Amount;
				attenuated_environment_color240.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attenuated_environment_color240.UseClamp = false;

			var diffuse_glossy_and_refraction241 = new MixClosureNode("diffuse_glossy_and_refraction");
				diffuse_glossy_and_refraction241.ins.Fac.Value = part.Transparency;

			var environment_map_diffuse242 = new DiffuseBsdfNode("environment_map_diffuse");
				environment_map_diffuse242.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				environment_map_diffuse242.ins.Roughness.Value = 0f;
				environment_map_diffuse242.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness201 = new MathSubtract("invert_roughness");
				invert_roughness201.ins.Value1.Value = 1f;
				invert_roughness201.ins.Value2.Value = part.RefractionRoughnessPow2;
				invert_roughness201.Operation = MathNode.Operations.Subtract;
				invert_roughness201.UseClamp = false;

			var multiply_transparency202 = new MathMultiply("multiply_transparency");
				multiply_transparency202.ins.Value1.Value = 1f;
				multiply_transparency202.ins.Value2.Value = part.Transparency;
				multiply_transparency202.Operation = MathNode.Operations.Multiply;
				multiply_transparency202.UseClamp = false;

			var light_path243 = new LightPathNode("light_path");

			var multiply_with_shadowray203 = new MathMultiply("multiply_with_shadowray");
				multiply_with_shadowray203.ins.Value1.Value = 0f;
				multiply_with_shadowray203.ins.Value2.Value = 0f;
				multiply_with_shadowray203.Operation = MathNode.Operations.Multiply;
				multiply_with_shadowray203.UseClamp = false;

			var custom_environment_blend244 = new MixClosureNode("custom_environment_blend");
				custom_environment_blend244.ins.Fac.Value = part.EnvironmentTexture.Amount;

			var coloured_shadow_trans_color245 = new TransparentBsdfNode("coloured_shadow_trans_color");
				coloured_shadow_trans_color245.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow204 = new MathMultiply("weight_for_shadowray_coloured_shadow");
				weight_for_shadowray_coloured_shadow204.ins.Value1.Value = 0f;
				weight_for_shadowray_coloured_shadow204.ins.Value2.Value = 1f;
				weight_for_shadowray_coloured_shadow204.Operation = MathNode.Operations.Multiply;
				weight_for_shadowray_coloured_shadow204.UseClamp = false;

			var invert_alpha191 = new MathSubtract("invert_alpha");
				invert_alpha191.ins.Value1.Value = 1f;
				invert_alpha191.ins.Value2.Value = 0f;
				invert_alpha191.Operation = MathNode.Operations.Subtract;
				invert_alpha191.UseClamp = false;

			var transparency_texture246 = new ImageTextureNode("transparency_texture");
				transparency_texture246.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				transparency_texture246.Projection = TextureNode.TextureProjection.Flat;
				transparency_texture246.ColorSpace = TextureNode.TextureColorSpace.None;
				transparency_texture246.Extension = TextureNode.TextureExtension.Repeat;
				transparency_texture246.Interpolation = InterpolationType.Smart;
				transparency_texture246.UseAlpha = true;
				transparency_texture246.IsLinear = false;

			var transpluminance247 = new RgbToLuminanceNode("transpluminance");
				transpluminance247.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence205 = new MathSubtract("invert_luminence");
				invert_luminence205.ins.Value1.Value = 1f;
				invert_luminence205.ins.Value2.Value = 0f;
				invert_luminence205.Operation = MathNode.Operations.Subtract;
				invert_luminence205.UseClamp = false;

			var transparency_texture_amount206 = new MathMultiply("transparency_texture_amount");
				transparency_texture_amount206.ins.Value1.Value = 1f;
				transparency_texture_amount206.ins.Value2.Value = part.TransparencyTexture.Amount;
				transparency_texture_amount206.Operation = MathNode.Operations.Multiply;
				transparency_texture_amount206.UseClamp = false;

			var toggle_diffuse_texture_alpha_usage207 = new MathMultiply("toggle_diffuse_texture_alpha_usage");
				toggle_diffuse_texture_alpha_usage207.ins.Value1.Value = 1f;
				toggle_diffuse_texture_alpha_usage207.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
				toggle_diffuse_texture_alpha_usage207.Operation = MathNode.Operations.Multiply;
				toggle_diffuse_texture_alpha_usage207.UseClamp = false;

			var toggle_transparency_texture208 = new MathMultiply("toggle_transparency_texture");
				toggle_transparency_texture208.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
				toggle_transparency_texture208.ins.Value2.Value = 0f;
				toggle_transparency_texture208.Operation = MathNode.Operations.Multiply;
				toggle_transparency_texture208.UseClamp = false;

			var coloured_shadow_mix_custom248 = new MixClosureNode("coloured_shadow_mix_custom");
				coloured_shadow_mix_custom248.ins.Fac.Value = 0f;

			var transparent249 = new TransparentBsdfNode("transparent");
				transparent249.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var add_diffuse_texture_alpha209 = new MathAdd("add_diffuse_texture_alpha");
				add_diffuse_texture_alpha209.ins.Value1.Value = 0f;
				add_diffuse_texture_alpha209.ins.Value2.Value = 0f;
				add_diffuse_texture_alpha209.Operation = MathNode.Operations.Add;
				add_diffuse_texture_alpha209.UseClamp = false;

			var custom_alpha_cutter250 = new MixClosureNode("custom_alpha_cutter");
				custom_alpha_cutter250.ins.Fac.Value = 0f;

			var principledbsdf251 = new PrincipledBsdfNode("principledbsdf");
				principledbsdf251.ins.BaseColor.Value = part.BaseColor;
				principledbsdf251.ins.Subsurface.Value = 0f;
				principledbsdf251.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf251.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				principledbsdf251.ins.Metallic.Value = 0f;
				principledbsdf251.ins.Specular.Value = 0f;
				principledbsdf251.ins.SpecularTint.Value = 0f;
				principledbsdf251.ins.Roughness.Value = part.ReflectionRoughnessPow2;
				principledbsdf251.ins.Anisotropic.Value = 0f;
				principledbsdf251.ins.AnisotropicRotation.Value = 0f;
				principledbsdf251.ins.Sheen.Value = 0f;
				principledbsdf251.ins.SheenTint.Value = 0f;
				principledbsdf251.ins.Clearcoat.Value = 0f;
				principledbsdf251.ins.ClearcoatGloss.Value = 0f;
				principledbsdf251.ins.IOR.Value = part.IOR;
				principledbsdf251.ins.Transmission.Value = part.Transparency;
				principledbsdf251.ins.TransmissionRoughness.Value = part.RefractionRoughnessPow2;
				principledbsdf251.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf251.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf251.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var coloured_shadow_mix_glass_principled252 = new MixClosureNode("coloured_shadow_mix_glass_principled");
				coloured_shadow_mix_glass_principled252.ins.Fac.Value = 0f;
			

			m_shader.AddNode(texcoord211);
			m_shader.AddNode(diffuse_texture212);
			m_shader.AddNode(diffuse_texture_alpha_amount193);
			m_shader.AddNode(invert_transparency189);
			m_shader.AddNode(diff_tex_alpha_multiplied_with_inv_transparency194);
			m_shader.AddNode(bump_texture219);
			m_shader.AddNode(bump_texture_to_bw220);
			m_shader.AddNode(bump_amount198);
			m_shader.AddNode(diffuse_base_color_through_alphatmp648);
			m_shader.AddNode(bump222);
			m_shader.AddNode(final_diffuse223);
			m_shader.AddNode(shadeless_bsdf224);
			m_shader.AddNode(attenuated_reflection_color225);
			m_shader.AddNode(fresnel_based_on_constant226);
			m_shader.AddNode(simple_reflection227);
			m_shader.AddNode(fresnel_reflection228);
			m_shader.AddNode(fresnel_reflection_if_reflection_used199);
			m_shader.AddNode(select_reflection_or_fresnel_reflection229);
			m_shader.AddNode(shadeless230);
			m_shader.AddNode(glossy231);
			m_shader.AddNode(reflection_factor232);
			m_shader.AddNode(attennuated_refraction_color233);
			m_shader.AddNode(refraction234);
			m_shader.AddNode(diffuse_plus_glossy235);
			m_shader.AddNode(blend_in_transparency236);
			m_shader.AddNode(separate_envmap_texco237);
			m_shader.AddNode(flip_sign_envmap_texco_y200);
			m_shader.AddNode(recombine_envmap_texco238);
			m_shader.AddNode(environment_texture239);
			m_shader.AddNode(attenuated_environment_color240);
			m_shader.AddNode(diffuse_glossy_and_refraction241);
			m_shader.AddNode(environment_map_diffuse242);
			m_shader.AddNode(invert_roughness201);
			m_shader.AddNode(multiply_transparency202);
			m_shader.AddNode(light_path243);
			m_shader.AddNode(multiply_with_shadowray203);
			m_shader.AddNode(custom_environment_blend244);
			m_shader.AddNode(coloured_shadow_trans_color245);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow204);
			m_shader.AddNode(invert_alpha191);
			m_shader.AddNode(transparency_texture246);
			m_shader.AddNode(transpluminance247);
			m_shader.AddNode(invert_luminence205);
			m_shader.AddNode(transparency_texture_amount206);
			m_shader.AddNode(toggle_diffuse_texture_alpha_usage207);
			m_shader.AddNode(toggle_transparency_texture208);
			m_shader.AddNode(coloured_shadow_mix_custom248);
			m_shader.AddNode(transparent249);
			m_shader.AddNode(add_diffuse_texture_alpha209);
			m_shader.AddNode(custom_alpha_cutter250);
			m_shader.AddNode(principledbsdf251);
			m_shader.AddNode(coloured_shadow_mix_glass_principled252);
			

			texcoord211.outs.UV.Connect(diffuse_texture212.ins.Vector);
			diffuse_texture212.outs.Alpha.Connect(diffuse_texture_alpha_amount193.ins.Value1);
			diffuse_texture_alpha_amount193.outs.Value.Connect(diff_tex_alpha_multiplied_with_inv_transparency194.ins.Value1);
			invert_transparency189.outs.Value.Connect(diff_tex_alpha_multiplied_with_inv_transparency194.ins.Value2);
			texcoord211.outs.UV.Connect(bump_texture219.ins.Vector);
			bump_texture219.outs.Color.Connect(bump_texture_to_bw220.ins.Color);
			diffuse_texture212.outs.Color.Connect(diffuse_base_color_through_alphatmp648.ins.Color2);
			diff_tex_alpha_multiplied_with_inv_transparency194.outs.Value.Connect(diffuse_base_color_through_alphatmp648.ins.Fac);
			bump_texture_to_bw220.outs.Val.Connect(bump222.ins.Height);
			bump_amount198.outs.Value.Connect(bump222.ins.Strength);
			diffuse_base_color_through_alphatmp648.outs.Color.Connect(final_diffuse223.ins.Color);
			bump222.outs.Normal.Connect(final_diffuse223.ins.Normal);
			diffuse_base_color_through_alphatmp648.outs.Color.Connect(shadeless_bsdf224.ins.Color);
			bump222.outs.Normal.Connect(fresnel_based_on_constant226.ins.Normal);
			fresnel_based_on_constant226.outs.Fac.Connect(fresnel_reflection228.ins.R);
			simple_reflection227.outs.Image.Connect(select_reflection_or_fresnel_reflection229.ins.Color1);
			fresnel_reflection228.outs.Image.Connect(select_reflection_or_fresnel_reflection229.ins.Color2);
			fresnel_reflection_if_reflection_used199.outs.Value.Connect(select_reflection_or_fresnel_reflection229.ins.Fac);
			final_diffuse223.outs.BSDF.Connect(shadeless230.ins.Closure1);
			shadeless_bsdf224.outs.Emission.Connect(shadeless230.ins.Closure2);
			attenuated_reflection_color225.outs.Color.Connect(glossy231.ins.Color);
			bump222.outs.Normal.Connect(glossy231.ins.Normal);
			select_reflection_or_fresnel_reflection229.outs.Color.Connect(reflection_factor232.ins.Image);
			attennuated_refraction_color233.outs.Color.Connect(refraction234.ins.Color);
			bump222.outs.Normal.Connect(refraction234.ins.Normal);
			shadeless230.outs.Closure.Connect(diffuse_plus_glossy235.ins.Closure1);
			glossy231.outs.BSDF.Connect(diffuse_plus_glossy235.ins.Closure2);
			reflection_factor232.outs.R.Connect(diffuse_plus_glossy235.ins.Fac);
			shadeless230.outs.Closure.Connect(blend_in_transparency236.ins.Closure1);
			refraction234.outs.BSDF.Connect(blend_in_transparency236.ins.Closure2);
			texcoord211.outs.EnvEmap.Connect(separate_envmap_texco237.ins.Vector);
			separate_envmap_texco237.outs.Y.Connect(flip_sign_envmap_texco_y200.ins.Value1);
			separate_envmap_texco237.outs.X.Connect(recombine_envmap_texco238.ins.X);
			flip_sign_envmap_texco_y200.outs.Value.Connect(recombine_envmap_texco238.ins.Y);
			separate_envmap_texco237.outs.Z.Connect(recombine_envmap_texco238.ins.Z);
			recombine_envmap_texco238.outs.Vector.Connect(environment_texture239.ins.Vector);
			environment_texture239.outs.Color.Connect(attenuated_environment_color240.ins.Color2);
			diffuse_plus_glossy235.outs.Closure.Connect(diffuse_glossy_and_refraction241.ins.Closure1);
			blend_in_transparency236.outs.Closure.Connect(diffuse_glossy_and_refraction241.ins.Closure2);
			attenuated_environment_color240.outs.Color.Connect(environment_map_diffuse242.ins.Color);
			invert_roughness201.outs.Value.Connect(multiply_transparency202.ins.Value1);
			multiply_transparency202.outs.Value.Connect(multiply_with_shadowray203.ins.Value1);
			light_path243.outs.IsShadowRay.Connect(multiply_with_shadowray203.ins.Value2);
			diffuse_glossy_and_refraction241.outs.Closure.Connect(custom_environment_blend244.ins.Closure1);
			environment_map_diffuse242.outs.BSDF.Connect(custom_environment_blend244.ins.Closure2);
			diffuse_base_color_through_alphatmp648.outs.Color.Connect(coloured_shadow_trans_color245.ins.Color);
			multiply_with_shadowray203.outs.Value.Connect(weight_for_shadowray_coloured_shadow204.ins.Value1);
			diffuse_texture212.outs.Alpha.Connect(invert_alpha191.ins.Value2);
			texcoord211.outs.UV.Connect(transparency_texture246.ins.Vector);
			transparency_texture246.outs.Color.Connect(transpluminance247.ins.Color);
			transpluminance247.outs.Val.Connect(invert_luminence205.ins.Value2);
			invert_luminence205.outs.Value.Connect(transparency_texture_amount206.ins.Value1);
			invert_alpha191.outs.Value.Connect(toggle_diffuse_texture_alpha_usage207.ins.Value1);
			transparency_texture_amount206.outs.Value.Connect(toggle_transparency_texture208.ins.Value2);
			custom_environment_blend244.outs.Closure.Connect(coloured_shadow_mix_custom248.ins.Closure1);
			coloured_shadow_trans_color245.outs.BSDF.Connect(coloured_shadow_mix_custom248.ins.Closure2);
			weight_for_shadowray_coloured_shadow204.outs.Value.Connect(coloured_shadow_mix_custom248.ins.Fac);
			toggle_diffuse_texture_alpha_usage207.outs.Value.Connect(add_diffuse_texture_alpha209.ins.Value1);
			toggle_transparency_texture208.outs.Value.Connect(add_diffuse_texture_alpha209.ins.Value2);
			coloured_shadow_mix_custom248.outs.Closure.Connect(custom_alpha_cutter250.ins.Closure1);
			transparent249.outs.BSDF.Connect(custom_alpha_cutter250.ins.Closure2);
			add_diffuse_texture_alpha209.outs.Value.Connect(custom_alpha_cutter250.ins.Fac);
			bump222.outs.Normal.Connect(principledbsdf251.ins.Normal);
			bump222.outs.Normal.Connect(principledbsdf251.ins.ClearcoatNormal);
			principledbsdf251.outs.BSDF.Connect(coloured_shadow_mix_glass_principled252.ins.Closure1);
			coloured_shadow_trans_color245.outs.BSDF.Connect(coloured_shadow_mix_glass_principled252.ins.Closure2);
			weight_for_shadowray_coloured_shadow204.outs.Value.Connect(coloured_shadow_mix_glass_principled252.ins.Fac);

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture212, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture212, texcoord211);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture219, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture219, texcoord211);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture246, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture246, texcoord211);
			}

			if (part.HasEnvironmentTexture)
			{
				RenderEngine.SetTextureImage(environment_texture239, part.EnvironmentTexture);
				RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture239, texcoord211);
			}

			if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass) return coloured_shadow_mix_glass_principled252;
			return custom_alpha_cutter250;
		}

	}
}
