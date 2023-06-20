using ccl;
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

namespace RhinoCyclesCore
{
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

		public static (bool Success, float4 Result, bool IsOn, float Amount) HandleTexturedColor(RenderMaterial rm, string slotname, CyclesTextureImage tex, Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			bool success = false;
			float4 rc = new float4(0.0f);
			bool onness = false;
			float amount = 0.0f;

			if(bitmapConverter==null) bitmapConverter = new Converters.BitmapConverter();

			if (rm.Fields.TryGetValue(slotname, out Color4f c))
			{
				rc = c.ToFloat4();
				success = true;
			}
			var texAmountConv = rm.GetChildSlotParameter(slotname, "texture-amount") as IConvertible;
			if (texAmountConv!=null)
			{
				float texamount = Convert.ToSingle(texAmountConv);
				amount = texamount / 100.0f;
			}

			var texOnnessConv = rm.GetChildSlotParameter(slotname, "texture-on") as IConvertible;
			if (texOnnessConv != null)
			{
				bool texon = Convert.ToBoolean(texOnnessConv);
				if(texon) {
					onness = texon;
					if (onness)
					{
						if (rm.FindChild(slotname) is RenderTexture rt)
						{
							HandleRenderTexture(rt, tex, true, false, bitmapConverter, docsrn, (rm as ICyclesMaterial)?.Gamma ?? 1.0f);
							tex.Amount = amount;
						}
					}
				}
			}

			return (success, rc, onness, amount);
		}

		public static (bool Success, float Result, bool IsOn, float Amount) HandleTexturedValue(RenderMaterial rm, string slotname, CyclesTextureImage tex, Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			bool success = false;
			float rc = 0.0f;
			bool onness = false;
			float amount = 0.0f;
			if (rm.Fields.TryGetValue(slotname, out float c))
			{
				rc = c;
				success = true;
			}
			var texAmountConv = rm.GetChildSlotParameter("base", "texture-amount") as IConvertible;
			if (texAmountConv!=null)
			{
				float texamount = Convert.ToSingle(texAmountConv);
				amount = texamount / 100.0f;
			}

			var texOnnessConv = rm.GetChildSlotParameter("base", "texture-on") as IConvertible;
			if (texOnnessConv != null)
			{
				bool texon = Convert.ToBoolean(texOnnessConv);
				if(texon) {
					onness = texon;
					if (onness)
					{
						if (rm.FindChild(slotname) is RenderTexture rt)
						{
							HandleRenderTexture(rt, tex, true, false, bitmapConverter, docsrn, (rm as ICyclesMaterial)?.Gamma ?? 1.0f );
							tex.Amount = amount;
						}
					}
				}
			}

			return (success, rc, onness, amount);
		}

		public static void HandleRenderTexture(RenderTexture rt, CyclesTextureImage tex, bool check_for_normal_map, bool is_leaf_bitmap, Converters.BitmapConverter bitmapConverter, uint docsrn, float gamma = 1.0f)
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

