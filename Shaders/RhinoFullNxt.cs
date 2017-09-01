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
			var texcoord207 = new TextureCoordinateNode("texcoord");

			var diffuse_texture208 = new ImageTextureNode("diffuse_texture");
				diffuse_texture208.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				diffuse_texture208.Projection = TextureNode.TextureProjection.Flat;
				diffuse_texture208.ColorSpace = TextureNode.TextureColorSpace.None;
				diffuse_texture208.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
				diffuse_texture208.Interpolation = InterpolationType.Smart;
				diffuse_texture208.UseAlpha = true;
				diffuse_texture208.IsLinear = false;

			var diffuse_texture_alpha_amount192 = new MathMultiply("diffuse_texture_alpha_amount");
				diffuse_texture_alpha_amount192.ins.Value1.Value = 0f;
				diffuse_texture_alpha_amount192.ins.Value2.Value = part.DiffuseTexture.Amount;
				diffuse_texture_alpha_amount192.Operation = MathNode.Operations.Multiply;
				diffuse_texture_alpha_amount192.UseClamp = false;

			var invert_transparency189 = new MathSubtract("invert_transparency");
				invert_transparency189.ins.Value1.Value = 1f;
				invert_transparency189.ins.Value2.Value = part.Transparency;
				invert_transparency189.Operation = MathNode.Operations.Subtract;
				invert_transparency189.UseClamp = false;

			var diff_tex_alpha_multiplied_with_inv_transparency193 = new MathMultiply("diff_tex_alpha_multiplied_with_inv_transparency");
				diff_tex_alpha_multiplied_with_inv_transparency193.ins.Value1.Value = 0f;
				diff_tex_alpha_multiplied_with_inv_transparency193.ins.Value2.Value = 1f;
				diff_tex_alpha_multiplied_with_inv_transparency193.Operation = MathNode.Operations.Multiply;
				diff_tex_alpha_multiplied_with_inv_transparency193.UseClamp = false;

			var bump_texture209 = new ImageTextureNode("bump_texture");
				bump_texture209.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				bump_texture209.Projection = TextureNode.TextureProjection.Flat;
				bump_texture209.ColorSpace = TextureNode.TextureColorSpace.None;
				bump_texture209.Extension = TextureNode.TextureExtension.Repeat;
				bump_texture209.Interpolation = InterpolationType.Smart;
				bump_texture209.UseAlpha = true;
				bump_texture209.IsLinear = false;

			var bump_texture_to_bw210 = new RgbToBwNode("bump_texture_to_bw");
				bump_texture_to_bw210.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount194 = new MathMultiply("bump_amount");
				bump_amount194.ins.Value1.Value = 4.66f;
				bump_amount194.ins.Value2.Value = part.BumpTexture.Amount;
				bump_amount194.Operation = MathNode.Operations.Multiply;
				bump_amount194.UseClamp = false;

			var diffuse_base_color_through_alpha244 = new MixNode("diffuse_base_color_through_alpha");
				diffuse_base_color_through_alpha244.ins.Color1.Value = part.BaseColor;
				diffuse_base_color_through_alpha244.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				diffuse_base_color_through_alpha244.ins.Fac.Value = 0f;
				diffuse_base_color_through_alpha244.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				diffuse_base_color_through_alpha244.UseClamp = false;

			var bump211 = new BumpNode("bump");
				bump211.ins.Height.Value = 0f;
				bump211.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				bump211.ins.Strength.Value = 4.66f;
				bump211.ins.Distance.Value = 0.1f;

			var light_path232 = new LightPathNode("light_path");

			var final_diffuse212 = new DiffuseBsdfNode("final_diffuse");
				final_diffuse212.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				final_diffuse212.ins.Roughness.Value = 0f;
				final_diffuse212.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf213 = new EmissionNode("shadeless_bsdf");
				shadeless_bsdf213.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				shadeless_bsdf213.ins.Strength.Value = 1f;

			var shadeless_on_cameraray246 = new MathMultiply("shadeless_on_cameraray");
				shadeless_on_cameraray246.ins.Value1.Value = 0f;
				shadeless_on_cameraray246.ins.Value2.Value = part.ShadelessAsFloat;
				shadeless_on_cameraray246.Operation = MathNode.Operations.Multiply;
				shadeless_on_cameraray246.UseClamp = false;

			var attenuated_reflection_color214 = new MixNode("attenuated_reflection_color");
				attenuated_reflection_color214.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_reflection_color214.ins.Color2.Value = part.ReflectionColorGamma;
				attenuated_reflection_color214.ins.Fac.Value = part.Reflectivity;
				attenuated_reflection_color214.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attenuated_reflection_color214.UseClamp = false;

			var fresnel_based_on_constant215 = new FresnelNode("fresnel_based_on_constant");
				fresnel_based_on_constant215.ins.IOR.Value = part.FresnelIOR;
				fresnel_based_on_constant215.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var simple_reflection216 = new CombineRgbNode("simple_reflection");
				simple_reflection216.ins.R.Value = part.Reflectivity;
				simple_reflection216.ins.G.Value = 0f;
				simple_reflection216.ins.B.Value = 0f;

			var fresnel_reflection217 = new CombineRgbNode("fresnel_reflection");
				fresnel_reflection217.ins.R.Value = 0f;
				fresnel_reflection217.ins.G.Value = 0f;
				fresnel_reflection217.ins.B.Value = 0f;

			var fresnel_reflection_if_reflection_used195 = new MathMultiply("fresnel_reflection_if_reflection_used");
				fresnel_reflection_if_reflection_used195.ins.Value1.Value = part.Reflectivity;
				fresnel_reflection_if_reflection_used195.ins.Value2.Value = part.FresnelReflectionsAsFloat;
				fresnel_reflection_if_reflection_used195.Operation = MathNode.Operations.Multiply;
				fresnel_reflection_if_reflection_used195.UseClamp = false;

			var select_reflection_or_fresnel_reflection218 = new MixNode("select_reflection_or_fresnel_reflection");
				select_reflection_or_fresnel_reflection218.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				select_reflection_or_fresnel_reflection218.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				select_reflection_or_fresnel_reflection218.ins.Fac.Value = 0f;
				select_reflection_or_fresnel_reflection218.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				select_reflection_or_fresnel_reflection218.UseClamp = false;

			var shadeless219 = new MixClosureNode("shadeless");
				shadeless219.ins.Fac.Value = 0f;

			var glossy220 = new GlossyBsdfNode("glossy");
				glossy220.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				glossy220.ins.Roughness.Value = part.ReflectionRoughnessPow2;
				glossy220.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var reflection_factor221 = new SeparateRgbNode("reflection_factor");
				reflection_factor221.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var attennuated_refraction_color222 = new MixNode("attennuated_refraction_color");
				attennuated_refraction_color222.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attennuated_refraction_color222.ins.Color2.Value = part.TransparencyColorGamma;
				attennuated_refraction_color222.ins.Fac.Value = part.Transparency;
				attennuated_refraction_color222.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attennuated_refraction_color222.UseClamp = false;

			var refraction223 = new RefractionBsdfNode("refraction");
				refraction223.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				refraction223.ins.Roughness.Value = part.RefractionRoughnessPow2;
				refraction223.ins.IOR.Value = part.IOR;
				refraction223.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				refraction223.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;

			var diffuse_plus_glossy224 = new MixClosureNode("diffuse_plus_glossy");
				diffuse_plus_glossy224.ins.Fac.Value = 0f;

			var blend_in_transparency225 = new MixClosureNode("blend_in_transparency");
				blend_in_transparency225.ins.Fac.Value = part.Transparency;

			var separate_envmap_texco226 = new SeparateXyzNode("separate_envmap_texco");
				separate_envmap_texco226.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var flip_sign_envmap_texco_y196 = new MathMultiply("flip_sign_envmap_texco_y");
				flip_sign_envmap_texco_y196.ins.Value1.Value = 0f;
				flip_sign_envmap_texco_y196.ins.Value2.Value = -1f;
				flip_sign_envmap_texco_y196.Operation = MathNode.Operations.Multiply;
				flip_sign_envmap_texco_y196.UseClamp = false;

			var recombine_envmap_texco227 = new CombineXyzNode("recombine_envmap_texco");
				recombine_envmap_texco227.ins.X.Value = 0f;
				recombine_envmap_texco227.ins.Y.Value = 0f;
				recombine_envmap_texco227.ins.Z.Value = 0f;

			var environment_texture228 = new ImageTextureNode("environment_texture");
				environment_texture228.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				environment_texture228.Projection = TextureNode.TextureProjection.Flat;
				environment_texture228.ColorSpace = TextureNode.TextureColorSpace.None;
				environment_texture228.Extension = TextureNode.TextureExtension.Repeat;
				environment_texture228.Interpolation = InterpolationType.Smart;
				environment_texture228.UseAlpha = true;
				environment_texture228.IsLinear = false;

			var attenuated_environment_color229 = new MixNode("attenuated_environment_color");
				attenuated_environment_color229.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
				attenuated_environment_color229.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				attenuated_environment_color229.ins.Fac.Value = part.EnvironmentTexture.Amount;
				attenuated_environment_color229.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
				attenuated_environment_color229.UseClamp = false;

			var diffuse_glossy_and_refraction230 = new MixClosureNode("diffuse_glossy_and_refraction");
				diffuse_glossy_and_refraction230.ins.Fac.Value = part.Transparency;

			var environment_map_diffuse231 = new DiffuseBsdfNode("environment_map_diffuse");
				environment_map_diffuse231.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				environment_map_diffuse231.ins.Roughness.Value = 0f;
				environment_map_diffuse231.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness197 = new MathSubtract("invert_roughness");
				invert_roughness197.ins.Value1.Value = 1f;
				invert_roughness197.ins.Value2.Value = part.RefractionRoughnessPow2;
				invert_roughness197.Operation = MathNode.Operations.Subtract;
				invert_roughness197.UseClamp = false;

			var multiply_transparency198 = new MathMultiply("multiply_transparency");
				multiply_transparency198.ins.Value1.Value = 1f;
				multiply_transparency198.ins.Value2.Value = part.Transparency;
				multiply_transparency198.Operation = MathNode.Operations.Multiply;
				multiply_transparency198.UseClamp = false;

			var multiply_with_shadowray199 = new MathMultiply("multiply_with_shadowray");
				multiply_with_shadowray199.ins.Value1.Value = 0f;
				multiply_with_shadowray199.ins.Value2.Value = 0f;
				multiply_with_shadowray199.Operation = MathNode.Operations.Multiply;
				multiply_with_shadowray199.UseClamp = false;

			var custom_environment_blend233 = new MixClosureNode("custom_environment_blend");
				custom_environment_blend233.ins.Fac.Value = part.EnvironmentTexture.Amount;

			var coloured_shadow_trans_color234 = new TransparentBsdfNode("coloured_shadow_trans_color");
				coloured_shadow_trans_color234.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow200 = new MathMultiply("weight_for_shadowray_coloured_shadow");
				weight_for_shadowray_coloured_shadow200.ins.Value1.Value = 0f;
				weight_for_shadowray_coloured_shadow200.ins.Value2.Value = 1f;
				weight_for_shadowray_coloured_shadow200.Operation = MathNode.Operations.Multiply;
				weight_for_shadowray_coloured_shadow200.UseClamp = false;

			var coloured_shadow_mix_custom237 = new MixClosureNode("coloured_shadow_mix_custom");
				coloured_shadow_mix_custom237.ins.Fac.Value = 0f;

			var diffuse_from_emission_color247 = new DiffuseBsdfNode("diffuse_from_emission_color");
				diffuse_from_emission_color247.ins.Color.Value = part.EmissionColorGamma;
				diffuse_from_emission_color247.ins.Roughness.Value = 0f;
				diffuse_from_emission_color247.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_alpha191 = new MathSubtract("invert_alpha");
				invert_alpha191.ins.Value1.Value = 1f;
				invert_alpha191.ins.Value2.Value = 0f;
				invert_alpha191.Operation = MathNode.Operations.Subtract;
				invert_alpha191.UseClamp = false;

			var transparency_texture235 = new ImageTextureNode("transparency_texture");
				transparency_texture235.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
				transparency_texture235.Projection = TextureNode.TextureProjection.Flat;
				transparency_texture235.ColorSpace = TextureNode.TextureColorSpace.None;
				transparency_texture235.Extension = TextureNode.TextureExtension.Repeat;
				transparency_texture235.Interpolation = InterpolationType.Smart;
				transparency_texture235.UseAlpha = true;
				transparency_texture235.IsLinear = false;

			var transpluminance236 = new RgbToLuminanceNode("transpluminance");
				transpluminance236.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence201 = new MathSubtract("invert_luminence");
				invert_luminence201.ins.Value1.Value = 1f;
				invert_luminence201.ins.Value2.Value = 0f;
				invert_luminence201.Operation = MathNode.Operations.Subtract;
				invert_luminence201.UseClamp = false;

			var transparency_texture_amount202 = new MathMultiply("transparency_texture_amount");
				transparency_texture_amount202.ins.Value1.Value = 1f;
				transparency_texture_amount202.ins.Value2.Value = part.TransparencyTexture.Amount;
				transparency_texture_amount202.Operation = MathNode.Operations.Multiply;
				transparency_texture_amount202.UseClamp = false;

			var toggle_diffuse_texture_alpha_usage203 = new MathMultiply("toggle_diffuse_texture_alpha_usage");
				toggle_diffuse_texture_alpha_usage203.ins.Value1.Value = 1f;
				toggle_diffuse_texture_alpha_usage203.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
				toggle_diffuse_texture_alpha_usage203.Operation = MathNode.Operations.Multiply;
				toggle_diffuse_texture_alpha_usage203.UseClamp = false;

			var toggle_transparency_texture204 = new MathMultiply("toggle_transparency_texture");
				toggle_transparency_texture204.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
				toggle_transparency_texture204.ins.Value2.Value = 0f;
				toggle_transparency_texture204.Operation = MathNode.Operations.Multiply;
				toggle_transparency_texture204.UseClamp = false;

			var add_emission_to_final248 = new AddClosureNode("add_emission_to_final");

			var transparent238 = new TransparentBsdfNode("transparent");
				transparent238.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var add_diffuse_texture_alpha205 = new MathAdd("add_diffuse_texture_alpha");
				add_diffuse_texture_alpha205.ins.Value1.Value = 0f;
				add_diffuse_texture_alpha205.ins.Value2.Value = 0f;
				add_diffuse_texture_alpha205.Operation = MathNode.Operations.Add;
				add_diffuse_texture_alpha205.UseClamp = false;

			var custom_alpha_cutter239 = new MixClosureNode("custom_alpha_cutter");
				custom_alpha_cutter239.ins.Fac.Value = 0f;

			var principledbsdf240 = new PrincipledBsdfNode("principledbsdf");
				principledbsdf240.ins.BaseColor.Value = part.BaseColor;
				principledbsdf240.ins.Subsurface.Value = 0f;
				principledbsdf240.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf240.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
				principledbsdf240.ins.Metallic.Value = 0f;
				principledbsdf240.ins.Specular.Value = 0f;
				principledbsdf240.ins.SpecularTint.Value = 0f;
				principledbsdf240.ins.Roughness.Value = part.ReflectionRoughnessPow2;
				principledbsdf240.ins.Anisotropic.Value = 0f;
				principledbsdf240.ins.AnisotropicRotation.Value = 0f;
				principledbsdf240.ins.Sheen.Value = 0f;
				principledbsdf240.ins.SheenTint.Value = 0f;
				principledbsdf240.ins.Clearcoat.Value = 0f;
				principledbsdf240.ins.ClearcoatGloss.Value = 0f;
				principledbsdf240.ins.IOR.Value = part.IOR;
				principledbsdf240.ins.Transmission.Value = part.Transparency;
				principledbsdf240.ins.TransmissionRoughness.Value = part.RefractionRoughnessPow2;
				principledbsdf240.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf240.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
				principledbsdf240.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var coloured_shadow_mix_glass_principled241 = new MixClosureNode("coloured_shadow_mix_glass_principled");
				coloured_shadow_mix_glass_principled241.ins.Fac.Value = 0f;
			

			m_shader.AddNode(texcoord207);
			m_shader.AddNode(diffuse_texture208);
			m_shader.AddNode(diffuse_texture_alpha_amount192);
			m_shader.AddNode(invert_transparency189);
			m_shader.AddNode(diff_tex_alpha_multiplied_with_inv_transparency193);
			m_shader.AddNode(bump_texture209);
			m_shader.AddNode(bump_texture_to_bw210);
			m_shader.AddNode(bump_amount194);
			m_shader.AddNode(diffuse_base_color_through_alpha244);
			m_shader.AddNode(bump211);
			m_shader.AddNode(light_path232);
			m_shader.AddNode(final_diffuse212);
			m_shader.AddNode(shadeless_bsdf213);
			m_shader.AddNode(shadeless_on_cameraray246);
			m_shader.AddNode(attenuated_reflection_color214);
			m_shader.AddNode(fresnel_based_on_constant215);
			m_shader.AddNode(simple_reflection216);
			m_shader.AddNode(fresnel_reflection217);
			m_shader.AddNode(fresnel_reflection_if_reflection_used195);
			m_shader.AddNode(select_reflection_or_fresnel_reflection218);
			m_shader.AddNode(shadeless219);
			m_shader.AddNode(glossy220);
			m_shader.AddNode(reflection_factor221);
			m_shader.AddNode(attennuated_refraction_color222);
			m_shader.AddNode(refraction223);
			m_shader.AddNode(diffuse_plus_glossy224);
			m_shader.AddNode(blend_in_transparency225);
			m_shader.AddNode(separate_envmap_texco226);
			m_shader.AddNode(flip_sign_envmap_texco_y196);
			m_shader.AddNode(recombine_envmap_texco227);
			m_shader.AddNode(environment_texture228);
			m_shader.AddNode(attenuated_environment_color229);
			m_shader.AddNode(diffuse_glossy_and_refraction230);
			m_shader.AddNode(environment_map_diffuse231);
			m_shader.AddNode(invert_roughness197);
			m_shader.AddNode(multiply_transparency198);
			m_shader.AddNode(multiply_with_shadowray199);
			m_shader.AddNode(custom_environment_blend233);
			m_shader.AddNode(coloured_shadow_trans_color234);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow200);
			m_shader.AddNode(coloured_shadow_mix_custom237);
			m_shader.AddNode(diffuse_from_emission_color247);
			m_shader.AddNode(invert_alpha191);
			m_shader.AddNode(transparency_texture235);
			m_shader.AddNode(transpluminance236);
			m_shader.AddNode(invert_luminence201);
			m_shader.AddNode(transparency_texture_amount202);
			m_shader.AddNode(toggle_diffuse_texture_alpha_usage203);
			m_shader.AddNode(toggle_transparency_texture204);
			m_shader.AddNode(add_emission_to_final248);
			m_shader.AddNode(transparent238);
			m_shader.AddNode(add_diffuse_texture_alpha205);
			m_shader.AddNode(custom_alpha_cutter239);
			m_shader.AddNode(principledbsdf240);
			m_shader.AddNode(coloured_shadow_mix_glass_principled241);
			

			texcoord207.outs.UV.Connect(diffuse_texture208.ins.Vector);
			diffuse_texture208.outs.Alpha.Connect(diffuse_texture_alpha_amount192.ins.Value1);
			diffuse_texture_alpha_amount192.outs.Value.Connect(diff_tex_alpha_multiplied_with_inv_transparency193.ins.Value1);
			invert_transparency189.outs.Value.Connect(diff_tex_alpha_multiplied_with_inv_transparency193.ins.Value2);
			texcoord207.outs.UV.Connect(bump_texture209.ins.Vector);
			bump_texture209.outs.Color.Connect(bump_texture_to_bw210.ins.Color);
			diffuse_texture208.outs.Color.Connect(diffuse_base_color_through_alpha244.ins.Color2);
			diff_tex_alpha_multiplied_with_inv_transparency193.outs.Value.Connect(diffuse_base_color_through_alpha244.ins.Fac);
			bump_texture_to_bw210.outs.Val.Connect(bump211.ins.Height);
			bump_amount194.outs.Value.Connect(bump211.ins.Strength);
			diffuse_base_color_through_alpha244.outs.Color.Connect(final_diffuse212.ins.Color);
			bump211.outs.Normal.Connect(final_diffuse212.ins.Normal);
			diffuse_base_color_through_alpha244.outs.Color.Connect(shadeless_bsdf213.ins.Color);
			light_path232.outs.IsCameraRay.Connect(shadeless_on_cameraray246.ins.Value1);
			bump211.outs.Normal.Connect(fresnel_based_on_constant215.ins.Normal);
			fresnel_based_on_constant215.outs.Fac.Connect(fresnel_reflection217.ins.R);
			simple_reflection216.outs.Image.Connect(select_reflection_or_fresnel_reflection218.ins.Color1);
			fresnel_reflection217.outs.Image.Connect(select_reflection_or_fresnel_reflection218.ins.Color2);
			fresnel_reflection_if_reflection_used195.outs.Value.Connect(select_reflection_or_fresnel_reflection218.ins.Fac);
			final_diffuse212.outs.BSDF.Connect(shadeless219.ins.Closure1);
			shadeless_bsdf213.outs.Emission.Connect(shadeless219.ins.Closure2);
			shadeless_on_cameraray246.outs.Value.Connect(shadeless219.ins.Fac);
			attenuated_reflection_color214.outs.Color.Connect(glossy220.ins.Color);
			bump211.outs.Normal.Connect(glossy220.ins.Normal);
			select_reflection_or_fresnel_reflection218.outs.Color.Connect(reflection_factor221.ins.Image);
			attennuated_refraction_color222.outs.Color.Connect(refraction223.ins.Color);
			bump211.outs.Normal.Connect(refraction223.ins.Normal);
			shadeless219.outs.Closure.Connect(diffuse_plus_glossy224.ins.Closure1);
			glossy220.outs.BSDF.Connect(diffuse_plus_glossy224.ins.Closure2);
			reflection_factor221.outs.R.Connect(diffuse_plus_glossy224.ins.Fac);
			shadeless219.outs.Closure.Connect(blend_in_transparency225.ins.Closure1);
			refraction223.outs.BSDF.Connect(blend_in_transparency225.ins.Closure2);
			texcoord207.outs.EnvEmap.Connect(separate_envmap_texco226.ins.Vector);
			separate_envmap_texco226.outs.Y.Connect(flip_sign_envmap_texco_y196.ins.Value1);
			separate_envmap_texco226.outs.X.Connect(recombine_envmap_texco227.ins.X);
			flip_sign_envmap_texco_y196.outs.Value.Connect(recombine_envmap_texco227.ins.Y);
			separate_envmap_texco226.outs.Z.Connect(recombine_envmap_texco227.ins.Z);
			recombine_envmap_texco227.outs.Vector.Connect(environment_texture228.ins.Vector);
			environment_texture228.outs.Color.Connect(attenuated_environment_color229.ins.Color2);
			diffuse_plus_glossy224.outs.Closure.Connect(diffuse_glossy_and_refraction230.ins.Closure1);
			blend_in_transparency225.outs.Closure.Connect(diffuse_glossy_and_refraction230.ins.Closure2);
			attenuated_environment_color229.outs.Color.Connect(environment_map_diffuse231.ins.Color);
			invert_roughness197.outs.Value.Connect(multiply_transparency198.ins.Value1);
			multiply_transparency198.outs.Value.Connect(multiply_with_shadowray199.ins.Value1);
			light_path232.outs.IsShadowRay.Connect(multiply_with_shadowray199.ins.Value2);
			diffuse_glossy_and_refraction230.outs.Closure.Connect(custom_environment_blend233.ins.Closure1);
			environment_map_diffuse231.outs.BSDF.Connect(custom_environment_blend233.ins.Closure2);
			diffuse_base_color_through_alpha244.outs.Color.Connect(coloured_shadow_trans_color234.ins.Color);
			multiply_with_shadowray199.outs.Value.Connect(weight_for_shadowray_coloured_shadow200.ins.Value1);
			custom_environment_blend233.outs.Closure.Connect(coloured_shadow_mix_custom237.ins.Closure1);
			coloured_shadow_trans_color234.outs.BSDF.Connect(coloured_shadow_mix_custom237.ins.Closure2);
			weight_for_shadowray_coloured_shadow200.outs.Value.Connect(coloured_shadow_mix_custom237.ins.Fac);
			diffuse_texture208.outs.Alpha.Connect(invert_alpha191.ins.Value2);
			texcoord207.outs.UV.Connect(transparency_texture235.ins.Vector);
			transparency_texture235.outs.Color.Connect(transpluminance236.ins.Color);
			transpluminance236.outs.Val.Connect(invert_luminence201.ins.Value2);
			invert_luminence201.outs.Value.Connect(transparency_texture_amount202.ins.Value1);
			invert_alpha191.outs.Value.Connect(toggle_diffuse_texture_alpha_usage203.ins.Value1);
			transparency_texture_amount202.outs.Value.Connect(toggle_transparency_texture204.ins.Value2);
			coloured_shadow_mix_custom237.outs.Closure.Connect(add_emission_to_final248.ins.Closure1);
			diffuse_from_emission_color247.outs.BSDF.Connect(add_emission_to_final248.ins.Closure2);
			toggle_diffuse_texture_alpha_usage203.outs.Value.Connect(add_diffuse_texture_alpha205.ins.Value1);
			toggle_transparency_texture204.outs.Value.Connect(add_diffuse_texture_alpha205.ins.Value2);
			add_emission_to_final248.outs.Closure.Connect(custom_alpha_cutter239.ins.Closure1);
			transparent238.outs.BSDF.Connect(custom_alpha_cutter239.ins.Closure2);
			add_diffuse_texture_alpha205.outs.Value.Connect(custom_alpha_cutter239.ins.Fac);
			bump211.outs.Normal.Connect(principledbsdf240.ins.Normal);
			bump211.outs.Normal.Connect(principledbsdf240.ins.ClearcoatNormal);
			principledbsdf240.outs.BSDF.Connect(coloured_shadow_mix_glass_principled241.ins.Closure1);
			coloured_shadow_trans_color234.outs.BSDF.Connect(coloured_shadow_mix_glass_principled241.ins.Closure2);
			weight_for_shadowray_coloured_shadow200.outs.Value.Connect(coloured_shadow_mix_glass_principled241.ins.Fac);

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture208, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture208, texcoord207);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture209, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture209, texcoord207);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture235, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture235, texcoord207);
			}

			if (part.HasEnvironmentTexture)
			{
				RenderEngine.SetTextureImage(environment_texture228, part.EnvironmentTexture);
				RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture228, texcoord207);
			}

			if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass) return coloured_shadow_mix_glass_principled241;
			return custom_alpha_cutter239;
		}

	}
}
