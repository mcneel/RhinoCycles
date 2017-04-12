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
using RhinoCyclesCore.Core;

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
			var texcoord60 = new TextureCoordinateNode("texcoord");

			var invert_transparency79 = new MathNode("invert_transparency");
			invert_transparency79.ins.Value1.Value = 1f;
			invert_transparency79.ins.Value2.Value = part.Transparency;
			invert_transparency79.Operation = MathNode.Operations.Subtract;
			invert_transparency79.UseClamp = false;

			var diffuse_texture61 = new ImageTextureNode("diffuse_texture");
			diffuse_texture61.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture61.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture61.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture61.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
			diffuse_texture61.Interpolation = InterpolationType.Linear;
			diffuse_texture61.UseAlpha = true;
			diffuse_texture61.IsLinear = false;

			var weight_diffuse_amount_by_transparency_inv78 = new MathNode("weight_diffuse_amount_by_transparency_inv");
			weight_diffuse_amount_by_transparency_inv78.ins.Value1.Value = part.DiffuseTexture.Amount;
			weight_diffuse_amount_by_transparency_inv78.ins.Value2.Value = 0f;
			weight_diffuse_amount_by_transparency_inv78.Operation = MathNode.Operations.Multiply;
			weight_diffuse_amount_by_transparency_inv78.UseClamp = false;

			var invert_alpha76 = new MathNode("invert_alpha");
			invert_alpha76.ins.Value1.Value = 1f;
			invert_alpha76.ins.Value2.Value = 0f;
			invert_alpha76.Operation = MathNode.Operations.Subtract;
			invert_alpha76.UseClamp = false;

			var diffuse_texture_amount65 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount65.ins.Color1.Value = part.BaseColor;
			diffuse_texture_amount65.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount65.ins.Fac.Value = 0f;
			diffuse_texture_amount65.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount65.UseClamp = false;

			var honor_texture_repeat77 = new MathNode("honor_texture_repeat");
			honor_texture_repeat77.ins.Value1.Value = 1f;
			honor_texture_repeat77.ins.Value2.Value = part.DiffuseTexture.InvertRepeatAsFloat;
			honor_texture_repeat77.Operation = MathNode.Operations.Multiply;
			honor_texture_repeat77.UseClamp = false;

			var repeat_mixer75 = new MixNode("repeat_mixer");
			repeat_mixer75.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			repeat_mixer75.ins.Color2.Value = part.BaseColor;
			repeat_mixer75.ins.Fac.Value = 1f;
			repeat_mixer75.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			repeat_mixer75.UseClamp = false;

			var diffuse_behind_texture_through_alpha119 = new MixNode("diffuse_behind_texture_through_alpha");
			diffuse_behind_texture_through_alpha119.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_behind_texture_through_alpha119.ins.Color2.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_behind_texture_through_alpha119.ins.Fac.Value = part.Transparency;
			diffuse_behind_texture_through_alpha119.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			diffuse_behind_texture_through_alpha119.UseClamp = false;

			var multiply120 = new MathNode("multiply");
			multiply120.ins.Value1.Value = 0f;
			multiply120.ins.Value2.Value = part.DiffuseTexture.Amount;
			multiply120.Operation = MathNode.Operations.Multiply;
			multiply120.UseClamp = false;

			var multiply129 = new MathNode("multiply");
			multiply129.ins.Value1.Value = 0f;
			multiply129.ins.Value2.Value = 0f;
			multiply129.Operation = MathNode.Operations.Multiply;
			multiply129.UseClamp = false;

			var mix125 = new MixNode("mix");
			mix125.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			mix125.ins.Color2.Value = part.BaseColor;
			mix125.ins.Fac.Value = 0f;
			mix125.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			mix125.UseClamp = false;

			var separate_base_color73 = new SeparateRgbNode("separate_base_color");
			separate_base_color73.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_diffuse_texture_color72 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color72.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var subtract126 = new MathNode("subtract");
			subtract126.ins.Value1.Value = 0f;
			subtract126.ins.Value2.Value = 0f;
			subtract126.Operation = MathNode.Operations.Subtract;
			subtract126.UseClamp = false;

			var subtract127 = new MathNode("subtract");
			subtract127.ins.Value1.Value = 0f;
			subtract127.ins.Value2.Value = 0f;
			subtract127.Operation = MathNode.Operations.Subtract;
			subtract127.UseClamp = false;

			var subtract128 = new MathNode("subtract");
			subtract128.ins.Value1.Value = 0f;
			subtract128.ins.Value2.Value = 0f;
			subtract128.Operation = MathNode.Operations.Subtract;
			subtract128.UseClamp = false;

			var bump_texture69 = new ImageTextureNode("bump_texture");
			bump_texture69.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture69.Projection = TextureNode.TextureProjection.Flat;
			bump_texture69.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture69.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture69.Interpolation = InterpolationType.Linear;
			bump_texture69.UseAlpha = true;
			bump_texture69.IsLinear = false;

			var bump_texture_to_bw70 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw70.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount71 = new MathNode("bump_amount");
			bump_amount71.ins.Value1.Value = 4.66f;
			bump_amount71.ins.Value2.Value = part.BumpTexture.Amount;
			bump_amount71.Operation = MathNode.Operations.Multiply;
			bump_amount71.UseClamp = false;

			var final_base_color74 = new CombineRgbNode("final_base_color");
			final_base_color74.ins.R.Value = 0f;
			final_base_color74.ins.G.Value = 0f;
			final_base_color74.ins.B.Value = 0f;

			var bump68 = new BumpNode("bump");
			bump68.ins.Height.Value = 0f;
			bump68.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump68.ins.Strength.Value = 0f;
			bump68.ins.Distance.Value = 0.1f;

			var diffuse82 = new DiffuseBsdfNode("diffuse");
			diffuse82.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse82.ins.Roughness.Value = 0f;
			diffuse82.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf94 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf94.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf94.ins.Strength.Value = 1f;

			var attenuated_reflection_color104 = new MixNode("attenuated_reflection_color");
			attenuated_reflection_color104.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			attenuated_reflection_color104.ins.Color2.Value = part.ReflectionColorGamma;
			attenuated_reflection_color104.ins.Fac.Value = part.Reflectivity;
			attenuated_reflection_color104.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attenuated_reflection_color104.UseClamp = false;

			var fresnel_based_on_constant124 = new FresnelNode("fresnel_based_on_constant");
			fresnel_based_on_constant124.ins.IOR.Value = part.FresnelIOR;
			fresnel_based_on_constant124.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var simple_reflection110 = new CombineRgbNode("simple_reflection");
			simple_reflection110.ins.R.Value = part.Reflectivity;
			simple_reflection110.ins.G.Value = 0f;
			simple_reflection110.ins.B.Value = 0f;

			var fresnel_reflection111 = new CombineRgbNode("fresnel_reflection");
			fresnel_reflection111.ins.R.Value = 0f;
			fresnel_reflection111.ins.G.Value = 0f;
			fresnel_reflection111.ins.B.Value = 0f;

			var fresnel_reflection_if_reflection_used131 = new MathNode("fresnel_reflection_if_reflection_used");
			fresnel_reflection_if_reflection_used131.ins.Value1.Value = part.Reflectivity;
			fresnel_reflection_if_reflection_used131.ins.Value2.Value = part.FresnelReflectionsAsFloat;
			fresnel_reflection_if_reflection_used131.Operation = MathNode.Operations.Multiply;
			fresnel_reflection_if_reflection_used131.UseClamp = false;

			var select_reflection_or_fresnel_reflection109 = new MixNode("select_reflection_or_fresnel_reflection");
			select_reflection_or_fresnel_reflection109.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			select_reflection_or_fresnel_reflection109.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			select_reflection_or_fresnel_reflection109.ins.Fac.Value = 1f;
			select_reflection_or_fresnel_reflection109.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			select_reflection_or_fresnel_reflection109.UseClamp = false;

			var shadeless95 = new MixClosureNode("shadeless");
			shadeless95.ins.Fac.Value = part.ShadelessAsFloat;

			var glossy96 = new GlossyBsdfNode("glossy");
			glossy96.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			glossy96.ins.Roughness.Value = part.ReflectionRoughnessPow2;
			glossy96.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var reflection_factor112 = new SeparateRgbNode("reflection_factor");
			reflection_factor112.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var attennuated_refraction_color106 = new MixNode("attennuated_refraction_color");
			attennuated_refraction_color106.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			attennuated_refraction_color106.ins.Color2.Value = part.TransparencyColorGamma;
			attennuated_refraction_color106.ins.Fac.Value = part.Transparency;
			attennuated_refraction_color106.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attennuated_refraction_color106.UseClamp = false;

			var refraction84 = new RefractionBsdfNode("refraction");
			refraction84.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			refraction84.ins.Roughness.Value = part.RefractionRoughnessPow2;
			refraction84.ins.IOR.Value = part.IOR;
			refraction84.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			refraction84.Distribution = RefractionBsdfNode.RefractionDistribution.Beckmann;

			var diffuse_plus_glossy107 = new MixClosureNode("diffuse_plus_glossy");
			diffuse_plus_glossy107.ins.Fac.Value = 0f;

			var blend_in_transparency83 = new MixClosureNode("blend_in_transparency");
			blend_in_transparency83.ins.Fac.Value = part.Transparency;

			var separate_xyz100 = new SeparateXyzNode("separate_xyz");
			separate_xyz100.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var multiply101 = new MathNode("multiply");
			multiply101.ins.Value1.Value = 0f;
			multiply101.ins.Value2.Value = -1f;
			multiply101.Operation = MathNode.Operations.Multiply;
			multiply101.UseClamp = false;

			var combine_xyz99 = new CombineXyzNode("combine_xyz");
			combine_xyz99.ins.X.Value = 0f;
			combine_xyz99.ins.Y.Value = 0f;
			combine_xyz99.ins.Z.Value = 0f;

			var environment_texture97 = new ImageTextureNode("environment_texture");
			environment_texture97.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			environment_texture97.Projection = TextureNode.TextureProjection.Flat;
			environment_texture97.ColorSpace = TextureNode.TextureColorSpace.None;
			environment_texture97.Extension = TextureNode.TextureExtension.Repeat;
			environment_texture97.Interpolation = InterpolationType.Linear;
			environment_texture97.UseAlpha = true;
			environment_texture97.IsLinear = false;

			var attenuated_environment_color102 = new MixNode("attenuated_environment_color");
			attenuated_environment_color102.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			attenuated_environment_color102.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			attenuated_environment_color102.ins.Fac.Value = part.EnvironmentTexture.Amount;
			attenuated_environment_color102.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attenuated_environment_color102.UseClamp = false;

			var diffuse_glossy_and_refraction108 = new MixClosureNode("diffuse_glossy_and_refraction");
			diffuse_glossy_and_refraction108.ins.Fac.Value = part.Transparency;

			var diffuse98 = new DiffuseBsdfNode("diffuse");
			diffuse98.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse98.ins.Roughness.Value = 0f;
			diffuse98.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness88 = new MathNode("invert_roughness");
			invert_roughness88.ins.Value1.Value = 1f;
			invert_roughness88.ins.Value2.Value = part.RefractionRoughnessPow2;
			invert_roughness88.Operation = MathNode.Operations.Subtract;
			invert_roughness88.UseClamp = false;

			var multiply_transparency89 = new MathNode("multiply_transparency");
			multiply_transparency89.ins.Value1.Value = 1f;
			multiply_transparency89.ins.Value2.Value = part.Transparency;
			multiply_transparency89.Operation = MathNode.Operations.Multiply;
			multiply_transparency89.UseClamp = false;

			var light_path86 = new LightPathNode("light_path");

			var multiply_with_shadowray90 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray90.ins.Value1.Value = 1f;
			multiply_with_shadowray90.ins.Value2.Value = 0f;
			multiply_with_shadowray90.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray90.UseClamp = false;

			var custom_environment_blend103 = new MixClosureNode("custom_environment_blend");
			custom_environment_blend103.ins.Fac.Value = part.EnvironmentTexture.Amount;

			var coloured_shadow_trans_color87 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color87.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow91 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow91.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow91.ins.Value2.Value = 1f;
			weight_for_shadowray_coloured_shadow91.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow91.UseClamp = false;

			var transparency_texture62 = new ImageTextureNode("transparency_texture");
			transparency_texture62.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			transparency_texture62.Projection = TextureNode.TextureProjection.Flat;
			transparency_texture62.ColorSpace = TextureNode.TextureColorSpace.None;
			transparency_texture62.Extension = TextureNode.TextureExtension.Repeat;
			transparency_texture62.Interpolation = InterpolationType.Linear;
			transparency_texture62.UseAlpha = true;
			transparency_texture62.IsLinear = false;

			var transpluminance63 = new RgbToLuminanceNode("transpluminance");
			transpluminance63.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var invert_luminence64 = new MathNode("invert_luminence");
			invert_luminence64.ins.Value1.Value = 1f;
			invert_luminence64.ins.Value2.Value = 0f;
			invert_luminence64.Operation = MathNode.Operations.Subtract;
			invert_luminence64.UseClamp = false;

			var transparency_texture_amount67 = new MathNode("transparency_texture_amount");
			transparency_texture_amount67.ins.Value1.Value = 1f;
			transparency_texture_amount67.ins.Value2.Value = part.TransparencyTexture.Amount;
			transparency_texture_amount67.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount67.UseClamp = false;

			var toggle_diffuse_texture_alpha_usage117 = new MathNode("toggle_diffuse_texture_alpha_usage");
			toggle_diffuse_texture_alpha_usage117.ins.Value1.Value = 1f;
			toggle_diffuse_texture_alpha_usage117.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
			toggle_diffuse_texture_alpha_usage117.Operation = MathNode.Operations.Multiply;
			toggle_diffuse_texture_alpha_usage117.UseClamp = false;

			var toggle_transparency_texture66 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture66.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
			toggle_transparency_texture66.ins.Value2.Value = 0f;
			toggle_transparency_texture66.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture66.UseClamp = false;

			var coloured_shadow_mix_custom93 = new MixClosureNode("coloured_shadow_mix_custom");
			coloured_shadow_mix_custom93.ins.Fac.Value = 0f;

			var transparent80 = new TransparentBsdfNode("transparent");
			transparent80.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var add_diffuse_texture_alpha118 = new MathNode("add_diffuse_texture_alpha");
			add_diffuse_texture_alpha118.ins.Value1.Value = 0f;
			add_diffuse_texture_alpha118.ins.Value2.Value = 0f;
			add_diffuse_texture_alpha118.Operation = MathNode.Operations.Add;
			add_diffuse_texture_alpha118.UseClamp = false;

			var custom_alpha_cutter85 = new MixClosureNode("custom_alpha_cutter");
			custom_alpha_cutter85.ins.Fac.Value = 0f;

			var principledbsdf132 = new UberBsdfNode("principledbsdf");
			principledbsdf132.ins.BaseColor.Value = part.TransparencyColorGamma;
			principledbsdf132.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			principledbsdf132.ins.Metallic.Value = 0f;
			principledbsdf132.ins.Subsurface.Value = 0f;
			principledbsdf132.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principledbsdf132.ins.Specular.Value = 0f;
			principledbsdf132.ins.Roughness.Value = part.ReflectionRoughnessPow2;
			principledbsdf132.ins.SpecularTint.Value = 0f;
			principledbsdf132.ins.Anisotropic.Value = 0f;
			principledbsdf132.ins.Sheen.Value = 0f;
			principledbsdf132.ins.SheenTint.Value = 0f;
			principledbsdf132.ins.Clearcoat.Value = 0f;
			principledbsdf132.ins.ClearcoatGloss.Value = 0f;
			principledbsdf132.ins.IOR.Value = part.IOR;
			principledbsdf132.ins.Transparency.Value = part.Transparency;
			principledbsdf132.ins.RefractionRoughness.Value = part.RefractionRoughnessPow2;
			principledbsdf132.ins.AnisotropicRotation.Value = 0f;
			principledbsdf132.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principledbsdf132.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			principledbsdf132.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var coloured_shadow_mix_glass_principled116 = new MixClosureNode("coloured_shadow_mix_glass_principled");
			coloured_shadow_mix_glass_principled116.ins.Fac.Value = 0f;


			m_shader.AddNode(texcoord60);
			m_shader.AddNode(invert_transparency79);
			m_shader.AddNode(diffuse_texture61);
			m_shader.AddNode(weight_diffuse_amount_by_transparency_inv78);
			m_shader.AddNode(invert_alpha76);
			m_shader.AddNode(diffuse_texture_amount65);
			m_shader.AddNode(honor_texture_repeat77);
			m_shader.AddNode(repeat_mixer75);
			m_shader.AddNode(diffuse_behind_texture_through_alpha119);
			m_shader.AddNode(multiply120);
			m_shader.AddNode(multiply129);
			m_shader.AddNode(mix125);
			m_shader.AddNode(separate_base_color73);
			m_shader.AddNode(separate_diffuse_texture_color72);
			m_shader.AddNode(subtract126);
			m_shader.AddNode(subtract127);
			m_shader.AddNode(subtract128);
			m_shader.AddNode(bump_texture69);
			m_shader.AddNode(bump_texture_to_bw70);
			m_shader.AddNode(bump_amount71);
			m_shader.AddNode(final_base_color74);
			m_shader.AddNode(bump68);
			m_shader.AddNode(diffuse82);
			m_shader.AddNode(shadeless_bsdf94);
			m_shader.AddNode(attenuated_reflection_color104);
			m_shader.AddNode(fresnel_based_on_constant124);
			m_shader.AddNode(simple_reflection110);
			m_shader.AddNode(fresnel_reflection111);
			m_shader.AddNode(fresnel_reflection_if_reflection_used131);
			m_shader.AddNode(select_reflection_or_fresnel_reflection109);
			m_shader.AddNode(shadeless95);
			m_shader.AddNode(glossy96);
			m_shader.AddNode(reflection_factor112);
			m_shader.AddNode(attennuated_refraction_color106);
			m_shader.AddNode(refraction84);
			m_shader.AddNode(diffuse_plus_glossy107);
			m_shader.AddNode(blend_in_transparency83);
			m_shader.AddNode(separate_xyz100);
			m_shader.AddNode(multiply101);
			m_shader.AddNode(combine_xyz99);
			m_shader.AddNode(environment_texture97);
			m_shader.AddNode(attenuated_environment_color102);
			m_shader.AddNode(diffuse_glossy_and_refraction108);
			m_shader.AddNode(diffuse98);
			m_shader.AddNode(invert_roughness88);
			m_shader.AddNode(multiply_transparency89);
			m_shader.AddNode(light_path86);
			m_shader.AddNode(multiply_with_shadowray90);
			m_shader.AddNode(custom_environment_blend103);
			m_shader.AddNode(coloured_shadow_trans_color87);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow91);
			m_shader.AddNode(transparency_texture62);
			m_shader.AddNode(transpluminance63);
			m_shader.AddNode(invert_luminence64);
			m_shader.AddNode(transparency_texture_amount67);
			m_shader.AddNode(toggle_diffuse_texture_alpha_usage117);
			m_shader.AddNode(toggle_transparency_texture66);
			m_shader.AddNode(coloured_shadow_mix_custom93);
			m_shader.AddNode(transparent80);
			m_shader.AddNode(add_diffuse_texture_alpha118);
			m_shader.AddNode(custom_alpha_cutter85);
			m_shader.AddNode(principledbsdf132);
			m_shader.AddNode(coloured_shadow_mix_glass_principled116);


			texcoord60.outs.UV.Connect(diffuse_texture61.ins.Vector);
			invert_transparency79.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv78.ins.Value2);
			diffuse_texture61.outs.Alpha.Connect(invert_alpha76.ins.Value2);
			diffuse_texture61.outs.Color.Connect(diffuse_texture_amount65.ins.Color2);
			weight_diffuse_amount_by_transparency_inv78.outs.Value.Connect(diffuse_texture_amount65.ins.Fac);
			invert_alpha76.outs.Value.Connect(honor_texture_repeat77.ins.Value1);
			diffuse_texture_amount65.outs.Color.Connect(repeat_mixer75.ins.Color1);
			honor_texture_repeat77.outs.Value.Connect(repeat_mixer75.ins.Fac);
			repeat_mixer75.outs.Color.Connect(diffuse_behind_texture_through_alpha119.ins.Color1);
			diffuse_texture61.outs.Alpha.Connect(multiply120.ins.Value1);
			multiply120.outs.Value.Connect(multiply129.ins.Value1);
			invert_transparency79.outs.Value.Connect(multiply129.ins.Value2);
			multiply129.outs.Value.Connect(mix125.ins.Fac);
			diffuse_behind_texture_through_alpha119.outs.Color.Connect(separate_base_color73.ins.Image);
			mix125.outs.Color.Connect(separate_diffuse_texture_color72.ins.Image);
			separate_base_color73.outs.R.Connect(subtract126.ins.Value1);
			separate_diffuse_texture_color72.outs.R.Connect(subtract126.ins.Value2);
			separate_base_color73.outs.G.Connect(subtract127.ins.Value1);
			separate_diffuse_texture_color72.outs.G.Connect(subtract127.ins.Value2);
			separate_base_color73.outs.B.Connect(subtract128.ins.Value1);
			separate_diffuse_texture_color72.outs.B.Connect(subtract128.ins.Value2);
			texcoord60.outs.UV.Connect(bump_texture69.ins.Vector);
			bump_texture69.outs.Color.Connect(bump_texture_to_bw70.ins.Color);
			subtract126.outs.Value.Connect(final_base_color74.ins.R);
			subtract127.outs.Value.Connect(final_base_color74.ins.G);
			subtract128.outs.Value.Connect(final_base_color74.ins.B);
			bump_texture_to_bw70.outs.Val.Connect(bump68.ins.Height);
			bump_amount71.outs.Value.Connect(bump68.ins.Strength);
			final_base_color74.outs.Image.Connect(diffuse82.ins.Color);
			bump68.outs.Normal.Connect(diffuse82.ins.Normal);
			final_base_color74.outs.Image.Connect(shadeless_bsdf94.ins.Color);
			bump68.outs.Normal.Connect(fresnel_based_on_constant124.ins.Normal);
			fresnel_based_on_constant124.outs.Fac.Connect(fresnel_reflection111.ins.R);
			simple_reflection110.outs.Image.Connect(select_reflection_or_fresnel_reflection109.ins.Color1);
			fresnel_reflection111.outs.Image.Connect(select_reflection_or_fresnel_reflection109.ins.Color2);
			fresnel_reflection_if_reflection_used131.outs.Value.Connect(select_reflection_or_fresnel_reflection109.ins.Fac);
			diffuse82.outs.BSDF.Connect(shadeless95.ins.Closure1);
			shadeless_bsdf94.outs.Emission.Connect(shadeless95.ins.Closure2);
			attenuated_reflection_color104.outs.Color.Connect(glossy96.ins.Color);
			bump68.outs.Normal.Connect(glossy96.ins.Normal);
			select_reflection_or_fresnel_reflection109.outs.Color.Connect(reflection_factor112.ins.Image);
			attennuated_refraction_color106.outs.Color.Connect(refraction84.ins.Color);
			bump68.outs.Normal.Connect(refraction84.ins.Normal);
			shadeless95.outs.Closure.Connect(diffuse_plus_glossy107.ins.Closure1);
			glossy96.outs.BSDF.Connect(diffuse_plus_glossy107.ins.Closure2);
			reflection_factor112.outs.R.Connect(diffuse_plus_glossy107.ins.Fac);
			shadeless95.outs.Closure.Connect(blend_in_transparency83.ins.Closure1);
			refraction84.outs.BSDF.Connect(blend_in_transparency83.ins.Closure2);
			texcoord60.outs.EnvEmap.Connect(separate_xyz100.ins.Vector);
			separate_xyz100.outs.Y.Connect(multiply101.ins.Value1);
			separate_xyz100.outs.X.Connect(combine_xyz99.ins.X);
			multiply101.outs.Value.Connect(combine_xyz99.ins.Y);
			separate_xyz100.outs.Z.Connect(combine_xyz99.ins.Z);
			combine_xyz99.outs.Vector.Connect(environment_texture97.ins.Vector);
			environment_texture97.outs.Color.Connect(attenuated_environment_color102.ins.Color2);
			diffuse_plus_glossy107.outs.Closure.Connect(diffuse_glossy_and_refraction108.ins.Closure1);
			blend_in_transparency83.outs.Closure.Connect(diffuse_glossy_and_refraction108.ins.Closure2);
			attenuated_environment_color102.outs.Color.Connect(diffuse98.ins.Color);
			invert_roughness88.outs.Value.Connect(multiply_transparency89.ins.Value1);
			multiply_transparency89.outs.Value.Connect(multiply_with_shadowray90.ins.Value1);
			light_path86.outs.IsShadowRay.Connect(multiply_with_shadowray90.ins.Value2);
			diffuse_glossy_and_refraction108.outs.Closure.Connect(custom_environment_blend103.ins.Closure1);
			diffuse98.outs.BSDF.Connect(custom_environment_blend103.ins.Closure2);
			final_base_color74.outs.Image.Connect(coloured_shadow_trans_color87.ins.Color);
			multiply_with_shadowray90.outs.Value.Connect(weight_for_shadowray_coloured_shadow91.ins.Value1);
			texcoord60.outs.UV.Connect(transparency_texture62.ins.Vector);
			transparency_texture62.outs.Color.Connect(transpluminance63.ins.Color);
			transpluminance63.outs.Val.Connect(invert_luminence64.ins.Value2);
			invert_luminence64.outs.Value.Connect(transparency_texture_amount67.ins.Value1);
			invert_alpha76.outs.Value.Connect(toggle_diffuse_texture_alpha_usage117.ins.Value1);
			transparency_texture_amount67.outs.Value.Connect(toggle_transparency_texture66.ins.Value2);
			custom_environment_blend103.outs.Closure.Connect(coloured_shadow_mix_custom93.ins.Closure1);
			coloured_shadow_trans_color87.outs.BSDF.Connect(coloured_shadow_mix_custom93.ins.Closure2);
			weight_for_shadowray_coloured_shadow91.outs.Value.Connect(coloured_shadow_mix_custom93.ins.Fac);
			toggle_diffuse_texture_alpha_usage117.outs.Value.Connect(add_diffuse_texture_alpha118.ins.Value1);
			toggle_transparency_texture66.outs.Value.Connect(add_diffuse_texture_alpha118.ins.Value2);
			coloured_shadow_mix_custom93.outs.Closure.Connect(custom_alpha_cutter85.ins.Closure1);
			transparent80.outs.BSDF.Connect(custom_alpha_cutter85.ins.Closure2);
			add_diffuse_texture_alpha118.outs.Value.Connect(custom_alpha_cutter85.ins.Fac);
			bump68.outs.Normal.Connect(principledbsdf132.ins.Normal);
			bump68.outs.Normal.Connect(principledbsdf132.ins.ClearcoatNormal);
			principledbsdf132.outs.BSDF.Connect(coloured_shadow_mix_glass_principled116.ins.Closure1);
			coloured_shadow_trans_color87.outs.BSDF.Connect(coloured_shadow_mix_glass_principled116.ins.Closure2);
			weight_for_shadowray_coloured_shadow91.outs.Value.Connect(coloured_shadow_mix_glass_principled116.ins.Fac);

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture61, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture61, texcoord60);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture69, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture69, texcoord60);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture62, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture62, texcoord60);
			}

			if (part.HasEnvironmentTexture)
			{
				RenderEngine.SetTextureImage(environment_texture97, part.EnvironmentTexture);
				RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture97, texcoord60);
			}

			if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass) return coloured_shadow_mix_glass_principled116;
			return custom_alpha_cutter85;
		}

	}
}
