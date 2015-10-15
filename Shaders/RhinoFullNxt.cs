using System;
using ccl;
using ccl.ShaderNodes;

namespace RhinoCycles.Shaders
{
	public class RhinoFullNxt : RhinoShader
	{
#region Shader Nodes

		/// <summary>
		/// Texture coordinates
		/// </summary>
		private readonly TextureCoordinateNode m_texture_coordinate = new TextureCoordinateNode("Texture Coordinates");

		/// <summary>
		/// Fresnel to fac [0.0f - 1.0f]
		/// </summary>
		private readonly FresnelNode m_fresnel_factor = new FresnelNode("Fresnel factor");

		/// <summary>
		/// Light path inputs
		/// </summary>
		private readonly LightPathNode m_lightpath = new LightPathNode("light paths");

		#region diffuse
		/// <summary>
		/// Diffuse solid color
		/// </summary>
		private readonly ColorNode m_diffuse_solid_color = new ColorNode("Diffuse solid color");

		/// <summary>
		/// Diffuse BSDF
		/// </summary>
		private readonly DiffuseBsdfNode m_diffuse_bsdf = new DiffuseBsdfNode("Diffuse BSDF");

		/// <summary>
		/// MixNode to drive amount of solid color
		/// </summary>
		private readonly MixNode m_diffuse_col_amount = new MixNode("Diffuse solid color amount") { BlendType = MixNode.BlendTypes.Add };

		/// <summary>
		/// MixNode to drive amount of texture color
		/// </summary>
		private readonly MixNode m_diffuse_tex_amount = new MixNode("Diffuse texture amount") { BlendType = MixNode.BlendTypes.Add };

		/// <summary>
		/// Mix texture color and diffuse color, driven by texture alpha to enforce diffuse texture merge
		/// </summary>
		private readonly MixNode m_diffuse_merge_texalpha_solid = new MixNode("Diffuse texture +alpha merge with solid color") { BlendType = MixNode.BlendTypes.Mix };

		/// <summary>
		/// MixNode to drive amount of texture color
		/// </summary>
		private readonly MixNode m_diffuse_tex_and_solid_col_add = new MixNode("Diffuse texture + Diffuse solid color") { BlendType = MixNode.BlendTypes.Add };

		/// <summary>
		/// Diffuse Color Texture
		/// </summary>
		private readonly ImageTextureNode m_diffuse_texture = new ImageTextureNode("Diffuse Texture Image");
		#endregion

		#region reflection

		/// <summary>
		/// Reflection solid color
		/// </summary>
		private readonly ColorNode m_reflection_solid_color = new ColorNode("Reflection solid color");

		/// <summary>
		/// Mix node to drive amount of solid reflection color
		/// </summary>
		private readonly MixNode m_reflection_col_amount = new MixNode("Reflection solid color amount") { BlendType = MixNode.BlendTypes.Add };

		/// <summary>
		/// Reflection BSDF
		/// </summary>
		private readonly GlossyBsdfNode m_reflection_bsdf = new GlossyBsdfNode("Reflection BSDF") { Distribution = GlossyBsdfNode.GlossyDistribution.GGX };

		/// <summary>
		/// Drive reflection amount based on fresnel factor
		/// </summary>
		private readonly MixClosureNode m_reflection_fresnel_mod = new MixClosureNode("Reflection fresnel mod");

		#endregion

		#region transparency

		/// <summary>
		/// Transparency solid color
		/// </summary>
		private readonly ColorNode m_transparency_solid_color = new ColorNode("Transparency solid color");

		/// <summary>
		/// Drive transparency color amount
		/// </summary>
		private readonly MixNode m_transparency_col_amount = new MixNode("Transparency solid color amount") { BlendType = MixNode.BlendTypes.Add };

		/// <summary>
		/// Refraction BSDF for transparent material with IOR capability
		/// </summary>
		private readonly RefractionBsdfNode m_transparency_bsdf = new RefractionBsdfNode("Transparency BSDF") { Distribution = RefractionBsdfNode.RefractionDistribution.GGX };

		/// <summary>
		/// Drive transparency amount based on (inverse) fresnel factor
		/// </summary>
		private readonly MixClosureNode m_transparency_fresnel_mod = new MixClosureNode("Transparency fresnel mod");

