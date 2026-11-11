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
using Eto.Drawing;
using Eto.Forms;
using Rhino.Render;
using Rhino.UI;
using Rhino.UI.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// One output of a Cycles Graph material in the material editor, as Blender's material
	/// properties show it: the node plugged in with its inputs, and behind a linked input the
	/// node it comes from, unfolded on request. A changed value goes to the graph through
	/// <see cref="CyclesGraphMaterial.SetGraphValue"/>.
	/// </summary>
	public abstract class CyclesGraphOutputSection : EtoContentUISection3
	{
		private const int Indent = 14;
		private const int MaxDepth = 16;

		/* Linked inputs shown unfolded, by their path from the output, kept across rebuilds. */
		private readonly HashSet<string> _unfolded = new HashSet<string>(StringComparer.Ordinal);

		private Guid _materialId = Guid.Empty;
		/* The XML on show; a change made here sets it first, so the rebuild that follows is skipped. */
		private string _xml;
		private CyclesGraphXml _graph;

		protected CyclesGraphOutputSection()
		{
			Content = new TableLayout();
			DataChanged += (sender, e) => Refresh(false);

			// Changes from elsewhere, such as Grasshopper 2 writing the graph, only while on show.
			LoadComplete += (sender, e) =>
			{
				RenderContent.ContentChanged -= OnContentChanged;
				RenderContent.ContentChanged += OnContentChanged;
			};
			UnLoad += (sender, e) => RenderContent.ContentChanged -= OnContentChanged;
		}

		/// <summary>
		/// The input of Cycles' output node this section shows, e.g. "surface".
		/// </summary>
		protected abstract string OutputSocket { get; }

		public override Guid ViewModelId => Guid.Empty;

		public override int SectionHeight => (int)Math.Ceiling(Content.GetPreferredSize(new SizeF(Width > 0 ? Width : 300, float.PositiveInfinity)).Height);

		protected override void Dispose(bool disposing)
		{
			RenderContent.ContentChanged -= OnContentChanged;
			base.Dispose(disposing);
		}

		private void OnContentChanged(object sender, RenderContentChangedEventArgs e)
		{
			if (e.Content != null && e.Content.Id == _materialId)
				Application.Instance.AsyncInvoke(() => Refresh(false));
		}

		private void Refresh(bool force)
		{
			if (IsDisposed)
				return;

			var material = SelectedMaterial();
			var xml = material != null && material.Fields.TryGetValue(CyclesGraphMaterial.GraphXmlField, out string text) ? text : null;
			if (!force && (material?.Id ?? Guid.Empty) == _materialId && xml == _xml)
				return;

			_materialId = material?.Id ?? Guid.Empty;
			_xml = xml;
			Rebuild(material);
		}

		private CyclesGraphMaterial SelectedMaterial()
		{
			using (var iterator = GetSelection().Iterator())
			{
				if (!(iterator?.First() is CyclesGraphMaterial selected))
					return null;

				// The document's own material, so a change is undoable and reaches Raytraced.
				return selected.DocumentAssoc?.RenderMaterials.FirstOrDefault(m => m.Id == selected.Id) as CyclesGraphMaterial ?? selected;
			}
		}

		private void Rebuild(CyclesGraphMaterial material)
		{
			var layout = new TableLayout { Spacing = new Size(4, 2), Padding = new Padding(0, 2) };

			// An empty graph renders as the Principled BSDF a new graph starts with.
			_graph = material == null ? null : CyclesGraphXml.Parse(string.IsNullOrWhiteSpace(_xml) ? CyclesGraphMaterial.EmptyGraphXml : _xml);
			var source = _graph?.Source("output", OutputSocket, OutputSocket);
			if (_graph == null)
			{
				if (material != null)
					layout.Rows.Add(Row(0, LOC.STR("The graph XML does not parse."), null));
			}
			else if (source.HasValue)
				AddNode(layout, material, source.Value.Node, 0, OutputSocket, new HashSet<string>(StringComparer.Ordinal));
			else
				layout.Rows.Add(Row(0, LOC.STR("None"), null));

			layout.Rows.Add(null);
			Content = layout;
		}

		/// <summary>
		/// A node's title and inputs, and the nodes behind its unfolded linked inputs.
		/// </summary>
		private void AddNode(TableLayout layout, CyclesGraphMaterial material, string node, int depth, string path, HashSet<string> visiting)
		{
			var type = _graph.NodeType(node);
			var nodeType = ShaderNodeTypes.Find(type);
			layout.Rows.Add(Row(depth, CyclesGraphXml.DisplayName(type ?? node), null, true));
			if (nodeType == null || depth >= MaxDepth || !visiting.Add(node))
				return;

			foreach (var socket in nodeType.Inputs)
			{
				// Texture mapping is left to Grasshopper, as Blender keeps it off the material properties.
				if (socket.IsInternal || socket.Name.StartsWith("tex_mapping.", StringComparison.Ordinal))
					continue;

				var source = _graph.Source(node, socket.Name, socket.UiName);
				if (source.HasValue)
				{
					var from = source.Value.Node;
					var key = path + "/" + socket.Name;
					var unfolded = _unfolded.Contains(key);
					var link = new Button { Text = (unfolded ? "▾ " : "▸ ") + CyclesGraphXml.DisplayName(_graph.NodeType(from) ?? from) };
					link.Click += (sender, e) =>
					{
						if (!_unfolded.Remove(key))
							_unfolded.Add(key);
						Refresh(true);
					};
					layout.Rows.Add(Row(depth, socket.UiName, link));
					if (unfolded)
						AddNode(layout, material, from, depth + 1, key, visiting);
					continue;
				}

				var editor = Editor(material, node, socket);
				if (editor != null)
					layout.Rows.Add(Row(depth, socket.UiName, editor));
			}

			visiting.Remove(node);
		}

		private static TableRow Row(int depth, string label, Control editor, bool title = false)
		{
			var text = new Label { Text = label, VerticalAlignment = VerticalAlignment.Center, Wrap = WrapMode.None };
			if (title)
				text.Font = SystemFonts.Bold();

			var cell = new Panel { Content = text, Padding = new Padding(depth * Indent, 0, 4, 0) };
			return new TableRow(new TableCell(cell), new TableCell(editor, true));
		}

		#region editors
		/// <summary>
		/// A control for an input's value, or null for inputs the material editor does not set,
		/// such as tables.
		/// </summary>
		private Control Editor(CyclesGraphMaterial material, string node, ShaderNodeSocketInfo socket)
		{
			var text = _graph.Value(node, socket.Name) ?? socket.Default;
			void Set(string value) => SetValue(material, node, socket.Name, value);

			switch (socket.Type)
			{
				case "float": return Number(text, 3, socket, Set);
				case "int": return Number(text, 0, socket, Set);
				case "uint": return Number(text, 0, socket, Set);
				case "boolean": return Flag(text, Set);
				case "color":
				case "color2": return Colour(text, Set);
				case "vector":
				case "point":
				case "normal": return Numbers(text, 3, Set);
				case "point2": return Numbers(text, 2, Set);
				case "enum": return Choice(text, socket, Set);
				case "string": return Text(text, Set);
				case "closure": return new Label { Text = LOC.STR("None"), VerticalAlignment = VerticalAlignment.Center };
				default: return null;
			}
		}

		private static Control Number(string text, int decimals, ShaderNodeSocketInfo socket, Action<string> set)
		{
			var stepper = new NumericStepper
			{
				DecimalPlaces = decimals,
				MaximumDecimalPlaces = decimals == 0 ? 0 : 6,
				Increment = decimals == 0 ? 1.0 : 0.01,
				Value = Numbers(text, 1)[0],
			};
			if (socket.Type == "uint")
				stepper.MinValue = 0.0;

			stepper.ValueChanged += (sender, e) => set(decimals == 0
				? ((long)Math.Round(stepper.Value)).ToString(CultureInfo.InvariantCulture)
				: Format(stepper.Value));
			return stepper;
		}

		private static Control Numbers(string text, int count, Action<string> set)
		{
			var values = Numbers(text, count);
			var steppers = values.Select(v => new NumericStepper { DecimalPlaces = 3, MaximumDecimalPlaces = 6, Increment = 0.01, Value = v }).ToList();
			foreach (var stepper in steppers)
				stepper.ValueChanged += (sender, e) => set(string.Join(" ", steppers.Select(s => Format(s.Value))));

			// The fields share the width, as narrow as the panel may be.
			return new TableLayout(new TableRow(steppers.Select(s => new TableCell(s, true)).ToArray())) { Spacing = new Size(2, 0) };
		}

		private static Control Flag(string text, Action<string> set)
		{
			var check = new CheckBox { Checked = text == "true" || text == "1" };
			check.CheckedChanged += (sender, e) => set(check.Checked == true ? "true" : "false");
			return check;
		}

		/// <summary>
		/// Cycles colours are linear; the picker shows sRGB.
		/// </summary>
		private static Control Colour(string text, Action<string> set)
		{
			var rgb = Numbers(text, 3);
			var picker = new ColorPicker { Value = new Color(ToSrgb(rgb[0]), ToSrgb(rgb[1]), ToSrgb(rgb[2])) };
			picker.ValueChanged += (sender, e) =>
			{
				var c = picker.Value;
				set(string.Join(" ", new[] { c.R, c.G, c.B }.Select(v => ToLinear(v).ToString("G6", CultureInfo.InvariantCulture))));
			};
			return picker;
		}

		private static Control Choice(string text, ShaderNodeSocketInfo socket, Action<string> set)
		{
			var choice = new DropDown();
			foreach (var entry in socket.EnumValues.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
				choice.Items.Add(new ListItem { Text = CyclesGraphXml.DisplayName(entry.Key), Key = entry.Key });
			choice.SelectedKey = text;

			choice.SelectedIndexChanged += (sender, e) =>
			{
				if (choice.SelectedKey != null)
					set(choice.SelectedKey);
			};
			return choice;
		}

		private static Control Text(string text, Action<string> set)
		{
			var committed = text ?? string.Empty;
			var box = new TextBox { Text = committed };
			void Commit()
			{
				if (box.Text == committed)
					return;
				committed = box.Text;
				set(committed);
			}

			box.LostFocus += (sender, e) => Commit();
			box.KeyDown += (sender, e) =>
			{
				if (e.Key == Keys.Enter)
					Commit();
			};
			return box;
		}

		private void SetValue(CyclesGraphMaterial material, string node, string socket, string value)
		{
			if (_graph == null)
				return;

			// The section already shows the value, and a rebuild would take the focus away.
			_xml = _graph.WithValue(node, socket, value);
			if (!material.SetGraphValue(_xml, node, socket, value))
				Application.Instance.AsyncInvoke(() => Refresh(true));
		}

		private static double[] Numbers(string text, int count)
		{
			var numbers = new double[count];
			var tokens = (text ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			for (var i = 0; i < count && i < tokens.Length; i++)
				double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i]);
			return numbers;
		}

		private static string Format(double value)
		{
			return ((float)value).ToString("R", CultureInfo.InvariantCulture);
		}

		private static float ToSrgb(double c)
		{
			c = Math.Max(0.0, Math.Min(1.0, c));
			return (float)(c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055);
		}

		private static double ToLinear(float c)
		{
			return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
		}
		#endregion
	}

	public sealed class CyclesGraphSurfaceSection : CyclesGraphOutputSection
	{
		protected override string OutputSocket => "surface";

		public override LocalizeStringPair Caption => new LocalizeStringPair("Surface", LOC.STR("Surface"));
	}

	public sealed class CyclesGraphVolumeSection : CyclesGraphOutputSection
	{
		protected override string OutputSocket => "volume";

		public override LocalizeStringPair Caption => new LocalizeStringPair("Volume", LOC.STR("Volume"));
	}

	public sealed class CyclesGraphDisplacementSection : CyclesGraphOutputSection
	{
		protected override string OutputSocket => "displacement";

		public override LocalizeStringPair Caption => new LocalizeStringPair("Displacement", LOC.STR("Displacement"));
	}
}
