using ccl;
using sdd = System.Diagnostics.Debug;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Settings;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.ExtensionMethods;
using RhinoCyclesCore.Materials;
using System;
using System.IO;
using System.Globalization;
using static Rhino.Render.RenderContent;
using Pbr = Rhino.Render.PhysicallyBasedMaterial.ParametersNames;
using Rhino.Runtime.InteropWrappers;
using Rhino.Runtime;
using System.Collections.Generic;
using ccl.ShaderNodes;
using ccl.ShaderNodes.Sockets;
using Rhino.Render.Fields;
using System.Linq;

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
		public static ISocket PbrGraphForSlot<T>(Shader sh, TexturedValue<T> slot, CyclesTextureImage teximg, List<ISocket> socks, bool invert, float gamma, bool IsData)
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
				GammaNode gammaNode = null;
				if (!IsData)
				{
					gammaNode = new GammaNode(sh, "gamma node for color channel");
					gammaNode.ins.Gamma.Value = gamma;
					cn.outs.Color.Connect(gammaNode.ins.Color);
					valsock = gammaNode.outs.Color;
				}
				else
				{
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
			}
			if(valsock == null) {
				throw new UnrecognizedTexturedSlotType($"Type tried is {t}");
			}
			return GraphForSlot(sh, valsock, slot.On, slot.Amount, teximg, socks, false, false, invert, IsData, gamma);
		}

		public static ISocket GraphForSlot(Shader sh, ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, List<ISocket> socketsToConnectTo, bool toBw, bool normalMap, bool invert, bool IsData, float gamma)
		{
			ISocket alphaOut = null;
			if(IsOn && null != teximg && teximg.HasProcedural)
			{
				var texco = new RhinoTextureCoordinateNode(sh, $"texco for input {valueSocket?.Parent.VariableName ?? "unknown input"}");
				var mixerNode = new MixNode(sh, $"rgb mix node for imtexnode and {valueSocket?.Parent.VariableName ?? "unknown input"}");

				mixerNode.ins.Fac.Value = Math.Min(1.0f, Math.Max(0.0f, amount));

				var alpha_node = new MathAdd(sh);
				alpha_node.ins.Value1.Value = 0.0f;
				alpha_node.ins.Value2.Value = 1.0f;

				VectorSocket uv_output_socket = RenderEngine.GetProjectionModeOutputSocket(sh, teximg.Procedural.ProjectionMode, teximg.Procedural.EnvironmentMappingMode, texco);

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

				teximg.Procedural.CreateAndConnectProceduralNode(sh, uv_output_socket, color_input_node, alphaNodes, IsData);

				// 2023-10-17 David E.
				// Don't apply pre-gamma if the procedural is a bitmap texture, because Cycles will already
				// output the expected linearized color values. This code is now the same as the logic for
				// shader procedurals (see the out-parameter 'out bool apply_gamma' in GL33_Shading.inc.glsl).
				// Fixes RH-77715.
				if (!IsData && !(teximg.Procedural is BitmapTextureProcedural))
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
				foreach (var sock in socketsToConnectTo)
				{
					valueSocket?.Connect(sock);
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
		}

		public static void DisableGpus()
		{
			if(!File.Exists(_DisableGpusFile))
			{
				File.Create(_DisableGpusFile);
			}
		}
	}
}
