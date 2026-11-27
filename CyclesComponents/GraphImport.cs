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
using LOC = Rhino.UI.LOC;

namespace CyclesComponents
{
	/// <summary>
	/// Builds a Grasshopper 2 graph from Cycles XML: one node component per element, its
	/// attributes as input values, a wire per connect, ending in Material Output.
	/// </summary>
	internal static class GraphImport
	{
		internal const float ColumnWidth = 300f;
		internal const float RowHeight = 20f;

		internal sealed class Result
		{
			public Document Document;
			public CyclesMaterialOutput Output;
			public List<string> Problems = new List<string>();

			// Inputs Cycles does not have, such as a newer Blender's, which no render reads either.
			public List<string> Ignored = new List<string>();
		}

		internal static Result Import(string xml)
		{
			var result = new Result { Document = Document.NewInactiveDocument() };

			var elements = ParseElements(xml, result.Problems);
			if (elements == null)
				return result;

			// Nodes in XML order. As ccycles reads it, a connect names the nodes defined before it,
			// the last of a name, so each is resolved where it stands.
			var nodes = new List<(string Type, CyclesNodeComponent Component)>();
			var named = new Dictionary<string, int>(StringComparer.Ordinal);
			var editors = new Dictionary<int, List<(IParameter Target, AbstractParameter Editor)>>();
			var connects = new List<(int From, string FromNode, string FromSocket, int To, string ToNode, string ToSocket)>();
			var positions = new Dictionary<int, PointF>();
			PointF? outputPosition = null;
			int Named(string node) => named.TryGetValue(node, out var index) ? index : NoNode;

			foreach (var element in elements)
			{
				var type = element.Name.LocalName;
				if (type == "connect")
				{
					if (SplitSocketRef((string)element.Attribute("from"), out var fn, out var fs) &&
					    SplitSocketRef((string)element.Attribute("to"), out var tn, out var ts))
						connects.Add((Named(fn), fn, fs, tn == "output" ? OutputNode : Named(tn), tn, ts));
					else
						result.Problems.Add(LOC.STR("connect needs from=\"node socket\" and to=\"node socket\""));
					continue;
				}

				// Layout only, so a position that does not read is no problem.
				var hasPosition = CyclesXmlWriter.TryParsePosition((string)element.Attribute(CyclesXmlWriter.PositionAttribute), out var position);

				if (type == "output")
				{
					if (element.Attributes().Any(a => a.Name.LocalName != "name" && a.Name.LocalName != CyclesXmlWriter.PositionAttribute))
						result.Problems.Add(LOC.STR("Values on the output node are not imported."));
					if (hasPosition)
						outputPosition = position;
					continue;
				}

				// Name collision with the scene's background, as in cycles_xml.cpp.
				if (type == "background")
					type = "background_shader";

				// A node that cannot be made leaves its name to an earlier one.
				var component = CreateNode(type, result.Problems);
				if (component == null)
					continue;

				var name = (string)element.Attribute("name");
				var hasName = !string.IsNullOrEmpty(name);
				if (hasName && named.ContainsKey(name))
					result.Problems.Add(string.Format(LOC.STR("Two nodes are named '{0}', so connects after the second use it."), name));
				if (hasName)
					named[name] = nodes.Count;

				// The XML's own name for the node, so the graph reads as it did.
				if (hasName)
					component.SetLabel(name);

				// ccycles starts a tangent on Rhino's UV map, which the XML can change.
				if (type == "tangent")
				{
					if (element.Attribute("direction_type") == null)
						SetInput(component, type, "direction_type", "uv_map", result);
					if (element.Attribute("attribute") == null)
						SetInput(component, type, "attribute", "uvmap1", result);
				}

				// Tables are read whole below, not attribute by attribute.
				var nodeType = ShaderNodeTypes.Find(type);
				var hasRamp = nodeType.Inputs.Any(NodeCatalog.IsRamp);
				var curves = nodeType.Inputs.FirstOrDefault(s => NodeCatalog.CurveChannels(s) != null);
				foreach (var attribute in element.Attributes())
				{
					var socketName = attribute.Name.LocalName;
					if (socketName == "name" || socketName == CyclesXmlWriter.PositionAttribute || socketName == curves?.Name || (hasRamp && (socketName == NodeCatalog.RampSocket || socketName == NodeCatalog.RampAlphaSocket)))
						continue;
					SetInput(component, type, socketName, attribute.Value, result);
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
						if (FindChannel(component, NodeCatalog.CurveChannels(curves), channel) is FunctionParameter curveInput)
						{
							curveInput.Set(curve);
							wired.Add((curveInput, Editors.ForCurve(channel.Code, curve, minX, maxX)));
						}
				}

				if (wired.Count > 0)
					editors[nodes.Count] = wired;
				if (hasPosition)
					positions[nodes.Count] = position;
				nodes.Add((type, component));
			}

			// Columns by depth, so data flows left to right. Editors get the column before their node.
			var depth = Enumerable.Range(0, nodes.Count).Select(n => editors.ContainsKey(n) ? 1 : 0).ToArray();
			if (nodes.Count > LayoutLimit)
			{
				// The passes below would take too long, so columns of nodes in XML order, with room for editors.
				for (var n = 0; n < nodes.Count; n++)
					depth[n] = 1 + 2 * (n / NodesPerColumn);
			}
			else
			{
				var links = connects.Where(c => c.From >= 0 && c.To >= 0).ToList();
				for (var pass = 0; pass < nodes.Count; pass++)
				{
					var deeper = false;
					foreach (var c in links)
						if (depth[c.To] < depth[c.From] + 1)
						{
							depth[c.To] = depth[c.From] + 1;
							deeper = true;
						}
					if (!deeper)
						break;
				}
			}

			// Then each node moves right to just before its nearest consumer, so wires stay short.
			var outputColumn = nodes.Count == 0 ? 0 : depth.Max() + 1;
			if (nodes.Count <= LayoutLimit)
			{
				var consumers = connects.Where(c => c.From >= 0 && (c.To >= 0 || c.To == OutputNode)).ToLookup(c => c.From, c => c.To);
				for (var pass = 0; pass < nodes.Count; pass++)
				{
					var moved = false;
					for (var n = 0; n < nodes.Count; n++)
					{
						if (!consumers.Contains(n))
							continue;
						var column = consumers[n].Min(to => to == OutputNode ? outputColumn : depth[to]) - 1;
						if (column > depth[n])
						{
							depth[n] = column;
							moved = true;
						}
					}
					if (!moved)
						break;
				}
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

			for (var n = 0; n < nodes.Count; n++)
			{
				var component = nodes[n].Component;
				editors.TryGetValue(n, out var wired);

				// Where the XML says, with its editors stacked below it, where Blender draws a ramp or curve.
				if (positions.TryGetValue(n, out var corner))
				{
					CyclesXmlWriter.AddAt(result.Document, component, corner);
					var editorTop = component.Attributes.Bounds.Bottom + RowHeight;
					foreach (var (target, editor) in wired ?? Enumerable.Empty<(IParameter, AbstractParameter)>())
					{
						CyclesXmlWriter.AddAt(result.Document, editor, new PointF(corner.X, editorTop));
						editorTop = editor.Attributes.Bounds.Bottom + RowHeight;
						Connections.Connect(editor, target);
					}
					continue;
				}

				var column = depth[n];
				var nodeTop = Top(column);
				Place(component, column, nodeTop, Math.Max(component.Parameters.InputCount, component.Parameters.OutputCount) + 1);

				// Editors wired into their inputs, level with the node where the column allows.
				foreach (var (target, editor) in wired ?? Enumerable.Empty<(IParameter, AbstractParameter)>())
				{
					Place(editor, column - 1, Math.Max(Top(column - 1), nodeTop), Editors.Rows(editor));
					Connections.Connect(editor, target);
				}
			}

			var output = (CyclesMaterialOutput)ObjectProxies.FindByType(typeof(CyclesMaterialOutput)).Emit();
			if (outputPosition.HasValue)
				CyclesXmlWriter.AddAt(result.Document, output, outputPosition.Value);
			else
				Place(output, outputColumn, 50f, output.Parameters.InputCount + 1);
			result.Output = output;

			// Refused as ccycles refuses them, in its order, so an input's first link is one it keeps.
			var connected = new HashSet<IParameter>();
			var upstream = nodes.Select(_ => new List<int>()).ToArray();
			foreach (var c in connects)
			{
				var target = c.To == OutputNode
					? FindOutputInput(output, c.ToSocket)
					: c.To >= 0 ? FindParameter(nodes[c.To].Component, nodes[c.To].Type, c.ToSocket, true) : null;
				var source = c.From >= 0 ? FindParameter(nodes[c.From].Component, nodes[c.From].Type, c.FromSocket, false) : null;

				if (source == null || target == null)
				{
					result.Problems.Add(string.Format(LOC.STR("Cannot connect {0} {1} to {2} {3}."), c.FromNode, c.FromSocket, c.ToNode, c.ToSocket));
					continue;
				}

				var fromType = SocketType(nodes[c.From].Type, c.FromSocket, false);
				var toType = SocketType(c.To == OutputNode ? "output" : nodes[c.To].Type, c.ToSocket, true);
				if (!CyclesNodeComponent.CanLink(fromType, toType))
				{
					result.Problems.Add(string.Format(LOC.STR("Cycles cannot connect {0} {1} ({2}) to {3} {4} ({5})."), c.FromNode, c.FromSocket, fromType, c.ToNode, c.ToSocket, toType));
					continue;
				}

				if (c.To >= 0 && TakesFrom(upstream, c.From, c.To))
				{
					result.Problems.Add(string.Format(LOC.STR("The connect from {0} {1} to {2} {3} would make a loop, so it is left out."), c.FromNode, c.FromSocket, c.ToNode, c.ToSocket));
					continue;
				}

				// Cycles keeps an input's first link.
				if (!connected.Add(target))
				{
					result.Problems.Add(string.Format(LOC.STR("{0} {1} is connected already, so the connect from {2} {3} is left out."), c.ToNode, c.ToSocket, c.FromNode, c.FromSocket));
					continue;
				}

				Connections.Connect(source, target);
				if (c.To >= 0)
					upstream[c.To].Add(c.From);
			}

			return result;
		}

		/// <summary>
		/// The type of a node's socket, or null if it has no such socket.
		/// </summary>
		private static string SocketType(string type, string socketName, bool input)
		{
			var nodeType = ShaderNodeTypes.Find(type);
			var sockets = nodeType == null ? null : input ? nodeType.Inputs : nodeType.Outputs;
			return sockets?.FirstOrDefault(s => string.Equals(s.Name, socketName, StringComparison.OrdinalIgnoreCase))?.Type;
		}

		/// <summary>
		/// Whether a node takes anything from another, directly or through other nodes, as
		/// shader_xml.cpp's takes_from does. A link from the first into the second would close a loop.
		/// </summary>
		private static bool TakesFrom(List<int>[] upstream, int node, int target)
		{
			var pending = new Stack<int>();
			var seen = new HashSet<int>();
			pending.Push(node);
			while (pending.Count > 0)
			{
				var current = pending.Pop();
				if (current == target)
					return true;
				if (!seen.Add(current))
					continue;
				foreach (var feeding in upstream[current])
					pending.Push(feeding);
			}
			return false;
		}

		/* What a connect names, other than a node by its index. */
		private const int NoNode = -1;
		private const int OutputNode = -2;

		/* Beyond so many nodes, the layout puts them in columns as they come, of so many each. */
		private const int LayoutLimit = 300;
		private const int NodesPerColumn = 50;

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
				problems.Add(string.Format(LOC.STR("The XML does not parse: {0}"), ex.Message));
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
				problems.Add(string.Format(ShaderNodeTypes.Find(type) == null
					? LOC.STR("'{0}' is not a Cycles shader node.")
					: LOC.STR("'{0}' has no Grasshopper component."), type));
				return null;
			}
			return proxy.Emit() as CyclesNodeComponent;
		}