		#endregion

		#region transparency texture

		/// <summary>
		/// Transparency texture (alpha map)
		/// </summary>
		private readonly ImageTextureNode m_transparency_texture = new ImageTextureNode("Transparency texture");

		/// <summary>
		/// Convert input image to luminance
		/// </summary>
		private readonly RgbToLuminanceNode m_transparency_to_luminance = new RgbToLuminanceNode("Transparency to Luminance");

		/// <summary>
		/// Invert luminance value, white is opaque
		/// </summary>
		private readonly MathNode m_luminance_invert = new MathNode("Invert luminance - white is opaque");

		/// <summary>
		/// Factor luminance strength with texture channel amount
		/// </summary>
		private readonly MathNode m_transparency_texture_amount = new MathNode("Transparency texture amount") { Operation = MathNode.Operations.Multiply };

		#endregion

		#region texture alpha

		/// <summary>
		/// invert diffuse texture alpha
		/// </summary>
		private readonly MathNode m_inverse_diffuse_alpha = new MathNode("Inverse of diffuse texture alpha") { Operation = MathNode.Operations.Subtract };

		/// <summary>
		/// add transparency alpha and texture alpha
		/// </summary>
		private readonly MathNode m_transparency_plus_diffuse = new MathNode("Add transparency texture alpha and diffuse texture alpha") { Operation = MathNode.Operations.Add };

		/// <summary>
		/// effective alpha
		/// </summary>
		private readonly MathNode m_effective_alpha = new MathNode("1.0 - ((1.0-diff alpha) + transp alpha)") {Operation = MathNode.Operations.Subtract };

		/// <summary>
		/// Drive diffuse and transparency alpha
		/// </summary>
		private readonly MixClosureNode m_diffuse_and_transparency_alpha = new MixClosureNode("Effective object transparency");

		/// <summary>
		/// Complete transparency to mix final shader with (such that we can have alpha mask after reflection and regular transparency)
		/// </summary>
		private readonly TransparentBsdfNode m_transparent = new TransparentBsdfNode("transparent BSDF for texture (diff and transp) alpha");

		#endregion

		#region lighting transport through transparency

		/// <summary>
		/// Color transparency
		/// </summary>
		private readonly TransparentBsdfNode m_light_transport_bsdf = new TransparentBsdfNode("Light Transport through Transparency BSDF");

		/// <summary>
		/// Give 1.0f when shadow or reflection ray
		/// </summary>
		private readonly MathNode m_lightpath_transport_max = new MathNode("Max (shadow,reflection)");

		/// <summary>
		/// if shadow or reflection ray, make sure we let those through to color shadow and light objects behind
		/// </summary>
		private readonly MixClosureNode m_mix_lightpath = new MixClosureNode("Mix lightpaths and transparent bsdf");

		#endregion

		#region shadeless

		/// <summary>
		/// Emission node to set to 1.0f strength for color input, to result in shadeless effect (self-illumination)
		/// </summary>
		private readonly EmissionNode m_shadeless_control = new EmissionNode("Control shadeless");

		/// <summary>
		/// Choose between shaded or shadeless
		/// </summary>
		private readonly MixClosureNode m_shaded_or_shadeless = new MixClosureNode("Shaded or shadeless");

		#endregion

		#region emission
		
		/// <summary>
		/// Emission color
		/// </summary>
		private readonly ColorNode m_emission_solid_color = new ColorNode("Emission color");

		/// <summary>
		/// Emission BSDF
		/// </summary>
		private readonly EmissionNode m_emission_bsdf = new EmissionNode("Emission BSDF");

		#endregion

		#region bump nodes

		/// <summary>
		/// Bump the normals
		/// </summary>
		private readonly BumpNode m_bump_normal = new BumpNode("Bump the normals");

		private readonly RgbToBwNode m_bump_bw = new RgbToBwNode("Bump texture color to bw");

		private readonly ImageTextureNode m_bump_texture = new ImageTextureNode("Bump texture");
		#endregion

		#region shader adders

		/// <summary>
		/// Add diffuse and reflection components
		/// </summary>
		private readonly AddClosureNode m_diffuse_and_reflection = new AddClosureNode("Diffuse + Reflection");

