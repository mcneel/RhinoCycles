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
using RhinoCyclesCore.ExtensionMethods;
using System;
using System.Linq;
using System.Collections.Generic;

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

				var backfacing=  new GeometryInfoNode("backfacepicker_");
				var flipper = new MixClosureNode("front_or_back_");

				m_shader.AddNode(backfacing);
				m_shader.AddNode(flipper);

				backfacing.outs.Backfacing.Connect(flipper.ins.Fac);

				var frontclosure = front.GetClosureSocket();
				var backclosure = back.GetClosureSocket();

				frontclosure.Connect(flipper.ins.Closure1);
				backclosure.Connect(flipper.ins.Closure2);

				return flipper.GetClosureSocket();
			}
			else
			{
				var last = GetShaderPart(m_original.Front);
				var lastclosure = last.GetClosureSocket();

				return lastclosure;
			}

		}

		public override Shader GetShader()
		{
			if (RcCore.It.AllSettings.DebugSimpleShaders)
			{
				ccl.ShaderNodes.DiffuseBsdfNode diff = new DiffuseBsdfNode("debug_diff_");
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

		static private void SetupOneDecalNodes(CyclesDecal decal, TextureCoordinateNode texco, ImageTextureNode imgtex, MathMultiply transp)
		{
			texco.ObjectTransform = decal.Transform;
			texco.UseTransform = true;

			RenderEngine.SetTextureImage(imgtex, decal.Texture);
			texco.UvMap = decal.Texture.GetUvMapForChannel();
			imgtex.Extension = TextureNode.TextureExtension.Clip;
			imgtex.UseAlpha = true;

			float4 t = decal.Texture.Transform.x;
			imgtex.Translation = t;
			imgtex.Translation.z = 0.0f;
			imgtex.Translation.w = 1.0f;
			imgtex.Scale.x = 1.0f / decal.Texture.Transform.y.x;
			imgtex.Scale.y = 1.0f / decal.Texture.Transform.y.y;
			imgtex.Rotation.z = -1.0f * RenderEngine.DegToRad(decal.Texture.Transform.z.z);

			switch(decal.Projection) {
				case Rhino.Render.DecalProjection.Forward:
					texco.Direction = DecalDirection.Forward;
					break;
				case Rhino.Render.DecalProjection.Backward:
					texco.Direction = DecalDirection.Backward;
					break;
				default:
					texco.Direction = DecalDirection.Both;
					break;
			}

			texco.DecalOrigin = decal.Origin;
			texco.Across = decal.Across;
			texco.Up = decal.Up;
			texco.DecalPxyz = decal.TextureMapping.PrimitiveTransform.ToCyclesTransform();
			texco.DecalNxyz = decal.TextureMapping.NormalTransform.ToCyclesTransform();
			texco.DecalUvw = decal.TextureMapping.UvwTransform.ToCyclesTransform();

			imgtex.Projection = TextureNode.TextureProjection.Flat;
			imgtex.Extension = TextureNode.TextureExtension.Repeat;

			texco.DecalHeight = decal.Height;
			texco.DecalRadius = decal.Radius;
			texco.HorizontalSweepStart = decal.HorizontalSweepStart;
			texco.HorizontalSweepEnd = decal.HorizontalSweepEnd;
			texco.VerticalSweepStart = decal.VerticalSweepStart;
			texco.VerticalSweepEnd = decal.VerticalSweepEnd;

			transp.ins.Value2.Value = 1.0f - decal.Transparency;
			imgtex.outs.Alpha.Connect(transp.ins.Value1);

			switch(decal.Mapping) {
				case Rhino.Render.DecalMapping.Planar:
					texco.outs.DecalPlanar.Connect(imgtex.ins.Vector);
					break;
				case Rhino.Render.DecalMapping.Cylindrical:
					texco.outs.DecalCylindrical.Connect(imgtex.ins.Vector);
					break;
				case Rhino.Render.DecalMapping.Spherical:
					texco.outs.DecalSpherical.Connect(imgtex.ins.Vector);
					break;
				case Rhino.Render.DecalMapping.UV:
					texco.outs.DecalUv.Connect(imgtex.ins.Vector);
					break;
			}
			texco.outs.DecalForward.Connect(imgtex.ins.DecalForward);
			texco.outs.DecalUsage.Connect(imgtex.ins.DecalUsage);
		}

		/// <summary>
		/// Handle decals for this shader. Set up a partial shader graph
		/// and return the ShaderNodes that can be bound into the basecolor
		/// of the actual shader.
		/// </summary>
		/// <returns>ShaderNode, the final node in the shader graph branch. This will be a MixNode.
		/// The base color (color or texture) will have to be connected to the Color1 input.</returns>
		/// <since>7.0</since>
		private MixNode HandleDecals() {
			//ccl.CodeShader m_codeshader = new ccl.CodeShader(ccl.Shader.ShaderType.Material);
			MixNode nodeToBindIntoShader = null;

			int count = m_original.Decals?.Count ?? 0;

			if (count > 0)
			{
				List<TextureCoordinateNode> texcos = new List<TextureCoordinateNode>(count);
				List<ImageTextureNode> imgtexs= new List<ImageTextureNode>(count);
				List<MixNode> mixrgbs = new List<MixNode>(count);
				List<MathMultiply> transparencies = new List<MathMultiply>(count);
				List<MathAdd> alphamaths = new List<MathAdd>(count);
				int idx = 1;

				// First create all the nodes we need to set up decals
				// for this material.
				for (int i = 0; i < count; i++)
				{
					texcos.Add(new TextureCoordinateNode($"Decal_{idx}_texco_"));
					imgtexs.Add(new ImageTextureNode($"Texture_for_decal_{idx}_"));
					mixrgbs.Add(new MixNode($"Decal_mixer_{idx}_"));
					transparencies.Add(new MathMultiply($"Decal_transparency_multiplier_{idx}_"));
					if(i < count-1) {
						alphamaths.Add(new MathAdd($"Decal_alpha_adder_{idx}_"));
					}

					idx++;
				}

				// Add the nodes to the shader.
				texcos.ForEach(sn => m_shader.AddNode(sn));
				imgtexs.ForEach(sn => m_shader.AddNode(sn));
				mixrgbs.ForEach(sn => m_shader.AddNode(sn));
				transparencies.ForEach(sn => m_shader.AddNode(sn));
				alphamaths.ForEach(sn => m_shader.AddNode(sn));

				/*texcos.ForEach(sn => m_codeshader.AddNode(sn));
				imgtexs.ForEach(sn => m_codeshader.AddNode(sn));
				mixrgbs.ForEach(sn => m_codeshader.AddNode(sn));
				transparencies.ForEach(sn => m_codeshader.AddNode(sn));
				alphamaths.ForEach(sn => m_codeshader.AddNode(sn));*/

				MixNode lastMixer = mixrgbs.Last();

				if(count == 1) {
					var texco = texcos[0];
					var imgtex = imgtexs[0];
					var trans = transparencies[0];
					SetupOneDecalNodes(m_original.Decals.First(), texco, imgtex, trans);
					imgtex.outs.Color.Connect(lastMixer.ins.Color2);
					trans.outs.Value.Connect(lastMixer.ins.Fac);
				}
				else {
					idx = 0;

					// Set up decal images and texture coordinates.
					foreach(var decal in m_original.Decals) {
						var texco = texcos[idx];
						var imgtex = imgtexs[idx];
						var trans = transparencies[idx];
						SetupOneDecalNodes(decal, texco, imgtex, trans);
						idx++;
					}
					idx = 0;

					MixNode previousMixRgb = null;
					MathAdd previousAlphaMath = null;
					ImageTextureNode imgA = null;
					MathMultiply transA = null;
					// Use alpa addition nodes to go through all
					// node lists and connect them as needed.
					foreach(MathAdd alphaMath in alphamaths) {
						alphaMath.UseClamp = true;
						if(idx==0) {
							MixNode mixer = mixrgbs[idx];
							mixer.BlendType = MixNode.BlendTypes.Blend;
							imgA = imgtexs[idx];
							ImageTextureNode imgB = imgtexs[idx+1];
							transA = transparencies[idx];
							MathMultiply transB = transparencies[idx+1];


							imgA.outs.Color.Connect(mixer.ins.Color1);
							imgB.outs.Color.Connect(mixer.ins.Color2);

							transA.outs.Value.Connect(alphaMath.ins.Value1);
							transB.outs.Value.Connect(alphaMath.ins.Value2);

							transB.outs.Value.Connect(mixer.ins.Fac);

							previousAlphaMath = alphaMath;
							previousMixRgb = mixer;
						}
						else {
							MixNode mixer = mixrgbs[idx];
							imgA = imgtexs[idx+1];
							transA = transparencies[idx+1];

							previousMixRgb.outs.Color.Connect(mixer.ins.Color1);
							imgA.outs.Color.Connect(mixer.ins.Color2);

							previousAlphaMath.outs.Value.Connect(alphaMath.ins.Value1);
							transA.outs.Value.Connect(alphaMath.ins.Value2);
							transA.outs.Value.Connect(mixer.ins.Fac);

							previousAlphaMath = alphaMath;
							previousMixRgb = mixer;
						}

						idx++;
						if(idx==alphamaths.Count) {
							previousMixRgb.outs.Color.Connect(lastMixer.ins.Color2);
							previousAlphaMath.outs.Value.Connect(lastMixer.ins.Fac);
						}
					}
				}
				nodeToBindIntoShader = lastMixer;
				//lastMixer.outs.Color.Connect(m_codeshader.Output.ins.Surface);

				//m_codeshader.FinalizeGraph();

				//Rhino.RhinoApp.OutputDebugString($"{m_codeshader.Code}\n");
			}


			return nodeToBindIntoShader;
		}

		private ShaderNode GetShaderPart(ShaderBody part)
		{
			MixNode decalMixin = HandleDecals();

			if (part.IsPbr)
			{
				var principled = new PrincipledBsdfNode("pbr_principled");
				var texco = new TextureCoordinateNode("pbr_texco");
				var basewithao = new MixNode("pbr_basewithao");
				var aoamount = new MixNode("pbr_aoamount");
				var aotex = new ImageTextureNode("pbr_aotex");

				var roughwithsmudge = new MixNode("pbr_roughwithsmudge");
				var smudgetex = new ImageTextureNode("pbr_smudgetex");
				var bumpwithscratch = new BumpNode("pbr_bumpwithscratch");
				var scratchtex = new ImageTextureNode("pbr_scratchtex");

				var tangent = new TangentNode("tangents");

				var coloured_shadow_mix_custom = new MixClosureNode("coloured_shadow_mix_custom");
				var lightpath = new LightPathNode("light_path_for_coloured_shadow");
				var coloured_shadow_switch = new MathMultiply("coloured_shadow_switch");
				var coloured_shadow = new TransparentBsdfNode("coloured_shadow_transp_bsdf");

				aoamount.BlendType = MixNode.BlendTypes.Blend;
				aoamount.ins.Color1.Value = Rhino.Display.Color4f.Black.ToFloat4();
				aoamount.ins.Color2.Value = Rhino.Display.Color4f.White.ToFloat4();
				aoamount.ins.Fac.Value = 1.0f;
				basewithao.BlendType = MixNode.BlendTypes.Multiply;
				basewithao.ins.Fac.Value = 1.0f;
				basewithao.ins.Color2.Value = Rhino.Display.Color4f.White.ToFloat4();


				var emissive = new EmissionNode("pbr_emission");
				emissive.ins.Strength.Value = 1.0f;
				var addemissive = new AddClosureNode("pbr_addinemissive");

				principled.Sss = SubsurfaceScatteringNode.SssEnumFromInt(RcCore.It.AllSettings.SssMethod);

				var alpha_transparency_bsdf = new TransparentBsdfNode("alpha_transparency_bsdf");
				var alpha_transparency_mixer = new MixClosureNode("alpha_transparency_mixer");
				var alpha_transp_component = new MathSubtract("alpha_transp_component");
				alpha_transp_component.ins.Value1.Value = 1.0f;
				var alpha_invert_basecolalpha_component = new MathSubtract("alpha_invert_basecolalpha_component");
				alpha_invert_basecolalpha_component.ins.Value1.Value = 1.0f;

				var alpha_basecolalpha_plus_alphatransp = new MathAdd("alpha_basecolalpha_plus_alphatransp");
				var alpha_transparency_final = new MathSubtract("alpha_transparency_final");
				alpha_transparency_final.ins.Value1.Value = 1.0f;

				m_shader.AddNode(texco);
				m_shader.AddNode(emissive);
				m_shader.AddNode(addemissive);
				m_shader.AddNode(principled);
				m_shader.AddNode(basewithao);
				m_shader.AddNode(tangent);

				m_shader.AddNode(coloured_shadow_mix_custom);
				m_shader.AddNode(lightpath);
				m_shader.AddNode(coloured_shadow_switch);
				m_shader.AddNode(coloured_shadow);

				m_shader.AddNode(alpha_transparency_bsdf);
				m_shader.AddNode(alpha_transparency_mixer);
				m_shader.AddNode(alpha_transp_component);
				m_shader.AddNode(alpha_invert_basecolalpha_component);
				m_shader.AddNode(alpha_transparency_final);
				m_shader.AddNode(alpha_basecolalpha_plus_alphatransp);

				if(part.PbrAmbientOcclusion.On && part.PbrAmbientOcclusion.Amount > 0.01f && part.PbrAmbientOcclusionTexture.HasTextureImage) {
					m_shader.AddNode(aotex);
					m_shader.AddNode(aoamount);

					RenderEngine.SetTextureImage(aotex, part.PbrAmbientOcclusionTexture);
					aotex.Extension = part.PbrAmbientOcclusionTexture.Repeat ? ccl.ShaderNodes.TextureNode.TextureExtension.Repeat : ccl.ShaderNodes.TextureNode.TextureExtension.Clip;
					aotex.ColorSpace = TextureNode.TextureColorSpace.None;
					aotex.Projection = TextureNode.TextureProjection.Flat;
					RenderEngine.SetProjectionMode(m_shader, part.PbrAmbientOcclusionTexture, aotex, texco);
					aotex.outs.Color.Connect(aoamount.ins.Color2);
					aoamount.ins.Fac.Value = part.PbrAmbientOcclusion.Amount;
					aoamount.outs.Color.Connect(basewithao.ins.Color2);
				}

				ImageTextureNode basecoltex;

				if(decalMixin!=null) {
					basecoltex = Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture, decalMixin.ins.Color1.ToList(), texco, false);
					decalMixin.outs.Color.Connect(basewithao.ins.Color1);
				} else {
					basecoltex = Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture, basewithao.ins.Color1.ToList(), texco, false);
				}

				if(basecoltex != null && part.UseBaseColorTextureAlphaAsObjectAlpha) {
					basecoltex.outs.Alpha.Connect(alpha_invert_basecolalpha_component.ins.Value2);
					alpha_invert_basecolalpha_component.outs.Value.Connect(alpha_basecolalpha_plus_alphatransp.ins.Value1);
				}

				basewithao.outs.Color.Connect(principled.ins.BaseColor);
				basewithao.outs.Color.Connect(coloured_shadow.ins.Color);


				Utilities.PbrGraphForSlot(m_shader, part.PbrMetallic, part.PbrMetallicTexture, principled.ins.Metallic, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSpecular, part.PbrSpecularTexture, principled.ins.Specular, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSpecularTint, part.PbrSpecularTintTexture, principled.ins.SpecularTint, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrRoughness, part.PbrRoughnessTexture, principled.ins.Roughness, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSheen, part.PbrSheenTexture, principled.ins.Sheen, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSheenTint, part.PbrSheenTintTexture, principled.ins.SheenTint, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrClearcoat, part.PbrClearcoatTexture, principled.ins.Clearcoat, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrClearcoatRoughness, part.PbrClearcoatRoughnessTexture, principled.ins.ClearcoatGloss, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurface, part.PbrSubsurfaceTexture, principled.ins.Subsurface, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurfaceColor, part.PbrSubsurfaceColorTexture, principled.ins.SubsurfaceColor, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurfaceRadius, part.PbrSubsurfaceRadiusTexture, principled.ins.SubsurfaceRadius, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrTransmission, part.PbrTransmissionTexture, principled.ins.Transmission, texco, true);
				Utilities.PbrGraphForSlot(m_shader, part.PbrTransmission, part.PbrTransmissionTexture, coloured_shadow_switch.ins.Value2, texco, true);
				Utilities.PbrGraphForSlot(m_shader, part.PbrTransmissionRoughness, part.PbrTransmissionRoughnessTexture, principled.ins.TransmissionRoughness, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrIor, part.PbrIorTexture, principled.ins.IOR, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrAnisotropic, part.PbrAnisotropicTexture, principled.ins.Anisotropic, texco, false);
				Utilities.PbrGraphForSlot(m_shader, part.PbrAnisotropicRotation, part.PbrAnisotropicRotationTexture, principled.ins.AnisotropicRotation, texco, false);

				if (part.PbrSmudge.On && part.PbrSmudge.Amount > 0.001)
				{
					m_shader.AddNode(smudgetex);
					m_shader.AddNode(roughwithsmudge);

					roughwithsmudge.BlendType = MixNode.BlendTypes.Screen;
					roughwithsmudge.ins.Fac.Value = part.PbrSmudge.Amount;
					RenderEngine.SetTextureImage(smudgetex, part.PbrSmudgeTexture);
					smudgetex.Extension = part.PbrSmudgeTexture.Repeat ? ccl.ShaderNodes.TextureNode.TextureExtension.Repeat : ccl.ShaderNodes.TextureNode.TextureExtension.Clip;
					smudgetex.ColorSpace = TextureNode.TextureColorSpace.None;
					smudgetex.Projection = TextureNode.TextureProjection.Flat;
					RenderEngine.SetProjectionMode(m_shader, part.PbrSmudgeTexture, smudgetex, texco);

					var curcon = principled.ins.Roughness.ConnectionFrom;
					if(curcon!=null) {
						curcon.Connect(roughwithsmudge.ins.Color1);
						smudgetex.outs.Color.Connect(roughwithsmudge.ins.Color2);
						roughwithsmudge.outs.Color.Connect(principled.ins.Roughness);
					} else {
						smudgetex.outs.Color.Connect(principled.ins.Roughness);
					}
				}

				if (part.PbrBump.On && part.PbrBumpTexture.HasTextureImage)
				{
					if (!part.PbrBumpTexture.IsNormalMap)
					{
						var bump = new ccl.ShaderNodes.BumpNode("bump");
						m_shader.AddNode(bump);
						bump.ins.Strength.Value = Math.Abs(part.PbrBump.Amount) * RcCore.It.AllSettings.BumpStrengthFactor;
						bump.Invert = part.PbrBump.Amount < 0.0f;
						bump.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
						part.PbrBump.Amount = 1.0f;
						Utilities.GraphForSlot(m_shader, null, part.PbrBump.On, part.PbrBump.Amount, part.PbrBumpTexture, bump.ins.Height.ToList(), texco, true);
						bump.outs.Normal.Connect(principled.ins.Normal);
					} else {
						Utilities.GraphForSlot(m_shader, null, part.PbrBump.On, part.PbrBump.Amount, part.PbrBumpTexture, principled.ins.Normal.ToList(), texco, false, true, false);
					}
				}
				if (part.PbrClearcoatBump.On && part.PbrClearcoatBumpTexture.HasTextureImage)
				{
					if (!part.PbrClearcoatBumpTexture.IsNormalMap)
					{
						var bump = new ccl.ShaderNodes.BumpNode("clearcoat_bump");
						m_shader.AddNode(bump);
						bump.ins.Strength.Value = Math.Abs(part.PbrClearcoatBump.Amount) * RcCore.It.AllSettings.BumpStrengthFactor;
						bump.Invert = part.PbrClearcoatBump.Amount < 0.0f;
						part.PbrClearcoatBump.Amount = 1.0f;
						bump.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
						Utilities.GraphForSlot(m_shader, null, part.PbrClearcoatBump.On, part.PbrClearcoatBump.Amount, part.PbrClearcoatBumpTexture, bump.ins.Height.ToList(), texco, true);
						bump.outs.Normal.Connect(principled.ins.ClearcoatNormal);
					} else {
						Utilities.GraphForSlot(m_shader, null, part.PbrClearcoatBump.On, part.PbrClearcoatBump.Amount, part.PbrClearcoatBumpTexture, principled.ins.ClearcoatNormal.ToList(), texco, false, true, false);
					}
				}

				if (part.PbrScratch.On && part.PbrScratch.Amount > 0.001)
				{
					m_shader.AddNode(scratchtex);
					m_shader.AddNode(bumpwithscratch);

					bumpwithscratch.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
					bumpwithscratch.ins.Strength.Value = Math.Abs(part.PbrScratch.Amount) * RcCore.It.AllSettings.BumpStrengthFactor;
					bumpwithscratch.Invert = part.PbrScratch.Amount < 0.0f;
					RenderEngine.SetTextureImage(scratchtex, part.PbrScratchTexture);
					scratchtex.Extension = part.PbrScratchTexture.Repeat ? ccl.ShaderNodes.TextureNode.TextureExtension.Repeat : ccl.ShaderNodes.TextureNode.TextureExtension.Clip;
					scratchtex.ColorSpace = TextureNode.TextureColorSpace.None;
					scratchtex.Projection = TextureNode.TextureProjection.Flat;
					RenderEngine.SetProjectionMode(m_shader, part.PbrScratchTexture, scratchtex, texco);
					scratchtex.outs.Color.Connect(bumpwithscratch.ins.Height);

					var curcon = principled.ins.Normal.ConnectionFrom;
					if(curcon!=null) {

						curcon.Connect(bumpwithscratch.ins.Normal);
						bumpwithscratch.outs.Normal.Connect(principled.ins.Normal);
					} else {
						bumpwithscratch.outs.Normal.Connect(principled.ins.Normal);
					}
				}

				lightpath.outs.IsShadowRay.Connect(coloured_shadow_switch.ins.Value1);
				coloured_shadow_switch.outs.Value.Connect(coloured_shadow_mix_custom.ins.Fac);
				coloured_shadow.outs.BSDF.Connect(coloured_shadow_mix_custom.ins.Closure2);
				principled.outs.BSDF.Connect(coloured_shadow_mix_custom.ins.Closure1);

				coloured_shadow_mix_custom.outs.Closure.Connect(addemissive.ins.Closure1);

				float emission_strength = part.PbrEmission.Value.LargestComponent();

				if(part.PbrEmission.On && part.PbrEmissionTexture.HasTextureImage)
				{
					emissive.ins.Strength.Value = emission_strength;
					Utilities.PbrGraphForSlot(m_shader, part.PbrEmission, part.PbrEmissionTexture, emissive.ins.Color, texco, false);
					emissive.outs.Emission.Connect(addemissive.ins.Closure2);
				}
				else if(!part.PbrEmission.Value.Equals(Rhino.Display.Color4f.Black))
				{
					emissive.ins.Color.Value = part.PbrEmission.Value.ToFloat4();
					emissive.outs.Emission.Connect(addemissive.ins.Closure2);
				}

				addemissive.outs.Closure.Connect(alpha_transparency_mixer.ins.Closure2);
				alpha_transparency_bsdf.outs.BSDF.Connect(alpha_transparency_mixer.ins.Closure1);
				Utilities.PbrGraphForSlot(m_shader, part.PbrAlpha, part.PbrAlphaTexture, alpha_transp_component.ins.Value2, texco, false);

				alpha_transp_component.outs.Value.Connect(alpha_basecolalpha_plus_alphatransp.ins.Value2);

				alpha_basecolalpha_plus_alphatransp.outs.Value.Connect(alpha_transparency_final.ins.Value2);
				alpha_transparency_final.outs.Value.Connect(alpha_transparency_mixer.ins.Fac);

				tangent.outs.Tangent.Connect(principled.ins.Tangent);

				if(part.PbrDisplacement.On && part.PbrDisplacementTexture.HasTextureImage)
				{
					var displacement = new DisplacementNode();
					var strength = new MathMultiply();
					displacement.ins.Midlevel.Value = 0.0f;
					m_shader.AddNode(displacement);
					m_shader.AddNode(strength);
					strength.ins.Value1.Value = part.PbrDisplacement.Amount;
					part.PbrDisplacement.Amount = 1.0f;
					Utilities.PbrGraphForSlot(m_shader, part.PbrDisplacement, part.PbrDisplacementTexture, strength.ins.Value2, texco, false);
					strength.outs.Value.Connect(displacement.ins.Height);
					displacement.outs.Displacement.Connect(m_shader.Output.ins.Displacement);
				}

				return alpha_transparency_mixer;

			} else {
				// NOTE: need to add separate texture coordinate nodes for each channel, since different channels
				// can have different texture mappings with different transform matrices. Using only the one would
				// result in only one transform being applied and the rest will appear untouched.
				// See https://mcneel.myjetbrains.com/youtrack/issue/RH-51531

				// NOTE: decalMixin is manually added outside of GH definition

				var texcoord84 = new TextureCoordinateNode("texcoord_for_diff_");
				var texcoord84bump = new TextureCoordinateNode("texcoord_for_bump_");
				var texcoord84env = new TextureCoordinateNode("texcoord_for_envmap_");
				var texcoord84transp = new TextureCoordinateNode("texcoord_for_transp_");

				texcoord84.UvMap = "";
				texcoord84bump.UvMap = "";
				texcoord84env.UvMap = "";
				texcoord84transp.UvMap = "";

				// NOTE THAT NORMALMAP NODE IS OUTSIDE OF GH DEFINITION ADDED
				var normalmap = new NormalMapNode("normal_map_for_bumpslot_");
					normalmap.SpaceType = NormalMapNode.Space.Tangent;
				m_shader.AddNode(normalmap);
				// NOTE THAT NORMALMAP NODE IS OUTSIDE OF GH DEFINITION ADDED


				var invert_transparency68 = new MathSubtract("invert_transparency_");
					invert_transparency68.ins.Value1.Value = 1f;
					invert_transparency68.ins.Value2.Value = part.Transparency;
					invert_transparency68.Operation = MathNode.Operations.Subtract;
					invert_transparency68.UseClamp = false;

				var weight_diffuse_amount_by_transparency_inv69 = new MathMultiply("weight_diffuse_amount_by_transparency_inv_");
					weight_diffuse_amount_by_transparency_inv69.ins.Value1.Value = part.DiffuseTexture.Amount;
					weight_diffuse_amount_by_transparency_inv69.Operation = MathNode.Operations.Multiply;
					weight_diffuse_amount_by_transparency_inv69.UseClamp = false;

				var diff_tex_amount_multiplied_with_inv_transparency181 = new MathMultiply("diff_tex_amount_multiplied_with_inv_transparency_");
					diff_tex_amount_multiplied_with_inv_transparency181.Operation = MathNode.Operations.Multiply;
					diff_tex_amount_multiplied_with_inv_transparency181.UseClamp = false;

				var diffuse_texture85 = new ImageTextureNode("diffuse_texture_");
					diffuse_texture85.Projection = TextureNode.TextureProjection.Flat;
					diffuse_texture85.ColorSpace = TextureNode.TextureColorSpace.None;
					diffuse_texture85.Extension = part.DiffuseTexture.Repeat ? TextureNode.TextureExtension.Repeat : TextureNode.TextureExtension.Clip;
					diffuse_texture85.Interpolation = InterpolationType.Smart;
					diffuse_texture85.UseAlpha = true;
					diffuse_texture85.IsLinear = false;
					diffuse_texture85.AlternateTiles = part.DiffuseTexture.AlternateTiles;

				var diff_tex_weighted_alpha_for_basecol_mix182 = new MathMultiply("diff_tex_weighted_alpha_for_basecol_mix_");
					diff_tex_weighted_alpha_for_basecol_mix182.Operation = MathNode.Operations.Multiply;
					diff_tex_weighted_alpha_for_basecol_mix182.UseClamp = false;

				var diffuse_base_color_through_alpha180 = new MixNode("diffuse_base_color_through_alpha_");
					diffuse_base_color_through_alpha180.ins.Color1.Value = part.BaseColor;
					diffuse_base_color_through_alpha180.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					diffuse_base_color_through_alpha180.UseClamp = false;

				var use_alpha_weighted_with_modded_amount71 = new MathMultiply("use_alpha_weighted_with_modded_amount_");
					use_alpha_weighted_with_modded_amount71.ins.Value1.Value = part.DiffuseTexture.UseAlphaAsFloat;
					use_alpha_weighted_with_modded_amount71.Operation = MathNode.Operations.Multiply;
					use_alpha_weighted_with_modded_amount71.UseClamp = false;

				var bump_texture86 = new ImageTextureNode("bump_texture_");
					bump_texture86.Projection = TextureNode.TextureProjection.Flat;
					bump_texture86.ColorSpace = TextureNode.TextureColorSpace.None;
					bump_texture86.Extension = TextureNode.TextureExtension.Repeat;
					bump_texture86.Interpolation = InterpolationType.Smart;
					bump_texture86.UseAlpha = true;
					bump_texture86.IsLinear = false;
					bump_texture86.AlternateTiles = part.BumpTexture.AlternateTiles;

				var bump_texture_to_bw87 = new RgbToBwNode("bump_texture_to_bw_");

				var bump_amount72 = new MathMultiply("bump_amount_");
					bump_amount72.ins.Value1.Value = 4.66f;
					bump_amount72.ins.Value2.Value = part.BumpTexture.Amount;
					bump_amount72.Operation = MathNode.Operations.Multiply;
					bump_amount72.UseClamp = false;

				var diffuse_base_color_through_alpha120 = new MixNode("diffuse_base_color_through_alpha_");
					diffuse_base_color_through_alpha120.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					diffuse_base_color_through_alpha120.UseClamp = false;

				var bump88 = new BumpNode("bump_");
					bump88.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
					bump88.ins.Strength.Value = RcCore.It.AllSettings.BumpStrengthFactor;
					bump88.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;

				var light_path109 = new LightPathNode("light_path_");

				var final_diffuse89 = new DiffuseBsdfNode("final_diffuse_");
					final_diffuse89.ins.Roughness.Value = 0f;

				var shadeless_bsdf90 = new EmissionNode("shadeless_bsdf_");
					shadeless_bsdf90.ins.Strength.Value = 1f;

				var shadeless_on_cameraray122 = new MathMultiply("shadeless_on_cameraray_");
					shadeless_on_cameraray122.ins.Value2.Value = part.ShadelessAsFloat;
					shadeless_on_cameraray122.Operation = MathNode.Operations.Multiply;
					shadeless_on_cameraray122.UseClamp = false;

				var attenuated_reflection_color91 = new MixNode("attenuated_reflection_color_");
					attenuated_reflection_color91.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
					attenuated_reflection_color91.ins.Color2.Value = part.ReflectionColorGamma;
					attenuated_reflection_color91.ins.Fac.Value = part.Reflectivity;
					attenuated_reflection_color91.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					attenuated_reflection_color91.UseClamp = false;

				var fresnel_based_on_constant92 = new FresnelNode("fresnel_based_on_constant_");
					fresnel_based_on_constant92.ins.IOR.Value = part.FresnelIOR;

				var simple_reflection93 = new CombineRgbNode("simple_reflection_");
					simple_reflection93.ins.R.Value = part.Reflectivity;
					simple_reflection93.ins.G.Value = 0f;
					simple_reflection93.ins.B.Value = 0f;

				var fresnel_reflection94 = new CombineRgbNode("fresnel_reflection_");
					fresnel_reflection94.ins.G.Value = 0f;
					fresnel_reflection94.ins.B.Value = 0f;

				var fresnel_reflection_if_reflection_used73 = new MathMultiply("fresnel_reflection_if_reflection_used_");
					fresnel_reflection_if_reflection_used73.ins.Value1.Value = part.Reflectivity;
					fresnel_reflection_if_reflection_used73.ins.Value2.Value = part.FresnelReflectionsAsFloat;
					fresnel_reflection_if_reflection_used73.Operation = MathNode.Operations.Multiply;
					fresnel_reflection_if_reflection_used73.UseClamp = false;

				var select_reflection_or_fresnel_reflection95 = new MixNode("select_reflection_or_fresnel_reflection_");
					select_reflection_or_fresnel_reflection95.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					select_reflection_or_fresnel_reflection95.UseClamp = false;

				var shadeless96 = new MixClosureNode("shadeless_");

				var glossy97 = new GlossyBsdfNode("glossy_");
					glossy97.ins.Roughness.Value = part.ReflectionRoughness;

				var reflection_factor98 = new SeparateRgbNode("reflection_factor_");

				var attennuated_refraction_color99 = new MixNode("attennuated_refraction_color_");
					attennuated_refraction_color99.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
					attennuated_refraction_color99.ins.Color2.Value = part.TransparencyColorGamma;
					attennuated_refraction_color99.ins.Fac.Value = part.Transparency;
					attennuated_refraction_color99.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					attennuated_refraction_color99.UseClamp = false;

				var refraction100 = new RefractionBsdfNode("refraction_");
					refraction100.ins.Roughness.Value = part.RefractionRoughnessPow2;
					refraction100.ins.IOR.Value = part.IOR;
					refraction100.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;

				var diffuse_plus_glossy101 = new MixClosureNode("diffuse_plus_glossy_");

				var blend_in_transparency102 = new MixClosureNode("blend_in_transparency_");
					blend_in_transparency102.ins.Fac.Value = part.Transparency;

				var separate_envmap_texco103 = new SeparateXyzNode("separate_envmap_texco_");

				var flip_sign_envmap_texco_y74 = new MathMultiply("flip_sign_envmap_texco_y_");
					flip_sign_envmap_texco_y74.ins.Value2.Value = -1f;
					flip_sign_envmap_texco_y74.Operation = MathNode.Operations.Multiply;
					flip_sign_envmap_texco_y74.UseClamp = false;

				var recombine_envmap_texco104 = new CombineXyzNode("recombine_envmap_texco_");

				var environment_texture105 = new ImageTextureNode("environment_texture_");
					environment_texture105.Projection = TextureNode.TextureProjection.Flat;
					environment_texture105.ColorSpace = TextureNode.TextureColorSpace.None;
					environment_texture105.Extension = TextureNode.TextureExtension.Repeat;
					environment_texture105.Interpolation = InterpolationType.Smart;
					environment_texture105.UseAlpha = true;
					environment_texture105.IsLinear = false;
					environment_texture105.AlternateTiles = part.EnvironmentTexture.AlternateTiles;

				var attenuated_environment_color106 = new MixNode("attenuated_environment_color_");
					attenuated_environment_color106.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
					attenuated_environment_color106.ins.Fac.Value = part.EnvironmentTexture.Amount;
					attenuated_environment_color106.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					attenuated_environment_color106.UseClamp = false;

				var diffuse_glossy_and_refraction107 = new MixClosureNode("diffuse_glossy_and_refraction_");
					diffuse_glossy_and_refraction107.ins.Fac.Value = part.Transparency;

				var environment_map_diffuse108 = new DiffuseBsdfNode("environment_map_diffuse_");
					environment_map_diffuse108.ins.Roughness.Value = 0f;
					environment_map_diffuse108.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

				var invert_roughness75 = new MathSubtract("invert_roughness_");
					invert_roughness75.ins.Value1.Value = 1f;
					invert_roughness75.ins.Value2.Value = part.RefractionRoughness;
					invert_roughness75.Operation = MathNode.Operations.Subtract;
					invert_roughness75.UseClamp = false;

				var multiply_transparency76 = new MathMultiply("multiply_transparency_");
					multiply_transparency76.ins.Value2.Value = part.Transparency;
					multiply_transparency76.Operation = MathNode.Operations.Multiply;
					multiply_transparency76.UseClamp = false;

				var multiply_with_shadowray77 = new MathMultiply("multiply_with_shadowray_");
					multiply_with_shadowray77.Operation = MathNode.Operations.Multiply;
					multiply_with_shadowray77.UseClamp = false;

				var custom_environment_blend110 = new MixClosureNode("custom_environment_blend_");
					custom_environment_blend110.ins.Fac.Value = part.EnvironmentTexture.Amount;

				var coloured_shadow_trans_color111 = new TransparentBsdfNode("coloured_shadow_trans_color_");

				var weight_for_shadowray_coloured_shadow78 = new MathMultiply("weight_for_shadowray_coloured_shadow_");
					weight_for_shadowray_coloured_shadow78.ins.Value2.Value = 1f;
					weight_for_shadowray_coloured_shadow78.Operation = MathNode.Operations.Multiply;
					weight_for_shadowray_coloured_shadow78.UseClamp = false;

				var diffuse_from_emission_color123 = new DiffuseBsdfNode("diffuse_from_emission_color_");
					diffuse_from_emission_color123.ins.Color.Value = part.EmissionColorGamma;
					diffuse_from_emission_color123.ins.Roughness.Value = 0f;
					diffuse_from_emission_color123.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

				var shadeless_emission125 = new EmissionNode("shadeless_emission_");
					shadeless_emission125.ins.Color.Value = part.EmissionColorGamma;
					shadeless_emission125.ins.Strength.Value = 1f;

				var coloured_shadow_mix_custom114 = new MixClosureNode("coloured_shadow_mix_custom_");

				var diffuse_or_shadeless_emission126 = new MixClosureNode("diffuse_or_shadeless_emission_");

				var one_if_usealphatransp_turned_off178 = new MathLess_Than("one_if_usealphatransp_turned_off_");
					one_if_usealphatransp_turned_off178.ins.Value1.Value = part.DiffuseTexture.UseAlphaAsFloat;
					one_if_usealphatransp_turned_off178.ins.Value2.Value = 1f;
					one_if_usealphatransp_turned_off178.Operation = MathNode.Operations.Less_Than;
					one_if_usealphatransp_turned_off178.UseClamp = false;

				var max_of_texalpha_or_usealpha179 = new MathMaximum("max_of_texalpha_or_usealpha_");
					max_of_texalpha_or_usealpha179.Operation = MathNode.Operations.Maximum;
					max_of_texalpha_or_usealpha179.UseClamp = false;

				var invert_alpha70 = new MathSubtract("invert_alpha_");
					invert_alpha70.ins.Value1.Value = 1f;
					invert_alpha70.Operation = MathNode.Operations.Subtract;
					invert_alpha70.UseClamp = false;

				var transparency_texture112 = new ImageTextureNode("transparency_texture_");
					transparency_texture112.Projection = TextureNode.TextureProjection.Flat;
					transparency_texture112.ColorSpace = TextureNode.TextureColorSpace.None;
					transparency_texture112.Extension = TextureNode.TextureExtension.Repeat;
					transparency_texture112.Interpolation = InterpolationType.Smart;
					transparency_texture112.UseAlpha = true;
					transparency_texture112.IsLinear = false;
					transparency_texture112.AlternateTiles = part.TransparencyTexture.AlternateTiles;

				var transpluminance113 = new RgbToLuminanceNode("transpluminance_");

				var invert_luminence79 = new MathSubtract("invert_luminence_");
					invert_luminence79.ins.Value1.Value = 1f;
					invert_luminence79.Operation = MathNode.Operations.Subtract;
					invert_luminence79.UseClamp = false;

				var transparency_texture_amount80 = new MathMultiply("transparency_texture_amount_");
					transparency_texture_amount80.ins.Value2.Value = part.TransparencyTexture.Amount;
					transparency_texture_amount80.Operation = MathNode.Operations.Multiply;
					transparency_texture_amount80.UseClamp = false;

				var toggle_diffuse_texture_alpha_usage81 = new MathMultiply("toggle_diffuse_texture_alpha_usage_");
					toggle_diffuse_texture_alpha_usage81.ins.Value2.Value = part.DiffuseTexture.UseAlphaAsFloat;
					toggle_diffuse_texture_alpha_usage81.Operation = MathNode.Operations.Multiply;
					toggle_diffuse_texture_alpha_usage81.UseClamp = false;

				var toggle_transparency_texture82 = new MathMultiply("toggle_transparency_texture_");
					toggle_transparency_texture82.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
					toggle_transparency_texture82.Operation = MathNode.Operations.Multiply;
					toggle_transparency_texture82.UseClamp = false;

				var add_emission_to_final124 = new AddClosureNode("add_emission_to_final_");

				var transparent115 = new TransparentBsdfNode("transparent_");
					transparent115.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

				var add_diffuse_texture_alpha83 = new MathAdd("add_diffuse_texture_alpha_");
					add_diffuse_texture_alpha83.Operation = MathNode.Operations.Add;
					add_diffuse_texture_alpha83.UseClamp = false;

				var custom_alpha_cutter116 = new MixClosureNode("custom_alpha_cutter_");

				var mix_diffuse_and_transparency_color187 = new MixNode("mix_diffuse_and_transparency_color_");
					mix_diffuse_and_transparency_color187.ins.Fac.Value = part.Transparency;
					mix_diffuse_and_transparency_color187.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					mix_diffuse_and_transparency_color187.UseClamp = false;

				var principledbsdf117 = new PrincipledBsdfNode("principledbsdf_");
					principledbsdf117.ins.Subsurface.Value = 0f;
					principledbsdf117.ins.SubsurfaceRadius.Value = new ccl.float4(0f, 0f, 0f, 1f);
					principledbsdf117.ins.SubsurfaceColor.Value = new ccl.float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
					principledbsdf117.ins.Metallic.Value = part.Metallic;
					principledbsdf117.ins.Specular.Value = part.Specular;
					principledbsdf117.ins.SpecularTint.Value = part.SpecularTint;
					principledbsdf117.ins.Roughness.Value = part.ReflectionRoughness;
					principledbsdf117.ins.Anisotropic.Value = 0f;
					principledbsdf117.ins.AnisotropicRotation.Value = 0f;
					principledbsdf117.ins.Sheen.Value = part.Sheen;
					principledbsdf117.ins.SheenTint.Value = part.SheenTint;
					principledbsdf117.ins.Clearcoat.Value = part.ClearCoat;
					principledbsdf117.ins.ClearcoatGloss.Value = part.ClearCoatGloss;
					principledbsdf117.ins.IOR.Value = part.IOR;
					principledbsdf117.ins.Transmission.Value = part.Transparency;
					principledbsdf117.ins.TransmissionRoughness.Value = part.RefractionRoughness;
					principledbsdf117.ins.Tangent.Value = new ccl.float4(0f, 0f, 0f, 1f);

				var custom_environment_blend195 = new MixClosureNode("custom_environment_blend_");
					custom_environment_blend195.ins.Fac.Value = part.EnvironmentTexture.Amount;

				var coloured_shadow_trans_color_for_principled188 = new TransparentBsdfNode("coloured_shadow_trans_color_for_principled_");

				var coloured_shadow_mix_glass_principled118 = new MixClosureNode("coloured_shadow_mix_glass_principled_");

				m_shader.AddNode(texcoord84);
				m_shader.AddNode(texcoord84bump);
				m_shader.AddNode(texcoord84env);
				m_shader.AddNode(texcoord84transp);
				m_shader.AddNode(invert_transparency68);
				m_shader.AddNode(weight_diffuse_amount_by_transparency_inv69);
				m_shader.AddNode(diff_tex_amount_multiplied_with_inv_transparency181);
				m_shader.AddNode(diffuse_texture85);
				m_shader.AddNode(diff_tex_weighted_alpha_for_basecol_mix182);
				m_shader.AddNode(diffuse_base_color_through_alpha180);
				m_shader.AddNode(use_alpha_weighted_with_modded_amount71);
				m_shader.AddNode(bump_texture86);
				m_shader.AddNode(bump_texture_to_bw87);
				m_shader.AddNode(bump_amount72);
				m_shader.AddNode(diffuse_base_color_through_alpha120);
				m_shader.AddNode(bump88);
				m_shader.AddNode(light_path109);
				m_shader.AddNode(final_diffuse89);
				m_shader.AddNode(shadeless_bsdf90);
				m_shader.AddNode(shadeless_on_cameraray122);
				m_shader.AddNode(attenuated_reflection_color91);
				m_shader.AddNode(fresnel_based_on_constant92);
				m_shader.AddNode(simple_reflection93);
				m_shader.AddNode(fresnel_reflection94);
				m_shader.AddNode(fresnel_reflection_if_reflection_used73);
				m_shader.AddNode(select_reflection_or_fresnel_reflection95);
				m_shader.AddNode(shadeless96);
				m_shader.AddNode(glossy97);
				m_shader.AddNode(reflection_factor98);
				m_shader.AddNode(attennuated_refraction_color99);
				m_shader.AddNode(refraction100);
				m_shader.AddNode(diffuse_plus_glossy101);
				m_shader.AddNode(blend_in_transparency102);
				m_shader.AddNode(separate_envmap_texco103);
				m_shader.AddNode(flip_sign_envmap_texco_y74);
				m_shader.AddNode(recombine_envmap_texco104);
				m_shader.AddNode(environment_texture105);
				m_shader.AddNode(attenuated_environment_color106);
				m_shader.AddNode(diffuse_glossy_and_refraction107);
				m_shader.AddNode(environment_map_diffuse108);
				m_shader.AddNode(invert_roughness75);
				m_shader.AddNode(multiply_transparency76);
				m_shader.AddNode(multiply_with_shadowray77);
				m_shader.AddNode(custom_environment_blend110);
				m_shader.AddNode(coloured_shadow_trans_color111);
				m_shader.AddNode(weight_for_shadowray_coloured_shadow78);
				m_shader.AddNode(diffuse_from_emission_color123);
				m_shader.AddNode(shadeless_emission125);
				m_shader.AddNode(coloured_shadow_mix_custom114);
				m_shader.AddNode(diffuse_or_shadeless_emission126);
				m_shader.AddNode(one_if_usealphatransp_turned_off178);
				m_shader.AddNode(max_of_texalpha_or_usealpha179);
				m_shader.AddNode(invert_alpha70);
				m_shader.AddNode(transparency_texture112);
				m_shader.AddNode(transpluminance113);
				m_shader.AddNode(invert_luminence79);
				m_shader.AddNode(transparency_texture_amount80);
				m_shader.AddNode(toggle_diffuse_texture_alpha_usage81);
				m_shader.AddNode(toggle_transparency_texture82);
				m_shader.AddNode(add_emission_to_final124);
				m_shader.AddNode(transparent115);
				m_shader.AddNode(add_diffuse_texture_alpha83);
				m_shader.AddNode(custom_alpha_cutter116);
				m_shader.AddNode(mix_diffuse_and_transparency_color187);
				m_shader.AddNode(principledbsdf117);
				m_shader.AddNode(custom_environment_blend195);
				m_shader.AddNode(coloured_shadow_trans_color_for_principled188);
				m_shader.AddNode(coloured_shadow_mix_glass_principled118);


				invert_transparency68.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv69.ins.Value2);
				weight_diffuse_amount_by_transparency_inv69.outs.Value.Connect(diff_tex_amount_multiplied_with_inv_transparency181.ins.Value1);
				invert_transparency68.outs.Value.Connect(diff_tex_amount_multiplied_with_inv_transparency181.ins.Value2);
				texcoord84.outs.UV.Connect(diffuse_texture85.ins.Vector);
				diff_tex_amount_multiplied_with_inv_transparency181.outs.Value.Connect(diff_tex_weighted_alpha_for_basecol_mix182.ins.Value1);
				diffuse_texture85.outs.Alpha.Connect(diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2);
				diffuse_texture85.outs.Color.Connect(diffuse_base_color_through_alpha180.ins.Color2);
				diff_tex_weighted_alpha_for_basecol_mix182.outs.Value.Connect(diffuse_base_color_through_alpha180.ins.Fac);
				weight_diffuse_amount_by_transparency_inv69.outs.Value.Connect(use_alpha_weighted_with_modded_amount71.ins.Value2);
				texcoord84bump.outs.UV.Connect(bump_texture86.ins.Vector);
				bump_texture86.outs.Color.Connect(bump_texture_to_bw87.ins.Color);
				bump_texture86.outs.Color.Connect(normalmap.ins.Color);
				diffuse_base_color_through_alpha180.outs.Color.Connect(diffuse_base_color_through_alpha120.ins.Color1);
				diffuse_texture85.outs.Color.Connect(diffuse_base_color_through_alpha120.ins.Color2);
				use_alpha_weighted_with_modded_amount71.outs.Value.Connect(diffuse_base_color_through_alpha120.ins.Fac);
				bump_texture_to_bw87.outs.Val.Connect(bump88.ins.Height);
				bump_amount72.outs.Value.Connect(bump88.ins.Strength);

				if(decalMixin!=null) {
					diffuse_base_color_through_alpha120.outs.Color.Connect(decalMixin.ins.Color1);
					decalMixin.outs.Color.Connect(final_diffuse89.ins.Color);
					decalMixin.outs.Color.Connect(shadeless_bsdf90.ins.Color);
				} else {
					diffuse_base_color_through_alpha120.outs.Color.Connect(final_diffuse89.ins.Color);
					diffuse_base_color_through_alpha120.outs.Color.Connect(shadeless_bsdf90.ins.Color);
				}

				light_path109.outs.IsCameraRay.Connect(shadeless_on_cameraray122.ins.Value1);
				fresnel_based_on_constant92.outs.Fac.Connect(fresnel_reflection94.ins.R);
				simple_reflection93.outs.Image.Connect(select_reflection_or_fresnel_reflection95.ins.Color1);
				fresnel_reflection94.outs.Image.Connect(select_reflection_or_fresnel_reflection95.ins.Color2);
				fresnel_reflection_if_reflection_used73.outs.Value.Connect(select_reflection_or_fresnel_reflection95.ins.Fac);
				final_diffuse89.outs.BSDF.Connect(shadeless96.ins.Closure1);
				shadeless_bsdf90.outs.Emission.Connect(shadeless96.ins.Closure2);
				shadeless_on_cameraray122.outs.Value.Connect(shadeless96.ins.Fac);
				attenuated_reflection_color91.outs.Color.Connect(glossy97.ins.Color);
				select_reflection_or_fresnel_reflection95.outs.Color.Connect(reflection_factor98.ins.Image);
				attennuated_refraction_color99.outs.Color.Connect(refraction100.ins.Color);
				shadeless96.outs.Closure.Connect(diffuse_plus_glossy101.ins.Closure1);
				glossy97.outs.BSDF.Connect(diffuse_plus_glossy101.ins.Closure2);
				reflection_factor98.outs.R.Connect(diffuse_plus_glossy101.ins.Fac);
				shadeless96.outs.Closure.Connect(blend_in_transparency102.ins.Closure1);
				refraction100.outs.BSDF.Connect(blend_in_transparency102.ins.Closure2);
				texcoord84.outs.EnvEmap.Connect(separate_envmap_texco103.ins.Vector);
				separate_envmap_texco103.outs.Y.Connect(flip_sign_envmap_texco_y74.ins.Value1);
				separate_envmap_texco103.outs.X.Connect(recombine_envmap_texco104.ins.X);
				flip_sign_envmap_texco_y74.outs.Value.Connect(recombine_envmap_texco104.ins.Y);
				separate_envmap_texco103.outs.Z.Connect(recombine_envmap_texco104.ins.Z);
				recombine_envmap_texco104.outs.Vector.Connect(environment_texture105.ins.Vector);
				environment_texture105.outs.Color.Connect(attenuated_environment_color106.ins.Color2);
				diffuse_plus_glossy101.outs.Closure.Connect(diffuse_glossy_and_refraction107.ins.Closure1);
				blend_in_transparency102.outs.Closure.Connect(diffuse_glossy_and_refraction107.ins.Closure2);
				attenuated_environment_color106.outs.Color.Connect(environment_map_diffuse108.ins.Color);
				invert_roughness75.outs.Value.Connect(multiply_transparency76.ins.Value1);
				multiply_transparency76.outs.Value.Connect(multiply_with_shadowray77.ins.Value1);
				light_path109.outs.IsShadowRay.Connect(multiply_with_shadowray77.ins.Value2);
				diffuse_glossy_and_refraction107.outs.Closure.Connect(custom_environment_blend110.ins.Closure1);
				environment_map_diffuse108.outs.BSDF.Connect(custom_environment_blend110.ins.Closure2);
				diffuse_base_color_through_alpha120.outs.Color.Connect(coloured_shadow_trans_color111.ins.Color);
				multiply_with_shadowray77.outs.Value.Connect(weight_for_shadowray_coloured_shadow78.ins.Value1);
				custom_environment_blend110.outs.Closure.Connect(coloured_shadow_mix_custom114.ins.Closure1);
				coloured_shadow_trans_color111.outs.BSDF.Connect(coloured_shadow_mix_custom114.ins.Closure2);
				weight_for_shadowray_coloured_shadow78.outs.Value.Connect(coloured_shadow_mix_custom114.ins.Fac);
				diffuse_from_emission_color123.outs.BSDF.Connect(diffuse_or_shadeless_emission126.ins.Closure1);
				shadeless_emission125.outs.Emission.Connect(diffuse_or_shadeless_emission126.ins.Closure2);
				shadeless_on_cameraray122.outs.Value.Connect(diffuse_or_shadeless_emission126.ins.Fac);
				diffuse_texture85.outs.Alpha.Connect(max_of_texalpha_or_usealpha179.ins.Value1);
				one_if_usealphatransp_turned_off178.outs.Value.Connect(max_of_texalpha_or_usealpha179.ins.Value2);
				max_of_texalpha_or_usealpha179.outs.Value.Connect(invert_alpha70.ins.Value2);
				texcoord84transp.outs.UV.Connect(transparency_texture112.ins.Vector);
				transparency_texture112.outs.Color.Connect(transpluminance113.ins.Color);
				transpluminance113.outs.Val.Connect(invert_luminence79.ins.Value2);
				invert_luminence79.outs.Value.Connect(transparency_texture_amount80.ins.Value1);
				invert_alpha70.outs.Value.Connect(toggle_diffuse_texture_alpha_usage81.ins.Value1);
				transparency_texture_amount80.outs.Value.Connect(toggle_transparency_texture82.ins.Value2);
				coloured_shadow_mix_custom114.outs.Closure.Connect(add_emission_to_final124.ins.Closure1);
				diffuse_or_shadeless_emission126.outs.Closure.Connect(add_emission_to_final124.ins.Closure2);
				toggle_diffuse_texture_alpha_usage81.outs.Value.Connect(add_diffuse_texture_alpha83.ins.Value1);
				toggle_transparency_texture82.outs.Value.Connect(add_diffuse_texture_alpha83.ins.Value2);
				add_emission_to_final124.outs.Closure.Connect(custom_alpha_cutter116.ins.Closure1);
				transparent115.outs.BSDF.Connect(custom_alpha_cutter116.ins.Closure2);
				add_diffuse_texture_alpha83.outs.Value.Connect(custom_alpha_cutter116.ins.Fac);
				diffuse_base_color_through_alpha120.outs.Color.Connect(mix_diffuse_and_transparency_color187.ins.Color1);
				attennuated_refraction_color99.outs.Color.Connect(mix_diffuse_and_transparency_color187.ins.Color2);
				mix_diffuse_and_transparency_color187.outs.Color.Connect(principledbsdf117.ins.BaseColor);
				principledbsdf117.outs.BSDF.Connect(custom_environment_blend195.ins.Closure1);
				environment_map_diffuse108.outs.BSDF.Connect(custom_environment_blend195.ins.Closure2);
				mix_diffuse_and_transparency_color187.outs.Color.Connect(coloured_shadow_trans_color_for_principled188.ins.Color);
				custom_environment_blend195.outs.Closure.Connect(coloured_shadow_mix_glass_principled118.ins.Closure1);
				coloured_shadow_trans_color_for_principled188.outs.BSDF.Connect(coloured_shadow_mix_glass_principled118.ins.Closure2);
				weight_for_shadowray_coloured_shadow78.outs.Value.Connect(coloured_shadow_mix_glass_principled118.ins.Fac);

				/* extra code */

				if (part.BumpTexture.HasTextureImage && part.BumpTexture.Amount > 0.0f)
				{
					if (!part.BumpTexture.IsNormalMap)
					{
						bump88.outs.Normal.Connect(final_diffuse89.ins.Normal);
						bump88.outs.Normal.Connect(fresnel_based_on_constant92.ins.Normal);
						bump88.outs.Normal.Connect(principledbsdf117.ins.Normal);
						bump88.outs.Normal.Connect(principledbsdf117.ins.ClearcoatNormal);
						bump88.outs.Normal.Connect(refraction100.ins.Normal);
						bump88.outs.Normal.Connect(glossy97.ins.Normal);
					} else {
						normalmap.outs.Normal.Connect(final_diffuse89.ins.Normal);
						normalmap.outs.Normal.Connect(fresnel_based_on_constant92.ins.Normal);
						normalmap.outs.Normal.Connect(principledbsdf117.ins.Normal);
						normalmap.outs.Normal.Connect(principledbsdf117.ins.ClearcoatNormal);
						normalmap.outs.Normal.Connect(refraction100.ins.Normal);
						normalmap.outs.Normal.Connect(glossy97.ins.Normal);
					}
				}

				if (part.HasTransparencyTexture)
				{
					texcoord84transp.UvMap = part.TransparencyTexture.GetUvMapForChannel();
					RenderEngine.SetTextureImage(transparency_texture112, part.TransparencyTexture);
					RenderEngine.SetProjectionMode(m_shader, part.TransparencyTexture, transparency_texture112, texcoord84transp);
				}

				if (part.HasDiffuseTexture)
				{
					texcoord84.UvMap = part.DiffuseTexture.GetUvMapForChannel();
					RenderEngine.SetTextureImage(diffuse_texture85, part.DiffuseTexture);
					RenderEngine.SetProjectionMode(m_shader, part.DiffuseTexture, diffuse_texture85, texcoord84);
				}

				if (part.HasBumpTexture)
				{
					texcoord84bump.UvMap = part.BumpTexture.GetUvMapForChannel();
					RenderEngine.SetTextureImage(bump_texture86, part.BumpTexture);
					RenderEngine.SetProjectionMode(m_shader, part.BumpTexture, bump_texture86, texcoord84bump);
				}

				if (part.HasEnvironmentTexture)
				{
					RenderEngine.SetTextureImage(environment_texture105, part.EnvironmentTexture);
					RenderEngine.SetProjectionMode(m_shader, part.EnvironmentTexture, environment_texture105, texcoord84env);
				}

				if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass
					|| part.CyclesMaterialType == ShaderBody.CyclesMaterial.SimplePlastic
					|| part.CyclesMaterialType == ShaderBody.CyclesMaterial.SimpleMetal) return coloured_shadow_mix_glass_principled118;
				return custom_alpha_cutter116;
			}
		}

	}
}