		internal static IParameter FindParameter(CyclesNodeComponent component, string type, string socketName, bool input)
		{
			var nodeType = ShaderNodeTypes.Find(type);
			if (nodeType == null)
				return null;

			var sockets = input ? NodeCatalog.VisibleInputs(nodeType).ToList() : nodeType.Outputs.ToList();
			var socket = sockets.FirstOrDefault(s => string.Equals(s.Name, socketName, StringComparison.OrdinalIgnoreCase));
			return socket == null ? null : component.FindParameter(socket, sockets, input);
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

		private static void SetInput(CyclesNodeComponent component, string type, string socketName, string text, Result result)
		{
			var problems = result.Problems;
			var socket = ShaderNodeTypes.Find(type)?.Inputs.FirstOrDefault(s => s.Name == socketName);
			if (socket == null)
			{
				result.Ignored.Add(string.Format(LOC.STR("{0} input '{1}' is left out, as Cycles in Rhino has no such input."), type, socketName));
				return;
			}

			var parameter = FindParameter(component, type, socketName, true);
			if (parameter == null)
			{
				problems.Add(string.Format(LOC.STR("{0} input '{1}' is not imported."), type, socketName));
				return;
			}

			var value = ParseValue(socket, text);
			if (value == null)
			{
				problems.Add(string.Format(LOC.STR("{0} input '{1}': cannot read '{2}' as {3}."), type, socketName, text, socket.Type));
				return;
			}

			// A Grasshopper colour holds 0 to 1, so a brighter one, such as an emission's, is clamped.
			if ((socket.Type == "color" || socket.Type == "color2") && OutsideUnitRange(text))
				problems.Add(string.Format(LOC.STR("{0} input '{1}': '{2}' is clamped to the 0 to 1 a Grasshopper colour holds."), type, socketName, text));

			parameter.PersistentDataWeak = Garden.ITreeFromList(new[] { value });
		}

		private static bool OutsideUnitRange(string text)
		{
			return text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.Any(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && (d < 0.0 || d > 1.0));
		}

		/// <summary>
		/// An XML attribute as the Grasshopper value <see cref="CyclesValues"/> turns back into the same text.
		/// </summary>
		internal static object ParseValue(ShaderNodeSocketInfo socket, string text)
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
					// Blender writes some checkboxes, such as Thin Wall, which Cycles keeps as integers.
					if (bool.TryParse(text, out var on)) return on ? 1 : 0;
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
				problems.Add(string.Format(LOC.STR("{0} input '{1}': cannot read it as a colour table with an alpha table of the same length."), type, NodeCatalog.RampSocket));
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

			ReportLargeTable(count, type, NodeCatalog.RampSocket, problems);
			samples = Resample(samples, interpolate);
			count = samples.Length;
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
				problems.Add(string.Format(LOC.STR("{0} input '{1}': cannot read it as a table of {2} number(s) per entry."), nodeType.Name, socket.Name, columns.Length));
				return curves;
			}

