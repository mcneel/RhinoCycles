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
	public static class Utilities
	{
		public static void TexturedSlot(RenderMaterial rm, string slotname, Color4f defaultColor, string prompt)
		{
			rm.Fields.AddTextured(slotname, defaultColor, prompt);
			var textureOn = rm.Fields.Add($"{slotname}-texture-on", false);
			rm.BindParameterToField(slotname, "texture-on", textureOn, ChangeContexts.UI);
			var textureamount = rm.Fields.Add($"{slotname}-texture-amount", 100.0);
			rm.BindParameterToField(slotname, "texture-amount", textureamount, ChangeContexts.UI);
		}

		public static void TexturedSlot(RenderMaterial rm, string slotname, float defaultValue, string prompt)
		{
			rm.Fields.AddTextured(slotname, defaultValue, prompt);
			var baseTextureOn = rm.Fields.Add($"{slotname}-texture-on", false);
			rm.BindParameterToField(slotname, "texture-on", baseTextureOn, ChangeContexts.UI);
			var baseTextureAmount = rm.Fields.Add($"{slotname}-texture-amount", 100.0);
			rm.BindParameterToField(slotname, "texture-amount", baseTextureAmount, ChangeContexts.UI);
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
			if (rm.Fields.TryGetValue($"{slotname}-texture-on", out bool texon))
			{
				onness = texon;
				if (onness)
				{
					if (rm.FindChild(slotname) is RenderTexture rt)
					{
						HandleRenderTexture(rm as ICyclesMaterial, rt, tex);
					}
				}
			}
			if (rm.Fields.TryGetValue($"{slotname}-texture-amount", out float texamount))
			{
				amount = texamount;
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
			if (rm.Fields.TryGetValue($"{slotname}-texture-on", out bool texon))
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
			if (rm.Fields.TryGetValue($"{slotname}-texture-amount", out float texamount))
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
			if (rm.Fields.TryGetValue($"{slotname}-texture-on", out bool texon))
			{
				onness = texon;
				if (onness)
				{
					if (rm.FindChild(slotname) is RenderTexture rt)
					{
						HandleRenderTexture(rm as ICyclesMaterial, rt, tex);
					}
				}
			}
			if (rm.Fields.TryGetValue($"{slotname}-texture-amount", out float texamount))
			{
				amount = texamount;
			}

			return new Tuple<bool, float, bool, float>(success, rc, onness, amount);
		}


		public static void HandleRenderTexture(ICyclesMaterial rm, RenderTexture rt, CyclesTextureImage tex)
		{
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
						img.ApplyGamma(rm.Gamma);
						tex.TexFloat = img.Data;
						tex.TexByte = null;
					}
					else
					{
						var img = Converters.BitmapConverter.RetrieveBytesImg(rid, pwidth, pheight, eval, linear, imgbased, canuse);
						img.ApplyGamma(rm.Gamma);
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
		public static void GraphForSlot(Shader sh, bool IsOn, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco)
		{
			GraphForSlot(sh, IsOn, teximg, sock, texco, true, false, false);
		}

		public static void GraphForSlot(Shader sh, bool IsOn, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco, bool toBw)
		{
			GraphForSlot(sh, IsOn, teximg, sock, texco, toBw, false, false);
		}

		public static void GraphForSlot(Shader sh, bool IsOn, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco, bool toBw, bool normalMap)
		{
			GraphForSlot(sh, IsOn, teximg, sock, texco, toBw, normalMap, false);
		}

		public static void GraphForSlot(Shader sh, bool IsOn, CyclesTextureImage teximg, ccl.ShaderNodes.Sockets.ISocket sock, ccl.ShaderNodes.TextureCoordinateNode texco, bool toBw, bool normalMap, bool invert)
		{
			if (IsOn && teximg.HasTextureImage)
			{
				var imtexnode = new ccl.ShaderNodes.ImageTextureNode();
				var invcol = new ccl.ShaderNodes.InvertNode();
				var normalmapnode = new ccl.ShaderNodes.NormalMapNode();
				sh.AddNode(imtexnode);
				RenderEngine.SetTextureImage(imtexnode, teximg);
				imtexnode.Extension = teximg.Repeat ? ccl.ShaderNodes.TextureNode.TextureExtension.Repeat : ccl.ShaderNodes.TextureNode.TextureExtension.Clip;
				imtexnode.ColorSpace = ccl.ShaderNodes.TextureNode.TextureColorSpace.None;
				imtexnode.Projection = ccl.ShaderNodes.TextureNode.TextureProjection.Flat;
				RenderEngine.SetProjectionMode(sh, teximg, imtexnode, texco);
				if (normalMap)
				{
					sh.AddNode(normalmapnode);
					imtexnode.outs.Color.Connect(normalmapnode.ins.Color);
					normalmapnode.outs.Normal.Connect(sock);
				}
				else
				{
					if (invert)
					{
						sh.AddNode(invcol);
						imtexnode.outs.Color.Connect(invcol.ins.Color);
						invcol.ins.Fac.Value = 1.0f;
						invcol.outs.Color.Connect(sock);
					}
					else
					{
						imtexnode.outs.Color.Connect(sock);
					}
				}
			}
		}
	}
}
