/**
Copyright 2014-2021 Robert McNeel and Associates

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
using RhinoCyclesCore.Converters;
using sdd = System.Diagnostics.Debug;
using System.IO;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoFullNxt : RhinoShader
	{
		public RhinoFullNxt(Session client, CyclesShader intermediate) : this(client, intermediate, null, intermediate.Front.Name, true)
		{
		}

		public RhinoFullNxt(Session client, CyclesShader intermediate, Shader existing, bool recreate) : this(client, intermediate, existing, intermediate.Front.Name, recreate)
		{
		}

		public RhinoFullNxt(Session client, CyclesShader intermediate, Shader existing) : this(client, intermediate, existing, intermediate.Front.Name, true)
		{
		}

		public RhinoFullNxt(Session client, CyclesShader intermediate, Shader existing, string name, bool recreate) : base(client, intermediate, name, existing, recreate)
		{
		}

		public ClosureSocket GetClosureSocket()
		{
			if (m_original.DisplayMaterial)
			{
				var front = GetShaderPart(m_original.Front);
				var back = GetShaderPart(m_original.Back);

				var backfacing=  new GeometryInfoNode(m_shader, "backfacepicker_");
				var flipper = new MixClosureNode(m_shader, "front_or_back_");

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

				// InvisibleUnderside may be true if it is set for a material
				// on a Ground Plane. Handle this case by adding a transparent BSDF
				// for when the backface is hit. Otherwise just 'regular' shader
				// as created by GetShaderPart() above.
				if (m_original.InvisibleUnderside)
				{
					var transparent = new TransparentBsdfNode(m_shader, "transparent_gp");
					transparent.ins.Color.Value = new float4(1.0, 1.0, 1.0, 1.0);
					var backfacing = new GeometryInfoNode(m_shader, "backfacepicker_");
					var flipper = new MixClosureNode(m_shader, "front_or_back_");

					lastclosure.Connect(flipper.ins.Closure1);
					transparent.outs.BSDF.Connect(flipper.ins.Closure2);
					backfacing.outs.Backfacing.Connect(flipper.ins.Fac);
					lastclosure = flipper.GetClosureSocket();
				}

				return lastclosure;
			}

		}

		public override Shader GetShader()
		{
			if (RcCore.It.AllSettings.DebugSimpleShaders)
			{
				RhinoTextureCoordinateNode texco = new RhinoTextureCoordinateNode(m_shader, "debug_texco");
				texco.UvMap = "uvmap1";
				ccl.ShaderNodes.DiffuseBsdfNode diff = new DiffuseBsdfNode(m_shader, "debug_diff_");
				diff.ins.Color.Value = new float4(0.8f, 0.6f, 0.5f, 1.0f);
				texco.outs.UV.Connect(diff.ins.Color);
				diff.outs.BSDF.Connect(m_shader.Output.ins.Surface);
			}
			else
			{
				var lc = GetClosureSocket();
				lc.Connect(m_shader.Output.ins.Surface);
			}
			m_shader.WriteDataToNodes();
			if (RcCore.It.AllSettings.DumpMaterialShaderGraph)
			{
				var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				var graph_path = Path.Combine(home, $"rhinofullnxt_{m_shader.Id}.dot");
				m_shader.DumpGraph(graph_path);
			}
			return m_shader;
		}

		static private void SetupOneDecalNodes(CyclesDecal decal, RhinoTextureCoordinateNode texco, ImageTextureNode imgtex, MathMultiply transp, TextureAdjustmentTextureProceduralNode adjust)
		{
			texco.ObjectTransform = decal.Transform;
			texco.UseTransform = true;

			RenderEngine.SetTextureImage(imgtex, decal.Texture);
			texco.UvMap = decal.Texture.GetUvMapForChannel();
			imgtex.Extension = TextureNode.TextureExtension.Clip;
			imgtex.UseAlpha = true;

			adjust.Grayscale = decal.Texture.AdjustGrayscale;
			adjust.Invert = decal.Texture.AdjustInvert;
			adjust.Clamp = decal.Texture.AdjustClamp;
			adjust.ScaleToClamp = decal.Texture.AdjustScaleToClamp;
			adjust.Multiplier = decal.Texture.AdjustMultiplier;
			adjust.ClampMin = decal.Texture.AdjustClampMin;
			adjust.ClampMax = decal.Texture.AdjustClampMax;
			adjust.Gain = decal.Texture.AdjustGain;
			adjust.Gamma = decal.Texture.AdjustGamma;
			adjust.Saturation = decal.Texture.AdjustSaturation;
			adjust.HueShift = decal.Texture.AdjustHueShift;
			adjust.IsHdr = decal.Texture.AdjustIsHdr;

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
				List<RhinoTextureCoordinateNode> texcos = new List<RhinoTextureCoordinateNode>(count);
				List<ImageTextureNode> imgtexs= new List<ImageTextureNode>(count);
				List<MixNode> mixrgbs = new List<MixNode>(count);
				List<MathMultiply> transparencies = new List<MathMultiply>(count);
				List<MathAdd> alphamaths = new List<MathAdd>(count);
				List<TextureAdjustmentTextureProceduralNode> adjustments = new List<TextureAdjustmentTextureProceduralNode>(count);
				int idx = 1;

				// First create all the nodes we need to set up decals
				// for this material.
				for (int i = 0; i < count; i++)
				{
					texcos.Add(new RhinoTextureCoordinateNode(m_shader, $"Decal_{idx}_texco_"));
					imgtexs.Add(new ImageTextureNode(m_shader, $"Texture_for_decal_{idx}_"));
					mixrgbs.Add(new MixNode(m_shader, $"Decal_mixer_{idx}_"));
					transparencies.Add(new MathMultiply(m_shader, $"Decal_transparency_multiplier_{idx}_"));
					adjustments.Add(new TextureAdjustmentTextureProceduralNode(m_shader, $"Decal_texadjustment_{idx}_"));
					if(i < count-1) {
						alphamaths.Add(new MathAdd(m_shader, $"Decal_alpha_adder_{idx}_"));
					}

					idx++;
				}

				MixNode lastMixer = mixrgbs.Last();

				if(count == 1) {
					var texco = texcos[0];
					var imgtex = imgtexs[0];
					var trans = transparencies[0];
					var adjust = adjustments[0];
					SetupOneDecalNodes(m_original.Decals.First(), texco, imgtex, trans, adjust);
					if(m_original.Decals[0].Texture.AdjustNeeded) {
						imgtex.outs.Color.Connect(adjust.ins.Color);
						adjust.outs.Color.Connect(lastMixer.ins.Color2);
					} else {
						imgtex.outs.Color.Connect(lastMixer.ins.Color2);
					}
					trans.outs.Value.Connect(lastMixer.ins.Fac);
				}
				else {
					idx = 0;

					// Set up decal images and texture coordinates.
					foreach(var decal in m_original.Decals) {
						var texco = texcos[idx];
						var imgtex = imgtexs[idx];
						var trans = transparencies[idx];
						var adjust = adjustments[idx];
						SetupOneDecalNodes(decal, texco, imgtex, trans, adjust);
						idx++;
					}
					idx = 0;

					MixNode previousMixRgb = null;
					MathAdd previousAlphaMath = null;
					ImageTextureNode imgA = null;
					MathMultiply transA = null;
					TextureAdjustmentTextureProceduralNode adjustA = null;
					// Use alpa addition nodes to go through all
					// node lists and connect them as needed.
					foreach(MathAdd alphaMath in alphamaths) {
						alphaMath.UseClamp = true;
						if(idx==0) {
							CyclesTextureImage teximA = m_original.Decals[idx].Texture;
							MixNode mixer = mixrgbs[idx];
							mixer.BlendType = MixNode.BlendTypes.Blend;
							imgA = imgtexs[idx];
							adjustA = adjustments[idx];
							transA = transparencies[idx];

							CyclesTextureImage teximB = m_original.Decals[idx+1].Texture;
							ImageTextureNode imgB = imgtexs[idx+1];
							MathMultiply transB = transparencies[idx+1];
							TextureAdjustmentTextureProceduralNode adjustB = adjustments[idx];

							if(teximA.AdjustNeeded) {
								imgA.outs.Color.Connect(adjustA.ins.Color);
								adjustA.outs.Color.Connect(mixer.ins.Color1);

							} else {
								imgA.outs.Color.Connect(mixer.ins.Color1);
							}
							if(teximB.AdjustNeeded)
							{
								imgB.outs.Color.Connect(adjustB.ins.Color);
								adjustB.outs.Color.Connect(mixer.ins.Color2);
							}
							else {
								imgB.outs.Color.Connect(mixer.ins.Color2);
							}

							transA.outs.Value.Connect(alphaMath.ins.Value1);
							transB.outs.Value.Connect(alphaMath.ins.Value2);

							transB.outs.Value.Connect(mixer.ins.Fac);

							previousAlphaMath = alphaMath;
							previousMixRgb = mixer;
						}
						else {
							MixNode mixer = mixrgbs[idx];
							CyclesTextureImage teximA = m_original.Decals[idx+1].Texture;
							imgA = imgtexs[idx+1];
							transA = transparencies[idx+1];
							adjustA = adjustments[idx+1];

							previousMixRgb.outs.Color.Connect(mixer.ins.Color1);
							if(teximA.AdjustNeeded) {
								imgA.outs.Color.Connect(adjustA.ins.Color);
								adjustA.outs.Color.Connect(mixer.ins.Color2);
							}
							else {
								imgA.outs.Color.Connect(mixer.ins.Color2);
							}

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
				//m_codeshader.WriteDataToNodes();
				//Rhino.RhinoApp.OutputDebugString($"{m_codeshader.Code}\n");
			}


			return nodeToBindIntoShader;
		}

		private ShaderNode GetShaderPart(ShaderBody part)
		{
			if (part.BlendMaterial)
			{
				ShaderNode materialOne = null;
				ShaderNode materialTwo = null;
				MixClosureNode blender = new MixClosureNode(m_shader, "blend material blender");
				blender.ins.Fac.Value = part.BlendMixAmount;
				if (part.MaterialOne != null)
				{
					materialOne = GetShaderPart(part.MaterialOne);
				}
				else
				{
					DiffuseBsdfNode diff = new DiffuseBsdfNode(m_shader, "materialOne diffuse bsdf");
					diff.ins.Color.Value = new float4(0.9, 0.9, 0.9, 1.0);
					materialOne = diff;
				}
				if (part.MaterialTwo != null)
				{
					materialTwo = GetShaderPart(part.MaterialTwo);
				}
				else
				{
					DiffuseBsdfNode diff = new DiffuseBsdfNode(m_shader, "materialTwo diffuse bsdf");
					diff.ins.Color.Value = new float4(0.9, 0.9, 0.9, 1.0);
					materialTwo = diff;
				}
				materialOne.GetClosureSocket().Connect(blender.ins.Closure1);
				materialTwo.GetClosureSocket().Connect(blender.ins.Closure2);

				if (part.BlendMixAmountTexture.HasProcedural) {
					Utilities.GraphForSlot(m_shader, null, part.BlendMixAmount > 0.0f, part.BlendMixAmountTexture.Amount, part.BlendMixAmountTexture, blender.ins.Fac.ToList(), true, false, false, true, part.Gamma);
				}
				return blender;
			}
			else
			{
				MixNode decalMixin = HandleDecals();

				if (part.IsPbr)
				{
					var principled = new PrincipledBsdfNode(m_shader, "pbr_principled");

					var tangent = new TangentNode(m_shader, "tangents");

					var coloured_shadow_mix_custom = new MixClosureNode(m_shader, "coloured_shadow_mix_custom");
					var lightpath = new LightPathNode(m_shader, "light_path_for_coloured_shadow");
					var coloured_shadow_switch = new MathMultiply(m_shader, "coloured_shadow_switch");
					var coloured_shadow = new TransparentBsdfNode(m_shader, "coloured_shadow_transp_bsdf");

					principled.Sss = PrincipledBsdfNode.ScatterMethod.RandomWalk; //SubsurfaceScatteringNode.SssEnumFromInt(RcCore.It.AllSettings.SssMethod);

					var alpha_transp_component = new MathSubtract(m_shader, "alpha_transp_component");
					alpha_transp_component.ins.Value1.Value = 1.0f;
					var alpha_invert_basecolalpha_component = new MathSubtract(m_shader, "alpha_invert_basecolalpha_component");
					alpha_invert_basecolalpha_component.ins.Value1.Value = 1.0f;

					var alpha_basecolalpha_plus_alphatransp = new MathAdd(m_shader, "alpha_basecolalpha_plus_alphatransp");
					var alpha_transparency_final = new MathSubtract(m_shader, "alpha_transparency_final");
					alpha_transparency_final.ins.Value1.Value = 1.0f;

					var alpha_cutter_bsdf = new TransparentBsdfNode(m_shader, "alpha_cutter_on_coloured_shadow");
					alpha_cutter_bsdf.ins.Color.Value = new float4(1.0f);

					var alpha_cutter_mixer = new MixClosureNode(m_shader, "alpha_cutter_on_coloured_shadow_mixer");
					alpha_cutter_bsdf.outs.BSDF.Connect(alpha_cutter_mixer.ins.Closure1);

					ISocket basecoltexAlphaOut;

					List<ISocket> colsocks = new()
					{
						principled.ins.BaseColor,
						coloured_shadow.ins.Color
					};

					if (decalMixin != null)
					{
						basecoltexAlphaOut = Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture, decalMixin.ins.Color1.ToList(), false, part.Gamma, false);
						foreach(var colsock in colsocks)
						{
							decalMixin.outs.Color.Connect(colsock);
						}
					}
					else
					{
						basecoltexAlphaOut = Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture, colsocks, false, part.Gamma, false);
					}

					if (basecoltexAlphaOut != null && part.UseBaseColorTextureAlphaAsObjectAlpha)
					{
						basecoltexAlphaOut.Connect(alpha_invert_basecolalpha_component.ins.Value2);
						alpha_invert_basecolalpha_component.outs.Value.Connect(alpha_basecolalpha_plus_alphatransp.ins.Value1);
					}

					Utilities.PbrGraphForSlot(m_shader, part.PbrMetallic, part.PbrMetallicTexture, principled.ins.Metallic.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSpecular, part.PbrSpecularTexture, principled.ins.Specular.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSpecularTint, part.PbrSpecularTintTexture, principled.ins.SpecularTint.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrRoughness, part.PbrRoughnessTexture, principled.ins.Roughness.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSheen, part.PbrSheenTexture, principled.ins.Sheen.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSheenTint, part.PbrSheenTintTexture, principled.ins.SheenTint.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrClearcoat, part.PbrClearcoatTexture, principled.ins.Clearcoat.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrClearcoatRoughness, part.PbrClearcoatRoughnessTexture, principled.ins.ClearcoatGloss.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurface, part.PbrSubsurfaceTexture, principled.ins.Subsurface.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurfaceColor, part.PbrSubsurfaceColorTexture, principled.ins.SubsurfaceColor.ToList(), false, part.Gamma, false);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurfaceRadius, part.PbrSubsurfaceRadiusTexture, principled.ins.SubsurfaceRadius.ToList(), false, part.Gamma, true);

					List<ISocket> transmissionSockets = new() {
						principled.ins.Transmission,
						coloured_shadow_switch.ins.Value2
					};
					Utilities.PbrGraphForSlot(m_shader, part.PbrTransmission, part.PbrTransmissionTexture, transmissionSockets, true, part.Gamma, true);

					Utilities.PbrGraphForSlot(m_shader, part.PbrTransmissionRoughness, part.PbrTransmissionRoughnessTexture, principled.ins.TransmissionRoughness.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrIor, part.PbrIorTexture, principled.ins.IOR.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrAnisotropic, part.PbrAnisotropicTexture, principled.ins.Anisotropic.ToList(), false, part.Gamma, true);
					Utilities.PbrGraphForSlot(m_shader, part.PbrAnisotropicRotation, part.PbrAnisotropicRotationTexture, principled.ins.AnisotropicRotation.ToList(), false, part.Gamma, true);

					if (part.PbrBump.On && part.PbrBumpTexture.HasProcedural)
					{
						if (!part.PbrBumpTexture.IsNormalMap)
						{
							var bump = new BumpNode(m_shader, "bump");
							bump.ins.Strength.Value = Math.Abs(part.PbrBump.Amount) * RcCore.It.AllSettings.BumpStrengthFactor;
							bump.Invert = part.PbrBump.Amount < 0.0f;
							bump.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
							part.PbrBump.Amount = 1.0f;
							Utilities.GraphForSlot(m_shader, null, part.PbrBump.On, part.PbrBump.Amount, part.PbrBumpTexture, bump.ins.Height.ToList(), true, false, false, true, part.Gamma);
							bump.outs.Normal.Connect(principled.ins.Normal);
						}
						else
						{
							Utilities.GraphForSlot(m_shader, null, part.PbrBump.On, part.PbrBump.Amount, part.PbrBumpTexture, principled.ins.Normal.ToList(), false, true, false, true, part.Gamma);
						}
					}
					if (part.PbrClearcoatBump.On && part.PbrClearcoatBumpTexture.HasProcedural)
					{
						if (!part.PbrClearcoatBumpTexture.IsNormalMap)
						{
							var bump = new BumpNode(m_shader, "clearcoat_bump");
							bump.ins.Strength.Value = Math.Abs(part.PbrClearcoatBump.Amount) * RcCore.It.AllSettings.BumpStrengthFactor;
							bump.Invert = part.PbrClearcoatBump.Amount < 0.0f;
							part.PbrClearcoatBump.Amount = 1.0f;
							bump.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
							Utilities.GraphForSlot(m_shader, null, part.PbrClearcoatBump.On, part.PbrClearcoatBump.Amount, part.PbrClearcoatBumpTexture, bump.ins.Height.ToList(), true, false, false, true, part.Gamma);
							bump.outs.Normal.Connect(principled.ins.ClearcoatNormal);
						}
						else
						{
							Utilities.GraphForSlot(m_shader, null, part.PbrClearcoatBump.On, part.PbrClearcoatBump.Amount, part.PbrClearcoatBumpTexture, principled.ins.ClearcoatNormal.ToList(), false, true, false, true, part.Gamma);
						}
					}

					lightpath.outs.IsShadowRay.Connect(coloured_shadow_switch.ins.Value1);
					coloured_shadow_switch.outs.Value.Connect(coloured_shadow_mix_custom.ins.Fac);
					coloured_shadow.outs.BSDF.Connect(coloured_shadow_mix_custom.ins.Closure2);
					principled.outs.BSDF.Connect(coloured_shadow_mix_custom.ins.Closure1);

					float emission_strength = part.EmissionStrength;
					// When an emission texture is added and active make sure that the emission
					// base color isn't black.
					if (part.PbrEmission.On) {
						if (part.PbrEmission.Value.Equals(Rhino.Display.Color4f.Black))
						{
							part.PbrEmission.Value = Rhino.Display.Color4f.White;
						}
					}

					Utilities.PbrGraphForSlot(m_shader, part.PbrEmission, part.PbrEmissionTexture, principled.ins.Emission.ToList(), false, part.Gamma, true);
					principled.ins.EmissionStrength.Value = emission_strength;

					Utilities.PbrGraphForSlot(m_shader, part.PbrAlpha, part.PbrAlphaTexture, alpha_transp_component.ins.Value2.ToList(), false, part.Gamma, true);

					alpha_transp_component.outs.Value.Connect(alpha_basecolalpha_plus_alphatransp.ins.Value2);

					alpha_basecolalpha_plus_alphatransp.outs.Value.Connect(alpha_transparency_final.ins.Value2);
					alpha_transparency_final.outs.Value.Connect(principled.ins.Alpha);
					alpha_transparency_final.outs.Value.Connect(alpha_cutter_mixer.ins.Fac);

					tangent.outs.Tangent.Connect(principled.ins.Tangent);

					if (part.PbrDisplacement.On && part.PbrDisplacementTexture.HasProcedural)
					{
						var displacement = new DisplacementNode(m_shader);
						var strength = new MathMultiply(m_shader);
						var adjust = new MathSubtract(m_shader);
						displacement.ins.Midlevel.Value = 0.0f;
						adjust.ins.Value2.Value = 0.5f;
						strength.ins.Value1.Value = part.PbrDisplacement.Amount * 2.0f;
						part.PbrDisplacement.Amount = 1.0f;
						Utilities.PbrGraphForSlot(m_shader, part.PbrDisplacement, part.PbrDisplacementTexture, adjust.ins.Value1.ToList(), false, part.Gamma, true);
						adjust.outs.Value.Connect(strength.ins.Value2);
						strength.outs.Value.Connect(displacement.ins.Height);
						displacement.outs.Displacement.Connect(m_shader.Output.ins.Displacement);
					}

					coloured_shadow_mix_custom.outs.Closure.Connect(alpha_cutter_mixer.ins.Closure2);
					return alpha_cutter_mixer;
				}
				else
				{
					// NOTE: decalMixin is manually added outside of GH definition

					var invert_transparency68 = new MathSubtract(m_shader, "invert_transparency_");
					invert_transparency68.ins.Value1.Value = 1f;
					invert_transparency68.ins.Value2.Value = part.Transparency;
					invert_transparency68.Operation = MathNode.Operations.Subtract;
					invert_transparency68.UseClamp = false;

					var weight_diffuse_amount_by_transparency_inv69 = new MathMultiply(m_shader, "weight_diffuse_amount_by_transparency_inv_");
					weight_diffuse_amount_by_transparency_inv69.ins.Value1.Value = part.DiffuseTexture.Amount;
					weight_diffuse_amount_by_transparency_inv69.Operation = MathNode.Operations.Multiply;
					weight_diffuse_amount_by_transparency_inv69.UseClamp = false;

					var diff_tex_amount_multiplied_with_inv_transparency181 = new MathMultiply(m_shader, "diff_tex_amount_multiplied_with_inv_transparency_");
					diff_tex_amount_multiplied_with_inv_transparency181.Operation = MathNode.Operations.Multiply;
					diff_tex_amount_multiplied_with_inv_transparency181.UseClamp = false;

					var diff_tex_weighted_alpha_for_basecol_mix182 = new MathMultiply(m_shader, "diff_tex_weighted_alpha_for_basecol_mix_");
					diff_tex_weighted_alpha_for_basecol_mix182.Operation = MathNode.Operations.Multiply;
					diff_tex_weighted_alpha_for_basecol_mix182.UseClamp = false;

					var diffuse_base_color_through_alpha180 = new MixNode(m_shader, "diffuse_base_color_through_alpha_");
					diffuse_base_color_through_alpha180.ins.Color1.Value = part.BaseColor;
					diffuse_base_color_through_alpha180.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					diffuse_base_color_through_alpha180.UseClamp = false;

					var use_alpha_weighted_with_modded_amount71 = new MathMultiply(m_shader, "use_alpha_weighted_with_modded_amount_");
					use_alpha_weighted_with_modded_amount71.ins.Value1.Value = 0.0f;
					use_alpha_weighted_with_modded_amount71.Operation = MathNode.Operations.Multiply;
					use_alpha_weighted_with_modded_amount71.UseClamp = false;

					var bump_texture_to_bw87 = new RgbToBwNode(m_shader, "bump_texture_to_bw_");

					var bump_amount72 = new MathMultiply(m_shader, "bump_amount_");
					bump_amount72.ins.Value1.Value = 1.0f;
					bump_amount72.ins.Value2.Value = part.BumpTexture.Amount * RcCore.It.AllSettings.BumpStrengthFactor * 4.66f;
					bump_amount72.Operation = MathNode.Operations.Multiply;
					bump_amount72.UseClamp = false;

					var diffuse_base_color_through_alpha120 = new MixNode(m_shader, "diffuse_base_color_through_alpha_");
					diffuse_base_color_through_alpha120.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					diffuse_base_color_through_alpha120.UseClamp = false;

					var bump88 = new BumpNode(m_shader, "bump_");
					bump88.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
					bump88.ins.Strength.Value = 1.0f;  //RcCore.It.AllSettings.BumpStrengthFactor; // * 100.0f;
					bump88.ins.Distance.Value = 1.0f;  //RcCore.It.AllSettings.BumpDistance;
					bump88.ins.UseObjectSpace.Value = true;

					var light_path109 = new LightPathNode(m_shader, "light_path_");

					var final_diffuse89 = new DiffuseBsdfNode(m_shader, "final_diffuse_");
					final_diffuse89.ins.Roughness.Value = 0f;

					var shadeless_bsdf90 = new EmissionNode(m_shader, "shadeless_bsdf_");
					shadeless_bsdf90.ins.Strength.Value = 1f;

					var shadeless_on_cameraray122 = new MathMultiply(m_shader, "shadeless_on_cameraray_");
					shadeless_on_cameraray122.ins.Value2.Value = part.ShadelessAsFloat;
					shadeless_on_cameraray122.Operation = MathNode.Operations.Multiply;
					shadeless_on_cameraray122.UseClamp = false;

					var attenuated_reflection_color91 = new MixNode(m_shader, "attenuated_reflection_color_");
					attenuated_reflection_color91.ins.Color1.Value = new float4(0f, 0f, 0f, 1f);
					attenuated_reflection_color91.ins.Color2.Value = part.ReflectionColorGamma;
					attenuated_reflection_color91.ins.Fac.Value = part.Reflectivity;
					attenuated_reflection_color91.BlendType = MixNode.BlendTypes.Blend;
					attenuated_reflection_color91.UseClamp = false;

					var fresnel_based_on_constant92 = new FresnelNode(m_shader, "fresnel_based_on_constant_");
					fresnel_based_on_constant92.ins.IOR.Value = part.FresnelIOR;

					var simple_reflection93 = new CombineRgbNode(m_shader, "simple_reflection_");
					simple_reflection93.ins.R.Value = part.Reflectivity;
					simple_reflection93.ins.G.Value = 0f;
					simple_reflection93.ins.B.Value = 0f;

					var fresnel_reflection94 = new CombineRgbNode(m_shader, "fresnel_reflection_");
					fresnel_reflection94.ins.G.Value = 0f;
					fresnel_reflection94.ins.B.Value = 0f;

					var fresnel_reflection_if_reflection_used73 = new MathMultiply(m_shader, "fresnel_reflection_if_reflection_used_");
					fresnel_reflection_if_reflection_used73.ins.Value1.Value = part.Reflectivity;
					fresnel_reflection_if_reflection_used73.ins.Value2.Value = part.FresnelReflectionsAsFloat;
					fresnel_reflection_if_reflection_used73.Operation = MathNode.Operations.Multiply;
					fresnel_reflection_if_reflection_used73.UseClamp = false;

					var select_reflection_or_fresnel_reflection95 = new MixNode(m_shader, "select_reflection_or_fresnel_reflection_");
					select_reflection_or_fresnel_reflection95.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					select_reflection_or_fresnel_reflection95.UseClamp = false;

					var shadeless96 = new MixClosureNode(m_shader, "shadeless_");

					var glossy97 = new GlossyBsdfNode(m_shader, "glossy_");
					glossy97.ins.Roughness.Value = part.ReflectionRoughness;

					var reflection_factor98 = new SeparateRgbNode(m_shader, "reflection_factor_");

					var attennuated_refraction_color99 = new MixNode(m_shader, "attennuated_refraction_color_");
					attennuated_refraction_color99.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
					attennuated_refraction_color99.ins.Color2.Value = part.TransparencyColorGamma;
					attennuated_refraction_color99.ins.Fac.Value = part.Transparency;
					attennuated_refraction_color99.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					attennuated_refraction_color99.UseClamp = false;

					var refraction100 = new RefractionBsdfNode(m_shader, "refraction_");
					refraction100.ins.Roughness.Value = part.RefractionRoughnessPow2;
					refraction100.ins.IOR.Value = part.IOR;
					refraction100.Distribution = RefractionBsdfNode.RefractionDistribution.GGX;

					var diffuse_plus_glossy101 = new MixClosureNode(m_shader, "diffuse_plus_glossy_");

					var blend_in_transparency102 = new MixClosureNode(m_shader, "blend_in_transparency_");
					blend_in_transparency102.ins.Fac.Value = part.Transparency;

					var attenuated_environment_color106 = new MixNode(m_shader, "attenuated_environment_color_");
					attenuated_environment_color106.ins.Color1.Value = new ccl.float4(0f, 0f, 0f, 1f);
					attenuated_environment_color106.ins.Fac.Value = part.EnvironmentTexture.Amount;
					attenuated_environment_color106.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					attenuated_environment_color106.UseClamp = false;

					var diffuse_glossy_and_refraction107 = new MixClosureNode(m_shader, "diffuse_glossy_and_refraction_");
					diffuse_glossy_and_refraction107.ins.Fac.Value = part.Transparency;

					var invert_roughness75 = new MathSubtract(m_shader, "invert_roughness_");
					invert_roughness75.ins.Value1.Value = 1f;
					invert_roughness75.ins.Value2.Value = part.RefractionRoughness;
					invert_roughness75.Operation = MathNode.Operations.Subtract;
					invert_roughness75.UseClamp = false;

					var multiply_transparency76 = new MathMultiply(m_shader, "multiply_transparency_");
					multiply_transparency76.ins.Value2.Value = part.Transparency;
					multiply_transparency76.Operation = MathNode.Operations.Multiply;
					multiply_transparency76.UseClamp = false;

					var multiply_with_shadowray77 = new MathMultiply(m_shader, "multiply_with_shadowray_");
					multiply_with_shadowray77.Operation = MathNode.Operations.Multiply;
					multiply_with_shadowray77.UseClamp = false;

					var coloured_shadow_trans_color111 = new TransparentBsdfNode(m_shader, "coloured_shadow_trans_color_");

					var weight_for_shadowray_coloured_shadow78 = new MathMultiply(m_shader, "weight_for_shadowray_coloured_shadow_");
					weight_for_shadowray_coloured_shadow78.ins.Value2.Value = 1f;
					weight_for_shadowray_coloured_shadow78.Operation = MathNode.Operations.Multiply;
					weight_for_shadowray_coloured_shadow78.UseClamp = false;

					var diffuse_from_emission_color123 = new DiffuseBsdfNode(m_shader, "diffuse_from_emission_color_");
					diffuse_from_emission_color123.ins.Color.Value = part.EmissionColorGamma;
					diffuse_from_emission_color123.ins.Roughness.Value = 0f;
					diffuse_from_emission_color123.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

					var shadeless_emission125 = new EmissionNode(m_shader, "shadeless_emission_");
					shadeless_emission125.ins.Color.Value = part.EmissionColorGamma;
					shadeless_emission125.ins.Strength.Value = 1f;

					var coloured_shadow_mix_custom114 = new MixClosureNode(m_shader, "coloured_shadow_mix_custom_");

					var diffuse_or_shadeless_emission126 = new MixClosureNode(m_shader, "diffuse_or_shadeless_emission_");

					var one_if_usealphatransp_turned_off178 = new MathLess_Than(m_shader, "one_if_usealphatransp_turned_off_");
					one_if_usealphatransp_turned_off178.ins.Value1.Value = 0.0f;
					one_if_usealphatransp_turned_off178.ins.Value2.Value = 1f;
					one_if_usealphatransp_turned_off178.Operation = MathNode.Operations.Less_Than;
					one_if_usealphatransp_turned_off178.UseClamp = false;

					var max_of_texalpha_or_usealpha179 = new MathMaximum(m_shader, "max_of_texalpha_or_usealpha_");
					max_of_texalpha_or_usealpha179.Operation = MathNode.Operations.Maximum;
					max_of_texalpha_or_usealpha179.UseClamp = false;

					var invert_alpha70 = new MathSubtract(m_shader, "invert_alpha_");
					invert_alpha70.ins.Value1.Value = 1f;
					invert_alpha70.Operation = MathNode.Operations.Subtract;
					invert_alpha70.UseClamp = false;

					var transpluminance113 = new RgbToLuminanceNode(m_shader, "transpluminance_");

					var invert_luminence79 = new MathSubtract(m_shader, "invert_luminence_");
					invert_luminence79.ins.Value1.Value = 1f;
					invert_luminence79.Operation = MathNode.Operations.Subtract;
					invert_luminence79.UseClamp = false;

					var transparency_texture_amount80 = new MathMultiply(m_shader, "transparency_texture_amount_");
					transparency_texture_amount80.ins.Value2.Value = part.TransparencyTexture.Amount;
					transparency_texture_amount80.Operation = MathNode.Operations.Multiply;
					transparency_texture_amount80.UseClamp = false;

					var toggle_diffuse_texture_alpha_usage81 = new MathMultiply(m_shader, "toggle_diffuse_texture_alpha_usage_");
					toggle_diffuse_texture_alpha_usage81.ins.Value2.Value = 0.0f;
					toggle_diffuse_texture_alpha_usage81.Operation = MathNode.Operations.Multiply;
					toggle_diffuse_texture_alpha_usage81.UseClamp = false;

					var toggle_transparency_texture82 = new MathMultiply(m_shader, "toggle_transparency_texture_");
					toggle_transparency_texture82.ins.Value1.Value = part.HasTransparencyTextureAsFloat;
					toggle_transparency_texture82.Operation = MathNode.Operations.Multiply;
					toggle_transparency_texture82.UseClamp = false;

					var add_emission_to_final124 = new AddClosureNode(m_shader, "add_emission_to_final_");

					var transparent115 = new TransparentBsdfNode(m_shader, "transparent_");
					transparent115.ins.Color.Value = new ccl.float4(1f, 1f, 1f, 1f);

					var add_diffuse_texture_alpha83 = new MathAdd(m_shader, "add_diffuse_texture_alpha_");
					add_diffuse_texture_alpha83.Operation = MathNode.Operations.Add;
					add_diffuse_texture_alpha83.UseClamp = false;

					var custom_alpha_cutter116 = new MixClosureNode(m_shader, "custom_alpha_cutter_");

					var mix_diffuse_and_transparency_color187 = new MixNode(m_shader, "mix_diffuse_and_transparency_color_");
					mix_diffuse_and_transparency_color187.ins.Fac.Value = part.Transparency;
					mix_diffuse_and_transparency_color187.BlendType = MixNode.BlendTypes.Blend;
					mix_diffuse_and_transparency_color187.UseClamp = false;

					var principledbsdf117 = new PrincipledBsdfNode(m_shader, "principledbsdf_");
					principledbsdf117.ins.Subsurface.Value = 0f;
					principledbsdf117.ins.SubsurfaceRadius.Value = new float4(0f, 0f, 0f, 1f);
					principledbsdf117.ins.SubsurfaceColor.Value = new float4(0.5019608f, 0.5019608f, 0.5019608f, 1f);
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
					principledbsdf117.ins.EmissionStrength.Value = 0.0f;
					principledbsdf117.ins.Transmission.Value = part.Transparency;
					principledbsdf117.ins.TransmissionRoughness.Value = part.RefractionRoughness;
					principledbsdf117.ins.Tangent.Value = new float4(0f, 0f, 0f, 1f);

					var custom_environment_blend = new MixNode(m_shader, "environment map color mixin");
					custom_environment_blend.BlendType = MixNode.BlendTypes.Blend;
					custom_environment_blend.ins.Fac.Value = part.EnvironmentTexture.Amount;

					var coloured_shadow_trans_color_for_principled188 = new TransparentBsdfNode(m_shader, "coloured_shadow_trans_color_for_principled_");

					var coloured_shadow_mix_glass_principled118 = new MixClosureNode(m_shader, "coloured_shadow_mix_glass_principled_");

					invert_transparency68.outs.Value.Connect(weight_diffuse_amount_by_transparency_inv69.ins.Value2);
					weight_diffuse_amount_by_transparency_inv69.outs.Value.Connect(diff_tex_amount_multiplied_with_inv_transparency181.ins.Value1);
					invert_transparency68.outs.Value.Connect(diff_tex_amount_multiplied_with_inv_transparency181.ins.Value2);
					diff_tex_amount_multiplied_with_inv_transparency181.outs.Value.Connect(diff_tex_weighted_alpha_for_basecol_mix182.ins.Value1);
					diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2.Value = 1.0f;
					diff_tex_weighted_alpha_for_basecol_mix182.outs.Value.Connect(diffuse_base_color_through_alpha180.ins.Fac);
					weight_diffuse_amount_by_transparency_inv69.outs.Value.Connect(use_alpha_weighted_with_modded_amount71.ins.Value2);
					diffuse_base_color_through_alpha180.outs.Color.Connect(diffuse_base_color_through_alpha120.ins.Color1);
					use_alpha_weighted_with_modded_amount71.outs.Value.Connect(diffuse_base_color_through_alpha120.ins.Fac);
					bump_amount72.outs.Value.Connect(bump88.ins.Height);

					if (decalMixin != null)
					{
						diffuse_base_color_through_alpha120.outs.Color.Connect(decalMixin.ins.Color1);
						decalMixin.outs.Color.Connect(final_diffuse89.ins.Color);
						decalMixin.outs.Color.Connect(shadeless_bsdf90.ins.Color);
					}
					else
					{
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
					//texcoord84.outs.EnvEmap.Connect(separate_envmap_texco103.ins.Vector);
					//recombine_envmap_texco104.outs.Vector.Connect(environment_texture105.ins.Vector);
					//environment_texture105.outs.Color.Connect(attenuated_environment_color106.ins.Color2);
					diffuse_plus_glossy101.outs.Closure.Connect(diffuse_glossy_and_refraction107.ins.Closure1);
					blend_in_transparency102.outs.Closure.Connect(diffuse_glossy_and_refraction107.ins.Closure2);
					attenuated_environment_color106.outs.Color.Connect(custom_environment_blend.ins.Color2);
					custom_environment_blend.outs.Color.Connect(principledbsdf117.ins.BaseColor);
					invert_roughness75.outs.Value.Connect(multiply_transparency76.ins.Value1);
					multiply_transparency76.outs.Value.Connect(multiply_with_shadowray77.ins.Value1);
					light_path109.outs.IsShadowRay.Connect(multiply_with_shadowray77.ins.Value2);
					diffuse_base_color_through_alpha120.outs.Color.Connect(coloured_shadow_trans_color111.ins.Color);
					multiply_with_shadowray77.outs.Value.Connect(weight_for_shadowray_coloured_shadow78.ins.Value1);
					coloured_shadow_mix_glass_principled118.outs.Closure.Connect(coloured_shadow_mix_custom114.ins.Closure1);
					weight_for_shadowray_coloured_shadow78.outs.Value.Connect(coloured_shadow_mix_custom114.ins.Fac);
					diffuse_from_emission_color123.outs.BSDF.Connect(diffuse_or_shadeless_emission126.ins.Closure1);
					shadeless_emission125.outs.Emission.Connect(diffuse_or_shadeless_emission126.ins.Closure2);
					shadeless_on_cameraray122.outs.Value.Connect(diffuse_or_shadeless_emission126.ins.Fac);
					max_of_texalpha_or_usealpha179.ins.Value1.Value = 1.0f;
					one_if_usealphatransp_turned_off178.outs.Value.Connect(max_of_texalpha_or_usealpha179.ins.Value2);
					max_of_texalpha_or_usealpha179.outs.Value.Connect(invert_alpha70.ins.Value2);
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
					mix_diffuse_and_transparency_color187.outs.Color.Connect(custom_environment_blend.ins.Color1);
					principledbsdf117.outs.BSDF.Connect(coloured_shadow_mix_glass_principled118.ins.Closure1);
					mix_diffuse_and_transparency_color187.outs.Color.Connect(coloured_shadow_trans_color_for_principled188.ins.Color);
					coloured_shadow_trans_color_for_principled188.outs.BSDF.Connect(coloured_shadow_mix_glass_principled118.ins.Closure2);
					weight_for_shadowray_coloured_shadow78.outs.Value.Connect(coloured_shadow_mix_glass_principled118.ins.Fac);

					/* extra code */

					if (part.TransparencyTexture.HasProcedural)
					{
						List<ISocket> sockets = new List<ISocket>
						{
							transpluminance113.ins.Color
						};
						Utilities.GraphForSlot(m_shader, null, true, part.TransparencyTexture.Amount, part.TransparencyTexture, sockets, false, false, false, true, part.Gamma);
					}

					if (part.DiffuseTexture.HasProcedural)
					{
						float useAlpha = 0.0f;
				//Rhino.RhinoApp.OutputDebugString($"{m_codeshader.Code}\n");
						if(part.DiffuseTexture.Procedural is BitmapTextureProcedural bmtp)
						{
							useAlpha = bmtp.UseAlpha ? 1.0f : 0.0f;
						}
						toggle_diffuse_texture_alpha_usage81.ins.Value2.Value = useAlpha;
						use_alpha_weighted_with_modded_amount71.ins.Value1.Value = useAlpha;
						one_if_usealphatransp_turned_off178.ins.Value1.Value = useAlpha;

						List<ISocket> sockets = new List<ISocket>
						{
							diffuse_base_color_through_alpha120.ins.Color2,
							diffuse_base_color_through_alpha180.ins.Color2
						};
						var alpha = Utilities.GraphForSlot(m_shader, null, true, part.DiffuseTexture.Amount, part.DiffuseTexture, sockets, false, false, false, false, part.Gamma);
						if (alpha != null)
						{
							alpha.Connect(diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2);
							alpha.Connect(max_of_texalpha_or_usealpha179.ins.Value1);
						}
						else
						{
							diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2.Value = 1.0f;
							max_of_texalpha_or_usealpha179.ins.Value1.Value = 1.0f;
						}
					}

					if (part.BumpTexture.HasProcedural)
					{
						Utilities.GraphForSlot(m_shader, null, true, part.BumpTexture.Amount, part.BumpTexture, bump_amount72.ins.Value1.ToList(), true, false, false, true, part.Gamma);
						bump88.outs.Normal.Connect(principledbsdf117.ins.Normal);
					}

					if (part.EnvironmentTexture.HasProcedural)
					{
						part.EnvironmentTexture.ProjectionMode = Rhino.Render.TextureProjectionMode.EnvironmentMap;
						Utilities.GraphForSlot(m_shader, null, true, part.EnvironmentTexture.Amount, part.EnvironmentTexture, attenuated_environment_color106.ins.Color2.ToList(), false, false, false, false, part.Gamma);
					}

					if (part.CyclesMaterialType == ShaderBody.CyclesMaterial.Glass
						|| part.CyclesMaterialType == ShaderBody.CyclesMaterial.SimplePlastic
						|| part.CyclesMaterialType == ShaderBody.CyclesMaterial.SimpleMetal) return coloured_shadow_mix_glass_principled118;
					return custom_alpha_cutter116;
				}
			}
		}
	}
}
