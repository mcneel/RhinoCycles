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

using ccl;
using Eto.Drawing;
using GrasshopperIO;
using Grasshopper2.Components;
using Grasshopper2.Doc.Attributes;
using Grasshopper2.Parameters;
using Grasshopper2.Parameters.Standard;
using Grasshopper2.Types.Colour;
using Grasshopper2.Types.Functions;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;
using Grasshopper2.UI.InputPanel;

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

			// Enum choices follow the Cycles that is running, not the one the file was saved with.
			var nodeType = ShaderNodeTypes.Find(NodeType);
			if (nodeType is null)
				return;

			var sockets = NodeCatalog.VisibleInputs(nodeType).ToList();
			for (var i = 0; i < Parameters.InputCount; i++)
				if (Parameters.Input(i) is TextParameter parameter && FindSocket(sockets, parameter.Nomen.Name) is ShaderNodeSocketInfo socket && socket.Type == "enum")
					SetEnumPresets(parameter, socket);
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

				if (NodeCatalog.IsRamp(socket))
				{
					AddRamp(inputs, name, code);
					continue;
				}

				var channels = NodeCatalog.CurveChannels(socket);
				if (channels != null)
				{
					AddCurves(inputs, channels);
					continue;
				}

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
					case "string": inputs.AddText(name, code, info, Access.Item, Requirement.MayBeMissing); break;
					case "enum": AddEnum(inputs, name, code, info, socket); break;
					default: inputs.AddGeneric(name, code, info, Access.Item, Requirement.MayBeMissing); break;
				}
			}
		}

		/// <summary>
		/// Enums are text inputs with one preset per Cycles value, so Grasshopper offers them
		/// as a list. They start at Cycles' default, so the list shows what is in use.
		/// </summary>
		private static void AddEnum(InputAdder inputs, string name, string code, string info, ShaderNodeSocketInfo socket)
		{
			var parameter = inputs.AddText(name, code, info, Access.Item, Requirement.MayBeMissing);
			SetEnumPresets(parameter, socket);
			if (socket.EnumValues.Any(e => e.Key == socket.Default))
				parameter.Set(socket.Default);
		}

		/// <summary>
		/// The colour ramp is a gradient, black to white as in Blender, and also fills the ramp's alpha.
		/// </summary>
		private static void AddRamp(InputAdder inputs, string name, string code)
		{
			var info = $"Cycles' '{NodeCatalog.RampSocket}' and '{NodeCatalog.RampAlphaSocket}' tables, as a gradient over 0 to 1. Add Editors in the input panel wires a Gradient Editor in.";
			var parameter = inputs.AddGradient(name, code, info, Access.Item, Requirement.MayBeMissing);
			parameter.Set(Editors.DefaultRamp());
		}

		/// <summary>
		/// A curve table is a function input per channel, over Min X to Max X. A channel with no
		/// function is a straight line, which leaves it as it is.
		/// </summary>
		private static void AddCurves(InputAdder inputs, (string Name, string Code)[] channels)
		{
			foreach (var channel in channels)
			{
				var info = $"The {channel.Name} curve over Min X to Max X, a straight line if there is none. Add Editors in the input panel wires a Function Editor in.";
				if (channels.Length == 4 && channel.Name == channels[0].Name)
					info += " Applied before the Red, Green and Blue curves.";
				inputs.AddFunction(channel.Name, channel.Code, info, Access.Item, Requirement.MayBeMissing);
			}
		}

		private static void SetEnumPresets(TextParameter parameter, ShaderNodeSocketInfo socket)
		{
			parameter.Presets.Clear();
			foreach (var entry in socket.EnumValues.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
				parameter.Presets.Add(entry.Key, null, entry.Key);
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

				if (NodeCatalog.IsRamp(socket))
				{
					if (value is Gradient gradient)
					{
						CyclesValues.FormatRamp(gradient, out var colours, out var alphas);
						values[NodeCatalog.RampSocket] = colours;
						values[NodeCatalog.RampAlphaSocket] = alphas;
					}
					else
						access.AddWarning("Invalid value", $"'{socket.UiName}' takes a gradient.");
					continue;
				}

				if (CyclesValues.TryFormat(socket, value, out var text, out var problem))
					values[socket.Name] = text;
				else
					access.AddWarning("Invalid value", problem);
			}

			// Without a table Cycles leaves a curve node out, so straight channels are written too.
			var curves = inputSockets.FirstOrDefault(s => NodeCatalog.CurveChannels(s) != null);
			if (curves != null)
			{
				var minX = Setting(values, inputSockets, "min_x");
				var maxX = Setting(values, inputSockets, "max_x");
				if (!(maxX > minX))
					access.AddWarning("Invalid range", "Max X has to be above Min X.");
				values[curves.Name] = CyclesValues.FormatCurves(CurveFunctions(access, NodeCatalog.CurveChannels(curves)), minX, maxX);
			}

			var node = new CyclesNode(NodeType, values, links);
			for (var o = 0; o < Parameters.OutputCount; o++)
			{
				var socket = FindSocket(nodeType.Outputs, Parameters.Output(o).Nomen.Name);
				if (socket != null)
					access.SetItem(o, new CyclesSocket(node, socket.Name));
			}
		}

		private Function[] CurveFunctions(IDataAccess access, (string Name, string Code)[] channels)
		{
			var functions = new Function[channels.Length];
			for (var i = 0; i < Parameters.InputCount; i++)
			{
				var c = Array.FindIndex(channels, channel => channel.Name == Parameters.Input(i).Nomen.Name);
				if (c < 0 || !access.GetItem(i, out object value) || value is null)
					continue;

				functions[c] = value as Function;
				if (functions[c] is null)
					access.AddWarning("Invalid value", $"'{channels[c].Name}' takes a function.");
			}
			return functions;
		}

		/// <summary>
		/// A number input as Process formatted it, or its Cycles default.
		/// </summary>
		private static double Setting(Dictionary<string, string> values, IEnumerable<ShaderNodeSocketInfo> sockets, string socketName)
		{
			var text = values.TryGetValue(socketName, out var set) ? set : sockets.FirstOrDefault(s => s.Name == socketName)?.Default;
			return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0.0;
		}

		private static ShaderNodeSocketInfo FindSocket(IReadOnlyList<ShaderNodeSocketInfo> sockets, string parameterName)
		{
			return sockets.FirstOrDefault(s => string.Equals(NodeCatalog.ParameterName(s, sockets), parameterName, StringComparison.Ordinal));
		}
		#endregion

		#region editors
		public override void AppendToInputPanel(InputPanel panel)
		{
			base.AppendToInputPanel(panel);

			if (!InputsWithoutEditor().Any())
				return;

			using (panel.BeginCategory("Editors"))
			{
				var bar = panel.AddBar(false);
				bar.AddPushButton(null, new Nomen("Add Editors", "Wire an editor into each ramp and curve input that has nothing connected.", "Editors", "Editors"), AddEditors);
			}
		}

		private IEnumerable<IParameter> InputsWithoutEditor()
		{
			for (var i = 0; i < Parameters.InputCount; i++)
			{
				var input = Parameters.Input(i);
				if (input.Inputs.Count == 0 && (input is GradientParameter || input is FunctionParameter))
					yield return input;
			}
		}

		/// <summary>
		/// Editors stacked to the left of the component, each starting from what its input holds.
		/// </summary>
		private void AddEditors()
		{
			var doc = Document;
			var nodeType = ShaderNodeTypes.Find(NodeType);
			if (doc is null || nodeType is null)
				return;

			var sockets = NodeCatalog.VisibleInputs(nodeType).ToList();
			double Persistent(string socketName)
			{
				var socket = sockets.FirstOrDefault(s => s.Name == socketName);
				for (var i = 0; i < Parameters.InputCount && socket != null; i++)
					if (Parameters.Input(i).Nomen.Name == NodeCatalog.ParameterName(socket, sockets))
						if (Parameters.Input(i).PersistentDataWeak.NonNullItems.FirstOrDefault() is double number)
							return number;
				return Setting(new Dictionary<string, string>(), sockets, socketName);
			}

			var undo = new Grasshopper2.Undo.ActionList();
			var bounds = Attributes.Bounds;
			var top = bounds.Top;
			foreach (var input in InputsWithoutEditor().ToList())
			{
				var current = input.PersistentDataWeak.NonNullItems.FirstOrDefault();
				var editor = input is GradientParameter
					? (AbstractParameter)Editors.ForRamp(input.UserName, current as GripGradient)
					: Editors.ForCurve(input.UserName, current as Function, Persistent("min_x"), Persistent("max_x"));

				var height = Editors.Rows(editor) * StandardLayout.ParameterMinHeight;
				doc.Methods.DropObject(editor, new PointF(bounds.Left - 160f, top + 0.5f * height), undo);
				Connections.Connect(editor, input, undo);
				top += height + 10f;
			}

			AddUndoRecord(undo.ToRecord(new Grasshopper2.Undo.VerbNoun("Add", "Editors")));
			doc.Solution.Start();
		}
		#endregion
	}
}
