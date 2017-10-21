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
			var texcoord210 = new TextureCoordinateNode("texcoord");

			var diffuse_texture211 = new ImageTextureNode("diffuse_texture");
				diffuse_texture211.Projection = TextureNode.TextureProjection.Flat;
				diffuse_texture211.ColorSpace = TextureNode.TextureColorSpace.None;
				diffuse_texture211.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
				diffuse_texture211.Interpolation = InterpolationType.Smart;
				diffuse_texture211.UseAlpha = true;
				diffuse_texture211.IsLinear = false;

			var diffuse_texture_alpha_amount195 = new MathMultiply("diffuse_texture_alpha_amount");
				diffuse_texture_alpha_amount195.ins.Value2.Value = part.DiffuseTexture.Amount;
				diffuse_texture_alpha_amount195.Operation = MathNode.Operations.Multiply;
				diffuse_texture_alpha_amount195.UseClamp = false;

			var invert_transparency192 = new MathSubtract("invert_transparency");
				invert_transparency192.ins.Value1.Value = 1f;
				invert_transparency192.ins.Value2.Value = part.Transparency;
				invert_transparency192.Operation = MathNode.Operations.Subtract;
				invert_transparency192.UseClamp = false;

			var diff_tex_alpha_multiplied_with_inv_transparency196 = new MathMultiply("diff_tex_alpha_multiplied_with_inv_transparency");
				diff_tex_alpha_multiplied_with_inv_transparency196.Operation = MathNode.Operations.Multiply;
				diff_tex_alpha_multiplied_with_inv_transparency196.UseClamp = false;

			var always_alpha_transp_when_enabled307 = new MathMaximum("always_alpha_transp_when_enabled");
				always_alpha_transp_when_enabled307.ins.Value1.Value = part.DiffuseTexture.UseAlphaAsFloat;
				always_alpha_transp_when_enabled307.Operation = MathNode.Operations.Maximum;
				always_alpha_transp_when_enabled307.UseClamp = false;

			var bump_texture212 = new ImageTextureNode("bump_texture");
				bump_texture212.Projection = TextureNode.TextureProjection.Flat;
				bump_texture212.ColorSpace = TextureNode.TextureColorSpace.None;
				bump_texture212.Extension = TextureNode.TextureExtension.Repeat;
				bump_texture212.Interpolation = InterpolationType.Smart;
				bump_texture212.UseAlpha = true;
				bump_texture212.IsLinear = false;

			var bump_texture_to_bw213 = new RgbToBwNode("bump_texture_to_bw");

			var bump_amount197 = new MathMultiply("bump_amount");
				bump_amount197.ins.Value1.Value = 4.66f;
				bump_amount197.ins.Value2.Value = part.BumpTexture.Amount;
				bump_amount197.Operation = MathNode.Operations.Multiply;
				bump_amount197.UseClamp = false;

			var diffuse_base_color_through_alpha247 = new MixNode("diffuse_base_color_through_alpha");
				diffuse_base_color_through_alpha247.ins.Color1.Value = part.BaseColor;
				diffuse_base_color_through_alpha247.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				diffuse_base_color_through_alpha247.UseClamp = false;

			var bump214 = new BumpNode("bump");
				bump214.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				bump214.ins.Distance.Value = 0.1f;

			var light_path235 = new LightPathNode("light_path");

			var final_diffuse215 = new DiffuseBsdfNode("final_diffuse");
				final_diffuse215.ins.Roughness.Value = 0f;

			var shadeless_bsdf216 = new EmissionNode("shadeless_bsdf");
				shadeless_bsdf216.ins.Strength.Value = 1f;

			var shadeless_on_cameraray249 = new MathMultiply("shadeless_on_cameraray");
				shadeless_on_cameraray249.ins.Value2.Value = part.ShadelessAsFloat;
				shadeless_on_cameraray249.Operation = MathNode.Operations.Multiply;
				shadeless_on_cameraray249.UseClamp = false;

			var attenuated_reflection_color217 = new MixNode("attenuated_reflection_color");
				attenuated_reflection_color217.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_reflection_color217.ins.Color2.Value = part.ReflectionColorGamma;
				attenuated_reflection_color217.ins.Fac.Value = part.Reflectivity;
				attenuated_reflection_color217.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attenuated_reflection_color217.UseClamp = false;

			var fresnel_based_on_constant218 = new FresnelNode("fresnel_based_on_constant");
				fresnel_based_on_constant218.ins.IOR.Value = part.FresnelIOR;

			var simple_reflection219 = new CombineRgbNode("simple_reflection");
				simple_reflection219.ins.R.Value = part.Reflectivity;
				simple_reflection219.ins.G.Value = 0f;
				simple_reflection219.ins.B.Value = 0f;

			var fresnel_reflection220 = new CombineRgbNode("fresnel_reflection");
				fresnel_reflection220.ins.G.Value = 0f;
				fresnel_reflection220.ins.B.Value = 0f;

			var fresnel_reflection_if_reflection_used198 = new MathMultiply("fresnel_reflection_if_reflection_used");
				fresnel_reflection_if_reflection_used198.ins.Value1.Value = part.Reflectivity;
				fresnel_reflection_if_reflection_used198.ins.Value2.Value = part.FresnelReflectionsAsFloat;
				fresnel_reflection_if_reflection_used198.Operation = MathNode.Operations.Multiply;
				fresnel_reflection_if_reflection_used198.UseClamp = false;

			var select_reflection_or_fresnel_reflection221 = new MixNode("select_reflection_or_fresnel_reflection");
				select_reflection_or_fresnel_reflection221.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				select_reflection_or_fresnel_reflection221.UseClamp = false;

			var shadeless222 = new MixClosureNode("shadeless");

			var glossy223 = new GlossyBsdfNode("glossy");
				glossy223.ins.Roughness.Value = part.ReflectionRoughnessPow2;

			var reflection_factor224 = new SeparateRgbNode("reflection_factor");

			var attennuated_refraction_color225 = new MixNode("attennuated_refraction_color");
				attennuated_refraction_color225.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attennuated_refraction_color225.ins.Color2.Value = part.TransparencyColorGamma;
				attennuated_refraction_color225.ins.Fac.Value = part.Transparency;
				attennuated_refraction_color225.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attennuated_refraction_color225.UseClamp = false;

			var refraction226 = new RefractionBsdfNode("refraction");
				refraction226.ins.Roughness.Value = part.RefractionRoughnessPow2;
				refraction226.ins.IOR.Value = part.IOR;
				refraction226.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;

			var diffuse_plus_glossy227 = new MixClosureNode("diffuse_plus_glossy");

			var blend_in_transparency228 = new MixClosureNode("blend_in_transparency");
				blend_in_transparency228.ins.Fac.Value = part.Transparency;

			var separate_envmap_texco229 = new SeparateXyzNode("separate_envmap_texco");

			var flip_sign_envmap_texco_y199 = new MathMultiply("flip_sign_envmap_texco_y");
				flip_sign_envmap_texco_y199.ins.Value2.Value = -1f;
				flip_sign_envmap_texco_y199.Operation = MathNode.Operations.Multiply;
				flip_sign_envmap_texco_y199.UseClamp = false;

			var recombine_envmap_texco230 = new CombineXyzNode("recombine_envmap_texco");

			var environment_texture231 = new ImageTextureNode("environment_texture");
				environment_texture231.Projection = TextureNode.TextureProjection.Flat;
				environment_texture231.ColorSpace = TextureNode.TextureColorSpace.None;
				environment_texture231.Extension = TextureNode.TextureExtension.Repeat;
				environment_texture231.Interpolation = InterpolationType.Smart;
				environment_texture231.UseAlpha = true;
				environment_texture231.IsLinear = false;

			var attenuated_environment_color232 = new MixNode("attenuated_environment_color");
				attenuated_environment_color232.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_environment_color232.ins.Fac.Value = part.EnvironmentTexture.Amount;
				attenuated_environment_color232.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attenuated_environment_color232.UseClamp = false;

			var diffuse_glossy_and_refraction233 = new MixClosureNode("diffuse_glossy_and_refraction");
				diffuse_glossy_and_refraction233.ins.Fac.Value = part.Transparency;

			var environment_map_diffuse234 = new DiffuseBsdfNode("environment_map_diffuse");
				environment_map_diffuse234.ins.Roughness.Value = 0f;
				environment_map_diffuse234.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness200 = new MathSubtract("invert_roughness");
				invert_roughness200.ins.Value1.Value = 1f;
				invert_roughness200.ins.Value2.Value = part.RefractionRoughnessPow2;
				invert_roughness200.Operation = MathNode.Operations.Subtract;
				invert_roughness200.UseClamp = false;

			var multiply_transparency201 = new MathMultiply("multiply_transparency");
				multiply_transparency201.ins.Value2.Value = part.Transparency;
				multiply_transparency201.Operation = MathNode.Operations.Multiply;
				multiply_transparency201.UseClamp = false;

			var multiply_with_shadowray202 = new MathMultiply("multiply_with_shadowray");
				multiply_with_shadowray202.Operation = MathNode.Operations.Multiply;
				multiply_with_shadowray202.UseClamp = false;

			var custom_environment_blend236 = new MixClosureNode("custom_environment_blend");
				custom_environment_blend236.ins.Fac.Value = part.EnvironmentTexture.Amount;

			var coloured_shadow_trans_color237 = new TransparentBsdfNode("coloured_shadow_trans_color");

			var weight_for_shadowray_coloured_shadow203 = new MathMultiply("weight_for_shadowray_coloured_shadow");
				weight_for_shadowray_coloured_shadow203.ins.Value2.Value = 1f;
				weight_for_shadowray_coloured_shadow203.Operation = MathNode.Operations.Multiply;
				weight_for_shadowray_coloured_shadow203.UseClamp = false;

			var diffuse_from_emission_color250 = new DiffuseBsdfNode("diffuse_from_emission_color");
				diffuse_from_emission_color250.ins.Color.Value = part.EmissionColorGamma;
				diffuse_from_emission_color250.ins.Roughness.Value = 0f;
				diffuse_from_emission_color250.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_emission252 = new EmissionNode("shadeless_emission");
				shadeless_emission252.ins.Color.Value = part.EmissionColorGamma;
				shadeless_emission252.ins.Strength.Value = 1f;

			var coloured_shadow_mix_custom240 = new MixClosureNode("coloured_shadow_mix_custom");

			var diffuse_or_shadeless_emission253 = new MixClosureNode("diffuse_or_shadeless_emission");

			var invert_alpha194 = new MathSubtract("invert_alpha");
				invert_alpha194.ins.Value1.Value = 1f;
				invert_alpha194.Operation = MathNode.Operations.Subtract;
				invert_alpha194.UseClamp = false;

			var transparency_texture238 = new ImageTextureNode("transparency_texture");
				transparency_texture238.Projection = TextureNode.TextureProjection.Flat;
				transparency_texture238.ColorSpace = TextureNode.TextureColorSpace.None;
				transparency_texture238.Extension = TextureNode.TextureExtension.Repeat;
				transparency_texture238.Interpolation = InterpolationType.Smart;
				transparency_texture238.UseAlpha = true;
				transparency_texture238.IsLinear = false;

			var transpluminance239 = new RgbToLuminanceNode("transpluminance");

			var invert_luminence204 = new MathSubtract("invert_luminence");
				invert_luminence204.ins.Value1.Value = 1f;
				invert_luminence204.Operation = MathNode.Operations.Subtract;
				invert_luminence204.UseClamp = false;

			var transparency_texture_amount205 = new MathMultiply("transparency_texture_amount");
				transparency_texture_amount205.ins.Value2.Value = part.TransparencyTexture.Amount;
				transparency_texture_amount205.Operation = MathNode.Operations.Multiply;
				transparency_texture_amount205.UseClamp = false;

			var toggle_diffuse_texture_alpha_usage206 = new MathMultiply("toggle_diffuse_texture_alpha_usage");
				toggle_diffuse_texture_alpha_usage206.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
				toggle_diffuse_texture_alpha_usage206.Operation = MathNode.Operations.Multiply;
				toggle_diffuse_texture_alpha_usage206.UseClamp = false;

			var toggle_transparency_texture207 = new MathMultiply("toggle_transparency_texture");
				toggle_transparency_texture207.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
				toggle_transparency_texture207.Operation = MathNode.Operations.Multiply;
				toggle_transparency_texture207.UseClamp = false;

			var add_emission_to_final251 = new AddClosureNode("add_emission_to_final");

			var transparent241 = new TransparentBsdfNode("transparent");
				transparent241.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var add_diffuse_texture_alpha208 = new MathAdd("add_diffuse_texture_alpha");
				add_diffuse_texture_alpha208.Operation = MathNode.Operations.Add;
				add_diffuse_texture_alpha208.UseClamp = false;

			var custom_alpha_cutter242 = new MixClosureNode("custom_alpha_cutter");

			var principledbsdf243 = new PrincipledBsdfNode("principledbsdf");
				principledbsdf243.ins.BaseColor.Value = part.BaseColor;
				principledbsdf243.ins.Subsurface.Value = 0f;
				principledbsdf243.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf243.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				principledbsdf243.ins.Metallic.Value = 0f;
				principledbsdf243.ins.Specular.Value = 0f;
				principledbsdf243.ins.SpecularTint.Value = 0f;
				principledbsdf243.ins.Roughness.Value = part.ReflectionRoughnessPow2;
				principledbsdf243.ins.Anisotropic.Value = 0f;
				principledbsdf243.ins.AnisotropicRotation.Value = 0f;
				principledbsdf243.ins.Sheen.Value = 0f;
				principledbsdf243.ins.SheenTint.Value = 0f;
				principledbsdf243.ins.Clearcoat.Value = 0f;
				principledbsdf243.ins.ClearcoatGloss.Value = 0f;
				principledbsdf243.ins.IOR.Value = part.IOR;
				principledbsdf243.ins.Transmission.Value = part.Transparency;
				principledbsdf243.ins.TransmissionRoughness.Value = part.RefractionRoughnessPow2;
				principledbsdf243.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var coloured_shadow_mix_glass_principled244 = new MixClosureNode("coloured_shadow_mix_glass_principled");
			

			m_shader.AddNode(texcoord210);
			m_shader.AddNode(diffuse_texture211);
			m_shader.AddNode(diffuse_texture_alpha_amount195);
			m_shader.AddNode(invert_transparency192);
			m_shader.AddNode(diff_tex_alpha_multiplied_with_inv_transparency196);
			m_shader.AddNode(always_alpha_transp_when_enabled307);
			m_shader.AddNode(bump_texture212);
			m_shader.AddNode(bump_texture_to_bw213);
			m_shader.AddNode(bump_amount197);
			m_shader.AddNode(diffuse_base_color_through_alpha247);
			m_shader.AddNode(bump214);
			m_shader.AddNode(light_path235);
			m_shader.AddNode(final_diffuse215);
			m_shader.AddNode(shadeless_bsdf216);
			m_shader.AddNode(shadeless_on_cameraray249);
			m_shader.AddNode(attenuated_reflection_color217);
			m_shader.AddNode(fresnel_based_on_constant218);
			m_shader.AddNode(simple_reflection219);
			m_shader.AddNode(fresnel_reflection220);
			m_shader.AddNode(fresnel_reflection_if_reflection_used198);
			m_shader.AddNode(select_reflection_or_fresnel_reflection221);
			m_shader.AddNode(shadeless222);
			m_shader.AddNode(glossy223);
			m_shader.AddNode(reflection_factor224);
			m_shader.AddNode(attennuated_refraction_color225);
			m_shader.AddNode(refraction226);
			m_shader.AddNode(diffuse_plus_glossy227);
			m_shader.AddNode(blend_in_transparency228);
			m_shader.AddNode(separate_envmap_texco229);
			m_shader.AddNode(flip_sign_envmap_texco_y199);
			m_shader.AddNode(recombine_envmap_texco230);
			m_shader.AddNode(environment_texture231);
			m_shader.AddNode(attenuated_environment_color232);
			m_shader.AddNode(diffuse_glossy_and_refraction233);
			m_shader.AddNode(environment_map_diffuse234);
			m_shader.AddNode(invert_roughness200);
			m_shader.AddNode(multiply_transparency201);
			m_shader.AddNode(multiply_with_shadowray202);
			m_shader.AddNode(custom_environment_blend236);
			m_shader.AddNode(coloured_shadow_trans_color237);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow203);
			m_shader.AddNode(diffuse_from_emission_color250);
			m_shader.AddNode(shadeless_emission252);
			m_shader.AddNode(coloured_shadow_mix_custom240);
			m_shader.AddNode(diffuse_or_shadeless_emission253);
			m_shader.AddNode(invert_alpha194);
			m_shader.AddNode(transparency_texture238);
			m_shader.AddNode(transpluminance239);
			m_shader.AddNode(invert_luminence204);
			m_shader.AddNode(transparency_texture_amount205);
			m_shader.AddNode(toggle_diffuse_texture_alpha_usage206);
			m_shader.AddNode(toggle_transparency_texture207);
			m_shader.AddNode(add_emission_to_final251);
			m_shader.AddNode(transparent241);
			m_shader.AddNode(add_diffuse_texture_alpha208);
			m_shader.AddNode(custom_alpha_cutter242);
			m_shader.AddNode(principledbsdf243);
			m_shader.AddNode(coloured_shadow_mix_glass_principled244);
			

			texcoord210.outs.UV.Connect(diffuse_texture211.ins.Vector);
			diffuse_texture211.outs.Alpha.Connect(diffuse_texture_alpha_amount195.ins.Value1);
			diffuse_texture_alpha_amount195.outs.Value.Connect(diff_tex_alpha_multiplied_with_inv_transparency196.ins.Value1);
			invert_transparency192.outs.Value.Connect(diff_tex_alpha_multiplied_with_inv_transparency196.ins.Value2);
			diff_tex_alpha_multiplied_with_inv_transparency196.outs.Value.Connect(always_alpha_transp_when_enabled307.ins.Value2);
			texcoord210.outs.UV.Connect(bump_texture212.ins.Vector);
			bump_texture212.outs.Color.Connect(bump_texture_to_bw213.ins.Color);
			diffuse_texture211.outs.Color.Connect(diffuse_base_color_through_alpha247.ins.Color2);
			always_alpha_transp_when_enabled307.outs.Value.Connect(diffuse_base_color_through_alpha247.ins.Fac);
			bump_texture_to_bw213.outs.Val.Connect(bump214.ins.Height);
			bump_amount197.outs.Value.Connect(bump214.ins.Strength);
			diffuse_base_color_through_alpha247.outs.Color.Connect(final_diffuse215.ins.Color);
			bump214.outs.Normal.Connect(final_diffuse215.ins.Normal);
			diffuse_base_color_through_alpha247.outs.Color.Connect(shadeless_bsdf216.ins.Color);
			light_path235.outs.IsCameraRay.Connect(shadeless_on_cameraray249.ins.Value1);
			bump214.outs.Normal.Connect(fresnel_based_on_constant218.ins.Normal);
			fresnel_based_on_constant218.outs.Fac.Connect(fresnel_reflection220.ins.R);
			simple_reflection219.outs.Image.Connect(select_reflection_or_fresnel_reflection221.ins.Color1);
			fresnel_reflection220.outs.Image.Connect(select_reflection_or_fresnel_reflection221.ins.Color2);
			fresnel_reflection_if_reflection_used198.outs.Value.Connect(select_reflection_or_fresnel_reflection221.ins.Fac);
			final_diffuse215.outs.BSDF.Connect(shadeless222.ins.Closure1);
			shadeless_bsdf216.outs.Emission.Connect(shadeless222.ins.Closure2);
			shadeless_on_cameraray249.outs.Value.Connect(shadeless222.ins.Fac);
			attenuated_reflection_color217.outs.Color.Connect(glossy223.ins.Color);
			bump214.outs.Normal.Connect(glossy223.ins.Normal);
			select_reflection_or_fresnel_reflection221.outs.Color.Connect(reflection_factor224.ins.Image);
			attennuated_refraction_color225.outs.Color.Connect(refraction226.ins.Color);
			bump214.outs.Normal.Connect(refraction226.ins.Normal);
			shadeless222.outs.Closure.Connect(diffuse_plus_glossy227.ins.Closure1);
			glossy223.outs.BSDF.Connect(diffuse_plus_glossy227.ins.Closure2);
			reflection_factor224.outs.R.Connect(diffuse_plus_glossy227.ins.Fac);
			shadeless222.outs.Closure.Connect(blend_in_transparency228.ins.Closure1);
			refraction226.outs.BSDF.Connect(blend_in_transparency228.ins.Closure2);
			texcoord210.outs.EnvEmap.Connect(separate_envmap_texco229.ins.Vector);
			separate_envmap_texco229.outs.Y.Connect(flip_sign_envmap_texco_y199.ins.Value1);
			separate_envmap_texco229.outs.X.Connect(recombine_envmap_texco230.ins.X);
			flip_sign_envmap_texco_y199.outs.Value.Connect(recombine_envmap_texco230.ins.Y);
			separate_envmap_texco229.outs.Z.Connect(recombine_envmap_texco230.ins.Z);
			recombine_envmap_texco230.outs.Vector.Connect(environment_texture231.ins.Vector);
			environment_texture231.outs.Color.Connect(attenuated_environment_color232.ins.Color2);
			diffuse_plus_glossy227.outs.Closure.Connect(diffuse_glossy_and_refraction233.ins.Closure1);
			blend_in_transparency228.outs.Closure.Connect(diffuse_glossy_and_refraction233.ins.Closure2);
			attenuated_environment_color232.outs.Color.Connect(environment_map_diffuse234.ins.Color);
			invert_roughness200.outs.Value.Connect(multiply_transparency201.ins.Value1);
			multiply_transparency201.outs.Value.Connect(multiply_with_shadowray202.ins.Value1);
			light_path235.outs.IsShadowRay.Connect(multiply_with_shadowray202.ins.Value2);
			diffuse_glossy_and_refraction233.outs.Closure.Connect(custom_environment_blend236.ins.Closure1);
			environment_map_diffuse234.outs.BSDF.Connect(custom_environment_blend236.ins.Closure2);
			diffuse_base_color_through_alpha247.outs.Color.Connect(coloured_shadow_trans_color237.ins.Color);
			multiply_with_shadowray202.outs.Value.Connect(weight_for_shadowray_coloured_shadow203.ins.Value1);
			custom_environment_blend236.outs.Closure.Connect(coloured_shadow_mix_custom240.ins.Closure1);
			coloured_shadow_trans_color237.outs.BSDF.Connect(coloured_shadow_mix_custom240.ins.Closure2);
			weight_for_shadowray_coloured_shadow203.outs.Value.Connect(coloured_shadow_mix_custom240.ins.Fac);
			diffuse_from_emission_color250.outs.BSDF.Connect(diffuse_or_shadeless_emission253.ins.Closure1);
			shadeless_emission252.outs.Emission.Connect(diffuse_or_shadeless_emission253.ins.Closure2);
			shadeless_on_cameraray249.outs.Value.Connect(diffuse_or_shadeless_emission253.ins.Fac);
			diffuse_texture211.outs.Alpha.Connect(invert_alpha194.ins.Value2);
			texcoord210.outs.UV.Connect(transparency_texture238.ins.Vector);
			transparency_texture238.outs.Color.Connect(transpluminance239.ins.Color);
			transpluminance239.outs.Val.Connect(invert_luminence204.ins.Value2);
			invert_luminence204.outs.Value.Connect(transparency_texture_amount205.ins.Value1);
			invert_alpha194.outs.Value.Connect(toggle_diffuse_texture_alpha_usage206.ins.Value1);
			transparency_texture_amount205.outs.Value.Connect(toggle_transparency_texture207.ins.Value2);
			coloured_shadow_mix_custom240.outs.Closure.Connect(add_emission_to_final251.ins.Closure1);
			diffuse_or_shadeless_emission253.outs.Closure.Connect(add_emission_to_final251.ins.Closure2);
			toggle_diffuse_texture_alpha_usage206.outs.Value.Connect(add_diffuse_texture_alpha208.ins.Value1);
			toggle_transparency_texture207.outs.Value.Connect(add_diffuse_texture_alpha208.ins.Value2);
			add_emission_to_final251.outs.Closure.Connect(custom_alpha_cutter242.ins.Closure1);
			transparent241.outs.BSDF.Connect(custom_alpha_cutter242.ins.Closure2);
			add_diffuse_texture_alpha208.outs.Value.Connect(custom_alpha_cutter242.ins.Fac);
			bump214.outs.Normal.Connect(principledbsdf243.ins.Normal);
			bump214.outs.Normal.Connect(principledbsdf243.ins.ClearcoatNormal);
			principledbsdf243.outs.BSDF.Connect(coloured_shadow_mix_glass_principled244.ins.Closure1);
			coloured_shadow_trans_color237.outs.BSDF.Connect(coloured_shadow_mix_glass_principled244.ins.Closure2);
			weight_for_shadowray_coloured_shadow203.outs.Value.Connect(coloured_shadow_mix_glass_principled244.ins.Fac);

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture211, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture211, texcoord210);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture212, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture212, texcoord210);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture238, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture238, texcoord210);
			}

			if (part.HasEnvironmentTexture)
			{
				RenderEngine.SetTextureImage(environment_texture231, part.EnvironmentTexture);
				RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture231, texcoord210);
			}

			if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass) return coloured_shadow_mix_glass_principled244;
			return custom_alpha_cutter242;
		}

	}
}
