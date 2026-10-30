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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using ccl;
using Eto.Drawing;
using Grasshopper2.Components;
using Grasshopper2.Data;
using Grasshopper2.Doc;
using Grasshopper2.Framework;
using Grasshopper2.Parameters;
using Grasshopper2.Parameters.Standard;
using Grasshopper2.Types.Colour;
using Grasshopper2.Types.Functions;
using Grasshopper2.Types.Functions.Standard;
using Rhino.Geometry;

namespace CyclesComponents
{
	/// <summary>
	/// Builds a Grasshopper 2 graph from Cycles XML: one node component per element, its
	/// attributes as input values, a wire per connect, ending in Material Output.
	/// </summary>
	internal static class GraphImport
	{
		private const float ColumnWidth = 300f;
		private const float RowHeight = 20f;

		internal sealed class Result
		{
			public Document Document;
			public CyclesMaterialOutput Output;
			public List<string> Problems = new List<string>();
		}

		internal static Result Import(string xml)
		{
			var result = new Result { Document = Document.NewInactiveDocument() };

			var elements = ParseElements(xml, result.Problems);
			if (elements == null)
				return result;

			var nodes = new Dictionary<string, (string Type, CyclesNodeComponent Component)>(StringComparer.Ordinal);
			var editors = new Dictionary<string, List<(IParameter Target, AbstractParameter Editor)>>(StringComparer.Ordinal);
			var connects = new List<(string FromNode, string FromSocket, string ToNode, string ToSocket)>();

			foreach (var element in elements)
			{
				var type = element.Name.LocalName;
				if (type == "connect")
				{
					if (SplitSocketRef((string)element.Attribute("from"), out var fn, out var fs) &&
					    SplitSocketRef((string)element.Attribute("to"), out var tn, out var ts))
						connects.Add((fn, fs, tn, ts));
					else
						result.Problems.Add("connect needs from=\"node socket\" and to=\"node socket\"");
					continue;
				}

				if (type == "output")
				{
					if (element.Attributes().Any(a => a.Name.LocalName != "name"))
						result.Problems.Add("Values on the output node are not imported.");
					continue;
				}

				// Name collision with the scene's background, as in cycles_xml.cpp.
				if (type == "background")
					type = "background_shader";

				var name = (string)element.Attribute("name") ?? $"{type}{nodes.Count}";
				var component = CreateNode(type, result.Problems);
				if (component == null)
					continue;

				// Tables are read whole below, not attribute by attribute.
				var nodeType = ShaderNodeTypes.Find(type);
				var hasRamp = nodeType.Inputs.Any(NodeCatalog.IsRamp);
				var curves = nodeType.Inputs.FirstOrDefault(s => NodeCatalog.CurveChannels(s) != null);
				foreach (var attribute in element.Attributes())
				{
					var socketName = attribute.Name.LocalName;
					if (socketName == "name" || socketName == curves?.Name || (hasRamp && (socketName == NodeCatalog.RampSocket || socketName == NodeCatalog.RampAlphaSocket)))
						continue;
					SetInput(component, type, socketName, attribute.Value, result.Problems);
				}

				var wired = new List<(IParameter Target, AbstractParameter Editor)>();
				if (hasRamp && element.Attribute(NodeCatalog.RampSocket) != null)
				{
					var ramp = ReadRamp(type, element, result.Problems);
					if (ramp != null && FindParameter(component, type, NodeCatalog.RampSocket, true) is GradientParameter rampInput)
					{
						rampInput.Set(ramp);
						wired.Add((rampInput, Editors.ForRamp(NodeCatalog.RampSocket, ramp)));
					}
				}

				if (curves != null && element.Attribute(curves.Name) != null)
				{
					var read = ReadCurves(nodeType, element, curves, result.Problems, out var minX, out var maxX);
					foreach (var (channel, curve) in read)
						if (FindInput(component, channel.Name) is FunctionParameter curveInput)
						{
							curveInput.Set(curve);
							wired.Add((curveInput, Editors.ForCurve(channel.Code, curve, minX, maxX)));
						}
				}

				if (wired.Count > 0)
					editors[name] = wired;
				nodes[name] = (type, component);
			}

			// Columns by depth, so data flows left to right. Editors get the column before their node.
			var depth = nodes.Keys.ToDictionary(n => n, n => editors.ContainsKey(n) ? 1 : 0, StringComparer.Ordinal);
			for (var pass = 0; pass < nodes.Count; pass++)
				foreach (var c in connects)
					if (depth.ContainsKey(c.FromNode) && depth.ContainsKey(c.ToNode) && depth[c.ToNode] < depth[c.FromNode] + 1)
						depth[c.ToNode] = depth[c.FromNode] + 1;

			// Then each node moves right to just before its nearest consumer, so wires stay short.
			var outputColumn = depth.Count == 0 ? 0 : depth.Values.Max() + 1;
			for (var pass = 0; pass < nodes.Count; pass++)
				foreach (var name in nodes.Keys)
				{
					var consumers = connects
						.Where(c => c.FromNode == name && (c.ToNode == "output" || depth.ContainsKey(c.ToNode)))
						.Select(c => c.ToNode == "output" ? outputColumn : depth[c.ToNode])
						.ToList();
					if (consumers.Count > 0)
						depth[name] = Math.Max(depth[name], consumers.Min() - 1);
				}

			// Objects stack down their column by height; the document places them by their centre.
			var nextTop = new Dictionary<int, float>();
			float Top(int column) => nextTop.TryGetValue(column, out var top) ? top : 50f;
			void Place(IDocumentObject obj, int column, float top, int rows)
			{
				var height = rows * RowHeight;
				result.Document.Objects.Add(obj, new PointF(50f + column * ColumnWidth, top + 0.5f * height));
				nextTop[column] = top + height + 2 * RowHeight;
			}

			foreach (var pair in nodes)
			{
				var column = depth[pair.Key];
				var nodeTop = Top(column);
				var component = pair.Value.Component;
				Place(component, column, nodeTop, Math.Max(component.Parameters.InputCount, component.Parameters.OutputCount) + 1);

				// Editors wired into their inputs, level with the node where the column allows.
				if (editors.TryGetValue(pair.Key, out var wired))
					foreach (var (target, editor) in wired)
					{
						Place(editor, column - 1, Math.Max(Top(column - 1), nodeTop), Editors.Rows(editor));
						Connections.Connect(editor, target);
					}
			}

			var output = (CyclesMaterialOutput)ObjectProxies.FindByType(typeof(CyclesMaterialOutput)).Emit();
			Place(output, outputColumn, 50f, output.Parameters.InputCount + 1);
			result.Output = output;

			foreach (var c in connects)
			{
				var target = c.ToNode == "output"
					? FindOutputInput(output, c.ToSocket)
					: nodes.TryGetValue(c.ToNode, out var to) ? FindParameter(to.Component, to.Type, c.ToSocket, true) : null;
				var source = nodes.TryGetValue(c.FromNode, out var from) ? FindParameter(from.Component, from.Type, c.FromSocket, false) : null;

				if (source == null || target == null)
				{
					result.Problems.Add($"Cannot connect {c.FromNode} {c.FromSocket} to {c.ToNode} {c.ToSocket}.");
					continue;
				}
				Connections.Connect(source, target);
			}

			return result;
		}