		/// <summary>
		/// Add diffuse, reflection and transparency components
		/// </summary>
		private readonly AddClosureNode m_diffuse_and_reflection_and_transparency = new AddClosureNode("Diffuse + Reflection + Transparency");

		/// <summary>
		/// Add diffuse, reflection, transparency and emission components
		/// </summary>
		private readonly AddClosureNode m_diffuse_and_reflection_and_transparency_and_emission = new AddClosureNode("Diffuse + Reflection + Transparency + Emission");

		#endregion

#endregion

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

#region add shader nodes

			// Add texture coordinates
			m_shader.AddNode(m_texture_coordinate);

			// Add fresnel to factor
			m_shader.AddNode(m_fresnel_factor);

			// Add light paths input
			m_shader.AddNode(m_lightpath);

			// Add nodes for diffuse color (solid + texture)
			m_shader.AddNode(m_diffuse_bsdf);
			m_shader.AddNode(m_diffuse_solid_color);
			m_shader.AddNode(m_diffuse_texture);
			m_shader.AddNode(m_diffuse_col_amount);
			m_shader.AddNode(m_diffuse_tex_amount);
			m_shader.AddNode(m_diffuse_merge_texalpha_solid);
			m_shader.AddNode(m_diffuse_tex_and_solid_col_add);

			// Add nodes for reflection
			m_shader.AddNode(m_reflection_solid_color);
			m_shader.AddNode(m_reflection_col_amount);
			m_shader.AddNode(m_reflection_bsdf);
			m_shader.AddNode(m_reflection_fresnel_mod);

			// Add nodes for transparency
			m_shader.AddNode(m_transparency_solid_color);
			m_shader.AddNode(m_transparency_col_amount);
			m_shader.AddNode(m_transparency_bsdf);
			m_shader.AddNode(m_transparency_fresnel_mod);

			// Add nodes for transparency texture
			m_shader.AddNode(m_transparency_texture);
			m_shader.AddNode(m_transparency_to_luminance);
			m_shader.AddNode(m_luminance_invert);
			m_shader.AddNode(m_transparency_texture_amount);

			// Add nodes for transparency based on alpha channels and transp texture
			m_shader.AddNode(m_inverse_diffuse_alpha);
			m_shader.AddNode(m_transparency_plus_diffuse);
			m_shader.AddNode(m_effective_alpha);
			m_shader.AddNode(m_diffuse_and_transparency_alpha);
			m_shader.AddNode(m_transparent);

			// Add nodes for lighting through transparency
			m_shader.AddNode(m_light_transport_bsdf);
			m_shader.AddNode(m_lightpath_transport_max);
			m_shader.AddNode(m_mix_lightpath);

			// Add nodes for emission
			m_shader.AddNode(m_emission_solid_color);
			m_shader.AddNode(m_emission_bsdf);

			// Add nodes for shaded/shadeless control
			m_shader.AddNode(m_shadeless_control);
			m_shader.AddNode(m_shaded_or_shadeless);

			// Add bump texture nodes
			m_shader.AddNode(m_bump_normal);
			m_shader.AddNode(m_bump_bw);
			m_shader.AddNode(m_bump_texture);

			// Add nodes for adding shader components
			m_shader.AddNode(m_diffuse_and_reflection);
			m_shader.AddNode(m_diffuse_and_reflection_and_transparency);
			m_shader.AddNode(m_diffuse_and_reflection_and_transparency_and_emission);

#endregion

			m_fresnel_factor.ins.IOR.Value = m_original.FresnelIOR;

			if (m_original.FresnelReflections)
			{
				m_fresnel_factor.outs.Fac.Connect(m_reflection_fresnel_mod.ins.Fac);
				//m_fresnel_factor.outs.Fac.Connect(m_transparency_fresnel_mod.ins.Fac);
			}

#region configure and connect diffuse solid color and diffuse texture nodes

			m_diffuse_solid_color.Value = m_original.DiffuseColor ^ m_original.Gamma;
			m_diffuse_solid_color.outs.Color.Connect(m_diffuse_col_amount.ins.Color2);
			m_diffuse_solid_color.outs.Color.Connect(m_diffuse_merge_texalpha_solid.ins.Color1);

