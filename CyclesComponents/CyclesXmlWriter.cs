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
using System.Linq;
using System.Text;
using System.Xml.Linq;

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

		internal static string Write(IEnumerable<KeyValuePair<string, CyclesSocket>> outputInputs)
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
				foreach (var value in node.Values)
					element.Add(new XAttribute(value.Key, value.Value));
				xml.AppendLine(element.ToString(SaveOptions.DisableFormatting));
			}

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
