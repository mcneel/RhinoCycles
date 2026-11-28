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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// The XML of a Node-Based material as the material editor reads and changes it: nodes by
	/// name, the link into each input, and one value changed at a time.
	/// </summary>
	internal sealed class CyclesGraphXml
	{
		private readonly XElement _root;
		private readonly Dictionary<string, XElement> _nodes = new Dictionary<string, XElement>(StringComparer.Ordinal);
		/* The link into each input, by node and lower-case socket name, as ccycles keeps them. */
		private readonly Dictionary<(string Node, string Socket), (string Node, string Socket)> _sources =
			new Dictionary<(string Node, string Socket), (string Node, string Socket)>();
		/* The nodes each node takes from, for finding loops. */
		private readonly Dictionary<string, List<string>> _upstream = new Dictionary<string, List<string>>(StringComparer.Ordinal);

		private CyclesGraphXml(XElement root)
		{
			_root = root;

			// As ccycles reads it: the first shader of Cycles' example scenes, else the elements themselves.
			var body = root.Element("shader") ?? root.Element("cycles")?.Element("shader") ?? root;

			// Without ccycles' node types, connects are taken as they are.
			var typesKnown = TypeInfo("output") != null;

			// The nodes a connect can name so far, with their Cycles types.
			var defined = new Dictionary<string, string>(StringComparer.Ordinal) { ["output"] = "output" };
			foreach (var element in body.Elements())
			{
				var kind = element.Name.LocalName;
				if (kind == "connect")
				{
					if (Split((string)element.Attribute("from"), out var fromNode, out var fromSocket) &&
					    Split((string)element.Attribute("to"), out var toNode, out var toSocket))
						Connect(defined, typesKnown, fromNode, fromSocket, toNode, toSocket);
					continue;
				}

				var name = (string)element.Attribute("name");
				if (kind == "output" || name == null || name == "output")
					continue;
				_nodes[name] = element;

				// ccycles leaves out unknown types and convert nodes placed by hand.
				var type = kind == "background" ? "background_shader" : kind;
				if (name.Length > 0 && !type.StartsWith("convert_", StringComparison.Ordinal) && (!typesKnown || TypeInfo(type) != null))
					defined[name] = type;
			}
		}

		/// <summary>
		/// Add a link unless ccycles would leave it out: a node not defined before it, sockets the
		/// nodes lack, types Cycles cannot convert, a loop, or an input linked already.
		/// </summary>
		private void Connect(Dictionary<string, string> defined, bool typesKnown, string fromNode, string fromSocket, string toNode, string toSocket)
		{
			if (!defined.TryGetValue(fromNode, out var fromType) || !defined.TryGetValue(toNode, out var toType))
				return;

			if (typesKnown)
			{
				var output = TypeInfo(fromType)?.Outputs.FirstOrDefault(s => string.Equals(s.Name, fromSocket, StringComparison.OrdinalIgnoreCase));
				var input = TypeInfo(toType)?.Inputs.FirstOrDefault(s => s.IsLinkable && string.Equals(s.Name, toSocket, StringComparison.OrdinalIgnoreCase));
				if (output == null || input == null || !CanConnect(output.Type, input.Type))
					return;
				fromSocket = output.Name;
				toSocket = input.Name;
			}

			var key = (toNode, toSocket.ToLowerInvariant());
			if (_sources.ContainsKey(key) || TakesFrom(fromNode, toNode))
				return;

			_sources[key] = (fromNode, fromSocket);
			if (!_upstream.TryGetValue(toNode, out var from))
				_upstream[toNode] = from = new List<string>();
			from.Add(fromNode);
		}

		/* As ccycles' can_connect: through a convert node, and anything but a closure into a closure. */
		private static bool CanConnect(string from, string to)
		{
			if (from == to || to == "closure")
				return true;
			if (from == "closure")
				return false;
			return TypeInfo("convert_" + from + "_to_" + to) != null;
		}

		/* Whether node takes anything from target, so a link from node into target would close a loop. */
		private bool TakesFrom(string node, string target)
		{
			var pending = new Stack<string>();
			var seen = new HashSet<string>(StringComparer.Ordinal);
			pending.Push(node);
			while (pending.Count > 0)
			{
				var current = pending.Pop();
				if (current == target)
					return true;
				if (!seen.Add(current) || !_upstream.TryGetValue(current, out var from))
					continue;
				foreach (var f in from)
					pending.Push(f);
			}
			return false;
		}

		private static ShaderNodeTypeInfo TypeInfo(string type)
		{
			try
			{
				return ShaderNodeTypes.Find(type);
			}
			catch (Exception)
			{
				// ccycles missing or too old to list its nodes.
				return null;
			}
		}

		/* What pugixml, which ccycles reads the XML with, takes as text: an & that starts no
		 * entity, and a < in a value. */
		private static readonly Regex g_bareAmpersand = new Regex("&(?!(?:amp|lt|gt|quot|apos);|#[0-9]+;|#x[0-9A-Fa-f]+;)", RegexOptions.Compiled);
		private static readonly Regex g_quotedValue = new Regex("=\\s*(\"[^\"]*\"|'[^']*')", RegexOptions.Compiled);

		/// <summary>
		/// The graph, or null if the XML does not parse.
		/// </summary>
		internal static CyclesGraphXml Parse(string xml)
		{
			try
			{
				var body = Regex.Replace(xml ?? string.Empty, "<\\?xml[^>]*\\?>", string.Empty).TrimStart('\uFEFF');
				body = g_bareAmpersand.Replace(body, "&amp;");
				body = g_quotedValue.Replace(body, m => m.Value.Replace("<", "&lt;"));
				return new CyclesGraphXml(XElement.Parse("<root>" + body + "</root>"));
			}
			catch (XmlException)
			{
				return null;
			}
		}

		/// <summary>
		/// The Cycles node type of a node, or null if there is no such node.
		/// </summary>
		internal string NodeType(string node)
		{
			if (node == null || !_nodes.TryGetValue(node, out var element))
				return null;

			// Named so in the XML because the scene has a background too, as in cycles_xml.cpp.
			var type = element.Name.LocalName;
			return type == "background" ? "background_shader" : type;
		}

		/// <summary>
		/// A node's value for an input, or null if the XML leaves it at Cycles' default.
		/// </summary>
		internal string Value(string node, string socket)
		{
			return node != null && _nodes.TryGetValue(node, out var element) ? FindAttribute(element, socket)?.Value : null;
		}

		/// <summary>
		/// The value ccycles gives an input before it reads the XML, where that differs from Cycles'
		/// own default, or null.
		/// </summary>
		internal static string ReaderDefault(string nodeType, string socket)
		{
			// shader_xml.cpp starts a tangent on Rhino's UV map.
			if (nodeType == "tangent" && socket == "direction_type")
				return "uv_map";
			if (nodeType == "tangent" && socket == "attribute")
				return "uvmap1";
			return null;
		}

		/// <summary>
		/// The node and output linked into an input, by its Cycles socket name in any case, as
		/// ccycles matches it.
		/// </summary>
		internal (string Node, string Socket)? Source(string node, string socket)
		{
			if (node == null || socket == null)
				return null;
			return _sources.TryGetValue((node, socket.ToLowerInvariant()), out var source) ? source : ((string Node, string Socket)?)null;
		}

		/// <summary>
		/// Set a node's value for an input and return the whole graph, one element per line as
		/// Material Output writes it.
		/// </summary>
		internal string WithValue(string node, string socket, string value)
		{
			if (node != null && _nodes.TryGetValue(node, out var element))
			{
				var attribute = FindAttribute(element, socket);
				if (attribute != null)
					attribute.Value = value;
				else
					element.Add(new XAttribute(socket, value));
			}

			var xml = new StringBuilder();
			foreach (var child in _root.Nodes())
				xml.AppendLine(child.ToString(SaveOptions.DisableFormatting));
			return xml.ToString();
		}

		/// <summary>
		/// Add a change to the edits waiting for the Grasshopper 2 document, replacing an earlier
		/// one to the same input. Edits that do not parse are dropped.
		/// </summary>
		internal static string AddEdit(string edits, string node, string socket, string value)
		{
			var sets = new List<XElement>();
			try
			{
				if (!string.IsNullOrWhiteSpace(edits))
					sets.AddRange(XElement.Parse("<edits>" + edits + "</edits>").Elements("set"));
			}
			catch (XmlException)
			{
			}

			sets.RemoveAll(s => (string)s.Attribute("node") == node && (string)s.Attribute("socket") == socket);
			sets.Add(new XElement("set", new XAttribute("node", node), new XAttribute("socket", socket), new XAttribute("value", value)));

			var text = new StringBuilder();
			foreach (var set in sets)
				text.AppendLine(set.ToString(SaveOptions.DisableFormatting));
			return text.ToString();
		}

		/* Words Cycles names in lower case that read better in capitals. */
		private static readonly HashSet<string> g_acronyms = new HashSet<string>(StringComparer.Ordinal)
		{
			"aov", "bsdf", "bw", "ggx", "hsv", "ies", "ior", "rgb", "uv", "uvw", "xyz",
		};

		/// <summary>
		/// A Cycles name as a title, e.g. "Principled BSDF" for principled_bsdf, as the Grasshopper 2 nodes show it.
		/// </summary>
		internal static string DisplayName(string name)
		{
			var words = (name ?? string.Empty).Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(w => g_acronyms.Contains(w) ? w.ToUpperInvariant() : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w));
			return string.Join(" ", words);
		}

		/* By the exact name, as Cycles reads it. */
		private static XAttribute FindAttribute(XElement element, string socket)
		{
			return element.Attribute(socket);
		}

		private static bool Split(string text, out string node, out string socket)
		{
			var parts = (text ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			node = parts.Length == 2 ? parts[0] : null;
			socket = parts.Length == 2 ? parts[1] : null;
			return parts.Length == 2;
		}
	}
}
