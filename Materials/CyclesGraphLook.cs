/**
Copyright 2026 Robert McNeel and Associates

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
using System;
using System.Globalization;
using System.Linq;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// What a Cycles graph looks like as a physically based material, for display modes that do
	/// not render Cycles: the shader on Surface read as base colour, metallic, roughness and so
	/// on, with mixes blended. Colours are linear, as in the graph.
	/// </summary>
	internal sealed class CyclesGraphLook
	{
		public float[] BaseColor = { 0.8f, 0.8f, 0.8f };
		public double Metallic;
		public double Roughness = 0.5;
		public double Alpha = 1.0;
		public double Transmission;
		public double Ior = 1.5;
		public double Coat;
		public double CoatRoughness = 0.03;
		public double Sheen;
		public double Subsurface;
		public float[] Emission = { 0f, 0f, 0f };

		/// <summary>
		/// The image feeding the base colour, if there is one.
		/// </summary>
		public string BaseColorImage;

		private const int MaxDepth = 32;

		/// <summary>
		/// The look of the shader linked into the output's Surface.
		/// </summary>
		internal static CyclesGraphLook FromGraph(CyclesGraphXml graph)
		{
			var surface = graph.Source("output", "surface", "Surface");
			return surface.HasValue ? Closure(graph, surface.Value.Node, 0) : new CyclesGraphLook();
		}

		private static CyclesGraphLook Closure(CyclesGraphXml graph, string node, int depth)
		{
			var look = new CyclesGraphLook();
			if (depth > MaxDepth)
				return look;

			float[] Col(string socket) => Colour(graph, node, socket, depth, look);
			double Num(string socket) => Number(graph, node, socket);

			switch (graph.NodeType(node))
			{
				case "principled_bsdf":
					look.BaseColor = Col("base_color");
					look.Metallic = Num("metallic");
					look.Roughness = Num("roughness");
					look.Ior = Num("ior");
					look.Alpha = Num("alpha");
					look.Transmission = Num("transmission_weight");
					look.Coat = Num("coat_weight");
					look.CoatRoughness = Num("coat_roughness");
					look.Sheen = Num("sheen_weight");
					look.Subsurface = Num("subsurface_weight");
					look.Emission = Scale(Col("emission_color"), Num("emission_strength"));
					break;
				case "diffuse_bsdf":
				case "translucent_bsdf":
				case "toon_bsdf":
					look.BaseColor = Col("color");
					look.Roughness = 1.0;
					break;
				case "glossy_bsdf":
				case "metallic_bsdf":
					look.BaseColor = Col("color");
					look.Metallic = 1.0;
					look.Roughness = Num("roughness");
					break;
				case "glass_bsdf":
				case "refraction_bsdf":
					look.BaseColor = Col("color");
					look.Roughness = Num("roughness");
					look.Ior = Num("IOR");
					look.Transmission = 1.0;
					break;
				case "sheen_bsdf":
					look.BaseColor = Col("color");
					look.Sheen = 1.0;
					break;
				case "subsurface_scattering":
					look.BaseColor = Col("color");
					look.Subsurface = 1.0;
					break;
				case "transparent_bsdf":
				case "holdout":
					look.Alpha = 0.0;
					break;
				case "emission":
				case "background_shader":
					look.BaseColor = new[] { 0f, 0f, 0f };
					look.Emission = Scale(Col("color"), Num("strength"));
					break;
				case "mix_closure":
				{
					var fac = Math.Max(0.0, Math.Min(1.0, Num("fac")));
					return Blend(Input(graph, node, "closure1", depth), Input(graph, node, "closure2", depth), 1.0 - fac, fac);
				}
				case "add_closure":
				{
					var sum = Blend(Input(graph, node, "closure1", depth), Input(graph, node, "closure2", depth), 0.5, 0.5);
					sum.Emission = Scale(sum.Emission, 2.0);
					return sum;
				}
			}
			return look;
		}

		/// <summary>
		/// The look of the closure linked into an input, or nothing (transparent) if none is.
		/// </summary>
		private static CyclesGraphLook Input(CyclesGraphXml graph, string node, string socket, int depth)
		{
			var source = graph.Source(node, socket, socket);
			return source.HasValue ? Closure(graph, source.Value.Node, depth + 1) : new CyclesGraphLook { Alpha = 0.0 };
		}

		private static CyclesGraphLook Blend(CyclesGraphLook a, CyclesGraphLook b, double wa, double wb)
		{
			double L(double x, double y) => wa * x + wb * y;
			float[] C(float[] x, float[] y) => new[] { (float)L(x[0], y[0]), (float)L(x[1], y[1]), (float)L(x[2], y[2]) };

			return new CyclesGraphLook
			{
				// A transparent side tints nothing.
				BaseColor = a.Alpha <= 0.0 ? b.BaseColor : b.Alpha <= 0.0 ? a.BaseColor : C(a.BaseColor, b.BaseColor),
				Metallic = L(a.Metallic, b.Metallic),
				Roughness = L(a.Roughness, b.Roughness),
				Alpha = L(a.Alpha, b.Alpha),
				Transmission = L(a.Transmission, b.Transmission),
				Ior = Math.Max(a.Ior, b.Ior),
				Coat = L(a.Coat, b.Coat),
				CoatRoughness = L(a.CoatRoughness, b.CoatRoughness),
				Sheen = L(a.Sheen, b.Sheen),
				Subsurface = L(a.Subsurface, b.Subsurface),
				Emission = C(a.Emission, b.Emission),
				BaseColorImage = a.BaseColorImage ?? b.BaseColorImage,
			};
		}

		/// <summary>
		/// A colour input: its value, or an estimate of what the node linked into it gives. An
		/// image is noted for the display material instead.
		/// </summary>
		private static float[] Colour(CyclesGraphXml graph, string node, string socket, int depth, CyclesGraphLook look)
		{
			var source = graph.Source(node, socket, socket);
			if (!source.HasValue || depth > MaxDepth)
				return Numbers(graph.Value(node, socket) ?? Default(graph, node, socket), 3, 0.8f);

			var from = source.Value.Node;
			float[] Col(string s) => Colour(graph, from, s, depth + 1, look);

			switch (graph.NodeType(from))
			{
				case "image_texture":
				case "environment_texture":
					look.BaseColorImage = look.BaseColorImage ?? graph.Value(from, "filename");
					return new[] { 1f, 1f, 1f };
				case "color":
					return Numbers(graph.Value(from, "value"), 3, 0.8f);
				case "rgb_ramp":
					return Average(Numbers(graph.Value(from, "ramp"), 0, 0.5f));
				case "checker_texture":
				case "brick_texture":
					return Mix(Col("color1"), Col("color2"), 0.5);
				case "mix":
					return Mix(Col("color1"), Col("color2"), Clamp(Number(graph, from, "fac")));
				case "mix_color":
					return Mix(Col("a"), Col("b"), Clamp(Number(graph, from, "fac")));
				case "hsv":
				case "invert":
				case "gamma":
				case "brightness_contrast":
				case "rgb_curves":
				case "normal_map":
					return Col(graph.NodeType(from) == "rgb_curves" ? "value" : "color");
				default:
					// Procedural textures and the like: the input's own value is the best guess.
					return Numbers(graph.Value(node, socket) ?? Default(graph, node, socket), 3, 0.8f);
			}
		}

		/// <summary>
		/// A number input: its value, or a Value node's, else Cycles' default.
		/// </summary>
		private static double Number(CyclesGraphXml graph, string node, string socket)
		{
			var source = graph.Source(node, socket, socket);
			if (source.HasValue && graph.NodeType(source.Value.Node) == "value")
				return Numbers(graph.Value(source.Value.Node, "value"), 1, 0f)[0];

			return Numbers(graph.Value(node, socket) ?? Default(graph, node, socket), 1, 0f)[0];
		}

		private static string Default(CyclesGraphXml graph, string node, string socket)
		{
			return ShaderNodeTypes.Find(graph.NodeType(node))?.Inputs
				.FirstOrDefault(s => string.Equals(s.Name, socket, StringComparison.OrdinalIgnoreCase))?.Default;
		}

		/// <summary>
		/// The numbers in an attribute; count 0 reads them all. Missing ones are the fallback.
		/// </summary>
		private static float[] Numbers(string text, int count, float fallback)
		{
			var tokens = (text ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var numbers = new float[count > 0 ? count : tokens.Length];
			for (var i = 0; i < numbers.Length; i++)
				numbers[i] = i < tokens.Length && float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : fallback;
			return numbers;
		}

		private static float[] Average(float[] rgb)
		{
			var count = rgb.Length / 3;
			if (count == 0)
				return new[] { 0.5f, 0.5f, 0.5f };

			var sum = new float[3];
			for (var i = 0; i < count * 3; i++)
				sum[i % 3] += rgb[i];
			return sum.Select(s => s / count).ToArray();
		}

		private static float[] Mix(float[] a, float[] b, double t)
		{
			return new[] { (float)(a[0] + t * (b[0] - a[0])), (float)(a[1] + t * (b[1] - a[1])), (float)(a[2] + t * (b[2] - a[2])) };
		}

		private static float[] Scale(float[] c, double s)
		{
			return new[] { (float)(c[0] * s), (float)(c[1] * s), (float)(c[2] * s) };
		}

		private static double Clamp(double x)
		{
			return Math.Max(0.0, Math.Min(1.0, x));
		}
	}
}