		/// <summary>
		/// The node and connect elements, unwrapping a &lt;shader&gt; block as Cycles' example scenes have.
		/// </summary>
		private static List<XElement> ParseElements(string xml, List<string> problems)
		{
			try
			{
				var body = Regex.Replace(xml ?? string.Empty, "<\\?xml[^>]*\\?>", string.Empty);
				var root = XElement.Parse("<root>" + body + "</root>");
				var shader = root.Descendants("shader").FirstOrDefault();
				return (shader ?? root).Elements().ToList();
			}
			catch (Exception ex)
			{
				problems.Add("The XML does not parse: " + ex.Message);
				return null;
			}
		}

		private static bool SplitSocketRef(string text, out string node, out string socket)
		{
			var parts = (text ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			node = parts.Length == 2 ? parts[0] : null;
			socket = parts.Length == 2 ? parts[1] : null;
			return parts.Length == 2;
		}

		private static CyclesNodeComponent CreateNode(string type, List<string> problems)
		{
			var proxy = ObjectProxies.FindById(NodeCatalog.ProxyId(type));
			if (proxy == null)
			{
				problems.Add(ShaderNodeTypes.Find(type) == null
					? $"'{type}' is not a Cycles shader node."
					: $"'{type}' has no Grasshopper component.");
				return null;
			}
			return proxy.Emit() as CyclesNodeComponent;
		}

		private static IParameter FindParameter(CyclesNodeComponent component, string type, string socketName, bool input)
		{
			var nodeType = ShaderNodeTypes.Find(type);
			if (nodeType == null)
				return null;

			var sockets = input ? NodeCatalog.VisibleInputs(nodeType).ToList() : nodeType.Outputs.ToList();
			var socket = sockets.FirstOrDefault(s => string.Equals(s.Name, socketName, StringComparison.OrdinalIgnoreCase));
			if (socket == null)
				return null;

			var name = NodeCatalog.ParameterName(socket, sockets);
			var count = input ? component.Parameters.InputCount : component.Parameters.OutputCount;
			for (var i = 0; i < count; i++)
			{
				var parameter = input ? component.Parameters.Input(i) : component.Parameters.Output(i);
				if (parameter.Nomen.Name == name)
					return parameter;
			}
			return null;
		}

		private static IParameter FindOutputInput(CyclesMaterialOutput output, string socketName)
		{
			for (var i = 0; i < output.Parameters.InputCount; i++)
			{
				var parameter = output.Parameters.Input(i);
				if (string.Equals(parameter.Nomen.Name, socketName, StringComparison.OrdinalIgnoreCase))
					return parameter;
			}
			return null;
		}

		private static void SetInput(CyclesNodeComponent component, string type, string socketName, string text, List<string> problems)
		{
			var socket = ShaderNodeTypes.Find(type)?.Inputs.FirstOrDefault(s => s.Name == socketName);
			var parameter = FindParameter(component, type, socketName, true);
			if (socket == null || parameter == null)
			{
				problems.Add($"{type} input '{socketName}' is not imported.");
				return;
			}

			var value = ParseValue(socket, text);
			if (value == null)
			{
				problems.Add($"{type} input '{socketName}': cannot read '{text}' as {socket.Type}.");
				return;
			}

			parameter.PersistentDataWeak = Garden.ITreeFromList(new[] { value });
		}

		/// <summary>
		/// An XML attribute as the Grasshopper value <see cref="CyclesValues"/> turns back into the same text.
		/// </summary>
		private static object ParseValue(ShaderNodeSocketInfo socket, string text)
		{
			var numbers = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (double?)d : null)
				.ToArray();
			bool AllNumbers(int count) => numbers.Length == count && numbers.All(n => n.HasValue);

			switch (socket.Type)
			{
				case "float":
					return AllNumbers(1) ? numbers[0].Value : null;
				case "int":
				case "uint":
					return AllNumbers(1) ? (object)(int)Math.Round(numbers[0].Value) : null;
				case "boolean":
					if (bool.TryParse(text, out var flag)) return flag;
					return AllNumbers(1) ? (object)(numbers[0].Value != 0.0) : null;
				case "color":
				case "color2":
					// Cycles colours are linear; Grasshopper colours are sRGB.
					return AllNumbers(3) ? Colour.FromRgb(LinearToSrgb(numbers[0].Value), LinearToSrgb(numbers[1].Value), LinearToSrgb(numbers[2].Value)) : null;
				case "point":
					return AllNumbers(3) ? new Point3d(numbers[0].Value, numbers[1].Value, numbers[2].Value) : null;
				case "vector":
				case "normal":
					return AllNumbers(3) ? new Vector3d(numbers[0].Value, numbers[1].Value, numbers[2].Value) : null;
				case "point2":
					return AllNumbers(2) ? new Point2d(numbers[0].Value, numbers[1].Value) : null;
				case "string":
				case "enum":
					return text;
				default:
					return null;
			}
		}

