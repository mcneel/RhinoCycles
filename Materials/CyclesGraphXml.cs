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
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// The XML of a Cycles Graph material as the material editor reads and changes it: nodes by
	/// name, the link into each input, and one value changed at a time.
	/// </summary>
	internal sealed class CyclesGraphXml
	{
		private readonly XElement _root;
		private readonly Dictionary<string, XElement> _nodes = new Dictionary<string, XElement>(StringComparer.Ordinal);
		private readonly List<(string FromNode, string FromSocket, string ToNode, string ToSocket)> _links =
			new List<(string FromNode, string FromSocket, string ToNode, string ToSocket)>();

		private CyclesGraphXml(XElement root)
		{
			_root = root;

			// Cycles' example scenes wrap the graph in a shader element.
			var shader = root.Descendants("shader").FirstOrDefault();
			foreach (var element in (shader ?? root).Elements())
			{
				if (element.Name.LocalName == "connect")
				{
					if (Split((string)element.Attribute("from"), out var fromNode, out var fromSocket) &&
					    Split((string)element.Attribute("to"), out var toNode, out var toSocket))
						_links.Add((fromNode, fromSocket, toNode, toSocket));
					continue;
				}

				var name = (string)element.Attribute("name");
				if (name != null && element.Name.LocalName != "output")
					_nodes[name] = element;
			}
		}

		/// <summary>
		/// The graph, or null if the XML does not parse.
		/// </summary>
		internal static CyclesGraphXml Parse(string xml)
		{
			try
			{
				var body = Regex.Replace(xml ?? string.Empty, "<\\?xml[^>]*\\?>", string.Empty);
				return new CyclesGraphXml(XElement.Parse("<root>" + body + "</root>"));
			}
			catch (XmlException)
			{
				return null;
			}
		}

		/// <summary>
		/// Whether there is no node and no connect, as for text that is not a graph.
		/// </summary>
		internal bool IsEmpty => _nodes.Count == 0 && _links.Count == 0;

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
		/// The node and output linked into an input, which connect names by either of its names.
		/// </summary>
		internal (string Node, string Socket)? Source(string node, string socket, string uiName)
		{
			foreach (var link in _links)
			{
				if (link.ToNode == node &&
				    (string.Equals(link.ToSocket, socket, StringComparison.OrdinalIgnoreCase) ||
				     string.Equals(link.ToSocket, uiName, StringComparison.OrdinalIgnoreCase)))
					return (link.FromNode, link.FromSocket);
			}
			return null;
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

		private static XAttribute FindAttribute(XElement element, string socket)
		{
			return element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, socket, StringComparison.OrdinalIgnoreCase));
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
