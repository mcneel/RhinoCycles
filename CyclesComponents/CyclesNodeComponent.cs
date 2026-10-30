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

using ccl;
using GrasshopperIO;
using Grasshopper2.Components;
using Grasshopper2.Parameters;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;

namespace CyclesComponents
{
	/// <summary>
	/// Any Cycles shader node. Which one is set when a <see cref="CyclesNodeProxy"/> emits it,
	/// and is stored with the component; its parameters come from the Cycles node registry.
	/// </summary>
	[IoId("00000000-9e6b-471c-b5b2-2d3b723f7cb9"), Grasshopper2.Author("Lars")]
	public sealed class CyclesNodeComponent : Component
	{
		/// <summary>
		/// The node type for the component being constructed. The base constructor adds the
		/// parameters before ours runs, so the proxy hands the type over here.
		/// </summary>
		[ThreadStatic]
		internal static string PendingNodeType;

		#region constructor
		public CyclesNodeComponent()
			: base(CreateNomen(PendingNodeType))
		{
			NodeType = PendingNodeType ?? string.Empty;

			// The slab shows the Cycles name; the palette keeps the readable one.
			if (!string.IsNullOrEmpty(NodeType))
				UserName = NodeType;
		}

		public CyclesNodeComponent(IReader reader) : base(reader)
		{
			NodeType = reader.String(nameof(NodeType));
		}

		public override void Store(IWriter writer)
		{
			base.Store(writer);
			writer.String(nameof(NodeType), NodeType);
		}

		private static Nomen CreateNomen(string nodeType)
		{
			// Without a node type this is only the registration instance; keep it off the palette.
			if (string.IsNullOrEmpty(nodeType))
				return new Nomen("Cycles Node", "A Cycles shader node.", NodeCatalog.Chapter, "Nodes", 0, Rank.Hidden);

			return NodeCatalog.Nomen(nodeType);
		}

		protected override void AddInputs(InputAdder inputs)
		{
			var nodeType = ShaderNodeTypes.Find(PendingNodeType);
			if (nodeType is null)
				return;

			var sockets = NodeCatalog.VisibleInputs(nodeType).ToList();
			foreach (var socket in sockets)
			{
				var name = NodeCatalog.ParameterName(socket, sockets);
				// The canvas shows the code: Cycles' own socket name, as the XML uses it.
				var code = socket.Name;
				var info = Describe(socket);

				if (socket.IsLinkable)
				{
					// Either a value or a socket from another node, so it has to be generic.
					inputs.AddGeneric(name, code, info, Access.Item, Requirement.MayBeMissing);
					continue;
				}

				switch (socket.Type)
				{
					case "boolean": inputs.AddBoolean(name, code, info, Access.Item, Requirement.MayBeMissing); break;
					case "int":
					case "uint": inputs.AddInteger(name, code, info, Access.Item, Requirement.MayBeMissing); break;
					case "float": inputs.AddNumber(name, code, info, Access.Item, Requirement.MayBeMissing); break;
					case "string":
					case "enum": inputs.AddText(name, code, info, Access.Item, Requirement.MayBeMissing); break;
					default: inputs.AddGeneric(name, code, info, Access.Item, Requirement.MayBeMissing); break;
				}
			}
		}

		protected override void AddOutputs(OutputAdder outputs)
		{
			var nodeType = ShaderNodeTypes.Find(PendingNodeType);
			if (nodeType is null)
				return;

			foreach (var socket in nodeType.Outputs)
			{
				var name = NodeCatalog.ParameterName(socket, nodeType.Outputs);
				outputs.AddGeneric(name, socket.Name, $"Cycles {socket.Type} output '{socket.Name}'.");
			}
		}

		private static string Describe(ShaderNodeSocketInfo socket)
		{
			var info = $"Cycles {socket.Type} input '{socket.Name}'.";
			if (socket.Default.Length > 0)
				info += $" Default {socket.Default}.";
			if (socket.EnumValues.Count > 0)
				info += $" One of: {CyclesValues.EnumNames(socket)}.";
			return info;
		}
		#endregion

		#region properties
		/// <summary>
		/// Cycles node type name, e.g. "principled_bsdf".
		/// </summary>
		public string NodeType { get; }

		protected override IIcon IconInternal => NodeCatalog.Icon(string.IsNullOrEmpty(NodeType) ? "cycles" : NodeType);
		#endregion

		#region implementation
		protected override void Process(IDataAccess access)
		{
			var nodeType = ShaderNodeTypes.Find(NodeType);
			if (nodeType is null)
			{
				access.AddError("Unknown node type", $"This Cycles build has no shader node type '{NodeType}'.");
				return;
			}

			var values = new Dictionary<string, string>();
			var links = new Dictionary<string, CyclesSocket>();
			var inputSockets = NodeCatalog.VisibleInputs(nodeType).ToList();

			// Match parameters to sockets by their fixed name (users can rename the display
			// name), so a file outlives a reordering in Cycles.
			for (var i = 0; i < Parameters.InputCount; i++)
			{
				var socket = FindSocket(inputSockets, Parameters.Input(i).Nomen.Name);
				if (socket is null)
					continue;

				if (!access.GetItem(i, out object value) || value is null)
					continue;

				if (value is CyclesSocket link)
				{
					if (socket.IsLinkable)
						links[socket.Name] = link;
					else
						access.AddWarning("Not linkable", $"'{socket.UiName}' is a setting and cannot take a link.");
					continue;
				}

				if (CyclesValues.TryFormat(socket, value, out var text, out var problem))
					values[socket.Name] = text;
				else
					access.AddWarning("Invalid value", problem);
			}

			var node = new CyclesNode(NodeType, values, links);
			for (var o = 0; o < Parameters.OutputCount; o++)
			{
				var socket = FindSocket(nodeType.Outputs, Parameters.Output(o).Nomen.Name);
				if (socket != null)
					access.SetItem(o, new CyclesSocket(node, socket.Name));
			}
		}

		private static ShaderNodeSocketInfo FindSocket(IReadOnlyList<ShaderNodeSocketInfo> sockets, string parameterName)
		{
			return sockets.FirstOrDefault(s => string.Equals(NodeCatalog.ParameterName(s, sockets), parameterName, StringComparison.Ordinal));
		}
		#endregion
	}
}