		private static double LinearToSrgb(double c)
		{
			c = Math.Max(0.0, Math.Min(1.0, c));
			return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
		}

		/* How far dropping a point may move a table; well below what eight-bit output shows. */
		private const double TableTolerance = 1e-3;

		/// <summary>
		/// rgb_ramp's tables as a gradient, with only the grips needed to reproduce them.
		/// Without interpolation each entry holds until the next, as Cycles reads it.
		/// </summary>
		private static GripGradient ReadRamp(string type, XElement element, List<string> problems)
		{
			var rgb = ReadNumbers((string)element.Attribute(NodeCatalog.RampSocket));
			var alphaText = (string)element.Attribute(NodeCatalog.RampAlphaSocket);
			var alpha = alphaText == null ? null : ReadNumbers(alphaText);
			var count = rgb == null ? 0 : rgb.Length / 3;
			if (count == 0 || rgb.Length % 3 != 0 || (alphaText != null && (alpha == null || alpha.Length != count)))
			{
				problems.Add($"{type} input '{NodeCatalog.RampSocket}': cannot read it as a colour table with an alpha table of the same length.");
				return null;
			}

			var samples = new double[count][];
			for (var i = 0; i < count; i++)
				samples[i] = new[] { rgb[3 * i], rgb[3 * i + 1], rgb[3 * i + 2], alpha?[i] ?? 1.0 };

			var interpolate = true;
			var interpolateText = (string)element.Attribute("interpolate");
			var interpolateSocket = ShaderNodeTypes.Find(type).Inputs.FirstOrDefault(s => s.Name == "interpolate");
			if (interpolateText != null && interpolateSocket != null && ParseValue(interpolateSocket, interpolateText) is bool flag)
				interpolate = flag;

			var grips = Simplify(samples, interpolate).Select(i => new GradientGrip(count == 1 ? 0.0 : i / (count - 1.0),
				Colour.FromRgbLinear(samples[i][0], samples[i][1], samples[i][2], (float)samples[i][3]))).ToArray();
			return new GripGradient(interpolate ? Progression.Linear : Progression.Immediate, Space3.RgbLinear, grips);
		}

