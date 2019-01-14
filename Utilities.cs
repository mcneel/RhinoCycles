using ccl;
using Rhino.Display;
using Rhino.Render;
using RhinoCyclesCore.ExtensionMethods;
using RhinoCyclesCore.Materials;
using System;
using System.Globalization;
using static Rhino.Render.RenderContent;

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
		public static void TexturedSlot(RenderMaterial rm, string slotname, Color4f defaultColor, string prompt)
		{
			rm.Fields.AddTextured(slotname, defaultColor, prompt);
		}

		public static void TexturedSlot(RenderMaterial rm, string slotname, float defaultValue, string prompt)
		{
			rm.Fields.AddTextured(slotname, defaultValue, prompt);
		}
		public static Tuple<bool, float4, bool, float> HandleTexturedColor(RenderMaterial rm, string slotname, CyclesTextureImage tex)
		{
			bool success = false;
			float4 rc = new float4(0.0f);
			bool onness = false;
			float amount = 0.0f;
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
							HandleRenderTexture(rt, tex, (rm as ICyclesMaterial)?.Gamma ?? 1.0f);
							tex.Amount = amount;
						}
					}
				}
			}

			return new Tuple<bool, float4, bool, float>(success, rc, onness, amount);
		}
		public static Tuple<bool, float4, bool, float, RenderMaterial> HandleMaterialSlot(RenderMaterial rm, string slotname)
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
			if (rm.Fields.TryGetValue($"{slotname}-on", out bool texon))
			{
				onness = texon;
				if (onness)
				{
					if (rm.FindChild(slotname) is RenderMaterial rt)
					{
						rmchild = rt;
					}
				}
			}
			if (rm.Fields.TryGetValue($"{slotname}-amount", out float texamount))
			{
				amount = texamount;
			}

			return new Tuple<bool, float4, bool, float, RenderMaterial>(success, rc, onness, amount, rmchild);
		}
		public static Tuple<bool, float, bool, float> HandleTexturedValue(RenderMaterial rm, string slotname, CyclesTextureImage tex)
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
							HandleRenderTexture(rt, tex, (rm as ICyclesMaterial)?.Gamma ?? 1.0f );
							tex.Amount = amount;
						}
					}
				}
			}

			return new Tuple<bool, float, bool, float>(success, rc, onness, amount);
		}


		public static void HandleRenderTexture(RenderTexture rt, CyclesTextureImage tex, float gamma = 1.0f)
		{
			if (rt == null) return;
			uint rid = rt.RenderHashWithoutLocalMapping;

			var rhinotfm = rt.LocalMappingTransform;

			var projectionMode = rt.GetProjectionMode();
			var envProjectionMode = rt.GetInternalEnvironmentMappingMode();
			var repeat = rt.GetWrapType() == TextureWrapType.Repeating;

			using (var textureEvaluator = rt.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
			{
				SimulatedTexture st = textureEvaluator == null ? rt.SimulatedTexture(RenderTexture.TextureGeneration.Disallow) : null;
				using (
					var eval = textureEvaluator ?? RenderTexture.NewBitmapTexture(st, rt.DocumentAssoc).CreateEvaluator(RenderTexture.TextureEvaluatorFlags.DisableLocalMapping))
				{
					var canuse = eval.Initialize();

					int pwidth;
					int pheight;

					if (!canuse)
					{
						pwidth = pheight = 1;
					}
					else
					{
						try
						{
							rt.PixelSize(out int width, out int height, out int depth);
							if (width == 0 || height == 0)
							{
								pwidth = pheight = 1024;
							}
							else
							{
								pwidth = width;
								pheight = height;
							}
						}
						catch
						{
							pwidth = pheight = 1024;
						}
					}


					Transform t = new Transform(
						rhinotfm.ToFloatArray(true)
						);
					var imgbased = rt.IsImageBased();
					var linear = rt.IsLinear();
					var isFloat = rt.IsHdrCapable();
					if (isFloat)
					{
						var img = Converters.BitmapConverter.RetrieveFloatsImg(rid, pwidth, pheight, eval, linear, imgbased, canuse);
						img.ApplyGamma(gamma);
						tex.TexFloat = img.Data;
						tex.TexByte = null;
					}
					else
					{
						var img = Converters.BitmapConverter.RetrieveBytesImg(rid, pwidth, pheight, eval, linear, imgbased, canuse);
						img.ApplyGamma(gamma);
						tex.TexByte = img.Data;
						tex.TexFloat = null;
					}
					tex.TexWidth = pwidth;
					tex.TexHeight = pheight;
					tex.Name = rid.ToString(CultureInfo.InvariantCulture);
					tex.IsLinear = linear;
					tex.ProjectionMode = projectionMode;
					tex.EnvProjectionMode = envProjectionMode;
					tex.Transform = t;
					tex.Repeat = repeat;
				}
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
		/// <param name="sock"></param>
		/// <param name="texco"></param>
		public static void PbrGraphForSlot<T>(Shader sh, TexturedValue<T> slot, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco)
		{
			Type t = typeof(T);
			ccl.ShaderNodes.Sockets.ISocket valsock = null;
			if (t == typeof(float))
			{
				ccl.ShaderNodes.ValueNode vn = new ccl.ShaderNodes.ValueNode($"input value for {slot.Name}");
				sh.AddNode(vn);
				vn.Value = (float)(object)slot.Value;
				valsock = vn.outs.Value;
			}
			else if (t == typeof(Color4f))
			{
				ccl.ShaderNodes.ColorNode cn = new ccl.ShaderNodes.ColorNode($"input color for {slot.Name}");
				sh.AddNode(cn);
				cn.Value = ((Color4f)(object)slot.Value).ToFloat4();
				valsock = cn.outs.Color;
			}
			if(valsock == null) {
				throw new UnrecognizedTexturedSlotType($"Type tried is {t}");
			}
			GraphForSlot(sh, valsock, slot.On, slot.Amount, teximg, sock, texco, false, slot.Name.Equals(Pbr.Normal));
		}

		public static void GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco)
		{
			GraphForSlot(sh, valueSocket, IsOn, amount, teximg, sock, texco, false, false, false);
		}

		public static void GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco, bool toBw)
		{
			GraphForSlot(sh, valueSocket, IsOn, amount, teximg, sock, texco, toBw, false, false);
		}

		public static void GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco, bool toBw, bool normalMap)
		{
			GraphForSlot(sh, valueSocket, IsOn, amount, teximg, sock, texco, toBw, normalMap, false);
		}

		public static void GraphForSlot(Shader sh, ccl.ShaderNodes.Sockets.ISocket valueSocket, bool IsOn, float amount, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco, bool toBw, bool normalMap, bool invert)
		{
			if (IsOn && teximg.HasTextureImage)
			{
				var imtexnode = new ccl.ShaderNodes.ImageTextureNode();
				var invcol = new ccl.ShaderNodes.InvertNode();
				var normalmapnode = new ccl.ShaderNodes.NormalMapNode();
				var tobwnode = new ccl.ShaderNodes.RgbToBwNode();

				var mixerNode = new ccl.ShaderNodes.MixNode();

				mixerNode.ins.Fac.Value = amount;

				sh.AddNode(mixerNode);
				sh.AddNode(imtexnode);

				valueSocket?.Connect(mixerNode.ins.Color1);
				if (valueSocket == null) mixerNode.ins.Fac.Value = 1.0f;

				RenderEngine.SetTextureImage(imtexnode, teximg);
				imtexnode.Extension = teximg.Repeat ? ccl.ShaderNodes.TextureNode.TextureExtension.Repeat : ccl.ShaderNodes.TextureNode.TextureExtension.Clip;
				imtexnode.ColorSpace = ccl.ShaderNodes.TextureNode.TextureColorSpace.None;
				imtexnode.Projection = ccl.ShaderNodes.TextureNode.TextureProjection.Flat;
				RenderEngine.SetProjectionMode(sh, teximg, imtexnode, texco);
				if (normalMap)
				{
					// ideally we calculate the tangents and switch to Tangent space here.
					normalmapnode.SpaceType = ccl.ShaderNodes.NormalMapNode.Space.Object;
					sh.AddNode(normalmapnode);
					imtexnode.outs.Color.Connect(normalmapnode.ins.Color);
					normalmapnode.ins.Strength.Value = amount;
					normalmapnode.outs.Normal.Connect(sock);
				}
				else
				{
					if (invert)
					{
						sh.AddNode(invcol);
						imtexnode.outs.Color.Connect(invcol.ins.Color);

						invcol.ins.Fac.Value = 1.0f;
						invcol.outs.Color.Connect(mixerNode.ins.Color2);

					}
					else
					{
						ccl.ShaderNodes.Sockets.ISocket outsock = imtexnode.outs.Color;
						if(toBw) {
							sh.AddNode(tobwnode);
							outsock.Connect(tobwnode.ins.Color);
							outsock = tobwnode.outs.Val;
						}
						outsock.Connect(mixerNode.ins.Color2);
					}
					mixerNode.outs.Color.Connect(sock);
				}
			}
			else
			{
				valueSocket?.Connect(sock);
			}
		}
	}
}