			ReportLargeTable(count, nodeType.Name, socket.Name, problems);
			var x0 = minX;
			var dx = (maxX - minX) / (Math.Min(count, CyclesValues.RampSize) - 1);
			for (var c = 0; c < columns.Length; c++)
			{
				var samples = Resample(Enumerable.Range(0, count).Select(i => new[] { numbers[i * columns.Length + c] }).ToArray(), true);
				if (Enumerable.Range(0, samples.Length).All(i => Math.Abs(samples[i][0] - (x0 + i * dx)) <= TableTolerance))
					continue;

				var points = Simplify(samples, true).Select(i => new Point2d(x0 + i * dx, samples[i][0]));
				curves.Add((columns[c], new InterpolationFunction(InterpolationAlgorithm.Linear, points)));
			}
			return curves;
		}

		/* More entries than this are no table Cycles or Blender writes. */
		private const int LargeTable = 4096;

		private static void ReportLargeTable(int count, string type, string socketName, List<string> problems)
		{
			if (count > LargeTable)
				problems.Add(string.Format(LOC.STR("{0} input '{1}' has {2} entries, so it is resampled to the {3} of Cycles' own tables."), type, socketName, count, CyclesValues.RampSize));
		}

		/// <summary>
		/// A table longer than Cycles' own at that size, which the graph writes anyway, so Simplify
		/// stays quick: linear between entries, or without interpolation, the entry that holds
		/// there, as Cycles looks it up.
		/// </summary>
		private static double[][] Resample(double[][] samples, bool interpolate)
		{
			var size = CyclesValues.RampSize;
			if (samples.Length <= size)
				return samples;

			var resampled = new double[size][];
			for (var i = 0; i < size; i++)
			{
				var x = i * (samples.Length - 1.0) / (size - 1);
				var k = Math.Min((int)x, samples.Length - 1);
				var t = x - k;
				resampled[i] = !interpolate || k == samples.Length - 1
					? samples[k]
					: samples[k].Zip(samples[k + 1], (a, b) => a + t * (b - a)).ToArray();
			}
			return resampled;
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

		private static IParameter FindChannel(CyclesNodeComponent component, (string Name, string Code)[] channels, (string Name, string Code) channel)
		{
			var c = Array.IndexOf(channels, channel);
			for (var i = 0; i < component.Parameters.InputCount && c >= 0; i++)
				if (CyclesNodeComponent.ChannelIndex(channels, component.Parameters.Input(i)) == c)
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
