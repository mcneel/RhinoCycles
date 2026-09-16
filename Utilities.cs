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
using Rhino.Display;
using Rhino.Render;
using Rhino.Render.Fields;
using Rhino.Runtime;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.ExtensionMethods;
using RhinoCyclesCore.Settings;
using RhinoCyclesCore.Shaders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RhinoCyclesCore
{
	public class DeviceAndPath
	{
		Device _device;
		public Device Device => _device;

		string _path;
		public string Path => _path;
		public DeviceAndPath(Device device, string path)
		{
			_device = device;
			_path = path;
		}
	}

	/// <summary>
	/// Exception thrown when TexturedSlot type is unsupported.
	/// </summary>
	/// <since>6.12</since>
	internal class UnrecognizedTexturedSlotType : Exception
	{
		/// <summary>
		/// Construct exception
		/// </summary>
		/// <param name="message"></param>
		/// <since>6.12</since>
		internal UnrecognizedTexturedSlotType(string message) : base(message) { }
	}

	public static class Utilities
	{
		public static IAllSettings GetEngineDocumentSettings(uint doc_serial)
		{
			return new EngineDocumentSettings(doc_serial);
		}
		public static void TexturedSlot(RenderMaterial rm, string slotname, Color4f defaultColor, string prompt)
		{
			rm.Fields.AddTextured(slotname, defaultColor, prompt, false);
		}

		public static void TexturedSlot(RenderMaterial rm, string slotname, float defaultValue, string prompt)
		{
			rm.Fields.AddTextured(slotname, defaultValue, prompt, false);
		}

		public static (bool Success, float4 Result, bool IsOn, float Amount, RenderMaterial Child) HandleMaterialSlot(RenderMaterial rm, string slotname)
		{
			bool success = false;
			float4 rc = new float4(0.0f);
			bool onness = false;
			float amount = 0.0f;
			RenderMaterial rmchild = null;
			if (rm.Fields.TryGetValue(slotname, out Color4f c))
			{
				rc = c.ToFloat4();
				success = true;
			}
			var texamount = rm.GetChildSlotParameter(slotname, "texture-amount") as IConvertible;
			if(texamount != null) {
				amount = Convert.ToSingle(texamount) / 100.0f;
			}
			var texon = rm.GetChildSlotParameter(slotname, "texture-on") as IConvertible;
			if(texon != null) {
				onness = Convert.ToBoolean(texon);
			}
			if(rm.FindChild(slotname) is RenderMaterial rt) {
				rmchild = rt;
			}

			return (success, rc, onness, amount, rmchild);
		}

		private static void GatherAdjustments(RenderTexture render_texture, CyclesTextureImage textureImage)
		{
				var rtf = render_texture.Fields;

				if (rtf.TryGetValue("rdk-texture-adjust-grayscale", out bool grayscale))
					textureImage.AdjustGrayscale = grayscale;

				if (rtf.TryGetValue("rdk-texture-adjust-invert", out bool invert))
					textureImage.AdjustInvert = invert;

				if (rtf.TryGetValue("rdk-texture-adjust-clamp", out bool clamp))
					textureImage.AdjustClamp = clamp;

				if (rtf.TryGetValue("rdk-texture-adjust-scale-to-clamp", out bool scale_to_clamp))
					textureImage.AdjustScaleToClamp = scale_to_clamp;

				if (rtf.TryGetValue("rdk-texture-adjust-multiplier", out double multiplier))
					textureImage.AdjustMultiplier = (float)multiplier;

				if (rtf.TryGetValue("rdk-texture-adjust-clamp-min", out double clamp_min))
					textureImage.AdjustClampMin = (float)clamp_min;

				if (rtf.TryGetValue("rdk-texture-adjust-clamp-max", out double clamp_max))
					textureImage.AdjustClampMax = (float)clamp_max;

				if (rtf.TryGetValue("rdk-texture-adjust-gain", out double gain))
					textureImage.AdjustGain = (float)gain;

				if (rtf.TryGetValue("rdk-texture-adjust-gamma", out double gamma))
					textureImage.AdjustGamma = (float)gamma;

				if (rtf.TryGetValue("rdk-texture-adjust-saturation", out double saturation))
					textureImage.AdjustSaturation = (float)saturation;

				if (rtf.TryGetValue("rdk-texture-adjust-hue-shift", out double hue_shift))
					textureImage.AdjustHueShift = (float)hue_shift;

				textureImage.AdjustIsHdr = render_texture.IsHdrCapable();

				if (textureImage.AdjustClamp || textureImage.AdjustScaleToClamp || textureImage.AdjustInvert || textureImage.AdjustGrayscale)
				{
					textureImage.AdjustNeeded = true;
				}
				else if (textureImage.AdjustGain != 0.5f ||
					textureImage.AdjustGamma != 1.0f ||
					textureImage.AdjustMultiplier != 1.0f ||
					textureImage.AdjustClampMin != 0.0f ||
					textureImage.AdjustClampMax != 1.0f ||
					textureImage.AdjustHueShift != 0.0f ||
					textureImage.AdjustSaturation != 1.0f)
				{
					textureImage.AdjustNeeded = true;
				}
		}

		public static void HandleRenderTexture(RenderTexture rt, CyclesTextureImage tex, bool check_for_normal_map, bool is_leaf_bitmap, Converters.BitmapConverter bitmapConverter, uint docsrn, float gamma, bool should_simulate, bool is_color)
		{
			if (rt == null) return;

			// JohnC: I had to change this to also exclude linear workflow because when I changed from using
			// the incorrect TextureRenderHashFlags to the correct CrcRenderHashFlags, an assert started firing
			// because we are not on the main thread.
			uint rid = rt.RenderHashWithoutLocalMappingOrLinearWorkflow;

			var rotationvec = rt.GetRotation();
			var repeatvec = rt.GetRepeat();
			var offsetvec = rt.GetOffset();

			Transform tt = new Transform(
				(float)offsetvec.X, (float)offsetvec.Y, (float)offsetvec.Z, 0.0f,
				(float)repeatvec.X, (float)repeatvec.Y, (float)repeatvec.Z, 0.0f,
				(float)rotationvec.X, (float)rotationvec.Y, (float)rotationvec.Z, 0.0f
			);


			var projectionMode = rt.GetProjectionMode();
			var envProjectionMode = rt.GetInternalEnvironmentMappingMode();
			var repeat = rt.GetWrapType() == TextureWrapType.Repeating;

			GatherAdjustments(rt, tex);

			var use_color_mask = false;
			{
				var use_mask = rt.GetParameter("has-transparent-color");
				if (use_mask != null)
				{
					use_color_mask = Convert.ToBoolean(use_mask);
				}
			}

			var color_mask = Color4f.White;
			{
				bool has_mask_color = rt.Fields.TryGetValue("transparent-color", out Color4f mask_color);
				if(has_mask_color)
				{
					color_mask = mask_color;
				}
			}

			var color_mask_sensitivity = 0.0f;
			{
				bool has_col_mask_sens = rt.Fields.TryGetValue("transparent-color-sensitivity", out double col_mask_sens);
				if(has_col_mask_sens)
				{
					color_mask_sensitivity = (float)col_mask_sens;
				}
			}

			bool alternate = false;
			if (rt.Fields.TryGetValue("mirror-alternate-tiles", out bool mirror_alternate_tiles))
				alternate = mirror_alternate_tiles;
			else if (rt.Fields.TryGetValue("flip-alternate", out bool flip_alternate))
				alternate = flip_alternate;

			tex.ProjectionMode = projectionMode;

			Procedural procedural = null;

			if (!is_leaf_bitmap)
			{
				procedural = Procedural.CreateProcedural(rt, tex.TextureList, bitmapConverter, docsrn, gamma, is_color);
			}

			if (procedural != null)
			{
				tex.Procedural = procedural;
				tex.IsNormalMap = tex.TextureList.Any<CyclesTextureImage>(cti => cti.IsNormalMap == true);
				tex.MappingChannel = rt.GetMappingChannel();
			}
			else
			{
				var fs = "";
				if (!should_simulate)
				{
					Field tf = rt.Fields.GetField("filename");
					if (tf != null)
					{
						var ofs = tf.GetValue<string>();
						RhinoDoc doc = rt.DocumentAssoc;
						fs = Rhino.Render.Utilities.FindFile(doc, ofs, true);
					}
				} else {
					SimulatedTexture simtex = rt.SimulatedTexture(RenderTexture.TextureGeneration.Allow);
					RhinoDoc doc = rt.DocumentAssoc;
					fs = simtex.Filename; //Rhino.Render.Utilities.FindFile(doc, simtex.Filename, true);
				}

				tex.IsNormalMap = rt.IsNormalMap();
				tex.Filename = string.IsNullOrEmpty(fs) ? null : fs;
				tex.Name = rid.ToString(CultureInfo.InvariantCulture);
				tex.EnvProjectionMode = envProjectionMode;
				tex.Transform = tt;
				tex.Repeat = repeat;
				tex.AlternateTiles = alternate;
				tex.MappingChannel = rt.GetMappingChannel();
				tex.UseColorMask = use_color_mask;
				tex.ColorMask = color_mask;
				tex.ColorMaskSensitivity = color_mask_sensitivity;
			}
		}

		/// <summary>
		/// Create the partial graph for a PBR-type slot.
		/// </summary>
		/// <since>6.12</since>
		/// <typeparam name="T"></typeparam>
		/// <param name="sh"></param>
		/// <param name="slot"></param>
		/// <param name="teximg"></param>
		/// <param name="socks"></param>
		public static ISocket PbrGraphForSlot<T>(Shader sh, TexturedValue<T> slot, CyclesTextureImage teximg, List<ISocket> socks, bool invert, float gamma, bool IsData, bool hasDecals, RhinoFullNxt.DecalProcessingInfo DecalProcessingInfo)
		{
			Type t = typeof(T);
			ISocket valsock = null;
			if (t == typeof(float))
			{
				ValueNode vn = new ValueNode(sh, $"input_value_for_{slot.Name}_");
				vn.Value = (float)(object)slot.Value;
				if (invert)
				{
					MathSubtract invval = new MathSubtract(sh, $"invert_value_for_{slot.Name}_");
					invval.ins.Value1.Value = 1.0f;
					vn.outs.Value.Connect(invval.ins.Value2);
					valsock = invval.outs.Value;
				}
				else
				{
					valsock = vn.outs.Value;
				}
			}
			else if (t == typeof(Color4f))
			{
				ColorNode cn = new ColorNode(sh, $"input_color_for_{slot.Name}_");
				cn.Value = ((Color4f)(object)slot.Value).ToFloat4();
				if (invert)
				{
					InvertNode invcol = new InvertNode(sh, $"invert_input_color_for_{slot.Name}_");
					invcol.ins.Fac.Value = 1.0f;
					cn.outs.Color.Connect(invcol.ins.Color);
					valsock = invcol.outs.Color;
				}
				else
				{
					valsock = cn.outs.Color;
				}
			}
			if(valsock == null) {
				throw new UnrecognizedTexturedSlotType($"Type tried is {t}");
			}
			return GraphForSlot(sh, valsock, slot.On, slot.Amount, teximg, socks, false, false, invert, IsData, gamma, hasDecals, DecalProcessingInfo);
		}

		public static ISocket GraphForSlot(Shader sh, ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, List<ISocket> socketsToConnectTo, bool toBw, bool normalMap, bool invert, bool IsData, float gamma, bool hasDecals, RhinoFullNxt.DecalProcessingInfo DecalProcessingInfo)
		{
			ISocket alphaOut = null;
			// RH-94469: snapshot Procedural - another thread can Dispose/Clear teximg mid-build.
			var procedural = teximg?.Procedural;
			if(IsOn && null != teximg && null != procedural)
			{
				var texco = new RhinoTextureCoordinateNode(sh, $"texco for input {valueSocket?.Parent.VariableName ?? "unknown input"}");
				var mixerNode = new MixNode(sh, $"rgb mix node for imtexnode and {valueSocket?.Parent.VariableName ?? "unknown input"}");

				mixerNode.ins.Fac.Value = Math.Min(1.0f, Math.Max(0.0f, amount));

				var alpha_node = new MathAdd(sh);
				alpha_node.ins.Value1.Value = 0.0f;
				alpha_node.ins.Value2.Value = 1.0f;

				// If we're processing a decal material, then use decal mappings,
				// otherwise, use object/texture mappings.
				VectorSocket uv_output_socket = null;
				if (DecalProcessingInfo != null)
				{
					// A texture in a decal material keeps its own WCS or WCS box projection,
					// like it does in the display. The decal region is masked separately, see
					// GetDecalMaskNode. RH-97945.
					var decalProjection = procedural.ProjectionMode;
					if (decalProjection == TextureProjectionMode.Wcs || decalProjection == TextureProjectionMode.WcsBox)
					{
						texco.UseTransform = true; // identity object transform, so world space
						uv_output_socket = RenderEngine.GetProjectionModeOutputSocket(sh, decalProjection, procedural.EnvironmentMappingMode, texco);
					}
					else
					{
						uv_output_socket = RhinoFullNxt.GetDecalUVNode(DecalProcessingInfo.Decal, texco);
					}
				}
				else
					uv_output_socket = RenderEngine.GetProjectionModeOutputSocket(sh, procedural.ProjectionMode, procedural.EnvironmentMappingMode, texco);

				ColorSocket color_input_node = mixerNode.ins.Color2;
				FloatSocket alpha_input_node = alpha_node.ins.Value2;
				GammaNode gammaNode = null;


				alphaOut = alpha_node.outs.Value;

				texco.UvMap = teximg.GetUvMapForChannel();

				valueSocket?.Connect(mixerNode.ins.Color1);

				ISocket use_outsocket = null;

				List<ISocket> alphaNodes = new List<ISocket>() { alpha_input_node };

				if (valueSocket == null) {
					mixerNode.ins.Fac.Value = 1.0f;
				} else {
					var alphamult = new MathMultiply(sh, $"alpha multiplier for {valueSocket?.Parent.VariableName ?? "unknown input"}");
					alphamult.ins.Value1.Value = Math.Min(1.0f, Math.Max(0.0f, amount));
					alphamult.ins.Value2.Value = 1.0f;
					alphamult.outs.Value.Connect(mixerNode.ins.Fac);
					alphaNodes.Add(alphamult.ins.Value2);
				}

				procedural.CreateAndConnectProceduralNode(sh, uv_output_socket, color_input_node, alphaNodes, IsData);

				// Gamma decode in the shader since the kernel no longer converts (RH-83550),
				// but only for trees with image content - procedurals are already linear (RH-92750).
				if (!IsData && teximg.TextureList.Count > 0 && !teximg.TextureList.Any(t => t.IsSimulatedProcedural))
				{
					gammaNode = new GammaNode(sh, "gamma node for color channel");
					gammaNode.ins.Gamma.Value = gamma;

					mixerNode.outs.Color.Connect(gammaNode.ins.Color);
				}

				if (normalMap)
				{
					var normalmapnode = new NormalMapNode(sh, $"Normal map node for {valueSocket?.Parent.VariableName ?? "unknown input"}")
					{
						Attribute = teximg.GetUvMapForChannel(),
						// ideally we calculate the tangents and switch to Tangent space here.
						SpaceType = NormalMapNode.Space.Tangent
					};
					mixerNode.outs.Color.Connect(normalmapnode.ins.Color);
					normalmapnode.ins.Strength.Value = amount * RcCore.It.AllSettings.NormalStrengthFactor;
					foreach(var sock in socketsToConnectTo) {
						normalmapnode.outs.Normal.Connect(sock);
					}
				}
				else
				{
					if (invert)
					{
						var invcol = new InvertNode(sh, $"invert color for imtexnode for {valueSocket?.Parent.VariableName ?? "unknown input"}");
						mixerNode.outs.Color.Connect(invcol.ins.Color);

						invcol.ins.Fac.Value = 1.0f;
						use_outsocket = invcol.outs.Color;
					}
					else
					{
						use_outsocket = mixerNode.outs.Color;
						if(toBw) {
							var tobwnode = new RgbToBwNode(sh, $"convert imtexnode to bw for {valueSocket?.Parent.VariableName ?? "unknown input"}");
							use_outsocket.Connect(tobwnode.ins.Color);
							use_outsocket = tobwnode.outs.Val;
						}
						if(!IsData && gammaNode != null) {
							use_outsocket = gammaNode.outs.Color;
						}
					}
					if (amount >= 0.0f && amount <= 1.0f)
					{
						foreach (var sock in socketsToConnectTo)
						{
							use_outsocket.Connect(sock);
						}
					} else { // multiply the output of mixerNode.outs.Color with amount.
						SeparateRgbNode separateRgbNode = new SeparateRgbNode(sh, $"separating the color for multiplication {valueSocket?.Parent.VariableName ?? "unknown input"}");
						MathMultiply multiplyR = new MathMultiply(sh, $"multiplier for R {valueSocket?.Parent.VariableName ?? "unknown input"}");
						MathMultiply multiplyG = new MathMultiply(sh, $"multiplier for G {valueSocket?.Parent.VariableName ?? "unknown input"}");
						MathMultiply multiplyB = new MathMultiply(sh, $"multiplier for B {valueSocket?.Parent.VariableName ?? "unknown input"}");
						CombineRgbNode combineRgbNode = new CombineRgbNode(sh, $"combining the new color values {valueSocket?.Parent.VariableName ?? "unknown input"}");

						multiplyR.UseClamp = false;
						multiplyG.UseClamp = false;
						multiplyB.UseClamp = false;

						multiplyR.ins.Value1.Value = amount;
						multiplyG.ins.Value1.Value = amount;
						multiplyB.ins.Value1.Value = amount;

						use_outsocket.Connect(separateRgbNode.ins.Image);

						separateRgbNode.outs.R.Connect(multiplyR.ins.Value2);
						separateRgbNode.outs.G.Connect(multiplyG.ins.Value2);
						separateRgbNode.outs.B.Connect(multiplyB.ins.Value2);

						multiplyR.outs.Value.Connect(combineRgbNode.ins.R);
						multiplyG.outs.Value.Connect(combineRgbNode.ins.G);
						multiplyB.outs.Value.Connect(combineRgbNode.ins.B);

						use_outsocket = combineRgbNode.outs.Image;

						if(!IsData && gammaNode != null) {
							combineRgbNode.outs.Image.Connect(gammaNode.ins.Color);
							use_outsocket = gammaNode.outs.Color;
						}

						foreach (var sock in socketsToConnectTo)
						{
							use_outsocket.Connect(sock);
						}
					}

				}
				return alphaOut;

			}
			else
			{
				if (valueSocket != null)
				{
					if(!hasDecals && valueSocket.XmlName.Equals("color"))
					{
						GammaNode gammaNode = new GammaNode(sh, "gamma node for color inpunputt");
						gammaNode.ins.Gamma.Value = gamma;
						valueSocket.Connect(gammaNode.ins.Color);
						valueSocket = gammaNode.outs.Color;

					}
					foreach (var sock in socketsToConnectTo)
					{
						valueSocket?.Connect(sock);
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Add a graph branch that implements a color mask as Rhino does.
		/// </summary>
		/// <param name="image_texture_node"></param>
		/// <param name="cyclesTexture"></param>
		/// <returns>MathMultiply node that combines image_texture_node alpha with the color mask alpha</returns>
		public static MathMultiply ApplyColorMaskGraph(ImageTextureNode image_texture_node, CyclesTextureImage cyclesTexture)
		{
			Shader shader = image_texture_node.Shader;
			var sep_img_col = new SeparateRgbNode(shader, "separate image color");
			var sep_mask_col = new SeparateRgbNode(shader, "separate mask color");

			MathSubtract sub_r = new(shader, "subtract r channels");
			MathSubtract sub_g = new(shader, "subtract g channels");
			MathSubtract sub_b = new(shader, "subtract b channels");
			sub_r.UseClamp = false;
			sub_g.UseClamp = false;
			sub_b.UseClamp = false;

			MathAbsolute abs_r = new(shader, "abs(r)");
			MathAbsolute abs_g = new(shader, "abs(g)");
			MathAbsolute abs_b = new(shader, "abs(b)");

			MathDivide div_sum_three = new(shader, "sum_abs_rgb__div_three");
			div_sum_three.ins.Value2.Value = 3.0f;

			MathLess_Than sensitivity_lt_absdiv3 = new(shader, "sensitivity lt absdiv3");
			sensitivity_lt_absdiv3.ins.Value1.Value = cyclesTexture.ColorMaskSensitivity;

			MathAdd add_abs_rg = new (shader, "add r and g abs");
			MathAdd add_abs_rg_b = new (shader, "add rg b abs");
			add_abs_rg.UseClamp = false;
			add_abs_rg_b.UseClamp = false;

			MathMultiply adjust_img_alpha = new (shader, "adjust_img_alpha");
			adjust_img_alpha.UseClamp = false;


			image_texture_node.outs.Color.Connect(sep_img_col.ins.Image);
			// Since images are most likely undergoing sRGB to Linear conversion
			// inside Cycles do the same for the color mask value.
			sep_mask_col.ins.Image.Value = float4.SrgbToLinear(cyclesTexture.ColorMask.ToFloat4());

			sep_img_col.outs.R.Connect(sub_r.ins.Value1);
			sep_mask_col.outs.R.Connect(sub_r.ins.Value2);

			sep_img_col.outs.G.Connect(sub_g.ins.Value1);
			sep_mask_col.outs.G.Connect(sub_g.ins.Value2);

			sep_img_col.outs.B.Connect(sub_b.ins.Value1);
			sep_mask_col.outs.B.Connect(sub_b.ins.Value2);

			sub_r.outs.Value.Connect(abs_r.ins.Value1);
			sub_g.outs.Value.Connect(abs_g.ins.Value1);
			sub_b.outs.Value.Connect(abs_b.ins.Value1);

			abs_r.outs.Value.Connect(add_abs_rg.ins.Value1);
			abs_g.outs.Value.Connect(add_abs_rg.ins.Value2);

			add_abs_rg.outs.Value.Connect(add_abs_rg_b.ins.Value1);
			abs_b.outs.Value.Connect(add_abs_rg_b.ins.Value2);


			add_abs_rg_b.outs.Value.Connect(div_sum_three.ins.Value1);

			div_sum_three.outs.Value.Connect(sensitivity_lt_absdiv3.ins.Value2);

			image_texture_node.outs.Alpha.Connect(adjust_img_alpha.ins.Value1);
			sensitivity_lt_absdiv3.outs.Value.Connect(adjust_img_alpha.ins.Value2);
			return adjust_img_alpha;
		}

		public static int GetSystemProcessorCount()
		{
			return HostUtils.GetSystemProcessorCount();
		}

		//public static readonly PlugIn RcPlugIn = Rhino.PlugIns.PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"));
		public static readonly Rhino.PlugIns.PlugIn RcPlugIn = Rhino.PlugIns.PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"));

		private static string _DisableGpusFile {
			get
			{
				var settingsDirectory = RcPlugIn.SettingsDirectory;
				if(!Directory.Exists(settingsDirectory))
				{
					Directory.CreateDirectory(settingsDirectory);
				}

				var disableGpusFile = Path.Combine(settingsDirectory, "disable_gpus");
				return disableGpusFile;
			}
		}

		public static bool GpusDisabled => File.Exists(_DisableGpusFile);

		public static bool HasGpus => Device.FirstGpu.Type != DeviceType.Cpu;

		public static void EnableGpus()
		{
			if(File.Exists(_DisableGpusFile))
			{
				File.Delete(_DisableGpusFile);
			}
			foreach (var gpu in AllGpus)
			{
				EnableGpu(gpu);
			}
		}

		private static string _GpuAbsentFile =>
			Path.Combine(RcPlugIn.SettingsDirectory, "gpu_absent");

		/// <summary>
		/// Record that the machine has a GPU but Cycles offered none. Nothing failed to start -
		/// the backend came up and found nothing it could use - so this is a note, not a disable:
		/// it never keeps a backend from being tried. Written once per environment, so it doubles
		/// as the 'has the user been told yet' flag. RH-98701.
		/// </summary>
		/// <returns>True when this is not on record yet.</returns>
		public static bool RecordGpuAbsent(string systemGpus, string cyclesDevices)
		{
			var signature = GpuEnvironmentSignature;
			try
			{
				var f = _GpuAbsentFile;
				if (File.Exists(f) && string.Equals(_RecordedSignature(f), signature, StringComparison.Ordinal))
				{
					return false;
				}
				var sb = new StringBuilder();
				sb.AppendLine(_SignatureKey + signature);
				sb.AppendLine("first=" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
				sb.AppendLine("systemgpus=" + _OneLine(systemGpus));
				sb.AppendLine("cyclesdevices=" + _OneLine(cyclesDevices));
				sb.AppendLine(_AnnouncedKey + "no");
				File.WriteAllText(f, sb.ToString());
			}
			catch (Exception) { return false; }
			return true;
		}

		/// <summary>A GPU is available again, so a later disappearance is worth reporting.</summary>
		public static void ForgetGpuAbsent()
		{
			try { if (File.Exists(_GpuAbsentFile)) File.Delete(_GpuAbsentFile); } catch (Exception) { }
		}

		/// <summary>The no-GPU-offered record for RhinoCyclesSupportReport, empty when there is none.</summary>
		public static string GpuAbsentRecord
		{
			get
			{
				try
				{
					var f = _GpuAbsentFile;
					return File.Exists(f) ? File.ReadAllText(f).TrimEnd() : string.Empty;
				}
				catch (Exception ex) { return "<could not read: " + ex.GetType().Name + ">"; }
			}
		}

		/// <summary>
		/// The failure record for each switched off backend, for RhinoCyclesSupportReport.
		/// Name, file path and the file's own contents.
		/// </summary>
		public static IEnumerable<(string Name, string Path, string Content)> DisabledGpuRecords()
		{
			foreach (var (mask, _, name) in _GpuBackends)
			{
				var f = _DisabledGpuFile(mask);
				if (f == null || !File.Exists(f)) continue;
				string content;
				try { content = File.ReadAllText(f).TrimEnd(); }
				catch (Exception ex) { content = "<could not read: " + ex.GetType().Name + ">"; }
				yield return (name, f, content);
			}
		}

		/// <summary>Re-enable only the auto-disabled backends, leaving RhinoCyclesDisableGpu alone.</summary>
		public static void EnableGpuBackends()
		{
			foreach (var gpu in AllGpus)
			{
				EnableGpu(gpu);
			}
		}

		public static void DisableGpus()
		{
			if(!File.Exists(_DisableGpusFile))
			{
				File.Create(_DisableGpusFile).Dispose();
			}
		}

		private static readonly (DeviceTypeMask Mask, string FileName, string Name)[] _GpuBackends = new[]
		{
			(DeviceTypeMask.CUDA,   "disable_cuda",   "CUDA"),
			(DeviceTypeMask.OPTIX,  "disable_optix",  "OptiX"),
			(DeviceTypeMask.HIP,    "disable_hip",    "HIP"),
			(DeviceTypeMask.METAL,  "disable_metal",  "Metal"),
			(DeviceTypeMask.ONEAPI, "disable_oneapi", "oneAPI"),
		};

		public static IEnumerable<DeviceTypeMask> AllGpus =>
			_GpuBackends.Select(b => b.Mask);

		/// <summary>Names of the backends currently switched off, for the UI. Empty when none are.</summary>
		public static string DisabledGpuNames =>
			string.Join(", ", _GpuBackends.Where(b => IsGpuDisabled(b.Mask)).Select(b => b.Name));

		/// <summary>Backend names for UI and command options, in a stable order.</summary>
		public static IEnumerable<string> GpuBackendNames => _GpuBackends.Select(b => b.Name);

		/// <summary>Look a backend up by the name from <see cref="GpuBackendNames"/>.</summary>
		public static bool TryFindGpuBackend(string name, out DeviceTypeMask gpu)
		{
			foreach (var b in _GpuBackends)
			{
				if (string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					gpu = b.Mask;
					return true;
				}
			}
			gpu = 0;
			return false;
		}

		private static string _DisabledGpuFile(DeviceTypeMask gpu)
		{
			var entry = _GpuBackends.FirstOrDefault(b => b.Mask == gpu);
			if (entry.FileName == null) return null;
			var settingsDirectory = RcPlugIn.SettingsDirectory;
			if (!Directory.Exists(settingsDirectory))
			{
				Directory.CreateDirectory(settingsDirectory);
			}
			return Path.Combine(settingsDirectory, entry.FileName);
		}

		public static DeviceTypeMask DisabledGpus
		{
			get
			{
				DeviceTypeMask mask = 0;
				foreach (var (m, _, _) in _GpuBackends)
				{
					if (File.Exists(_DisabledGpuFile(m))) mask |= m;
				}
				return mask;
			}
		}

		public static bool IsGpuDisabled(DeviceTypeMask gpu)
		{
			var f = _DisabledGpuFile(gpu);
			return f != null && File.Exists(f);
		}

		/// <summary>
		/// Record that a backend failed to start. The file is stamped with the environment, so
		/// a driver, GPU or Rhino change earns a retry. It is written only when there is no
		/// record yet or the environment has changed - a backend that fails at every start
		/// writes nothing after the first time.
		/// </summary>
		/// <param name="gpu">The backend that failed.</param>
		/// <param name="error">What Cycles said, if anything. Only ever read by a human.</param>
		/// <param name="alsoFailed">Every backend that failed in the same session.</param>
		/// <returns>True when this failure was not on record yet - the first time the user
		/// could be told about it.</returns>
		public static bool DisableGpu(DeviceTypeMask gpu, string error = null, DeviceTypeMask alsoFailed = 0)
		{
			var f = _DisabledGpuFile(gpu);
			if (f == null) return false;
			var signature = GpuEnvironmentSignature;
			try
			{
				// Same environment as the existing record: it already says all this.
				if (File.Exists(f) && string.Equals(_RecordedSignature(f), signature, StringComparison.Ordinal))
				{
					return false;
				}
				File.WriteAllText(f, _FailureRecord(gpu, signature, error, alsoFailed));
			}
			catch (Exception) { return false; }
			return true;
		}

		/// <summary>
		/// The failure record a support person reads. Plain key=value, one line each, deliberately dull:
		/// RhinoCyclesSupportReport prints this and customers paste that into the forum.
		/// </summary>
		private static string _FailureRecord(DeviceTypeMask gpu, string signature, string error,
			DeviceTypeMask alsoFailed)
		{
			var sb = new StringBuilder();
			// signature must stay the only thing compared - see _RecordedSignature.
			sb.AppendLine(_SignatureKey + signature);
			sb.AppendLine("backend=" + _GpuName(gpu));
			sb.AppendLine("first=" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
			sb.AppendLine("error=" + _OneLine(error));
			sb.AppendLine(_AnnouncedKey + "no");
			sb.AppendLine("alsofailed=" + string.Join(",", _GpuBackends
				.Where(b => b.Mask != gpu && (alsoFailed & b.Mask) != 0)
				.Select(b => b.Name)));
			// The two kernel-cache problems that are invisible by the time anyone asks.
			try
			{
				var cache = RcCore.It.GpuCompilePath;
				sb.AppendLine("cache=" + cache);
				sb.AppendLine("cacheexists=" + Directory.Exists(cache));
				var freeMb = new DriveInfo(Path.GetPathRoot(cache)).AvailableFreeSpace / (1024L * 1024L);
				sb.AppendLine("cachefreemb=" + freeMb.ToString(CultureInfo.InvariantCulture));
			}
			catch (Exception) { }
			return sb.ToString();
		}

		private const string _SignatureKey = "signature=";
		private const string _AnnouncedKey = "announced=";

		/// <summary>Read one key=value line out of a record, empty when it is not there.</summary>
		private static string _RecordField(string path, string key)
		{
			try
			{
				foreach (var line in File.ReadAllLines(path))
				{
					if (line.StartsWith(key, StringComparison.Ordinal))
					{
						return line.Substring(key.Length).Trim();
					}
				}
			}
			catch (Exception) { }
			return string.Empty;
		}

		/// <summary>
		/// True once, for the first start that shows a record to the user, and false forever
		/// after. Keeping this in the record rather than in a session variable means the panel
		/// is still pushed forward if the session that hit the failure never got that far -
		/// a crash right after a failed GPU init, say - and that a record written by
		/// RhinoCyclesDisableGpu is announced on the start where it actually takes effect.
		/// RH-98701.
		/// </summary>
		public static bool TakeGpuAnnouncement()
		{
			var claimed = false;
			var files = new List<string>();
			foreach (var (mask, _, _) in _GpuBackends)
			{
				var f = _DisabledGpuFile(mask);
				if (f != null && File.Exists(f)) files.Add(f);
			}
			if (File.Exists(_GpuAbsentFile)) files.Add(_GpuAbsentFile);

			foreach (var f in files)
			{
				if (!string.Equals(_RecordField(f, _AnnouncedKey), "no", StringComparison.Ordinal)) continue;
				try
				{
					var text = File.ReadAllText(f);
					File.WriteAllText(f, text.Replace(_AnnouncedKey + "no", _AnnouncedKey + "yes"));
					claimed = true;
				}
				catch (Exception) { }
			}
			return claimed;
		}

		/// <summary>Collapse to one short line - this ends up in a support report.</summary>
		private static string _OneLine(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return "(none reported)";
			// Collapse every whitespace run to one space: driver errors arrive multi-line, and this
			// has to stay a single short key=value line.
			var sb = new StringBuilder(s.Length);
			var pending = false;
			foreach (var c in s)
			{
				if (char.IsWhiteSpace(c)) { pending = true; continue; }
				if (pending && sb.Length > 0) sb.Append(' ');
				pending = false;
				sb.Append(c);
				if (sb.Length >= 300) break;
			}
			return sb.ToString();
		}

		private static string _GpuName(DeviceTypeMask gpu) =>
			_GpuBackends.FirstOrDefault(b => b.Mask == gpu).Name ?? gpu.ToString();

		/// <summary>
		/// The environment a failure record was written in. Only the signature line is compared - the
		/// rest of the file is notes for humans and must not affect the retry decision.
		/// Files written before this format existed have no signature line and read as empty,
		/// which makes them stale, which is what we want.
		/// </summary>
		private static string _RecordedSignature(string path)
		{
			try
			{
				foreach (var line in File.ReadAllLines(path))
				{
					if (line.StartsWith(_SignatureKey, StringComparison.Ordinal))
					{
						return line.Substring(_SignatureKey.Length).Trim();
					}
				}
			}
			catch (Exception) { }
			return string.Empty;
		}

		/// <summary>
		/// Identifies the GPU environment: changes when a GPU is swapped, a display driver is
		/// updated, or Rhino itself is updated. Windows-only detail; on other platforms this
		/// comes down to the Rhino version.
		/// </summary>
		private static string GpuEnvironmentSignature
		{
			get
			{
				var parts = new List<string> { RhinoApp.Version.ToString() };
				try
				{
					foreach (var gpu in DisplayDeviceInfo.GpuDeviceInfos())
					{
						parts.Add(string.Format("{0};{1};{2}", gpu.Name, gpu.Vendor, gpu.DriverDateAsString));
					}
				}
				catch (Exception) { }
				return string.Join("|", parts);
			}
		}

		/// <summary>
		/// Drop the failure records written in a different GPU environment, giving those backends one
		/// more chance. Without this a backend that failed once - a new card on a driver too old
		/// for it, say - stays off forever, even after the driver that would work is installed.
		/// A backend that fails again is disabled again, stamped with the new environment, so
		/// this costs one failed start per change and not one per session. RH-98701.
		/// </summary>
		/// <returns>The backends that were re-enabled.</returns>
		public static DeviceTypeMask ClearStaleGpuDisables()
		{
			DeviceTypeMask cleared = 0;
			if (DisabledGpus == 0) return cleared;
			var current = GpuEnvironmentSignature;
			foreach (var (mask, _, _) in _GpuBackends)
			{
				var f = _DisabledGpuFile(mask);
				if (f == null || !File.Exists(f)) continue;
				try
				{
					if (string.Equals(_RecordedSignature(f), current, StringComparison.Ordinal)) continue;
					File.Delete(f);
					cleared |= mask;
				}
				catch (Exception) { }
			}
			return cleared;
		}

		public static void EnableGpu(DeviceTypeMask gpu)
		{
			var f = _DisabledGpuFile(gpu);
			if (f != null && File.Exists(f))
			{
				File.Delete(f);
			}
		}
	}
}
