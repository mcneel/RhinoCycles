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
using System.Xml.Linq;

using Eto.Drawing;
using Grasshopper2.Doc;

namespace CyclesComponents
{
	/// <summary>
	/// Writes a shader graph in Cycles' own XML form, as cycles_xml.cpp reads it: one element
	/// per node named by its type, inputs as attributes, then connect elements. The "output"
	/// node is implied.
	/// </summary>
	internal static class CyclesXmlWriter
	{
		/// <summary>
		/// A node's name in the XML: its type and the start of its component's id, so a change
		/// made to the XML elsewhere finds the component again.
		/// </summary>
		internal static string NodeName(string type, Guid id)
		{
			return $"{type}_{id.ToString("N").Substring(0, 8)}";
		}

		/// <summary>
		/// Where a node sits in a node editor: its top-left corner, y down, as Blender's location
		/// gives it (with y turned round). Layout only; RhinoCycles drops it before Cycles reads the XML.
		/// </summary>
		internal const string PositionAttribute = "ui_position";

		/// <summary>
		/// Add an object with its top-left corner at a position from the XML. The document lays an
		/// object out as it adds it, centred on the point given, so it moves into place after.
		/// </summary>
		internal static void AddAt(Document doc, IDocumentObject obj, PointF corner)
		{
			doc.Objects.Add(obj, corner);
			var bounds = obj.Attributes.Bounds;
			if (bounds.Width > 0 || bounds.Height > 0)
				obj.Attributes.Move(corner.X - bounds.Left, corner.Y - bounds.Top);
		}

		/// <summary>
		/// An object's position as the XML writes it: the top-left corner of its bounds, or its
		/// pivot before it has been laid out.
		/// </summary>
		internal static string Position(IDocumentObject obj)
		{
			var bounds = obj.Attributes.Bounds;
			var corner = bounds.Width > 0 || bounds.Height > 0 ? bounds.TopLeft : obj.Attributes.Pivot;
			return string.Format(CultureInfo.InvariantCulture, "{0} {1}", Math.Round(corner.X) + 0.0, Math.Round(corner.Y) + 0.0);
		}

		internal static bool TryParsePosition(string text, out PointF corner)
		{
			var parts = (text ?? string.Empty).Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 2 &&
			    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
			    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
			{
				corner = new PointF(x, y);
				return true;
			}
			corner = PointF.Empty;
			return false;
		}

		/// <summary>
		/// The positions of the node components in a document, by component id.
		/// </summary>
		internal static Dictionary<Guid, string> Positions(Document doc)
		{
			var positions = new Dictionary<Guid, string>();
			if (doc != null)
				foreach (var component in doc.Objects.ForwardsOfT<CyclesNodeComponent>())
					positions[component.InstanceId] = Position(component);
			return positions;
		}

		/// <summary>
		/// The XML with its nodes, and the output, where the components now are; null if nothing moved.
		/// </summary>
		internal static string WithPositions(string xml, Document doc, IDocumentObject output)
		{
			XElement root;
			try
			{
				root = XElement.Parse("<root>" + (xml ?? string.Empty) + "</root>");
			}
			catch (System.Xml.XmlException)
			{
				return null;
			}

			var positions = new Dictionary<string, string>(StringComparer.Ordinal);
			if (doc != null)
				foreach (var component in doc.Objects.ForwardsOfT<CyclesNodeComponent>())
					positions[NodeName(component.NodeType, component.InstanceId)] = Position(component);
			var changed = false;
			void Place(XElement element, string position)
			{
				if ((string)element.Attribute(PositionAttribute) == position)
					return;
				element.SetAttributeValue(PositionAttribute, position);
				changed = true;
			}

			var placed = false;
			foreach (var element in root.Elements())
			{
				var name = (string)element.Attribute("name");
				if (element.Name.LocalName == "output")
				{
					if (output != null)
						Place(element, Position(output));
					continue;
				}
				if (name != null && positions.TryGetValue(name, out var position))
				{
					Place(element, position);
					placed = true;
				}
			}

			// An output element only for a graph whose nodes this document wrote.
			if (placed && output != null && root.Element("output") == null)
			{
				var outputElement = new XElement("output", new XAttribute("name", "output"), new XAttribute(PositionAttribute, Position(output)));
				var firstConnect = root.Elements("connect").FirstOrDefault();
				if (firstConnect != null)
					firstConnect.AddBeforeSelf(outputElement);
				else
					root.Add(outputElement);
				changed = true;
			}

			if (!changed)
				return null;

			var text = new StringBuilder();
			foreach (var child in root.Nodes())
				text.AppendLine(child.ToString(SaveOptions.DisableFormatting));
			return text.ToString();
		}

		/// <param name="positions">Node positions by the node's component id, or null.</param>
		/// <param name="outputPosition">The output's position, or null.</param>
		internal static string Write(IEnumerable<KeyValuePair<string, CyclesSocket>> outputInputs, IReadOnlyDictionary<Guid, string> positions = null, string outputPosition = null)
		{
			// CyclesNode has no Equals, so nodes are keyed by reference.
			var names = new Dictionary<CyclesNode, string>();
			var used = new HashSet<string>(StringComparer.Ordinal);
			var order = new List<CyclesNode>();

			void Visit(CyclesNode node)
			{
				if (names.ContainsKey(node))
					return;

				// Upstream first, so every node is defined before a connect names it.
				names[node] = null;
				foreach (var link in node.Links.Values)
					Visit(link.Node);

				// A component solved more than once gives several nodes; the others go by position.
				var name = node.Id == Guid.Empty ? null : NodeName(node.Type, node.Id);
				if (name == null || used.Contains(name))
					name = $"n{order.Count}";
				used.Add(name);

				names[node] = name;
				order.Add(node);
			}

			var outputs = outputInputs.Where(o => o.Value != null).ToList();
			foreach (var output in outputs)
				Visit(output.Value.Node);

			var xml = new StringBuilder();
			foreach (var node in order)
			{
				var element = new XElement(node.Type, new XAttribute("name", names[node]));
				if (positions != null && positions.TryGetValue(node.Id, out var position))
					element.Add(new XAttribute(PositionAttribute, position));
				foreach (var value in node.Values)
					element.Add(new XAttribute(value.Key, value.Value));
				xml.AppendLine(element.ToString(SaveOptions.DisableFormatting));
			}

			if (outputPosition != null)
				xml.AppendLine(new XElement("output", new XAttribute("name", "output"), new XAttribute(PositionAttribute, outputPosition)).ToString(SaveOptions.DisableFormatting));

			foreach (var node in order)
				foreach (var link in node.Links)
					xml.AppendLine(Connect($"{names[link.Value.Node]} {link.Value.Socket}", $"{names[node]} {link.Key}"));

			foreach (var output in outputs)
				xml.AppendLine(Connect($"{names[output.Value.Node]} {output.Value.Socket}", $"output {output.Key}"));

			return xml.ToString();
		}

		private static string Connect(string from, string to)
		{
			return new XElement("connect", new XAttribute("from", from), new XAttribute("to", to)).ToString(SaveOptions.DisableFormatting);
		}
	}
}