			// set diffuse texture and its projection (projection connects m_texture_coordinate)
			RenderEngine.SetTextureImage(m_diffuse_texture, m_original.DiffuseTexture);
			RenderEngine.SetProjectionMode(m_shader, m_original.DiffuseTexture, m_diffuse_texture, m_texture_coordinate);
			m_diffuse_texture.ColorSpace = TextureNode.TextureColorSpace.None;
			// connect diffuse texture to diffuse color strength mixer
			if (m_original.HasDiffuseTexture)
			{
				m_diffuse_texture.outs.Color.Connect(m_diffuse_tex_amount.ins.Color2);
				m_diffuse_texture.outs.Alpha.Connect(m_diffuse_merge_texalpha_solid.ins.Fac);
				m_diffuse_texture.outs.Alpha.Connect(m_diffuse_tex_and_solid_col_add.ins.Fac);
				if (m_original.DiffuseTexture.UseAlpha)
				{
					m_diffuse_texture.outs.Alpha.Connect(m_inverse_diffuse_alpha.ins.Value2);
				}
			}

			// set diffuse solid color. Mixing factor is 1.0 - diffuse texture strength
			m_diffuse_col_amount.ins.Fac.Value = 1.0f - m_original.DiffuseTexture.Amount;
			m_diffuse_col_amount.BlendType = MixNode.BlendTypes.Add;
			m_diffuse_col_amount.UseClamp = true;
			m_diffuse_col_amount.ins.Color1.Value = Colors.black;
			// connecting solid strength in second
			m_diffuse_col_amount.outs.Color.Connect(m_diffuse_tex_and_solid_col_add.ins.Color2);

			// prepare diffuse texture color strength mixer
			m_diffuse_tex_amount.ins.Fac.Value = m_original.DiffuseTexture.Amount;
			m_diffuse_tex_amount.BlendType = MixNode.BlendTypes.Add;
			m_diffuse_tex_amount.UseClamp = true;
			m_diffuse_tex_amount.ins.Color1.Value = Colors.black;
			m_diffuse_tex_amount.outs.Color.Connect(m_diffuse_merge_texalpha_solid.ins.Color2);

			m_diffuse_merge_texalpha_solid.BlendType = MixNode.BlendTypes.Mix;
			m_diffuse_merge_texalpha_solid.UseClamp = true;
			m_diffuse_merge_texalpha_solid.ins.Fac.Value = 0.0f;
			m_diffuse_merge_texalpha_solid.outs.Color.Connect(m_diffuse_tex_and_solid_col_add.ins.Color1);

			// prepare diffuse solid + diffuse texture adder
			m_diffuse_tex_and_solid_col_add.ins.Fac.Value = 0.0f;
			m_diffuse_tex_and_solid_col_add.UseClamp = true;
			// connect final diffuse color to bsdf BSDF
			m_diffuse_tex_and_solid_col_add.outs.Color.Connect(m_diffuse_bsdf.ins.Color);
			m_diffuse_tex_and_solid_col_add.outs.Color.Connect(m_shadeless_control.ins.Color);

			// connect diffuse bsdf to shaded/shadeless control
			m_diffuse_bsdf.outs.BSDF.Connect(m_shaded_or_shadeless.ins.Closure1);

#endregion

#region configure and connect reflection nodes

			// set solid reflection color
			m_reflection_solid_color.Value = m_original.ReflectionColor ^ m_original.Gamma;
			m_reflection_solid_color.outs.Color.Connect(m_reflection_col_amount.ins.Color2);

			// set driver of reflection color amount - mix with black, 1.0f means full reflection color
			m_reflection_col_amount.ins.Color1.Value = Colors.black;
			m_reflection_col_amount.BlendType = MixNode.BlendTypes.Add;
			m_reflection_col_amount.UseClamp = true;
			m_reflection_col_amount.ins.Fac.Value = m_original.Reflectivity;
			m_reflection_col_amount.outs.Color.Connect(m_reflection_bsdf.ins.Color);

			// actual reflection node settings, use roughness as well and
			m_reflection_bsdf.ins.Roughness.Value = m_original.ReflectionRoughness;
			m_reflection_bsdf.outs.BSDF.Connect(m_reflection_fresnel_mod.ins.Closure2);

