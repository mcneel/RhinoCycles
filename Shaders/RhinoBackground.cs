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
using Rhino.Display;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoBackground : RhinoShader
	{

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing) : this(client, intermediate, existing, "background")
		{
		}

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing, string name) : base(client, intermediate, name, existing)
		{
		}

		public override Shader GetShader()
		{
			if (!string.IsNullOrEmpty(m_original_background.Xml))
			{
				var xml = m_original_background.Xml;
				Shader.ShaderFromXml(ref m_shader, xml);
			}
			else
			{
				var texcoord210 = new TextureCoordinateNode("texcoord");

				var bgmapping263 = new MappingNode("bgmapping");

				var bg_env_texture255 = new EnvironmentTextureNode("bg_env_texture");
					bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Equirectangular;
					bg_env_texture255.ColorSpace = TextureNode.TextureColorSpace.None;
					bg_env_texture255.Extension = TextureNode.TextureExtension.Repeat;
					bg_env_texture255.Interpolation = InterpolationType.Linear;
					bg_env_texture255.IsLinear = false;

				var bg_color_or_texture260 = new MixNode("bg_color_or_texture");
					bg_color_or_texture260.ins.Color1.Value = m_original_background.Color1AsFloat4;
					bg_color_or_texture260.ins.Fac.Value = m_original_background.HasBgEnvTextureAsFloat;
					bg_color_or_texture260.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					bg_color_or_texture260.UseClamp = false;

				var separate_bg_color269 = new SeparateRgbNode("separate_bg_color");

				var factor_r266 = new MathMultiply("factor_r");
					factor_r266.ins.Value2.Value = m_original_background.BgStrength;
					factor_r266.Operation = MathNode.Operations.Multiply;
					factor_r266.UseClamp = false;

				var factor_g267 = new MathMultiply("factor_g");
					factor_g267.ins.Value2.Value = m_original_background.BgStrength;
					factor_g267.Operation = MathNode.Operations.Multiply;
					factor_g267.UseClamp = false;

				var factor_b268 = new MathMultiply("factor_b");
					factor_b268.ins.Value2.Value = m_original_background.BgStrength;
					factor_b268.Operation = MathNode.Operations.Multiply;
					factor_b268.UseClamp = false;

				var rotate_gradient287 = new MappingNode("rotate_gradient");

				var gradienttexture285 = new GradientTextureNode("gradienttexture");

				var factored_bg_color270 = new CombineRgbNode("factored_bg_color");

				var gradient_colorramp286 = new ColorRampNode("gradient_colorramp");
					gradient_colorramp286.ColorBand.Stops.Add(new ccl.ShaderNodes.ColorStop() {Color=new ccl.float4(0.9411765f, 0.5803922f, 0.07843138f, 1f), Position=0f});
					gradient_colorramp286.ColorBand.Stops.Add(new ccl.ShaderNodes.ColorStop() {Color=new ccl.float4(0.5019608f, 0f, 0f, 1f), Position=1f});

				var gradient_or_other288 = new MixNode("gradient_or_other");
					gradient_or_other288.ins.Fac.Value = m_original_background.UseGradientAsFloat;
					gradient_or_other288.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					gradient_or_other288.UseClamp = false;

				var envmapping264 = new MappingNode("envmapping");

				var skycolor_or_final_bg289 = new MixNode("skycolor_or_final_bg");
					skycolor_or_final_bg289.ins.Color2.Value = m_original_background.SkyColorAs4float;
					skycolor_or_final_bg289.ins.Fac.Value = m_original_background.UseSkyColorAsFloat;
					skycolor_or_final_bg289.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					skycolor_or_final_bg289.UseClamp = false;

				var sky_env_texture257 = new EnvironmentTextureNode("sky_env_texture");
					sky_env_texture257.Projection = TextureNode.EnvironmentProjection.Equirectangular;
					sky_env_texture257.ColorSpace = TextureNode.TextureColorSpace.None;
					sky_env_texture257.Extension = TextureNode.TextureExtension.Repeat;
					sky_env_texture257.Interpolation = InterpolationType.Linear;
					sky_env_texture257.IsLinear = false;

				var sky_color_or_texture259 = new MixNode("sky_color_or_texture");
					sky_color_or_texture259.ins.Fac.Value = m_original_background.HasSkyEnvTextureAsFloat;
					sky_color_or_texture259.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					sky_color_or_texture259.UseClamp = false;

				var separate_sky_color279 = new SeparateRgbNode("separate_sky_color");

				var sky_or_not262 = new MathMultiply("sky_or_not");
					sky_or_not262.ins.Value1.Value = m_original_background.SkyStrength;
					sky_or_not262.ins.Value2.Value = m_original_background.SkylightEnabledAsFloat;
					sky_or_not262.Operation = MathNode.Operations.Multiply;
					sky_or_not262.UseClamp = false;

				var factor_sky_r276 = new MathMultiply("factor_sky_r");
					factor_sky_r276.Operation = MathNode.Operations.Multiply;
					factor_sky_r276.UseClamp = false;

				var factor_sky_g277 = new MathMultiply("factor_sky_g");
					factor_sky_g277.Operation = MathNode.Operations.Multiply;
					factor_sky_g277.UseClamp = false;

				var factor_sky_b278 = new MathMultiply("factor_sky_b");
					factor_sky_b278.Operation = MathNode.Operations.Multiply;
					factor_sky_b278.UseClamp = false;

				var skymapping265 = new MappingNode("skymapping");

				var refl_env_texture256 = new EnvironmentTextureNode("refl_env_texture");
					refl_env_texture256.Projection = TextureNode.EnvironmentProjection.Equirectangular;
					refl_env_texture256.ColorSpace = TextureNode.TextureColorSpace.None;
					refl_env_texture256.Extension = TextureNode.TextureExtension.Repeat;
					refl_env_texture256.Interpolation = InterpolationType.Linear;
					refl_env_texture256.IsLinear = false;

				var refl_color_or_texture261 = new MixNode("refl_color_or_texture");
					refl_color_or_texture261.ins.Color1.Value = m_original_background.ReflectionColorAs4float;
					refl_color_or_texture261.ins.Fac.Value = m_original_background.HasReflEnvTextureAsFloat;
					refl_color_or_texture261.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					refl_color_or_texture261.UseClamp = false;

				var separate_refl_color274 = new SeparateRgbNode("separate_refl_color");

				var factor_refl_r271 = new MathMultiply("factor_refl_r");
					factor_refl_r271.ins.Value2.Value = m_original_background.ReflStrength;
					factor_refl_r271.Operation = MathNode.Operations.Multiply;
					factor_refl_r271.UseClamp = false;

				var factor_refl_g272 = new MathMultiply("factor_refl_g");
					factor_refl_g272.ins.Value2.Value = m_original_background.ReflStrength;
					factor_refl_g272.Operation = MathNode.Operations.Multiply;
					factor_refl_g272.UseClamp = false;

				var factor_refl_b273 = new MathMultiply("factor_refl_b");
					factor_refl_b273.ins.Value2.Value = m_original_background.ReflStrength;
					factor_refl_b273.Operation = MathNode.Operations.Multiply;
					factor_refl_b273.UseClamp = false;

				var factored_refl_color275 = new CombineRgbNode("factored_refl_color");

				var factored_sky_color280 = new CombineRgbNode("factored_sky_color");

				var custom_refl_or_bg_color281 = new MixNode("custom_refl_or_bg_color");
					custom_refl_or_bg_color281.ins.Fac.Value = m_original_background.UseCustomReflectionEnvironmentAsFloat;
					custom_refl_or_bg_color281.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					custom_refl_or_bg_color281.UseClamp = false;

				var light_path235 = new LightPathNode("light_path");

				var sky_or_refl_color282 = new MixNode("sky_or_refl_color");
					sky_or_refl_color282.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					sky_or_refl_color282.UseClamp = false;

				var use_reflect_refract_when_glossy_and_reflection1882 = new MathMultiply("use_reflect_refract_when_glossy_and_reflection");
					use_reflect_refract_when_glossy_and_reflection1882.Operation = MathNode.Operations.Multiply;
					use_reflect_refract_when_glossy_and_reflection1882.UseClamp = false;

				var bg_or_rest_color283 = new MixNode("bg_or_rest_color");
					bg_or_rest_color283.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Mix;
					bg_or_rest_color283.UseClamp = false;

				var final_bg284 = new BackgroundNode("final_bg");
					final_bg284.ins.Strength.Value = 1f;
				

				m_shader.AddNode(texcoord210);
				m_shader.AddNode(bgmapping263);
				m_shader.AddNode(bg_env_texture255);
				m_shader.AddNode(bg_color_or_texture260);
				m_shader.AddNode(separate_bg_color269);
				m_shader.AddNode(factor_r266);
				m_shader.AddNode(factor_g267);
				m_shader.AddNode(factor_b268);
				m_shader.AddNode(rotate_gradient287);
				m_shader.AddNode(gradienttexture285);
				m_shader.AddNode(factored_bg_color270);
				m_shader.AddNode(gradient_colorramp286);
				m_shader.AddNode(gradient_or_other288);
				m_shader.AddNode(envmapping264);
				m_shader.AddNode(skycolor_or_final_bg289);
				m_shader.AddNode(sky_env_texture257);
				m_shader.AddNode(sky_color_or_texture259);
				m_shader.AddNode(separate_sky_color279);
				m_shader.AddNode(sky_or_not262);
				m_shader.AddNode(factor_sky_r276);
				m_shader.AddNode(factor_sky_g277);
				m_shader.AddNode(factor_sky_b278);
				m_shader.AddNode(skymapping265);
				m_shader.AddNode(refl_env_texture256);
				m_shader.AddNode(refl_color_or_texture261);
				m_shader.AddNode(separate_refl_color274);
				m_shader.AddNode(factor_refl_r271);
				m_shader.AddNode(factor_refl_g272);
				m_shader.AddNode(factor_refl_b273);
				m_shader.AddNode(factored_refl_color275);
				m_shader.AddNode(factored_sky_color280);
				m_shader.AddNode(custom_refl_or_bg_color281);
				m_shader.AddNode(light_path235);
				m_shader.AddNode(sky_or_refl_color282);
				m_shader.AddNode(use_reflect_refract_when_glossy_and_reflection1882);
				m_shader.AddNode(bg_or_rest_color283);
				m_shader.AddNode(final_bg284);
				

				texcoord210.outs.Generated.Connect(bgmapping263.ins.Vector);
				bgmapping263.outs.Vector.Connect(bg_env_texture255.ins.Vector);
				bg_env_texture255.outs.Color.Connect(bg_color_or_texture260.ins.Color2);
				bg_color_or_texture260.outs.Color.Connect(separate_bg_color269.ins.Image);
				separate_bg_color269.outs.R.Connect(factor_r266.ins.Value1);
				separate_bg_color269.outs.G.Connect(factor_g267.ins.Value1);
				separate_bg_color269.outs.B.Connect(factor_b268.ins.Value1);
				texcoord210.outs.Window.Connect(rotate_gradient287.ins.Vector);
				rotate_gradient287.outs.Vector.Connect(gradienttexture285.ins.Vector);
				factor_r266.outs.Value.Connect(factored_bg_color270.ins.R);
				factor_g267.outs.Value.Connect(factored_bg_color270.ins.G);
				factor_b268.outs.Value.Connect(factored_bg_color270.ins.B);
				gradienttexture285.outs.Fac.Connect(gradient_colorramp286.ins.Fac);
				factored_bg_color270.outs.Image.Connect(gradient_or_other288.ins.Color1);
				gradient_colorramp286.outs.Color.Connect(gradient_or_other288.ins.Color2);
				texcoord210.outs.Generated.Connect(envmapping264.ins.Vector);
				gradient_or_other288.outs.Color.Connect(skycolor_or_final_bg289.ins.Color1);
				envmapping264.outs.Vector.Connect(sky_env_texture257.ins.Vector);
				skycolor_or_final_bg289.outs.Color.Connect(sky_color_or_texture259.ins.Color1);
				sky_env_texture257.outs.Color.Connect(sky_color_or_texture259.ins.Color2);
				sky_color_or_texture259.outs.Color.Connect(separate_sky_color279.ins.Image);
				separate_sky_color279.outs.R.Connect(factor_sky_r276.ins.Value1);
				sky_or_not262.outs.Value.Connect(factor_sky_r276.ins.Value2);
				separate_sky_color279.outs.G.Connect(factor_sky_g277.ins.Value1);
				sky_or_not262.outs.Value.Connect(factor_sky_g277.ins.Value2);
				separate_sky_color279.outs.B.Connect(factor_sky_b278.ins.Value1);
				sky_or_not262.outs.Value.Connect(factor_sky_b278.ins.Value2);
				texcoord210.outs.Generated.Connect(skymapping265.ins.Vector);
				skymapping265.outs.Vector.Connect(refl_env_texture256.ins.Vector);
				refl_env_texture256.outs.Color.Connect(refl_color_or_texture261.ins.Color2);
				refl_color_or_texture261.outs.Color.Connect(separate_refl_color274.ins.Image);
				separate_refl_color274.outs.R.Connect(factor_refl_r271.ins.Value1);
				separate_refl_color274.outs.G.Connect(factor_refl_g272.ins.Value1);
				separate_refl_color274.outs.B.Connect(factor_refl_b273.ins.Value1);
				factor_refl_r271.outs.Value.Connect(factored_refl_color275.ins.R);
				factor_refl_g272.outs.Value.Connect(factored_refl_color275.ins.G);
				factor_refl_b273.outs.Value.Connect(factored_refl_color275.ins.B);
				factor_sky_r276.outs.Value.Connect(factored_sky_color280.ins.R);
				factor_sky_g277.outs.Value.Connect(factored_sky_color280.ins.G);
				factor_sky_b278.outs.Value.Connect(factored_sky_color280.ins.B);
				gradient_or_other288.outs.Color.Connect(custom_refl_or_bg_color281.ins.Color1);
				factored_refl_color275.outs.Image.Connect(custom_refl_or_bg_color281.ins.Color2);
				factored_sky_color280.outs.Image.Connect(sky_or_refl_color282.ins.Color1);
				custom_refl_or_bg_color281.outs.Color.Connect(sky_or_refl_color282.ins.Color2);
				light_path235.outs.IsGlossyRay.Connect(sky_or_refl_color282.ins.Fac);
				light_path235.outs.IsGlossyRay.Connect(use_reflect_refract_when_glossy_and_reflection1882.ins.Value1);
				light_path235.outs.IsReflectionRay.Connect(use_reflect_refract_when_glossy_and_reflection1882.ins.Value2);
				gradient_or_other288.outs.Color.Connect(bg_or_rest_color283.ins.Color1);
				sky_or_refl_color282.outs.Color.Connect(bg_or_rest_color283.ins.Color2);
				use_reflect_refract_when_glossy_and_reflection1882.outs.Value.Connect(bg_or_rest_color283.ins.Fac);
				bg_or_rest_color283.outs.Color.Connect(final_bg284.ins.Color);

				// extra code

				gradient_colorramp286.ColorBand.Stops.Clear();
				// bottom color on 0.0f
				gradient_colorramp286.ColorBand.InsertColorStop(m_original_background.Color2AsFloat4, 0.0f);
				// top color on 1.0f
				gradient_colorramp286.ColorBand.InsertColorStop(m_original_background.Color1AsFloat4, 1.0f);

				// rotate the window vector
				rotate_gradient287.Rotation = RenderEngine.CreateFloat4(0.0, 0.0, 1.570796);

				if (m_original_background.BackgroundFill == BackgroundStyle.Environment && m_original_background.HasBgEnvTexture)
				{
					RenderEngine.SetTextureImage(bg_env_texture255, m_original_background.BgTexture);
					bgmapping263.Rotation = m_original_background.BgTexture.Transform.z;
				}
				if(m_original_background.BackgroundFill == BackgroundStyle.WallpaperImage && m_original_background.Wallpaper.HasTextureImage)
				{
					RenderEngine.SetTextureImage(bg_env_texture255, m_original_background.Wallpaper);
					bg_env_texture255.Projection = TextureNode.EnvironmentProjection.Wallpaper;
				}
				if(m_original_background.HasReflEnvTexture)
				{
					RenderEngine.SetTextureImage(refl_env_texture256, m_original_background.ReflectionTexture);
					envmapping264.Rotation = m_original_background.ReflectionTexture.Transform.z;
				}
				if(m_original_background.HasSkyEnvTexture)
				{
					RenderEngine.SetTextureImage(sky_env_texture257, m_original_background.SkyTexture);
					skymapping265.Rotation = m_original_background.SkyTexture.Transform.z;
				}

				final_bg284.outs.Background.Connect(m_shader.Output.ins.Surface);
			}

			// phew, done.
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