			var use_color_mask = false;
			{
				var use_mask = rt.GetParameter("has-transparent-color");
				if (use_mask != null)
				{
					use_color_mask = Convert.ToBoolean(use_mask);
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
				procedural = Procedural.CreateProcedural(rt, tex.TextureList, bitmapConverter, docsrn);
			}

			if (procedural != null)
			{
				tex.Procedural = procedural;
			}
			else
			{
				Field tf = rt.Fields.GetField("filename");
				var ofs = tf.GetValue<string>();
				var fs = "";
				RhinoDoc doc = rt.DocumentAssoc != null ? rt.DocumentAssoc : rt.DocumentOwner;
				if (doc != null)
				{
					fs = Rhino.Render.Utilities.FindFile(doc, ofs, true);
				} else
				{
					fs = ofs;
				}



				tex.Filename = string.IsNullOrEmpty(fs) ? null : fs;
				tex.Name = rid.ToString(CultureInfo.InvariantCulture);
				tex.EnvProjectionMode = envProjectionMode;
				tex.Transform = tt;
				tex.Repeat = repeat;
				tex.AlternateTiles = alternate;
				tex.MappingChannel = rt.GetMappingChannel();
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
		/// <param name="socketToConnectTo"></param>
		public static void PbrGraphForSlot<T>(Shader sh, TexturedValue<T> slot, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sockeToConnectTo, bool invert)
		{
			List<ccl.ShaderNodes.Sockets.ISocket> socks = new List<ccl.ShaderNodes.Sockets.ISocket>();
			socks.Add(sockeToConnectTo);
			PbrGraphForSlot(sh, slot, teximg, socks, invert);
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
		public static ISocket PbrGraphForSlot<T>(Shader sh, TexturedValue<T> slot, CyclesTextureImage teximg, List<ISocket> socks, bool invert)
		{
			Type t = typeof(T);
			ISocket valsock = null;
			if (t == typeof(float))
			{
				ValueNode vn = new ValueNode(sh, $"input_value_for_{slot.Name}_");
				sh.AddNode(vn);
				vn.Value = (float)(object)slot.Value;
				if (invert)
				{
					MathSubtract invval = new MathSubtract(sh, $"invert_value_for_{slot.Name}_");
					invval.ins.Value1.Value = 1.0f;
					sh.AddNode(invval);
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
				sh.AddNode(cn);
				cn.Value = ((Color4f)(object)slot.Value).ToFloat4();
				if (invert)
				{
					InvertNode invcol = new InvertNode(sh, $"invert_input_color_for_{slot.Name}_");
					invcol.ins.Fac.Value = 1.0f;
					sh.AddNode(invcol);
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
			return GraphForSlot(sh, valsock, slot.On, slot.Amount, teximg, socks, false, false, invert);
		}

		public static ISocket GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, List<ccl.ShaderNodes.Sockets.ISocket> socketsToConnectTo)
		{
			return GraphForSlot(sh, valueSocket, IsOn, amount, teximg, socketsToConnectTo, false, false, false);
		}

		public static ISocket GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, List<ccl.ShaderNodes.Sockets.ISocket> socketsToConnectTo, bool toBw)
		{
			return GraphForSlot(sh, valueSocket, IsOn, amount, teximg, socketsToConnectTo, toBw, false, false);
		}

		public static ISocket GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, List<ccl.ShaderNodes.Sockets.ISocket> socketsToConnectTo, bool toBw, bool normalMap)
		{
			return GraphForSlot(sh, valueSocket, IsOn, amount, teximg, socketsToConnectTo, toBw, normalMap, false);
		}

		public static ISocket GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, List<ccl.ShaderNodes.Sockets.ISocket> socketsToConnectTo, bool toBw, bool normalMap, bool invert)
		{
			ISocket alphaOut = null;
			if(IsOn && null != teximg && teximg.HasProcedural)
			{
				var texco = new RhinoTextureCoordinateNode(sh, $"texco for input {valueSocket?.Parent.VariableName ?? "unknown input"}");
				var invcol = new InvertNode(sh, $"invert color for imtexnode for {valueSocket?.Parent.VariableName ?? "unknown input"}");
				var normalmapnode = new NormalMapNode(sh, $"Normal map node for {valueSocket?.Parent.VariableName ?? "unknown input"}");
				var tobwnode = new RgbToBwNode(sh, $"convert imtexnode to bw for {valueSocket?.Parent.VariableName ?? "unknown input"}");
				var alphamult = new MathMultiply(sh, $"alpha multiplier for {valueSocket?.Parent.VariableName ?? "unknown input"}");

				var mixerNode = new MixNode(sh, $"rgb mix node for imtexnode and {valueSocket?.Parent.VariableName ?? "unknown input"}");

				alphamult.ins.Value1.Value = Math.Min(1.0f, Math.Max(0.0f, amount));
				alphamult.ins.Value2.Value = 1.0f;

				mixerNode.ins.Fac.Value = Math.Min(1.0f, Math.Max(0.0f, amount));

				sh.AddNode(texco);
				sh.AddNode(mixerNode);

				var gamma_node = new GammaNode(sh);
				sh.AddNode(gamma_node);

				gamma_node.ins.Gamma.Value = 2.2f;

				var alpha_node = new MathNode(sh);
				sh.AddNode(alpha_node);
				alpha_node.Operation = MathNode.Operations.Add;
				alpha_node.ins.Value1.Value = 0.0f;
				alpha_node.ins.Value2.Value = 1.0f;

				VectorSocket uv_output_socket = RenderEngine.GetProjectionModeOutputSocket(teximg, texco);

				ColorSocket color_input_node = mixerNode.ins.Color2;
				FloatSocket alpha_input_node = alpha_node.ins.Value2;

				teximg.Procedural.CreateAndConnectProceduralNode(sh, uv_output_socket, color_input_node, alpha_input_node);

				alphaOut = alpha_node.outs.Value;

				texco.UvMap = teximg.GetUvMapForChannel();
				normalmapnode.Attribute = teximg.GetUvMapForChannel();

				valueSocket?.Connect(mixerNode.ins.Color1);

				if (valueSocket == null) {
					mixerNode.ins.Fac.Value = 1.0f;
				} else {
					sh.AddNode(alphamult);
					alphamult.outs.Value.Connect(mixerNode.ins.Fac);
				}
				if (normalMap)
				{
					// ideally we calculate the tangents and switch to Tangent space here.
					normalmapnode.SpaceType = ccl.ShaderNodes.NormalMapNode.Space.Tangent;
					sh.AddNode(normalmapnode);
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
						sh.AddNode(invcol);
						gamma_node.outs.Color.Connect(invcol.ins.Color);

						invcol.ins.Fac.Value = 1.0f;
						invcol.outs.Color.Connect(gamma_node.ins.Color);
					}
					else
					{
						ccl.ShaderNodes.Sockets.ISocket outsock = mixerNode.outs.Color;
						if(toBw) {
							sh.AddNode(tobwnode);
							outsock.Connect(tobwnode.ins.Color);
							outsock = tobwnode.outs.Val;
						}
						outsock.Connect(gamma_node.ins.Color);
					}
					if (amount >= 0.0f && amount <= 1.0f)
					{
						foreach (var sock in socketsToConnectTo)
						{
							gamma_node.outs.Color.Connect(sock);
						}
					} else { // multiply the output of mixerNode.outs.Color with amount.
						SeparateRgbNode separateRgbNode = new SeparateRgbNode(sh, $"separating the color for multiplication {valueSocket?.Parent.VariableName ?? "unknown input"}");
						MathMultiply multiplyR = new MathMultiply(sh, $"multiplier for R {valueSocket?.Parent.VariableName ?? "unknown input"}");
						MathMultiply multiplyG = new MathMultiply(sh, $"multiplier for G {valueSocket?.Parent.VariableName ?? "unknown input"}");
						MathMultiply multiplyB = new MathMultiply(sh, $"multiplier for B {valueSocket?.Parent.VariableName ?? "unknown input"}");
						CombineRgbNode combineRgbNode = new CombineRgbNode(sh, $"combining the new color values {valueSocket?.Parent.VariableName ?? "unknown input"}");

						sh.AddNode(separateRgbNode);
						sh.AddNode(multiplyR);
						sh.AddNode(multiplyG);
						sh.AddNode(multiplyB);
						sh.AddNode(combineRgbNode);

						multiplyR.UseClamp = false;
						multiplyG.UseClamp = false;
						multiplyB.UseClamp = false;

						multiplyR.ins.Value1.Value = amount;
						multiplyG.ins.Value1.Value = amount;
						multiplyB.ins.Value1.Value = amount;

						gamma_node.outs.Color.Connect(separateRgbNode.ins.Image);

						separateRgbNode.outs.R.Connect(multiplyR.ins.Value2);
						separateRgbNode.outs.G.Connect(multiplyG.ins.Value2);
						separateRgbNode.outs.B.Connect(multiplyB.ins.Value2);

						multiplyR.outs.Value.Connect(combineRgbNode.ins.R);
						multiplyG.outs.Value.Connect(combineRgbNode.ins.G);
						multiplyB.outs.Value.Connect(combineRgbNode.ins.B);

						foreach (var sock in socketsToConnectTo)
						{
							combineRgbNode.outs.Image.Connect(sock);
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