			m_reflection_fresnel_mod.ins.Fac.Value = 1.0f;
			if(m_original.Reflectivity > 0.0f) m_reflection_fresnel_mod.outs.Closure.Connect(m_diffuse_and_reflection.ins.Closure2);

#endregion

#region configure and connect transparency nodes

			// set transparency 'solid' color
			m_transparency_solid_color.Value = m_original.TransparencyColor ^ m_original.Gamma;
			m_transparency_solid_color.outs.Color.Connect(m_transparency_col_amount.ins.Color2);

			// drive solid color amount with transparency amount
			m_transparency_col_amount.ins.Color1.Value = Colors.black;
			m_transparency_col_amount.UseClamp = true;
			m_transparency_col_amount.BlendType = MixNode.BlendTypes.Add;
			m_transparency_col_amount.ins.Fac.Value = m_original.Transparency;
			m_transparency_col_amount.outs.Color.Connect(m_transparency_bsdf.ins.Color);
			m_transparency_col_amount.outs.Color.Connect(m_light_transport_bsdf.ins.Color);

			// transparency bsdf, use roughness
			m_transparency_bsdf.ins.Roughness.Value = m_original.RefractionRoughness;
			m_transparency_bsdf.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;
			m_transparency_bsdf.ins.IOR.Value = m_original.IOR;
			m_transparency_bsdf.outs.BSDF.Connect(m_transparency_fresnel_mod.ins.Closure2);

			m_transparency_fresnel_mod.ins.Fac.Value = 1.0f;
			if(m_original.Transparency > 0.0f) m_transparency_fresnel_mod.outs.Closure.Connect(m_diffuse_and_reflection_and_transparency.ins.Closure2);

#endregion

#region configure and connect nodes for transparency texture handling
			
			// set transparency texture
			RenderEngine.SetTextureImage(m_transparency_texture, m_original.TransparencyTexture);
			RenderEngine.SetProjectionMode(m_shader, m_original.TransparencyTexture, m_transparency_texture, m_texture_coordinate);
			m_transparency_texture.ColorSpace = TextureNode.TextureColorSpace.None;

			// convert transparency texture input to luminance
			m_transparency_texture.outs.Color.Connect(m_transparency_to_luminance.ins.Color);

			m_transparency_to_luminance.outs.Val.Connect(m_luminance_invert.ins.Value2);

			// invert luminance
			m_luminance_invert.Operation = MathNode.Operations.Subtract;
			m_luminance_invert.UseClamp = true;
			m_luminance_invert.ins.Value1.Value = 1.0f;

			m_luminance_invert.outs.Value.Connect(m_transparency_texture_amount.ins.Value1);

			m_transparency_texture_amount.Operation = MathNode.Operations.Multiply;
			m_transparency_texture_amount.UseClamp = true;
			m_transparency_texture_amount.ins.Value2.Value = m_original.TransparencyTexture.Amount;

			if(m_original.HasTransparencyTexture) m_transparency_texture_amount.outs.Value.Connect(m_transparency_plus_diffuse.ins.Value1);

#endregion

#region Configure and connect nodes for transparency based on alpha channels and transp texture

			// by default inverse of diff alpha both value inputs to 1.0f, connect to trans and diff alpha adder
			m_inverse_diffuse_alpha.Operation = MathNode.Operations.Subtract;
			m_inverse_diffuse_alpha.UseClamp = true;
			m_inverse_diffuse_alpha.ins.Value1.Value = 1.0f;
			m_inverse_diffuse_alpha.ins.Value2.Value = 1.0f;
			m_inverse_diffuse_alpha.outs.Value.Connect(m_transparency_plus_diffuse.ins.Value2);

			// ensure adder value1 is by default 0.0f, driven by transparency texture luminance otherwise
			m_transparency_plus_diffuse.Operation = MathNode.Operations.Add;
			m_transparency_plus_diffuse.UseClamp = true;
			m_transparency_plus_diffuse.ins.Value1.Value = 0.0f;
			m_transparency_plus_diffuse.outs.Value.Connect(m_effective_alpha.ins.Value2);

