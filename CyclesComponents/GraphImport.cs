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
using Grasshopper2.Types.Colour;
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

				foreach (var attribute in element.Attributes())
				{
					if (attribute.Name.LocalName == "name")
						continue;
					SetInput(component, type, attribute.Name.LocalName, attribute.Value, result.Problems);
				}

				nodes[name] = (type, component);
			}

			// Columns by depth, so data flows left to right.
			var depth = nodes.Keys.ToDictionary(n => n, n => 0, StringComparer.Ordinal);
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

			var nextY = new Dictionary<int, float>();
			foreach (var pair in nodes)
			{
				var column = depth[pair.Key];
				nextY.TryGetValue(column, out var y);
				result.Document.Objects.Add(pair.Value.Component, new PointF(50f + column * ColumnWidth, 50f + y));
				var rows = Math.Max(pair.Value.Component.Parameters.InputCount, pair.Value.Component.Parameters.OutputCount);
				nextY[column] = y + (rows + 3) * RowHeight;
			}

			var output = (CyclesMaterialOutput)ObjectProxies.FindByType(typeof(CyclesMaterialOutput)).Emit();
			result.Document.Objects.Add(output, new PointF(50f + outputColumn * ColumnWidth, 50f));
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
	}
}
