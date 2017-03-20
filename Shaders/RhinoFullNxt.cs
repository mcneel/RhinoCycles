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
			var invert_shine_factor127 = new MathNode("invert_shine_factor");
			invert_shine_factor127.ins.Value1.Value = 1f;
			invert_shine_factor127.ins.Value2.Value = part.Shine;
			invert_shine_factor127.Operation = MathNode.Operations.Subtract;
			invert_shine_factor127.UseClamp = false;

			var pow2125 = new MathNode("pow2");
			pow2125.ins.Value1.Value = 1f;
			pow2125.ins.Value2.Value = 1f;
			pow2125.Operation = MathNode.Operations.Multiply;
			pow2125.UseClamp = false;

			var texcoord60 = new TextureCoordinateNode("texcoord");

			var bump_texture71 = new ImageTextureNode("bump_texture");
			bump_texture71.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump_texture71.Projection = TextureNode.TextureProjection.Flat;
			bump_texture71.ColorSpace = TextureNode.TextureColorSpace.None;
			bump_texture71.Extension = TextureNode.TextureExtension.Repeat;
			bump_texture71.Interpolation = InterpolationType.Linear;
			bump_texture71.UseAlpha = true;
			bump_texture71.IsLinear = false;

			var bump_texture_to_bw72 = new RgbToBwNode("bump_texture_to_bw");
			bump_texture_to_bw72.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var bump_amount73 = new MathNode("bump_amount");
			bump_amount73.ins.Value1.Value = 4.66f;
			bump_amount73.ins.Value2.Value = part.BumpTexture.Amount;
			bump_amount73.Operation = MathNode.Operations.Multiply;
			bump_amount73.UseClamp = false;

			var attenuated_gloss_color118 = new MixNode("attenuated_gloss_color");
			attenuated_gloss_color118.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			attenuated_gloss_color118.ins.Color2.Value = part.SpecularColor;
			attenuated_gloss_color118.ins.Fac.Value = part.Shine;
			attenuated_gloss_color118.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attenuated_gloss_color118.UseClamp = false;

			var add126 = new MathNode("add");
			add126.ins.Value1.Value = 0.2f;
			add126.ins.Value2.Value = 1f;
			add126.Operation = MathNode.Operations.Add;
			add126.UseClamp = true;

			var bump70 = new BumpNode("bump");
			bump70.ins.Height.Value = 0f;
			bump70.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			bump70.ins.Strength.Value = 0f;
			bump70.ins.Distance.Value = 0.1f;

			var glossiness_or_shine119 = new GlossyBsdfNode("glossiness_or_shine");
			glossiness_or_shine119.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			glossiness_or_shine119.ins.Roughness.Value = 1f;
			glossiness_or_shine119.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var diffuse_texture61 = new ImageTextureNode("diffuse_texture");
			diffuse_texture61.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture61.Projection = TextureNode.TextureProjection.Flat;
			diffuse_texture61.ColorSpace = TextureNode.TextureColorSpace.None;
			diffuse_texture61.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
			diffuse_texture61.Interpolation = InterpolationType.Linear;
			diffuse_texture61.UseAlpha = true;
			diffuse_texture61.IsLinear = false;

			var invert_alpha81 = new MathNode("invert_alpha");
			invert_alpha81.ins.Value1.Value = 1f;
			invert_alpha81.ins.Value2.Value = 0f;
			invert_alpha81.Operation = MathNode.Operations.Subtract;
			invert_alpha81.UseClamp = false;

			var honor_texture_repeat82 = new MathNode("honor_texture_repeat");
			honor_texture_repeat82.ins.Value1.Value = 1f;
			honor_texture_repeat82.ins.Value2.Value = part.DiffuseTexture.InvertRepeatAsFloat;
			honor_texture_repeat82.Operation = MathNode.Operations.Multiply;
			honor_texture_repeat82.UseClamp = false;

			var invert_transparency84 = new MathNode("invert_transparency");
			invert_transparency84.ins.Value1.Value = 1f;
			invert_transparency84.ins.Value2.Value = part.Transparency;
			invert_transparency84.Operation = MathNode.Operations.Subtract;
			invert_transparency84.UseClamp = false;

			var repeat_mixer80 = new MixNode("repeat_mixer");
			repeat_mixer80.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			repeat_mixer80.ins.Color2.Value = part.BaseColor;
			repeat_mixer80.ins.Fac.Value = 0f;
			repeat_mixer80.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			repeat_mixer80.UseClamp = false;

			var weight_diffuse_amount_by_transparency_inv83 = new MathNode("weight_diffuse_amount_by_transparency_inv");
			weight_diffuse_amount_by_transparency_inv83.ins.Value1.Value = part.DiffuseTexture.Amount;
			weight_diffuse_amount_by_transparency_inv83.ins.Value2.Value = 1f;
			weight_diffuse_amount_by_transparency_inv83.Operation = MathNode.Operations.Multiply;
			weight_diffuse_amount_by_transparency_inv83.UseClamp = false;

			var diffuse_texture_amount65 = new MixNode("diffuse_texture_amount");
			diffuse_texture_amount65.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_texture_amount65.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse_texture_amount65.ins.Fac.Value = 1f;
			diffuse_texture_amount65.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_texture_amount65.UseClamp = false;

			var invert_diffuse_color_amount68 = new MathNode("invert_diffuse_color_amount");
			invert_diffuse_color_amount68.ins.Value1.Value = 1f;
			invert_diffuse_color_amount68.ins.Value2.Value = 1f;
			invert_diffuse_color_amount68.Operation = MathNode.Operations.Subtract;
			invert_diffuse_color_amount68.UseClamp = false;

			var diffuse_col_amount67 = new MixNode("diffuse_col_amount");
			diffuse_col_amount67.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			diffuse_col_amount67.ins.Color2.Value = part.BaseColor;
			diffuse_col_amount67.ins.Fac.Value = 0f;
			diffuse_col_amount67.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Add;
			diffuse_col_amount67.UseClamp = false;

			var multiply142 = new MathNode("multiply");
			multiply142.ins.Value1.Value = 0f;
			multiply142.ins.Value2.Value = part.DiffuseTexture.Amount;
			multiply142.Operation = MathNode.Operations.Multiply;
			multiply142.UseClamp = false;

			var mix135 = new MixNode("mix");
			mix135.ins.Color1.Value = part.BaseColor;
			mix135.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			mix135.ins.Fac.Value = 0f;
			mix135.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			mix135.UseClamp = false;

			var separate_diffuse_texture_color136 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color136.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color137 = new SeparateRgbNode("separate_base_color");
			separate_base_color137.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r138 = new MathNode("add_base_color_r");
			add_base_color_r138.ins.Value1.Value = 0f;
			add_base_color_r138.ins.Value2.Value = 0f;
			add_base_color_r138.Operation = MathNode.Operations.Add;
			add_base_color_r138.UseClamp = true;

			var add_base_color_g139 = new MathNode("add_base_color_g");
			add_base_color_g139.ins.Value1.Value = 0f;
			add_base_color_g139.ins.Value2.Value = 0f;
			add_base_color_g139.Operation = MathNode.Operations.Add;
			add_base_color_g139.UseClamp = true;

			var add_base_color_b140 = new MathNode("add_base_color_b");
			add_base_color_b140.ins.Value1.Value = 0f;
			add_base_color_b140.ins.Value2.Value = 0f;
			add_base_color_b140.Operation = MathNode.Operations.Add;
			add_base_color_b140.UseClamp = true;

			var diffuse_plus_texture_alphaed141 = new CombineRgbNode("diffuse_plus_texture_alphaed");
			diffuse_plus_texture_alphaed141.ins.R.Value = 0f;
			diffuse_plus_texture_alphaed141.ins.G.Value = 0f;
			diffuse_plus_texture_alphaed141.ins.B.Value = 0f;

			var separate_diffuse_texture_color74 = new SeparateRgbNode("separate_diffuse_texture_color");
			separate_diffuse_texture_color74.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var separate_base_color75 = new SeparateRgbNode("separate_base_color");
			separate_base_color75.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var add_base_color_r76 = new MathNode("add_base_color_r");
			add_base_color_r76.ins.Value1.Value = 0f;
			add_base_color_r76.ins.Value2.Value = 0f;
			add_base_color_r76.Operation = MathNode.Operations.Add;
			add_base_color_r76.UseClamp = true;

			var add_base_color_g77 = new MathNode("add_base_color_g");
			add_base_color_g77.ins.Value1.Value = 0f;
			add_base_color_g77.ins.Value2.Value = 0f;
			add_base_color_g77.Operation = MathNode.Operations.Add;
			add_base_color_g77.UseClamp = true;

			var add_base_color_b78 = new MathNode("add_base_color_b");
			add_base_color_b78.ins.Value1.Value = 0f;
			add_base_color_b78.ins.Value2.Value = 0f;
			add_base_color_b78.Operation = MathNode.Operations.Add;
			add_base_color_b78.UseClamp = true;

			var final_base_color79 = new CombineRgbNode("final_base_color");
			final_base_color79.ins.R.Value = 0f;
			final_base_color79.ins.G.Value = 0f;
			final_base_color79.ins.B.Value = 0f;

			var diffuse87 = new DiffuseBsdfNode("diffuse");
			diffuse87.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse87.ins.Roughness.Value = 0f;
			diffuse87.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var shadeless_bsdf99 = new EmissionNode("shadeless_bsdf");
			shadeless_bsdf99.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			shadeless_bsdf99.ins.Strength.Value = 1f;

			var attenuated_reflection_color109 = new MixNode("attenuated_reflection_color");
			attenuated_reflection_color109.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			attenuated_reflection_color109.ins.Color2.Value = part.ReflectionColor;
			attenuated_reflection_color109.ins.Fac.Value = part.Reflectivity;
			attenuated_reflection_color109.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attenuated_reflection_color109.UseClamp = false;

			var fresnel110 = new FresnelNode("fresnel");
			fresnel110.ins.IOR.Value = part.IOR;
			fresnel110.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var simple_reflection115 = new CombineRgbNode("simple_reflection");
			simple_reflection115.ins.R.Value = part.Reflectivity;
			simple_reflection115.ins.G.Value = 0f;
			simple_reflection115.ins.B.Value = 0f;

			var fresnel_reflection116 = new CombineRgbNode("fresnel_reflection");
			fresnel_reflection116.ins.R.Value = 0f;
			fresnel_reflection116.ins.G.Value = 0f;
			fresnel_reflection116.ins.B.Value = 0f;

			var select_reflection_or_fresnel_reflection114 = new MixNode("select_reflection_or_fresnel_reflection");
			select_reflection_or_fresnel_reflection114.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			select_reflection_or_fresnel_reflection114.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			select_reflection_or_fresnel_reflection114.ins.Fac.Value = part.FresnelReflectionsAsFloat;
			select_reflection_or_fresnel_reflection114.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			select_reflection_or_fresnel_reflection114.UseClamp = false;

			var shadeless100 = new MixClosureNode("shadeless");
			shadeless100.ins.Fac.Value = part.ShadelessAsFloat;

			var glossy101 = new GlossyBsdfNode("glossy");
			glossy101.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			glossy101.ins.Roughness.Value = part.ReflectionRoughnessPow2;
			glossy101.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var reflection_factor117 = new SeparateRgbNode("reflection_factor");
			reflection_factor117.ins.Image.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var attenuate_shine_bsdf129 = new MixClosureNode("attenuate_shine_bsdf");
			attenuate_shine_bsdf129.ins.Fac.Value = 1f;

			var diffuse_plus_glossy112 = new MixClosureNode("diffuse_plus_glossy");
			diffuse_plus_glossy112.ins.Fac.Value = 0f;

			var attennuated_refraction_color111 = new MixNode("attennuated_refraction_color");
			attennuated_refraction_color111.ins.Color1.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			attennuated_refraction_color111.ins.Color2.Value = part.TransparencyColor;
			attennuated_refraction_color111.ins.Fac.Value = part.Transparency;
			attennuated_refraction_color111.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attennuated_refraction_color111.UseClamp = false;

			var refraction89 = new RefractionBsdfNode("refraction");
			refraction89.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			refraction89.ins.Roughness.Value = part.RefractionRoughnessPow2;
			refraction89.ins.IOR.Value = part.IOR;
			refraction89.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			refraction89.Distribution = RefractionBsdfNode.RefractionDistribution.Beckmann;

			var add128 = new AddClosureNode("add");

			var blend_in_transparency88 = new MixClosureNode("blend_in_transparency");
			blend_in_transparency88.ins.Fac.Value = part.Transparency;

			var separate_xyz105 = new SeparateXyzNode("separate_xyz");
			separate_xyz105.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var multiply106 = new MathNode("multiply");
			multiply106.ins.Value1.Value = 0f;
			multiply106.ins.Value2.Value = -1f;
			multiply106.Operation = MathNode.Operations.Multiply;
			multiply106.UseClamp = false;

			var combine_xyz104 = new CombineXyzNode("combine_xyz");
			combine_xyz104.ins.X.Value = 0f;
			combine_xyz104.ins.Y.Value = 0f;
			combine_xyz104.ins.Z.Value = 0f;

			var environment_texture102 = new ImageTextureNode("environment_texture");
			environment_texture102.ins.Vector.Value = new ccl.float4(0f, 0f, 0f, 1f);
			environment_texture102.Projection = TextureNode.TextureProjection.Flat;
			environment_texture102.ColorSpace = TextureNode.TextureColorSpace.None;
			environment_texture102.Extension = TextureNode.TextureExtension.Repeat;
			environment_texture102.Interpolation = InterpolationType.Linear;
			environment_texture102.UseAlpha = true;
			environment_texture102.IsLinear = false;

			var attenuated_environment_color107 = new MixNode("attenuated_environment_color");
			attenuated_environment_color107.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
			attenuated_environment_color107.ins.Color2.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			attenuated_environment_color107.ins.Fac.Value = part.EnvironmentTexture.Amount;
			attenuated_environment_color107.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
			attenuated_environment_color107.UseClamp = false;

			var diffuse_glossy_and_refraction113 = new MixClosureNode("diffuse_glossy_and_refraction");
			diffuse_glossy_and_refraction113.ins.Fac.Value = part.Transparency;

			var diffuse103 = new DiffuseBsdfNode("diffuse");
			diffuse103.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			diffuse103.ins.Roughness.Value = 0f;
			diffuse103.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var invert_roughness93 = new MathNode("invert_roughness");
			invert_roughness93.ins.Value1.Value = 1f;
			invert_roughness93.ins.Value2.Value = part.RefractionRoughnessPow2;
			invert_roughness93.Operation = MathNode.Operations.Subtract;
			invert_roughness93.UseClamp = false;

			var multiply_transparency94 = new MathNode("multiply_transparency");
			multiply_transparency94.ins.Value1.Value = 1f;
			multiply_transparency94.ins.Value2.Value = part.Transparency;
			multiply_transparency94.Operation = MathNode.Operations.Multiply;
			multiply_transparency94.UseClamp = false;

			var light_path91 = new LightPathNode("light_path");

			var multiply_with_shadowray95 = new MathNode("multiply_with_shadowray");
			multiply_with_shadowray95.ins.Value1.Value = 0f;
			multiply_with_shadowray95.ins.Value2.Value = 0f;
			multiply_with_shadowray95.Operation = MathNode.Operations.Multiply;
			multiply_with_shadowray95.UseClamp = false;

			var custom_environment_blend108 = new MixClosureNode("custom_environment_blend");
			custom_environment_blend108.ins.Fac.Value = part.EnvironmentTexture.Amount;

			var coloured_shadow_trans_color92 = new TransparentBsdfNode("coloured_shadow_trans_color");
			coloured_shadow_trans_color92.ins.Color.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);

			var weight_for_shadowray_coloured_shadow96 = new MathNode("weight_for_shadowray_coloured_shadow");
			weight_for_shadowray_coloured_shadow96.ins.Value1.Value = 0f;
			weight_for_shadowray_coloured_shadow96.ins.Value2.Value = 1f;
			weight_for_shadowray_coloured_shadow96.Operation = MathNode.Operations.Multiply;
			weight_for_shadowray_coloured_shadow96.UseClamp = false;

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

			var transparency_texture_amount69 = new MathNode("transparency_texture_amount");
			transparency_texture_amount69.ins.Value1.Value = 1f;
			transparency_texture_amount69.ins.Value2.Value = part.TransparencyTexture.Amount;
			transparency_texture_amount69.Operation = MathNode.Operations.Multiply;
			transparency_texture_amount69.UseClamp = false;

			var toggle_diffuse_texture_alpha_usage133 = new MathNode("toggle_diffuse_texture_alpha_usage");
			toggle_diffuse_texture_alpha_usage133.ins.Value1.Value = 1f;
			toggle_diffuse_texture_alpha_usage133.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
			toggle_diffuse_texture_alpha_usage133.Operation = MathNode.Operations.Multiply;
			toggle_diffuse_texture_alpha_usage133.UseClamp = false;

			var toggle_transparency_texture66 = new MathNode("toggle_transparency_texture");
			toggle_transparency_texture66.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
			toggle_transparency_texture66.ins.Value2.Value = 0f;
			toggle_transparency_texture66.Operation = MathNode.Operations.Multiply;
			toggle_transparency_texture66.UseClamp = false;

			var coloured_shadow_mix_custom98 = new MixClosureNode("coloured_shadow_mix_custom");
			coloured_shadow_mix_custom98.ins.Fac.Value = 0f;

			var transparent85 = new TransparentBsdfNode("transparent");
			transparent85.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

			var add_diffuse_texture_alpha134 = new MathNode("add_diffuse_texture_alpha");
			add_diffuse_texture_alpha134.ins.Value1.Value = 0f;
			add_diffuse_texture_alpha134.ins.Value2.Value = 0f;
			add_diffuse_texture_alpha134.Operation = MathNode.Operations.Add;
			add_diffuse_texture_alpha134.UseClamp = false;

			var custom_alpha_cutter90 = new MixClosureNode("custom_alpha_cutter");
			custom_alpha_cutter90.ins.Fac.Value = 0f;

			var uber132 = new UberBsdfNode("uber");
			uber132.ins.BaseColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			uber132.ins.SpecularColor.Value = part.SpecularColor;
			uber132.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
			uber132.ins.Metallic.Value = 0f;
			uber132.ins.Subsurface.Value = 0f;
			uber132.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
			uber132.ins.Specular.Value = 1f;
			uber132.ins.Roughness.Value = part.ReflectionRoughnessPow2;
			uber132.ins.SpecularTint.Value = 0.3f;
			uber132.ins.Anisotropic.Value = 0f;
			uber132.ins.Sheen.Value = 1f;
			uber132.ins.SheenTint.Value = 0.3f;
			uber132.ins.Clearcoat.Value = 1f;
			uber132.ins.ClearcoatGloss.Value = 0.1f;
			uber132.ins.IOR.Value = part.IOR;
			uber132.ins.Transparency.Value = part.Transparency;
			uber132.ins.RefractionRoughness.Value = part.RefractionRoughnessPow2;
			uber132.ins.AnisotropicRotation.Value = 0f;
			uber132.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			uber132.ins.ClearcoatNormal.Value = new ccl.float4(0f, 0f, 0f, 1f);
			uber132.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

			var coloured_shadow_mix_glass131 = new MixClosureNode("coloured_shadow_mix_glass");
			coloured_shadow_mix_glass131.ins.Fac.Value = 0f;


			m_shader.AddNode(invert_shine_factor127);
			m_shader.AddNode(pow2125);
			m_shader.AddNode(texcoord60);
			m_shader.AddNode(bump_texture71);
			m_shader.AddNode(bump_texture_to_bw72);
			m_shader.AddNode(bump_amount73);
			m_shader.AddNode(attenuated_gloss_color118);
			m_shader.AddNode(add126);
			m_shader.AddNode(bump70);
			m_shader.AddNode(glossiness_or_shine119);
			m_shader.AddNode(diffuse_texture61);
			m_shader.AddNode(invert_alpha81);
			m_shader.AddNode(honor_texture_repeat82);
			m_shader.AddNode(invert_transparency84);
			m_shader.AddNode(repeat_mixer80);
			m_shader.AddNode(weight_diffuse_amount_by_transparency_inv83);
			m_shader.AddNode(diffuse_texture_amount65);
			m_shader.AddNode(invert_diffuse_color_amount68);
			m_shader.AddNode(diffuse_col_amount67);
			m_shader.AddNode(multiply142);
			m_shader.AddNode(mix135);
			m_shader.AddNode(separate_diffuse_texture_color136);
			m_shader.AddNode(separate_base_color137);
			m_shader.AddNode(add_base_color_r138);
			m_shader.AddNode(add_base_color_g139);
			m_shader.AddNode(add_base_color_b140);
			m_shader.AddNode(diffuse_plus_texture_alphaed141);
			m_shader.AddNode(separate_diffuse_texture_color74);
			m_shader.AddNode(separate_base_color75);
			m_shader.AddNode(add_base_color_r76);
			m_shader.AddNode(add_base_color_g77);
			m_shader.AddNode(add_base_color_b78);
			m_shader.AddNode(final_base_color79);
			m_shader.AddNode(diffuse87);
			m_shader.AddNode(shadeless_bsdf99);
			m_shader.AddNode(attenuated_reflection_color109);
			m_shader.AddNode(fresnel110);
			m_shader.AddNode(simple_reflection115);
			m_shader.AddNode(fresnel_reflection116);
			m_shader.AddNode(select_reflection_or_fresnel_reflection114);
			m_shader.AddNode(shadeless100);
			m_shader.AddNode(glossy101);
			m_shader.AddNode(reflection_factor117);
			m_shader.AddNode(attenuate_shine_bsdf129);
			m_shader.AddNode(diffuse_plus_glossy112);
			m_shader.AddNode(attennuated_refraction_color111);
			m_shader.AddNode(refraction89);
			m_shader.AddNode(add128);
			m_shader.AddNode(blend_in_transparency88);
			m_shader.AddNode(separate_xyz105);
			m_shader.AddNode(multiply106);
			m_shader.AddNode(combine_xyz104);
			m_shader.AddNode(environment_texture102);
			m_shader.AddNode(attenuated_environment_color107);
			m_shader.AddNode(diffuse_glossy_and_refraction113);
			m_shader.AddNode(diffuse103);
			m_shader.AddNode(invert_roughness93);
			m_shader.AddNode(multiply_transparency94);
			m_shader.AddNode(light_path91);
			m_shader.AddNode(multiply_with_shadowray95);
			m_shader.AddNode(custom_environment_blend108);
			m_shader.AddNode(coloured_shadow_trans_color92);
			m_shader.AddNode(weight_for_shadowray_coloured_shadow96);
			m_shader.AddNode(transparency_texture62);
			m_shader.AddNode(transpluminance63);
			m_shader.AddNode(invert_luminence64);
			m_shader.AddNode(transparency_texture_amount69);
			m_shader.AddNode(toggle_diffuse_texture_alpha_usage133);
			m_shader.AddNode(toggle_transparency_texture66);
			m_shader.AddNode(coloured_shadow_mix_custom98);
			m_shader.AddNode(transparent85);
			m_shader.AddNode(add_diffuse_texture_alpha134);
			m_shader.AddNode(custom_alpha_cutter90);
			m_shader.AddNode(texcoord60);
			m_shader.AddNode(uber132);
			m_shader.AddNode(coloured_shadow_mix_glass131);


			invert_shine_factor127.outs.Value.Connect(pow2125.ins.Value1);
			invert_shine_factor127.outs.Value.Connect(pow2125.ins.Value2);
			texcoord60.outs.UV.Connect(bump_texture71.ins.Vector);
			bump_texture71.outs.Color.Connect(bump_texture_to_bw72.ins.Color);
			pow2125.outs.Value.Connect(add126.ins.Value2);
			bump_texture_to_bw72.outs.Val.Connect(bump70.ins.Height);
			bump_amount73.outs.Value.Connect(bump70.ins.Strength);
			attenuated_gloss_color118.outs.Color.Connect(glossiness_or_shine119.ins.Color);
			add126.outs.Value.Connect(glossiness_or_shine119.ins.Roughness);
			bump70.outs.Normal.Connect(glossiness_or_shine119.ins.Normal);
			texcoord60.outs.UV.Connect(diffuse_texture61.ins.Vector);
			diffuse_texture61.outs.Alpha.Connect(invert_alpha81.ins.Value2);
			invert_alpha81.outs.Value.Connect(honor_texture_repeat82.ins.Value1);
			diffuse_texture61.outs.Color.Connect(repeat_mixer80.ins.Color1);
			honor_texture_repeat82.outs.Value.Connect(repeat_mixer80.ins.Fac);
			invert_transparency84.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv83.ins.Value2);
			repeat_mixer80.outs.Color.Connect(diffuse_texture_amount65.ins.Color2);
			weight_diffuse_amount_by_transparency_inv83.outs.Value.Connect(diffuse_texture_amount65.ins.Fac);
			weight_diffuse_amount_by_transparency_inv83.outs.Value.Connect(invert_diffuse_color_amount68.ins.Value2);
			invert_diffuse_color_amount68.outs.Value.Connect(diffuse_col_amount67.ins.Fac);
			diffuse_texture61.outs.Alpha.Connect(multiply142.ins.Value1);
			diffuse_texture61.outs.Color.Connect(mix135.ins.Color2);
			multiply142.outs.Value.Connect(mix135.ins.Fac);
			diffuse_col_amount67.outs.Color.Connect(separate_diffuse_texture_color136.ins.Image);
			mix135.outs.Color.Connect(separate_base_color137.ins.Image);
			separate_diffuse_texture_color136.outs.R.Connect(add_base_color_r138.ins.Value1);
			separate_base_color137.outs.R.Connect(add_base_color_r138.ins.Value2);
			separate_diffuse_texture_color136.outs.G.Connect(add_base_color_g139.ins.Value1);
			separate_base_color137.outs.G.Connect(add_base_color_g139.ins.Value2);
			separate_diffuse_texture_color136.outs.B.Connect(add_base_color_b140.ins.Value1);
			separate_base_color137.outs.B.Connect(add_base_color_b140.ins.Value2);
			add_base_color_r138.outs.Value.Connect(diffuse_plus_texture_alphaed141.ins.R);
			add_base_color_g139.outs.Value.Connect(diffuse_plus_texture_alphaed141.ins.G);
			add_base_color_b140.outs.Value.Connect(diffuse_plus_texture_alphaed141.ins.B);
			diffuse_texture_amount65.outs.Color.Connect(separate_diffuse_texture_color74.ins.Image);
			diffuse_plus_texture_alphaed141.outs.Image.Connect(separate_base_color75.ins.Image);
			separate_diffuse_texture_color74.outs.R.Connect(add_base_color_r76.ins.Value1);
			separate_base_color75.outs.R.Connect(add_base_color_r76.ins.Value2);
			separate_diffuse_texture_color74.outs.G.Connect(add_base_color_g77.ins.Value1);
			separate_base_color75.outs.G.Connect(add_base_color_g77.ins.Value2);
			separate_diffuse_texture_color74.outs.B.Connect(add_base_color_b78.ins.Value1);
			separate_base_color75.outs.B.Connect(add_base_color_b78.ins.Value2);
			add_base_color_r76.outs.Value.Connect(final_base_color79.ins.R);
			add_base_color_g77.outs.Value.Connect(final_base_color79.ins.G);
			add_base_color_b78.outs.Value.Connect(final_base_color79.ins.B);
			final_base_color79.outs.Image.Connect(diffuse87.ins.Color);
			bump70.outs.Normal.Connect(diffuse87.ins.Normal);
			final_base_color79.outs.Image.Connect(shadeless_bsdf99.ins.Color);
			bump70.outs.Normal.Connect(fresnel110.ins.Normal);
			fresnel110.outs.Fac.Connect(fresnel_reflection116.ins.R);
			simple_reflection115.outs.Image.Connect(select_reflection_or_fresnel_reflection114.ins.Color1);
			fresnel_reflection116.outs.Image.Connect(select_reflection_or_fresnel_reflection114.ins.Color2);
			diffuse87.outs.BSDF.Connect(shadeless100.ins.Closure1);
			shadeless_bsdf99.outs.Emission.Connect(shadeless100.ins.Closure2);
			attenuated_reflection_color109.outs.Color.Connect(glossy101.ins.Color);
			bump70.outs.Normal.Connect(glossy101.ins.Normal);
			select_reflection_or_fresnel_reflection114.outs.Color.Connect(reflection_factor117.ins.Image);
			glossiness_or_shine119.outs.BSDF.Connect(attenuate_shine_bsdf129.ins.Closure2);
			add126.outs.Value.Connect(attenuate_shine_bsdf129.ins.Fac);
			shadeless100.outs.Closure.Connect(diffuse_plus_glossy112.ins.Closure1);
			glossy101.outs.BSDF.Connect(diffuse_plus_glossy112.ins.Closure2);
			reflection_factor117.outs.R.Connect(diffuse_plus_glossy112.ins.Fac);
			attennuated_refraction_color111.outs.Color.Connect(refraction89.ins.Color);
			bump70.outs.Normal.Connect(refraction89.ins.Normal);
			attenuate_shine_bsdf129.outs.Closure.Connect(add128.ins.Closure1);
			diffuse_plus_glossy112.outs.Closure.Connect(add128.ins.Closure2);
			shadeless100.outs.Closure.Connect(blend_in_transparency88.ins.Closure1);
			refraction89.outs.BSDF.Connect(blend_in_transparency88.ins.Closure2);
			texcoord60.outs.EnvEmap.Connect(separate_xyz105.ins.Vector);
			separate_xyz105.outs.Y.Connect(multiply106.ins.Value1);
			separate_xyz105.outs.X.Connect(combine_xyz104.ins.X);
			multiply106.outs.Value.Connect(combine_xyz104.ins.Y);
			separate_xyz105.outs.Z.Connect(combine_xyz104.ins.Z);
			combine_xyz104.outs.Vector.Connect(environment_texture102.ins.Vector);
			environment_texture102.outs.Color.Connect(attenuated_environment_color107.ins.Color2);
			add128.outs.Closure.Connect(diffuse_glossy_and_refraction113.ins.Closure1);
			blend_in_transparency88.outs.Closure.Connect(diffuse_glossy_and_refraction113.ins.Closure2);
			attenuated_environment_color107.outs.Color.Connect(diffuse103.ins.Color);
			invert_roughness93.outs.Value.Connect(multiply_transparency94.ins.Value1);
			multiply_transparency94.outs.Value.Connect(multiply_with_shadowray95.ins.Value1);
			light_path91.outs.IsShadowRay.Connect(multiply_with_shadowray95.ins.Value2);
			diffuse_glossy_and_refraction113.outs.Closure.Connect(custom_environment_blend108.ins.Closure1);
			diffuse103.outs.BSDF.Connect(custom_environment_blend108.ins.Closure2);
			final_base_color79.outs.Image.Connect(coloured_shadow_trans_color92.ins.Color);
			multiply_with_shadowray95.outs.Value.Connect(weight_for_shadowray_coloured_shadow96.ins.Value1);
			texcoord60.outs.UV.Connect(transparency_texture62.ins.Vector);
			transparency_texture62.outs.Color.Connect(transpluminance63.ins.Color);
			transpluminance63.outs.Val.Connect(invert_luminence64.ins.Value2);
			invert_luminence64.outs.Value.Connect(transparency_texture_amount69.ins.Value1);
			invert_alpha81.outs.Value.Connect(toggle_diffuse_texture_alpha_usage133.ins.Value1);
			transparency_texture_amount69.outs.Value.Connect(toggle_transparency_texture66.ins.Value2);
			custom_environment_blend108.outs.Closure.Connect(coloured_shadow_mix_custom98.ins.Closure1);
			coloured_shadow_trans_color92.outs.BSDF.Connect(coloured_shadow_mix_custom98.ins.Closure2);
			weight_for_shadowray_coloured_shadow96.outs.Value.Connect(coloured_shadow_mix_custom98.ins.Fac);
			toggle_diffuse_texture_alpha_usage133.outs.Value.Connect(add_diffuse_texture_alpha134.ins.Value1);
			toggle_transparency_texture66.outs.Value.Connect(add_diffuse_texture_alpha134.ins.Value2);
			coloured_shadow_mix_custom98.outs.Closure.Connect(custom_alpha_cutter90.ins.Closure1);
			transparent85.outs.BSDF.Connect(custom_alpha_cutter90.ins.Closure2);
			add_diffuse_texture_alpha134.outs.Value.Connect(custom_alpha_cutter90.ins.Fac);
			final_base_color79.outs.Image.Connect(uber132.ins.BaseColor);
			bump70.outs.Normal.Connect(uber132.ins.Normal);
			bump70.outs.Normal.Connect(uber132.ins.ClearcoatNormal);
			uber132.outs.BSDF.Connect(coloured_shadow_mix_glass131.ins.Closure1);
			coloured_shadow_trans_color92.outs.BSDF.Connect(coloured_shadow_mix_glass131.ins.Closure2);
			weight_for_shadowray_coloured_shadow96.outs.Value.Connect(coloured_shadow_mix_glass131.ins.Fac);

			if (part.HasDiffuseTexture)
			{
				RenderEngine.SetTextureImage(diffuse_texture61, part.DiffuseTexture);
				RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture61, texcoord60);
			}

			if (part.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(bump_texture71, part.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture71, texcoord60);
			}

			if (part.HasTransparencyTexture)
			{
				RenderEngine.SetTextureImage(transparency_texture62, part.TransparencyTexture);
				RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture62, texcoord60);
			}

			if (part.HasEnvironmentTexture)
			{
				RenderEngine.SetTextureImage(environment_texture102, part.EnvironmentTexture);
				RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture102, texcoord60);
			}

			if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass) return coloured_shadow_mix_glass131;
			return custom_alpha_cutter90;
		}

	}
}