			// let effective alpha drive final node.
			m_effective_alpha.Operation = MathNode.Operations.Subtract;
			m_effective_alpha.UseClamp = true;
			m_effective_alpha.ins.Value1.Value = 1.0f;
			m_effective_alpha.outs.Value.Connect(m_diffuse_and_transparency_alpha.ins.Fac);

			m_transparent.ins.Color.Value = Colors.white;
			m_transparent.outs.BSDF.Connect(m_diffuse_and_transparency_alpha.ins.Closure1);

#endregion

#region configure and connect nodes for light transport through transparent material
			m_lightpath.outs.IsShadowRay.Connect(m_lightpath_transport_max.ins.Value1);
			m_lightpath.outs.IsReflectionRay.Connect(m_lightpath_transport_max.ins.Value2);

			m_lightpath_transport_max.UseClamp = true;
			m_lightpath_transport_max.Operation = MathNode.Operations.Maximum;

			// connect max(shadow,reflection) only when transparency is defined on the transparent color channel
			// not the transparency texture channel
			m_lightpath_transport_max.outs.Value.Connect(m_mix_lightpath.ins.Fac);

			m_light_transport_bsdf.outs.BSDF.Connect(m_mix_lightpath.ins.Closure2);

			m_mix_lightpath.ins.Fac.Value = 0.0f;
			m_mix_lightpath.outs.Closure.Connect(m_diffuse_and_reflection_and_transparency.ins.Closure1);
#endregion

#region configure and connect emission
			m_emission_solid_color.Value = m_original.EmissionColor ^ m_original.Gamma;
			m_emission_solid_color.outs.Color.Connect(m_emission_bsdf.ins.Color);

			if (!m_original.EmissionColor.IsZero(false))
			{
				m_emission_bsdf.ins.Strength.Value = 1.0f;
				m_emission_bsdf.outs.Emission.Connect(m_diffuse_and_reflection_and_transparency_and_emission.ins.Closure2);
			}

#endregion

#region configure and connect shaded/shadeless controls
			m_lightpath.outs.IsCameraRay.Connect(m_shadeless_control.ins.Strength);
			m_shadeless_control.outs.Emission.Connect(m_shaded_or_shadeless.ins.Closure2);

			m_shaded_or_shadeless.ins.Fac.Value = m_original.ShadelessAsFloat; // will be 1.0f for shadeless, 0.0f for shaded
			m_shaded_or_shadeless.outs.Closure.Connect(m_diffuse_and_reflection.ins.Closure1);
#endregion

#region configure and connect bump texture nodes

			if (m_original.HasBumpTexture)
			{
				RenderEngine.SetTextureImage(m_bump_texture, m_original.BumpTexture);
				RenderEngine.SetProjectionMode(m_shader, m_original.BumpTexture, m_bump_texture, m_texture_coordinate);

				m_bump_texture.ColorSpace = TextureNode.TextureColorSpace.None;

				m_bump_texture.outs.Color.Connect(m_bump_bw.ins.Color);
				m_bump_bw.outs.Val.Connect(m_bump_normal.ins.Height);
				m_bump_normal.ins.Strength.Value = 100.0f * m_original.BumpTexture.Amount;
				m_bump_normal.ins.Distance.Value = 0.01f;

				m_bump_normal.outs.Normal.Connect(m_diffuse_bsdf.ins.Normal);
				m_bump_normal.outs.Normal.Connect(m_reflection_bsdf.ins.Normal);
				m_bump_normal.outs.Normal.Connect(m_transparency_bsdf.ins.Normal);
				m_bump_normal.outs.Normal.Connect(m_fresnel_factor.ins.Normal);
			}
#endregion

#region connect up nodes for adding shader components
			m_diffuse_and_reflection.outs.Closure.Connect(m_mix_lightpath.ins.Closure1);
			m_diffuse_and_reflection_and_transparency.outs.Closure.Connect(m_diffuse_and_reflection_and_transparency_and_emission.ins.Closure1);
			m_diffuse_and_reflection_and_transparency_and_emission.outs.Closure.Connect(m_diffuse_and_transparency_alpha.ins.Closure2);
#endregion

			// connect final shader mixer to output
			m_diffuse_and_transparency_alpha.outs.Closure.Connect(m_shader.Output.ins.Surface);

			// finalize
			m_shader.FinalizeGraph();

			return m_shader;
		}

	}
}