		/// <summary>
		/// A curve node's table as a function per channel, with only the points needed to reproduce
		/// it. Straight channels change nothing and are left out, as is rgb_curves' combined curve,
		/// which the table has already applied.
		/// </summary>
		private static List<((string Name, string Code) Channel, Function Curve)> ReadCurves(ShaderNodeTypeInfo nodeType, XElement element, ShaderNodeSocketInfo socket, List<string> problems, out double minX, out double maxX)
		{
			double Setting(string socketName)
			{
				var text = (string)element.Attribute(socketName) ?? nodeType.Inputs.FirstOrDefault(s => s.Name == socketName)?.Default;
				return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0.0;
			}
			minX = Setting("min_x");
			maxX = Setting("max_x");

			var channels = NodeCatalog.CurveChannels(socket);
			var columns = channels.Length == 4 ? channels.Skip(1).ToArray() : channels;
			var numbers = ReadNumbers((string)element.Attribute(socket.Name));
			var count = numbers == null ? 0 : numbers.Length / columns.Length;
			var curves = new List<((string Name, string Code) Channel, Function Curve)>();
			if (count < 2 || numbers.Length % columns.Length != 0)
			{
				problems.Add($"{nodeType.Name} input '{socket.Name}': cannot read it as a table of {columns.Length} number(s) per entry.");
				return curves;
			}

			var x0 = minX;
			var dx = (maxX - minX) / (count - 1);
			for (var c = 0; c < columns.Length; c++)
			{
				var samples = Enumerable.Range(0, count).Select(i => new[] { numbers[i * columns.Length + c] }).ToArray();
				if (Enumerable.Range(0, count).All(i => Math.Abs(samples[i][0] - (x0 + i * dx)) <= TableTolerance))
					continue;

				var points = Simplify(samples, true).Select(i => new Point2d(x0 + i * dx, samples[i][0]));
				curves.Add((columns[c], new InterpolationFunction(InterpolationAlgorithm.Linear, points)));
			}
			return curves;
		}

		/// <summary>
		/// The entries of an evenly spaced table that reproduce it: those the straight line between
		/// their neighbours misses, or without interpolation, those that change the value.
		/// </summary>
		private static List<int> Simplify(double[][] samples, bool interpolate)
		{
			bool Differs(double[] a, double[] b) => a.Zip(b, (x, y) => Math.Abs(x - y)).Any(d => d > TableTolerance);

			// Whether every entry between two lies on the straight line joining them.
			bool Straight(int from, int to)
			{
				for (var k = from + 1; k < to; k++)
				{
					var t = (k - from) / (double)(to - from);
					if (Differs(samples[k], samples[from].Zip(samples[to], (a, b) => a + t * (b - a)).ToArray()))
						return false;
				}
				return true;
			}

			var keep = new List<int> { 0 };
			for (var i = 1; i < samples.Length; i++)
			{
				var needed = i == samples.Length - 1 || (interpolate ? !Straight(keep.Last(), i + 1) : Differs(samples[i], samples[keep.Last()]));
				if (needed)
					keep.Add(i);
			}
			return keep;
		}

		private static IParameter FindInput(CyclesNodeComponent component, string name)
		{
			for (var i = 0; i < component.Parameters.InputCount; i++)
				if (component.Parameters.Input(i).Nomen.Name == name)
					return component.Parameters.Input(i);
			return null;
		}

		private static double[] ReadNumbers(string text)
		{
			var numbers = new List<double>();
			foreach (var token in (text ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
					return null;
				numbers.Add(number);
			}
			return numbers.ToArray();
		}
	}
}
