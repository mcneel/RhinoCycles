/**
Copyright 2014-2024 Robert McNeel and Associates

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
using Rhino;
using Rhino.Render;
using Rhino.Runtime;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoFullNxt : RhinoShader
	{
		// Tuned factor to align Cycles bump strength with the viewport display.
		private const float DisplayBumpMatchFactor = 0.2f;

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

				var backfacing = new GeometryInfoNode(m_shader, "backfacepicker_");
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

				if (m_original.ShadowCatcher)
				{
					var lightpath = new LightPathNode(m_shader, "light_path_for_shadow_catcher");
					var pathadder = new MathAdd(m_shader, "path_adder_for_shadow_catcher");
					var noshow = new TransparentBsdfNode(m_shader, "shadow_catcher_transp_bsdf");
					var refl_flipper = new MixClosureNode(m_shader, "shadow_catcher_reflection_flipper");
					pathadder.UseClamp = true;
					lightpath.outs.IsReflectionRay.Connect(pathadder.ins.Value1);
					lightpath.outs.IsDiffuseRay.Connect(pathadder.ins.Value2);
					pathadder.outs.Value.Connect(refl_flipper.ins.Fac);
					lastclosure.Connect(refl_flipper.ins.Closure1);
					noshow.outs.BSDF.Connect(refl_flipper.ins.Closure2);
					lastclosure = refl_flipper.outs.Closure;
				}

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
				AttributeNode attr = new AttributeNode(m_shader, "debug_attr");
				attr.Attribute = "uvmap1";
				//RhinoTextureCoordinateNode texco = new RhinoTextureCoordinateNode(m_shader, "debug_texco");
				//texco.UvMap = "uvmap1";
				//attr.outs.Vector.Connect(texco.ins.);
				ccl.ShaderNodes.DiffuseBsdfNode diff = new DiffuseBsdfNode(m_shader, "debug_diff_");
				diff.ins.Color.Value = new float4(0.8f, 0.6f, 0.5f, 1.0f);
				//texco.outs.UV.Connect(diff.ins.Color);
				attr.outs.Vector.Connect(diff.ins.Color);
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

		static private void SetupOneDecalNodes(Shader shader, CyclesDecal decal, RhinoTextureCoordinateNode texco, ImageTextureNode imgtex, MathMultiply transp, TextureAdjustmentTextureProceduralNode adjust)
		{
			texco.ObjectTransform = decal.Transform;
			texco.UseTransform = true;

			RenderEngine.SetTextureImage(imgtex, decal.Texture);
			imgtex.AlternateTiles = decal.Texture.AlternateTiles;
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

			switch (decal.Projection)
			{
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

			if (transp != null)
			{
				transp.ins.Value2.Value = 1.0f - decal.Transparency;

				// if color mask is set we add here a branch of nodes to adjust the
				// imgtex alpha output with the color mask.
				if (decal.Texture.UseColorMask)
				{
					MathMultiply adjust_img_alpha = Utilities.ApplyColorMaskGraph(imgtex, decal.Texture);
					adjust_img_alpha.outs.Value.Connect(transp.ins.Value1);
				}
				else
				{
					imgtex.outs.Alpha.Connect(transp.ins.Value1);
				}
			}

			switch (decal.Mapping)
			{
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

		static public VectorSocket GetDecalUVNode(CyclesDecal decal, RhinoTextureCoordinateNode texco)
		{
			texco.ObjectTransform = decal.Transform;
			texco.UseTransform = true;

			texco.UvMap = decal.Texture.GetUvMapForChannel();

			switch (decal.Projection)
			{
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

			texco.DecalHeight = decal.Height;
			texco.DecalRadius = decal.Radius;
			texco.HorizontalSweepStart = decal.HorizontalSweepStart;
			texco.HorizontalSweepEnd = decal.HorizontalSweepEnd;
			texco.VerticalSweepStart = decal.VerticalSweepStart;
			texco.VerticalSweepEnd = decal.VerticalSweepEnd;

			VectorSocket output_socket = null;

			switch (decal.Mapping)
			{
				case Rhino.Render.DecalMapping.Planar:
					output_socket = texco.outs.DecalPlanar;
					break;
				case Rhino.Render.DecalMapping.Cylindrical:
					output_socket = texco.outs.DecalCylindrical;
					break;
				case Rhino.Render.DecalMapping.Spherical:
					output_socket = texco.outs.DecalSpherical;
					break;
				case Rhino.Render.DecalMapping.UV:
				default:
					output_socket = texco.outs.DecalUv;
					break;
			}

			return output_socket;
		}

		// Creates a mask for the decal based on the UV coordinates.
		// The mask is 0.0f where there is no decal, otherwise it takes on the value of the decal alpha.
		// The decal alpha is a combination of the decal transparency and the alpha of the decal material.
		// This mask makes it easy to properly mix decals together.
		static private FloatSocket GetDecalMaskNode(Shader shader, CyclesDecal decal, RhinoTextureCoordinateNode texco, ISocket color_mask_transp_socket)
		{
			var decalUvwSocket = GetDecalUVNode(decal, texco);

			// Subtract 0.5 from the UV coordinates to center them around (0.0, 0.0).
			var subtract = new VectorMathNode(shader, "Decal mask subtract");
			subtract.Operation = VectorMathNode.Operations.Subtract;
			decalUvwSocket.Connect(subtract.ins.Vector1);
			subtract.ins.Vector2.Value = new float4(0.5f, 0.5f, 0.0f, 0.0f);

			// Separate the UV coordinates into X, Y, and Z components.
			var separate_uv = new SeparateXyzNode(shader, "Decal mask separate");
			subtract.outs.Vector.Connect(separate_uv.ins.Vector);

			// Take the absolute value of the X component.
			var abs1 = new MathNode(shader, "Decal mask absolute 1");
			abs1.Operation = MathNode.Operations.Absolute;
			separate_uv.outs.X.Connect(abs1.ins.Value1);

			// Take the absolute value of the Y component.
			var abs2 = new MathNode(shader, "Decal mask absolute 2");
			abs2.Operation = MathNode.Operations.Absolute;
			separate_uv.outs.Y.Connect(abs2.ins.Value1);

			// Find the maximum of the absolute X and Y components.
			var max = new MathNode(shader, "Decal mask max");
			max.Operation = MathNode.Operations.Maximum;
			abs1.outs.Value.Connect(max.ins.Value1);
			abs2.outs.Value.Connect(max.ins.Value2);

			// Check if the maximum is less than 0.5.
			var lessthan = new MathNode(shader, "Decal mask less than");
			lessthan.Operation = MathNode.Operations.Less_Than;
			max.outs.Value.Connect(lessthan.ins.Value1);
			lessthan.ins.Value2.Value = 0.5f;

			// Multiply the alpha of the decal with the alpha of the decal material
			var multiply = new MathMultiply(shader, "Decal mask multiply");
			multiply.ins.Value1.Value = 1.0f - decal.Transparency;
			color_mask_transp_socket.Connect(multiply.ins.Value2);

			// Transform (0.0f...1.0f) mask into a (0.0f...DecalAlpha) mask.
			var min = new MathNode(shader, "Decal mask min");
			min.Operation = MathNode.Operations.Minimum;
			lessthan.outs.Value.Connect(min.ins.Value1);
			multiply.outs.Value.Connect(min.ins.Value2);

			// Check if the Z component (w-coordinate) is greater than -0.5 (if not, decal is on the wrong side).
			var greaterthan = new MathNode(shader, "Decal mask greater than");
			greaterthan.Operation = MathNode.Operations.Greater_Than;
			separate_uv.outs.Z.Connect(greaterthan.ins.Value1);
			greaterthan.ins.Value2.Value = -0.5f;

			// Multiply the result of the above with the mask to zero out the mask where the decal is on the wrong side.
			var multiply2 = new MathMultiply(shader, "Decal mask multiply");
			greaterthan.outs.Value.Connect(multiply2.ins.Value1);
			min.outs.Value.Connect(multiply2.ins.Value2);

			return multiply2.outs.Value;
		}

		private (ClosureSocket, FloatSocket) HandleMaterialDecal(CyclesDecal decal, bool gamma_correct_decals)
		{
			var decalProcessingInfo = new DecalProcessingInfo { Decal = decal };
			ShaderNode shader = GetShaderPart(decal.MaterialShader, decalProcessingInfo);
			FloatSocket maskSocket = GetDecalMaskNode(m_shader, decal, new RhinoTextureCoordinateNode(m_shader, "decal_texco_"), decalProcessingInfo.AlphaOut);
			return (shader.GetClosureSocket(), maskSocket);
		}

		/// <summary>
		/// Handle texture decals for this shader. Set up a partial shader graph
		/// and return the ShaderNodes that can be bound into the basecolor
		/// of the actual shader.
		/// </summary>
		/// <returns>ShaderNode, the final node in the shader graph branch. This will be a MixNode.
		/// The base color (color or texture) will have to be connected to the Color1 input.</returns>
		/// <since>7.0</since>
		private MixNode HandleTextureDecals(bool gamma_correct_decals = false)
		{
			//ccl.CodeShader m_codeshader = new ccl.CodeShader(ccl.Shader.ShaderType.Material);
			MixNode nodeToBindIntoShader = null;

			var textureDecals = new List<CyclesDecal>();

			if (m_original.Decals != null)
			{
				foreach (CyclesDecal decal in m_original.Decals)
				{
					// A decal is a texture decal when it has no material shader.
					if (decal.MaterialShader == null)
					{
						textureDecals.Add(decal);
					}
				}
			}

			int count = textureDecals.Count;

			if (count > 0)
			{
				List<RhinoTextureCoordinateNode> texcos = new List<RhinoTextureCoordinateNode>(count);
				List<ImageTextureNode> imgtexs = new List<ImageTextureNode>(count);
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
					if (i < count - 1)
					{
						alphamaths.Add(new MathAdd(m_shader, $"Decal_alpha_adder_{idx}_"));
					}

					idx++;
				}

				MixNode lastMixer = mixrgbs.Last();
				GammaNode decalGammaNode = null;
				if (gamma_correct_decals)
				{
					decalGammaNode = new GammaNode(m_shader, "gamma node for decal");
					decalGammaNode.ins.Gamma.Value = m_original.Gamma;
					decalGammaNode.outs.Color.Connect(lastMixer.ins.Color2);
				}
				ISocket sock_to_connect_to = gamma_correct_decals ? decalGammaNode.ins.Color : lastMixer.ins.Color2;

				if (count == 1)
				{
					var texco = texcos[0];
					var imgtex = imgtexs[0];
					var trans = transparencies[0];
					var adjust = adjustments[0];
					SetupOneDecalNodes(m_shader, textureDecals.First(), texco, imgtex, trans, adjust);
					if (textureDecals[0].Texture.AdjustNeeded)
					{
						imgtex.outs.Color.Connect(adjust.ins.Color);
						adjust.outs.Color.Connect(sock_to_connect_to);
					}
					else
					{
						imgtex.outs.Color.Connect(sock_to_connect_to);
					}
					trans.outs.Value.Connect(lastMixer.ins.Fac);
				}
				else
				{
					idx = 0;

					// Set up decal images and texture coordinates.
					foreach (var decal in textureDecals)
					{
						var texco = texcos[idx];
						var imgtex = imgtexs[idx];
						var trans = transparencies[idx];
						var adjust = adjustments[idx];
						SetupOneDecalNodes(m_shader, decal, texco, imgtex, trans, adjust);
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
					foreach (MathAdd alphaMath in alphamaths)
					{
						alphaMath.UseClamp = true;
						if (idx == 0)
						{
							CyclesTextureImage teximA = textureDecals[idx].Texture;
							MixNode mixer = mixrgbs[idx];
							mixer.BlendType = MixNode.BlendTypes.Blend;
							imgA = imgtexs[idx];
							adjustA = adjustments[idx];
							transA = transparencies[idx];

							CyclesTextureImage teximB = textureDecals[idx + 1].Texture;
							ImageTextureNode imgB = imgtexs[idx + 1];
							MathMultiply transB = transparencies[idx + 1];
							TextureAdjustmentTextureProceduralNode adjustB = adjustments[idx];

							if (teximA.AdjustNeeded)
							{
								imgA.outs.Color.Connect(adjustA.ins.Color);
								adjustA.outs.Color.Connect(mixer.ins.Color1);

							}
							else
							{
								imgA.outs.Color.Connect(mixer.ins.Color1);
							}
							if (teximB.AdjustNeeded)
							{
								imgB.outs.Color.Connect(adjustB.ins.Color);
								adjustB.outs.Color.Connect(mixer.ins.Color2);
							}
							else
							{
								imgB.outs.Color.Connect(mixer.ins.Color2);
							}

							transA.outs.Value.Connect(alphaMath.ins.Value1);
							transB.outs.Value.Connect(alphaMath.ins.Value2);

							transB.outs.Value.Connect(mixer.ins.Fac);

							previousAlphaMath = alphaMath;
							previousMixRgb = mixer;
						}
						else
						{
							MixNode mixer = mixrgbs[idx];
							CyclesTextureImage teximA = textureDecals[idx + 1].Texture;
							imgA = imgtexs[idx + 1];
							transA = transparencies[idx + 1];
							adjustA = adjustments[idx + 1];

							previousMixRgb.outs.Color.Connect(mixer.ins.Color1);
							if (teximA.AdjustNeeded)
							{
								imgA.outs.Color.Connect(adjustA.ins.Color);
								adjustA.outs.Color.Connect(mixer.ins.Color2);
							}
							else
							{
								imgA.outs.Color.Connect(mixer.ins.Color2);
							}

							previousAlphaMath.outs.Value.Connect(alphaMath.ins.Value1);
							transA.outs.Value.Connect(alphaMath.ins.Value2);
							transA.outs.Value.Connect(mixer.ins.Fac);

							previousAlphaMath = alphaMath;
							previousMixRgb = mixer;
						}

						idx++;
						if (idx == alphamaths.Count)
						{
							previousMixRgb.outs.Color.Connect(sock_to_connect_to);
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

		/// <summary>
		/// Handle material decals for this shader. Set up a partial shader graph
		/// and return a tuple of decal material closures and decal mask sockets.
		/// These can be used to properly mix decals together in the shader graph.
		/// </summary>
		/// <returns>A tuple of decal material closures and decal mask sockets.
		/// These can be mixed together with MixClosureNodes</returns>
		/// <since>7.0</since>
		private (List<ClosureSocket> decalMaterials, List<FloatSocket> decalMaskSockets) HandleMaterialDecals(float shaderGamma, bool gamma_correct_decals = false)
		{

			var decalClosures = new List<ClosureSocket>();
			var decalMaskSockets = new List<FloatSocket>();

			var materialDecals = new List<CyclesDecal>();

			if (m_original.Decals != null)
			{
				foreach (CyclesDecal decal in m_original.Decals)
				{
					if (decal.MaterialShader != null)
					{
						// If on Mac and rendering on CPU then we disable full material decal support for now.
						// This is because I get a crash due to stack buffer overflow in the rendering code.
						bool unsupportedCase = HostUtils.RunningOnOSX && (IsCpuRender ?? true);
						if (!unsupportedCase)
						{
							materialDecals.Add(decal);
						}
					}
				}
			}

			foreach (var decal in materialDecals)
			{
				var (materialDecalClosure, decalMaskSocket) = HandleMaterialDecal(decal, gamma_correct_decals);

				decalClosures.Add(materialDecalClosure);
				decalMaskSockets.Add(decalMaskSocket);
			}

			return (decalClosures, decalMaskSockets);
		}

		public class DecalProcessingInfo
		{
			public CyclesDecal Decal;
			public ISocket AlphaOut;
		}

		private ShaderNode GemMaterial(ShaderBody part, DecalProcessingInfo decalProcessingInfo = null)
		{
			var baseIor = new MathMaximum(m_shader, "gem_base_ior");
			baseIor.ins.Value2.Value = 1.001f;

			var dispersionScale = new ValueNode(m_shader, "gem_dispersion_scale");
			dispersionScale.Value = 0.045f;

			var roughnessComplement = new MathSubtract(m_shader, "gem_roughness_complement");
			roughnessComplement.ins.Value1.Value = 1.0f;

			var roughnessClamp = new MathMaximum(m_shader, "gem_roughness_complement_clamp");
			var dispersionAmount = new MathMultiply(m_shader, "gem_dispersion_amount");
			var subIorAndDispersion = new MathSubtract(m_shader, "gem_sub_ior_and_dispersion");
			var addIorAndDispersion = new MathAdd(m_shader, "gem_add_ior_and_dispersion");
			var redIorFloor = new MathMaximum(m_shader, "gem_red_ior_floor");
			redIorFloor.ins.Value2.Value = 1.001f;

			var glassRed = new GlassBsdfNode(m_shader, "gem_red_channel");
			glassRed.ins.Color.Value = new float4(1.0f, 0.0f, 0.0f, 1.0f);

			var glassGreen = new GlassBsdfNode(m_shader, "gem_green_channel");
			glassGreen.ins.Color.Value = new float4(0.0f, 1.0f, 0.0f, 1.0f);

			var glassBlue = new GlassBsdfNode(m_shader, "gem_blue_channel");
			glassBlue.ins.Color.Value = new float4(0.0f, 0.0f, 1.0f, 1.0f);

			var glassCore = new GlassBsdfNode(m_shader, "gem_core");
			glassCore.ins.Color.Value = new float4(1.0f, 1.0f, 1.0f, 1.0f);

			var addRG = new AddClosureNode(m_shader, "gem_add_rg_channels");
			var addRGB = new AddClosureNode(m_shader, "gem_add_rgb_channels");

			var lightPath = new LightPathNode(m_shader, "gem_light_path");
			var cameraRayMix = new MixClosureNode(m_shader, "gem_camera_ray_mix");
			var finalMix = new MixClosureNode(m_shader, "gem_final_mix");
			finalMix.ins.Fac.Value = 0.75f;

			Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture,
				glassCore.ins.Color.ToList(),
				false, part.Gamma, false, false, decalProcessingInfo);

			Utilities.PbrGraphForSlot(m_shader, part.PbrTransmissionRoughness, part.PbrTransmissionRoughnessTexture,
				new List<ISocket> { roughnessComplement.ins.Value2, glassRed.ins.Roughness, glassGreen.ins.Roughness, glassBlue.ins.Roughness, glassCore.ins.Roughness },
				false, part.Gamma, true, false, decalProcessingInfo);

			Utilities.PbrGraphForSlot(m_shader, part.PbrIor, part.PbrIorTexture,
				baseIor.ins.Value1.ToList(),
				false, part.Gamma, true, false, decalProcessingInfo);

			roughnessComplement.outs.Value.Connect(roughnessClamp.ins.Value1);
			roughnessClamp.ins.Value2.Value = 0.0f;

			dispersionScale.outs.Value.Connect(dispersionAmount.ins.Value1);
			roughnessClamp.outs.Value.Connect(dispersionAmount.ins.Value2);

			baseIor.outs.Value.Connect(subIorAndDispersion.ins.Value1);
			baseIor.outs.Value.Connect(glassGreen.ins.IOR);
			baseIor.outs.Value.Connect(addIorAndDispersion.ins.Value1);
			baseIor.outs.Value.Connect(glassCore.ins.IOR);

			dispersionAmount.outs.Value.Connect(subIorAndDispersion.ins.Value2);
			dispersionAmount.outs.Value.Connect(addIorAndDispersion.ins.Value2);

			subIorAndDispersion.outs.Value.Connect(redIorFloor.ins.Value1);
			redIorFloor.outs.Value.Connect(glassRed.ins.IOR);
			addIorAndDispersion.outs.Value.Connect(glassBlue.ins.IOR);

			glassRed.outs.BSDF.Connect(addRG.ins.Closure1);
			glassGreen.outs.BSDF.Connect(addRG.ins.Closure2);
			addRG.outs.Closure.Connect(addRGB.ins.Closure1);
			glassBlue.outs.BSDF.Connect(addRGB.ins.Closure2);

			lightPath.outs.IsCameraRay.Connect(cameraRayMix.ins.Fac);
			glassCore.outs.BSDF.Connect(cameraRayMix.ins.Closure1);
			addRGB.outs.Closure.Connect(cameraRayMix.ins.Closure2);

			cameraRayMix.outs.Closure.Connect(finalMix.ins.Closure1);
			addRGB.outs.Closure.Connect(finalMix.ins.Closure2);

			return finalMix;
		}

		private ShaderNode GetShaderPart(ShaderBody part, DecalProcessingInfo decalProcessingInfo = null)
		{
			if (part.BlendMaterial)
			{
				ShaderNode materialOne = null;
				ShaderNode materialTwo = null;
				MixClosureNode blender = new MixClosureNode(m_shader, "blend material blender");
				blender.ins.Fac.Value = part.BlendMixAmount;
				if (part.MaterialOne != null)
				{
					materialOne = GetShaderPart(part.MaterialOne, decalProcessingInfo);
				}
				else
				{
					DiffuseBsdfNode diff = new DiffuseBsdfNode(m_shader, "materialOne diffuse bsdf");
					diff.ins.Color.Value = new float4(0.9, 0.9, 0.9, 1.0);
					materialOne = diff;
				}
				if (part.MaterialTwo != null)
				{
					materialTwo = GetShaderPart(part.MaterialTwo, decalProcessingInfo);
				}
				else
				{
					DiffuseBsdfNode diff = new DiffuseBsdfNode(m_shader, "materialTwo diffuse bsdf");
					diff.ins.Color.Value = new float4(0.9, 0.9, 0.9, 1.0);
					materialTwo = diff;
				}
				materialOne.GetClosureSocket().Connect(blender.ins.Closure1);
				materialTwo.GetClosureSocket().Connect(blender.ins.Closure2);

				if (part.BlendMixAmountTexture.HasProcedural)
				{
					Utilities.GraphForSlot(m_shader, null, part.BlendMixAmount > 0.0f, part.BlendMixAmountTexture.Amount, part.BlendMixAmountTexture, blender.ins.Fac.ToList(), true, false, false, true, part.Gamma, false, null);
				}
				return blender;
			}
			else
			{
				MixNode textureDecalMixin = null;
				List<ClosureSocket> decalMaterials = null;
				List<FloatSocket> decalMaskSockets = null;

				if (decalProcessingInfo == null)
				{
					textureDecalMixin = HandleTextureDecals(!part.IsPbr);
					(decalMaterials, decalMaskSockets) = HandleMaterialDecals(part.Gamma, !part.IsPbr);
				}

				if (part.IsPbr)
				{
					var engineSettings = Utilities.GetEngineDocumentSettings(m_original.DocumentSerialNumber);
					var productPreset = (RenderPresetHelpers.ProductPreset(engineSettings) == RenderPresetHelpers.Presets.Product);

					if ((part.MaterialKind == CyclesShader.ProbableMaterial.Gem) && productPreset)
					{
						return GemMaterial(part, decalProcessingInfo);
					}

					var principled = new PrincipledBsdfNode(m_shader, "pbr_principled");

					var tangent = new TangentNode(m_shader, "tangents");

					var basewithao = new MixNode(m_shader, "pbr_base_with_ao");
					basewithao.BlendType = MixNode.BlendTypes.Multiply;
					basewithao.ins.Fac.Value = 1.0f;
					basewithao.ins.Color2.Value = Rhino.Display.Color4f.White.ToFloat4();

					MixClosureNode coloured_shadow_mix_custom = null;
					MathMultiply coloured_shadow_switch = null;
					TransparentBsdfNode coloured_shadow = null;
					if (!productPreset)
					{
						coloured_shadow_mix_custom = new MixClosureNode(m_shader, "coloured_shadow_mix_custom");
						var lightpath = new LightPathNode(m_shader, "light_path_for_coloured_shadow");
						coloured_shadow_switch = new MathMultiply(m_shader, "coloured_shadow_switch");
						coloured_shadow = new TransparentBsdfNode(m_shader, "coloured_shadow_transp_bsdf");

						lightpath.outs.IsShadowRay.Connect(coloured_shadow_switch.ins.Value1);
						coloured_shadow_switch.outs.Value.Connect(coloured_shadow_mix_custom.ins.Fac);
						coloured_shadow.outs.BSDF.Connect(coloured_shadow_mix_custom.ins.Closure2);
						principled.outs.BSDF.Connect(coloured_shadow_mix_custom.ins.Closure1);
					}

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

					MixNode aoamount = null;

					if (part.PbrAmbientOcclusion.On && part.PbrAmbientOcclusion.Amount > 0.01f && part.PbrAmbientOcclusionTexture.HasProcedural)
					{
						aoamount = new(m_shader, "pbr_aoamount")
						{
							BlendType = MixNode.BlendTypes.Blend
						};
						aoamount.ins.Color1.Value = Rhino.Display.Color4f.Black.ToFloat4();
						aoamount.ins.Color2.Value = Rhino.Display.Color4f.White.ToFloat4();
						aoamount.ins.Fac.Value = 1.0f;

						m_shader.AddNode(aoamount);

						Utilities.PbrGraphForSlot(m_shader, part.PbrAmbientOcclusion, part.PbrAmbientOcclusionTexture, aoamount.ins.Color2.ToList(), false, part.Gamma, true, false, decalProcessingInfo);

						aoamount.ins.Fac.Value = part.PbrAmbientOcclusion.Amount;
						aoamount.outs.Color.Connect(basewithao.ins.Color2);
					}

					ISocket basecoltexAlphaOut;

					List<ISocket> colsocks = new()
					{
						basewithao.ins.Color1, //principled.ins.BaseColor,
					};
					if (coloured_shadow != null)
					{
						colsocks.Add(coloured_shadow.ins.Color);
					}

					if (textureDecalMixin != null)
					{
						// HACK: tell base tex is data, so that we can manually add here
						// gamma node after decal mixin before connecting _that_ up to colsocks
						basecoltexAlphaOut = Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture, textureDecalMixin.ins.Color1.ToList(), false, part.Gamma, true, true, decalProcessingInfo);

						// now add gamma node to ensure decals are corrected properly
						GammaNode gammaNode = new GammaNode(m_shader, "gamma node for decalled pbr base tex");
						gammaNode.ins.Gamma.Value = part.Gamma;
						textureDecalMixin.outs.Color.Connect(gammaNode.ins.Color);
						foreach (var colsock in colsocks)
						{
							gammaNode.outs.Color.Connect(colsock);
						}
					}
					else
					{
						basecoltexAlphaOut = Utilities.PbrGraphForSlot(m_shader, part.PbrBase, part.PbrBaseTexture, colsocks, false, part.Gamma, false, false, decalProcessingInfo);
					}

					basewithao.outs.Color.Connect(principled.ins.BaseColor);

					if (basecoltexAlphaOut != null && part.UseBaseColorTextureAlphaAsObjectAlpha)
					{
						basecoltexAlphaOut.Connect(alpha_invert_basecolalpha_component.ins.Value2);
						alpha_invert_basecolalpha_component.outs.Value.Connect(alpha_basecolalpha_plus_alphatransp.ins.Value1);
					}

					Utilities.PbrGraphForSlot(m_shader, part.PbrMetallic, part.PbrMetallicTexture, principled.ins.Metallic.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSpecular, part.PbrSpecularTexture, principled.ins.Specular.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSpecularTint, part.PbrSpecularTintTexture, principled.ins.SpecularTint.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrRoughness, part.PbrRoughnessTexture, principled.ins.Roughness.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSheen, part.PbrSheenTexture, principled.ins.Sheen.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSheenTint, part.PbrSheenTintTexture, principled.ins.SheenTint.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrClearcoat, part.PbrClearcoatTexture, principled.ins.Clearcoat.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrClearcoatRoughness, part.PbrClearcoatRoughnessTexture, principled.ins.ClearcoatGloss.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurface, part.PbrSubsurfaceTexture, principled.ins.Subsurface.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurfaceColor, part.PbrSubsurfaceColorTexture, principled.ins.SubsurfaceColor.ToList(), false, part.Gamma, false, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrSubsurfaceRadius, part.PbrSubsurfaceRadiusTexture, principled.ins.SubsurfaceRadius.ToList(), false, part.Gamma, true, false, decalProcessingInfo);

					List<ISocket> transmissionSockets = new() {
						principled.ins.Transmission
					};
					if (coloured_shadow_switch != null)
					{
						transmissionSockets.Add(coloured_shadow_switch.ins.Value2);
					}
					Utilities.PbrGraphForSlot(m_shader, part.PbrTransmission, part.PbrTransmissionTexture, transmissionSockets, true, part.Gamma, true, false, decalProcessingInfo);

					Utilities.PbrGraphForSlot(m_shader, part.PbrTransmissionRoughness, part.PbrTransmissionRoughnessTexture, principled.ins.TransmissionRoughness.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrIor, part.PbrIorTexture, principled.ins.IOR.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrAnisotropic, part.PbrAnisotropicTexture, principled.ins.Anisotropic.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
					Utilities.PbrGraphForSlot(m_shader, part.PbrAnisotropicRotation, part.PbrAnisotropicRotationTexture, principled.ins.AnisotropicRotation.ToList(), false, part.Gamma, true, false, decalProcessingInfo);

					if (part.PbrBump.On && part.PbrBumpTexture.HasProcedural)
					{
						if (!part.PbrBumpTexture.IsNormalMap)
						{
							var bump = new BumpNode(m_shader, "bump");
							bump.ins.Strength.Value = Math.Abs(part.PbrBump.Amount) * RcCore.It.AllSettings.BumpStrengthFactor * DisplayBumpMatchFactor;
							bump.Invert = part.PbrBump.Amount < 0.0f;
							bump.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
							part.PbrBump.Amount = 1.0f;
							Utilities.GraphForSlot(m_shader, null, part.PbrBump.On, part.PbrBump.Amount, part.PbrBumpTexture, bump.ins.Height.ToList(), true, false, false, true, part.Gamma, false, decalProcessingInfo);
							bump.outs.Normal.Connect(principled.ins.Normal);
						}
						else
						{
							Utilities.GraphForSlot(m_shader, null, part.PbrBump.On, part.PbrBump.Amount, part.PbrBumpTexture, principled.ins.Normal.ToList(), false, true, false, true, part.Gamma, false, decalProcessingInfo);
						}
					}
					if (part.PbrClearcoatBump.On && part.PbrClearcoatBumpTexture.HasProcedural)
					{
						if (!part.PbrClearcoatBumpTexture.IsNormalMap)
						{
							var bump = new BumpNode(m_shader, "clearcoat_bump");
							bump.ins.Strength.Value = Math.Abs(part.PbrClearcoatBump.Amount) * RcCore.It.AllSettings.BumpStrengthFactor * DisplayBumpMatchFactor;
							bump.Invert = part.PbrClearcoatBump.Amount < 0.0f;
							part.PbrClearcoatBump.Amount = 1.0f;
							bump.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
							Utilities.GraphForSlot(m_shader, null, part.PbrClearcoatBump.On, part.PbrClearcoatBump.Amount, part.PbrClearcoatBumpTexture, bump.ins.Height.ToList(), true, false, false, true, part.Gamma, false, decalProcessingInfo);
							bump.outs.Normal.Connect(principled.ins.ClearcoatNormal);
						}
						else
						{
							Utilities.GraphForSlot(m_shader, null, part.PbrClearcoatBump.On, part.PbrClearcoatBump.Amount, part.PbrClearcoatBumpTexture, principled.ins.ClearcoatNormal.ToList(), false, true, false, true, part.Gamma, false, decalProcessingInfo);
						}
					}

					float emission_strength = part.EmissionStrength;
					// When an emission texture is added and active make sure that the emission
					// base color isn't black.
					if (part.PbrEmission.On)
					{
						if (part.PbrEmission.Value.Equals(Rhino.Display.Color4f.Black))
						{
							part.PbrEmission.Value = Rhino.Display.Color4f.White;
						}
					}

					Utilities.PbrGraphForSlot(m_shader, part.PbrEmission, part.PbrEmissionTexture, principled.ins.Emission.ToList(), false, part.Gamma, false, false, decalProcessingInfo);
					principled.ins.EmissionStrength.Value = emission_strength;

					Utilities.PbrGraphForSlot(m_shader, part.PbrAlpha, part.PbrAlphaTexture, alpha_transp_component.ins.Value2.ToList(), false, part.Gamma, true, false, decalProcessingInfo);

					alpha_transp_component.outs.Value.Connect(alpha_basecolalpha_plus_alphatransp.ins.Value2);

					alpha_basecolalpha_plus_alphatransp.outs.Value.Connect(alpha_transparency_final.ins.Value2);

					if (decalProcessingInfo == null)
					{
						alpha_transparency_final.outs.Value.Connect(principled.ins.Alpha);
						alpha_transparency_final.outs.Value.Connect(alpha_cutter_mixer.ins.Fac);
					}
					else
					{
						// If this is a decal material, all alpha transparency gets routed to the decal transparency code.
						// Therefore, we set the alphas within the material to 1.0f.
						principled.ins.Alpha.Value = 1.0f;
						alpha_cutter_mixer.ins.Fac.Value = 1.0f;

						decalProcessingInfo.AlphaOut = alpha_transparency_final.outs.Value;
					}

					tangent.outs.Tangent.Connect(principled.ins.Tangent);

					if (false && part.PbrDisplacement.On && part.PbrDisplacementTexture.HasProcedural)
					{
						var displacement = new DisplacementNode(m_shader);
						var strength = new MathMultiply(m_shader);
						var adjust = new MathSubtract(m_shader);
						displacement.ins.Midlevel.Value = 0.0f;
						adjust.ins.Value2.Value = 0.5f;
						strength.ins.Value1.Value = part.PbrDisplacement.Amount * 2.0f;
						part.PbrDisplacement.Amount = 1.0f;
						Utilities.PbrGraphForSlot(m_shader, part.PbrDisplacement, part.PbrDisplacementTexture, adjust.ins.Value1.ToList(), false, part.Gamma, true, false, decalProcessingInfo);
						adjust.outs.Value.Connect(strength.ins.Value2);
						strength.outs.Value.Connect(displacement.ins.Height);
						displacement.outs.Displacement.Connect(m_shader.Output.ins.Displacement);
					}

					if (decalMaterials?.Count > 0)
					{
						var prevClosureSocket = !productPreset ? coloured_shadow_mix_custom.GetClosureSocket() : principled.GetClosureSocket();

						// Blend all decals together using MixClosureNodes.
						for (int idx = 0; idx < decalMaterials.Count; idx++)
						{
							var closureSocket = decalMaterials[idx];
							var maskSocket = decalMaskSockets[idx];

							MixClosureNode decalMixer = new MixClosureNode(m_shader, "decals blender");
							prevClosureSocket.Connect(decalMixer.ins.Closure1);

							closureSocket.Connect(decalMixer.ins.Closure2);
							maskSocket.Connect(decalMixer.ins.Fac);

							prevClosureSocket = decalMixer.outs.Closure;
						}

						prevClosureSocket.Connect(alpha_cutter_mixer.ins.Closure2);
					}
					else
					{
						if (!productPreset)
						{
							coloured_shadow_mix_custom.outs.Closure.Connect(alpha_cutter_mixer.ins.Closure2);
						}
						else
						{
							principled.outs.BSDF.Connect(alpha_cutter_mixer.ins.Closure2);
						}
					}

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
					bump_amount72.ins.Value2.Value = Math.Abs(part.BumpTexture.Amount) * RcCore.It.AllSettings.BumpStrengthFactor * DisplayBumpMatchFactor;
					bump_amount72.Operation = MathNode.Operations.Multiply;
					bump_amount72.UseClamp = false;

					var diffuse_base_color_through_alpha120 = new MixNode(m_shader, "diffuse_base_color_through_alpha_");
					diffuse_base_color_through_alpha120.BlendType = ccl.ShaderNodes.MixNode.BlendTypes.Blend;
					diffuse_base_color_through_alpha120.UseClamp = false;

					var bump88 = new BumpNode(m_shader, "bump_");
					bump88.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);
					bump88.ins.Strength.Value = RcCore.It.AllSettings.BumpStrengthFactor;  // overridden by bump_amount72 (Abs(Amount) * BSF) connected below
					bump88.ins.Distance.Value = RcCore.It.AllSettings.BumpDistance;
					bump88.ins.UseObjectSpace.Value = false;
					bump88.Invert = part.BumpTexture.Amount < 0.0f;

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

					var environment_map_diffuse108 = new DiffuseBsdfNode(m_shader, "environment_map_diffuse_");
					environment_map_diffuse108.ins.Roughness.Value = 0f;
					environment_map_diffuse108.ins.Normal.Value = new ccl.float4(0f, 0f, 0f, 1f);

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

					var custom_environment_blend110 = new MixClosureNode(m_shader, "custom_environment_blend_");
					custom_environment_blend110.ins.Fac.Value = part.EnvironmentTexture.Amount;

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
					principledbsdf117.ins.ClearcoatGloss.Value = part.Gloss;
					principledbsdf117.ins.IOR.Value = part.IOR;
					principledbsdf117.ins.EmissionStrength.Value = 0.0f;
					principledbsdf117.ins.Transmission.Value = part.Transparency;
					principledbsdf117.ins.TransmissionRoughness.Value = part.RefractionRoughness;
					principledbsdf117.ins.Tangent.Value = new float4(0f, 0f, 0f, 1f);

					var custom_environment_blend195 = new MixClosureNode(m_shader, "custom_environment_blend_principled_");
					custom_environment_blend195.ins.Fac.Value = part.EnvironmentTexture.Amount;

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
					bump_amount72.outs.Value.Connect(bump88.ins.Strength);

					if (textureDecalMixin != null)
					{
						diffuse_base_color_through_alpha120.outs.Color.Connect(textureDecalMixin.ins.Color1);
						textureDecalMixin.outs.Color.Connect(final_diffuse89.ins.Color);
						textureDecalMixin.outs.Color.Connect(shadeless_bsdf90.ins.Color);
						textureDecalMixin.outs.Color.Connect(coloured_shadow_trans_color111.ins.Color);
						textureDecalMixin.outs.Color.Connect(mix_diffuse_and_transparency_color187.ins.Color1);
					}
					else
					{
						diffuse_base_color_through_alpha120.outs.Color.Connect(final_diffuse89.ins.Color);
						diffuse_base_color_through_alpha120.outs.Color.Connect(shadeless_bsdf90.ins.Color);
						diffuse_base_color_through_alpha120.outs.Color.Connect(coloured_shadow_trans_color111.ins.Color);
						diffuse_base_color_through_alpha120.outs.Color.Connect(mix_diffuse_and_transparency_color187.ins.Color1);
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
					attenuated_environment_color106.outs.Color.Connect(environment_map_diffuse108.ins.Color);
					invert_roughness75.outs.Value.Connect(multiply_transparency76.ins.Value1);
					multiply_transparency76.outs.Value.Connect(multiply_with_shadowray77.ins.Value1);
					light_path109.outs.IsShadowRay.Connect(multiply_with_shadowray77.ins.Value2);
					diffuse_glossy_and_refraction107.outs.Closure.Connect(custom_environment_blend110.ins.Closure1);
					environment_map_diffuse108.outs.BSDF.Connect(custom_environment_blend110.ins.Closure2);
					multiply_with_shadowray77.outs.Value.Connect(weight_for_shadowray_coloured_shadow78.ins.Value1);
					custom_environment_blend110.outs.Closure.Connect(coloured_shadow_mix_custom114.ins.Closure1);
					coloured_shadow_trans_color111.outs.BSDF.Connect(coloured_shadow_mix_custom114.ins.Closure2);
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
					// either this or pbr into here, check which is better... coloured_shadow_mix_custom114.outs.Closure.Connect(add_emission_to_final124.ins.Closure1);
					coloured_shadow_mix_glass_principled118.outs.Closure.Connect(add_emission_to_final124.ins.Closure1);
					diffuse_or_shadeless_emission126.outs.Closure.Connect(add_emission_to_final124.ins.Closure2);
					toggle_diffuse_texture_alpha_usage81.outs.Value.Connect(add_diffuse_texture_alpha83.ins.Value1);
					toggle_transparency_texture82.outs.Value.Connect(add_diffuse_texture_alpha83.ins.Value2);
					add_emission_to_final124.outs.Closure.Connect(custom_alpha_cutter116.ins.Closure1);
					transparent115.outs.BSDF.Connect(custom_alpha_cutter116.ins.Closure2);
					add_diffuse_texture_alpha83.outs.Value.Connect(custom_alpha_cutter116.ins.Fac);
					attennuated_refraction_color99.outs.Color.Connect(mix_diffuse_and_transparency_color187.ins.Color2);
					mix_diffuse_and_transparency_color187.outs.Color.Connect(principledbsdf117.ins.BaseColor);
					if (part.Shadeless)
					{
						shadeless96.outs.Closure.Connect(custom_environment_blend195.ins.Closure1);
					}
					else
					{
						principledbsdf117.outs.BSDF.Connect(custom_environment_blend195.ins.Closure1);
					}
					environment_map_diffuse108.outs.BSDF.Connect(custom_environment_blend195.ins.Closure2);
					mix_diffuse_and_transparency_color187.outs.Color.Connect(coloured_shadow_trans_color_for_principled188.ins.Color);
					coloured_shadow_trans_color_for_principled188.outs.BSDF.Connect(coloured_shadow_mix_glass_principled118.ins.Closure2);
					weight_for_shadowray_coloured_shadow78.outs.Value.Connect(coloured_shadow_mix_glass_principled118.ins.Fac);
					custom_environment_blend195.outs.Closure.Connect(coloured_shadow_mix_glass_principled118.ins.Closure1);

					/* extra code */
					float useAlpha = 0.0f;

					if (part.DiffuseTexture.HasProcedural)
					{
						//Rhino.RhinoApp.OutputDebugString($"{m_codeshader.Code}\n");
						if (part.DiffuseTexture.Procedural is BitmapTextureProcedural bmtp)
						{
							useAlpha = part.DiffuseTexture.UseAlphaAsFloat;
						}
						toggle_diffuse_texture_alpha_usage81.ins.Value2.Value = useAlpha;
						use_alpha_weighted_with_modded_amount71.ins.Value1.Value = useAlpha;
						one_if_usealphatransp_turned_off178.ins.Value1.Value = useAlpha;

						List<ISocket> sockets = new List<ISocket>
						{
							diffuse_base_color_through_alpha120.ins.Color2,
							diffuse_base_color_through_alpha180.ins.Color2
						};
						var alpha = Utilities.GraphForSlot(m_shader, null, true, part.DiffuseTexture.Amount, part.DiffuseTexture, sockets, false, false, false, false, part.Gamma, textureDecalMixin != null, decalProcessingInfo);
						if (alpha != null)
						{
							if (decalProcessingInfo == null)
							{
								alpha.Connect(diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2);
								alpha.Connect(max_of_texalpha_or_usealpha179.ins.Value1);
							}
							else
							{
								// If this is a decal material, all alpha transparency gets routed to the decal transparency code.
								// Therefore, we set the alphas within the material to 1.0f.
								diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2.Value = 1.0f;
								max_of_texalpha_or_usealpha179.ins.Value1.Value = 1.0f;

								decalProcessingInfo.AlphaOut = alpha;
							}
						}
						else
						{
							diff_tex_weighted_alpha_for_basecol_mix182.ins.Value2.Value = 1.0f;
							max_of_texalpha_or_usealpha179.ins.Value1.Value = 1.0f;
						}
					}

					if (part.TransparencyTexture.HasProcedural)
					{
						List<ISocket> sockets = new List<ISocket>
						{
							transpluminance113.ins.Color
						};
						useAlpha = 1.0f;
						Utilities.GraphForSlot(m_shader, null, true, part.TransparencyTexture.Amount, part.TransparencyTexture, sockets, false, false, false, true, part.Gamma, false, decalProcessingInfo);
					}

					if (part.BumpTexture.HasProcedural)
					{
						Utilities.GraphForSlot(m_shader, null, true, 1.0f, part.BumpTexture, bump88.ins.Height.ToList(), true, false, false, true, part.Gamma, false, decalProcessingInfo);
						bump88.outs.Normal.Connect(principledbsdf117.ins.Normal);
					}

					if (part.EnvironmentTexture.HasProcedural)
					{
						// https://mcneel.myjetbrains.com/youtrack/issue/RH-84799
						// Need to manually set here the correct projection mode as this
						// information isn't available while the texture is being evaluated
						part.EnvironmentTexture.Procedural.ProjectionMode = Rhino.Render.TextureProjectionMode.EnvironmentMap;
						if (part.EnvironmentTexture.Procedural.EnvironmentMappingMode == Rhino.Render.TextureEnvironmentMappingMode.Automatic)
						{
							part.EnvironmentTexture.Procedural.EnvironmentMappingMode = Rhino.Render.TextureEnvironmentMappingMode.EnvironmentMap;
						}
						Utilities.GraphForSlot(m_shader, null, true, part.EnvironmentTexture.Amount, part.EnvironmentTexture, attenuated_environment_color106.ins.Color2.ToList(), false, false, false, false, part.Gamma, false, decalProcessingInfo);
					}

					// When useAlpha is set we need to ensure we actually pass on custom_alpha_cutter116, otherwise custom
					// materials with alpha transparency of any sort will fail.
					// See https://mcneel.myjetbrains.com/youtrack/issue/RH-84849
					MixClosureNode outputNode = useAlpha > 0.0 ? custom_alpha_cutter116 : coloured_shadow_mix_glass_principled118;

					if (decalMaterials?.Count > 0)
					{
						var prevOutputNode = outputNode;
						var prevClosureSocket = outputNode.GetClosureSocket();

						// Blend all decals together using MixClosureNodes
						for (int idx = 0; idx < decalMaterials.Count; idx++)
						{
							var closureSocket = decalMaterials[idx];
							var maskSocket = decalMaskSockets[idx];

							MixClosureNode decalMixer = new MixClosureNode(m_shader, "decals blender");
							prevClosureSocket.Connect(decalMixer.ins.Closure1);
							closureSocket.Connect(decalMixer.ins.Closure2);
							maskSocket.Connect(decalMixer.ins.Fac);

							prevOutputNode = decalMixer;
							prevClosureSocket = decalMixer.outs.Closure;
						}

						outputNode = prevOutputNode;
					}

					return outputNode;
				}
			}
		}
	}
}
