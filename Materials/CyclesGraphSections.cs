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
	/// One output of a Node-Based material in the material editor, as Blender's material
	/// properties show it: the node plugged in with its inputs, and behind a linked input the
	/// node it comes from, unfolded on request. A changed value goes to the graph through
	/// <see cref="NodeBasedMaterial.SetGraphValue"/>.
	/// </summary>
	public abstract class CyclesGraphOutputSection : EtoContentUISection3
	{
		private const int MaxDepth = 16;

		private static int Indent => RhinoLayout.Padding(RhinoLayout.PaddingType.Indented).Left;

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
				_following = true;
				Refresh(false);
			};
			UnLoad += (sender, e) =>
			{
				RenderContent.ContentChanged -= OnContentChanged;
				_following = false;
			};
		}

		/// <summary>
		/// The input of Cycles' output node this section shows, e.g. "surface".
		/// </summary>
		protected abstract string OutputSocket { get; }

		public override Guid ViewModelId => Guid.Empty;

		public override int SectionHeight => (int)Math.Ceiling(Content.GetPreferredSize(new SizeF(Width > 0 ? Width : 300, float.PositiveInfinity)).Height);

		/// <summary>
		/// True to hide the section while nothing is connected to its output, as nodes are
		/// added in Grasshopper 2 and not here.
		/// </summary>
		protected virtual bool HiddenWhenUnlinked => false;

		/* Whether the output is linked, as of the last rebuild. Rebuilds follow the material's changes
		 * while the section is loaded; until then the holder's polling reads the field. */
		private bool _linked;
		private bool _following;
		private string _linkXml;

		public override bool Hidden => base.Hidden || (HiddenWhenUnlinked && !(_following ? _linked : IsLinkedInField()));

		private bool IsLinkedInField()
		{
			string xml = null;
			using (var iterator = GetSelection().Iterator())
			{
				// Several materials show a note in Surface only.
				if (iterator?.First() is NodeBasedMaterial selected && iterator.Next() == null)
					selected.Fields.TryGetValue(NodeBasedMaterial.GraphXmlField, out xml);
			}

			xml = xml ?? string.Empty;
			if (_linkXml == null || !string.Equals(xml, _linkXml, StringComparison.Ordinal))
			{
				_linkXml = xml;
				_linked = !string.IsNullOrWhiteSpace(xml) && CyclesGraphXml.Parse(xml)?.Source("output", OutputSocket).HasValue == true;
			}
			return _linked;
		}

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

			var material = SelectedMaterial(out var several);
			var xml = material != null && material.Fields.TryGetValue(NodeBasedMaterial.GraphXmlField, out string text) ? text : null;
			if (!force && (material?.Id ?? Guid.Empty) == _materialId && xml == _xml && several == _several)
				return;

			_materialId = material?.Id ?? Guid.Empty;
			_xml = xml;
			_several = several;
			Rebuild(material);
		}

		/* Whether more than one material is selected, which the section does not edit. */
		private bool _several;

		private NodeBasedMaterial SelectedMaterial(out bool several)
		{
			several = false;
			using (var iterator = GetSelection().Iterator())
			{
				if (!(iterator?.First() is NodeBasedMaterial selected))
					return null;
				several = iterator.Next() != null;

				// The document's own material, so a change is undoable and reaches Raytraced.
				return selected.DocumentAssoc?.RenderMaterials.FirstOrDefault(m => m.Id == selected.Id) as NodeBasedMaterial ?? selected;
			}
		}

		private void Rebuild(NodeBasedMaterial material)
		{
			var layout = new TableLayout
			{
				Padding = RhinoLayout.Padding(RhinoLayout.PaddingType.Table),
				Spacing = RhinoLayout.Spacing(RhinoLayout.SpacingType.Table),
			};

			// An empty graph renders as the Principled BSDF a new graph starts with.
			_graph = material == null || _several ? null : CyclesGraphXml.Parse(string.IsNullOrWhiteSpace(_xml) ? NodeBasedMaterial.EmptyGraphXml : _xml);
			var source = _graph?.Source("output", OutputSocket);
			_linked = source.HasValue;
			if (_several)
				layout.Rows.Add(new TableRow(RhinoLayout.NewLabel(LOC.STR("Select one material to edit its nodes."))));
			else if (_graph == null)
			{
				if (material != null)
					layout.Rows.Add(new TableRow(RhinoLayout.NewLabel(LOC.STR("The graph XML does not parse."))));
			}
			else if (source.HasValue)
				AddNode(layout, material, source.Value.Node, 0, OutputSocket, new HashSet<string>(StringComparer.Ordinal));
			else
				layout.Rows.Add(new TableRow(RhinoLayout.NewLabel(LOC.STR("None"))));

			layout.Rows.Add(null);
			Content = layout;
		}

		/// <summary>
		/// A node's title and inputs, and the nodes behind its unfolded linked inputs.
		/// </summary>
		private void AddNode(TableLayout layout, NodeBasedMaterial material, string node, int depth, string path, HashSet<string> visiting)
		{
			var type = _graph.NodeType(node);
			var nodeType = ShaderNodeTypes.Find(type);
			layout.Rows.Add(Heading(depth, CyclesGraphXml.DisplayName(type ?? node)));
			if (nodeType == null || depth >= MaxDepth || !visiting.Add(node))
				return;

			// Null while a link sets it.
			string Setting(string name)
			{
				if (_graph.Source(node, name).HasValue)
					return null;
				return _graph.Value(node, name) ?? CyclesGraphXml.ReaderDefault(type, name) ?? nodeType.Inputs.FirstOrDefault(s => s.Name == name)?.Default;
			}

			foreach (var socket in nodeType.Inputs)
			{
				// Texture mapping is left to Grasshopper, as Blender keeps it off the material properties.
				if (socket.IsInternal || socket.Name.StartsWith("tex_mapping.", StringComparison.Ordinal))
					continue;

				if (!CyclesInputUse.IsUsed(type, socket.Name, Setting))
					continue;

				var source = _graph.Source(node, socket.Name);
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
					layout.Rows.Add(Row(depth + 1, socket.UiName, link));
					if (unfolded)
						AddNode(layout, material, from, depth + 1, key, visiting);
					continue;
				}

				var defaultLink = DefaultLink(socket.Flags);
				var editor = defaultLink != null ? Note(defaultLink) : Editor(material, node, type, socket);
				if (editor != null)
					layout.Rows.Add(Row(depth + 1, socket.UiName, editor));
			}

			visiting.Remove(node);
		}

		/// <summary>
		/// What Cycles links an input to when nothing else is, which makes its value unused, or null.
		/// </summary>
		private static string DefaultLink(int flags)
		{
			/* Cycles SocketType::LINK_* flags, in the order ShaderGraph::default_inputs tests them. */
			if ((flags & (1 << 4)) != 0) return LOC.STR("Generated");
			if ((flags & (1 << 5)) != 0) return LOC.STR("Object Normal");
			if ((flags & (1 << 6)) != 0) return LOC.STR("UV");
			if ((flags & ((1 << 7) | (1 << 8))) != 0) return LOC.STR("Incoming");
			if ((flags & (1 << 9)) != 0) return LOC.STR("Shading Normal");
			if ((flags & (1 << 10)) != 0) return LOC.STR("Position");
			if ((flags & (1 << 11)) != 0) return LOC.STR("Radial (Z Axis)");
			return null;
		}

		private static Control Note(string text)
		{
			return new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
		}

		/// <summary>
		/// A node's name over a line, as Rhino's options lists head a group.
		/// </summary>
		private static TableRow Heading(int depth, string text)
		{
			return new TableRow(new Panel { Content = new LabelSeparator { Text = text }, Padding = new Padding(depth * Indent, 0, 0, 0) });
		}

		/// <summary>
		/// A label and its control, each taking half the width, as in Rhino's options lists. Only
		/// the label is indented, so the controls of nested nodes line up.
		/// </summary>
		private static TableRow Row(int depth, string label, Control editor)
		{
			var row = RhinoLayout.LabelRow(label, editor, true);
			// Out of the cell first, or the cell takes it back from the panel.
			var text = row.Cells[0].Control;
			row.Cells[0].Control = null;
			row.Cells[0].Control = new Panel { Content = text, Padding = new Padding(depth * Indent, 0, 0, 0) };
			foreach (var cell in row.Cells)
				cell.ScaleWidth = true;

			return new TableRow(new TableLayout(row)
			{
				Spacing = new Size(RhinoLayout.Spacing(RhinoLayout.SpacingType.Table).Width, 0),
			});
		}

		#region editors
		/// <summary>
		/// A control for an input's value, or null for inputs the material editor does not set,
		/// such as tables.
		/// </summary>
		private Control Editor(NodeBasedMaterial material, string node, string type, ShaderNodeSocketInfo socket)
		{
			var text = _graph.Value(node, socket.Name) ?? CyclesGraphXml.ReaderDefault(type, socket.Name) ?? socket.Default;
			var rebuild = CyclesInputUse.ChangesUse(type, socket.Name);
			void Set(string value) => SetValue(material, node, socket.Name, value, rebuild);

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
				case "closure": return Note(LOC.STR("None"));
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

			CommitOnce(stepper, () => set(decimals == 0
				? ((long)Math.Round(stepper.Value)).ToString(CultureInfo.InvariantCulture)
				: Format(stepper.Value)));
			return stepper;
		}

		private static Control Numbers(string text, int count, Action<string> set)
		{
			var values = Numbers(text, count);
			var steppers = values.Select(v => new NumericStepper { DecimalPlaces = 3, MaximumDecimalPlaces = 6, Increment = 0.01, Value = v }).ToList();
			foreach (var stepper in steppers)
				CommitOnce(stepper, () => set(string.Join(" ", steppers.Select(s => Format(s.Value)))));

			// The fields share the width, as narrow as the panel may be.
			return new TableLayout(new TableRow(steppers.Select(s => new TableCell(s, true)).ToArray())) { Spacing = new Size(2, 0) };
		}

		/// <summary>
		/// Pass a stepper's value on once: a typed value on Enter or when the stepper loses focus,
		/// and steps of its arrows after a short pause, so each change is one undo step.
		/// </summary>
		private static void CommitOnce(NumericStepper stepper, Action commit)
		{
			var committed = stepper.Value;
			var typing = false;
			var pause = new UITimer { Interval = 0.4 };

			void Commit()
			{
				pause.Stop();
				typing = false;
				if (stepper.Value.Equals(committed))
					return;
				committed = stepper.Value;
				commit();
			}

			pause.Elapsed += (sender, e) => Commit();
			stepper.ValueChanged += (sender, e) =>
			{
				pause.Stop();
				if (!typing)
					pause.Start();
			};
			stepper.KeyDown += (sender, e) =>
			{
				if (e.Key == Keys.Enter)
					Commit();
				else if (e.Key != Keys.Up && e.Key != Keys.Down && e.Key != Keys.PageUp && e.Key != Keys.PageDown)
					typing = true;
			};
			stepper.LostFocus += (sender, e) => Commit();
			stepper.UnLoad += (sender, e) => pause.Stop();
		}

		private static Control Flag(string text, Action<string> set)
		{
			var check = new CheckBox { Checked = IsTrue(text) };
			check.CheckedChanged += (sender, e) => set(check.Checked == true ? "true" : "false");
			return check;
		}

		/// <summary>
		/// A boolean as Cycles reads it: "true" in any case, or a number atoi reads as not 0.
		/// </summary>
		private static bool IsTrue(string text)
		{
			text = text ?? string.Empty;
			if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
				return true;

			var i = 0;
			while (i < text.Length && char.IsWhiteSpace(text[i]))
				i++;
			if (i < text.Length && (text[i] == '+' || text[i] == '-'))
				i++;
			for (; i < text.Length && text[i] >= '0' && text[i] <= '9'; i++)
			{
				if (text[i] != '0')
					return true;
			}
			return false;
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
			// Cycles' own names, as the XML has them.
			foreach (var entry in socket.EnumValues.Where(e => !string.IsNullOrWhiteSpace(e.Key)))
				choice.Items.Add(new ListItem { Text = entry.Key, Key = entry.Key });
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

		private void SetValue(NodeBasedMaterial material, string node, string socket, string value, bool rebuild)
		{
			// A control of an earlier build, such as a text box losing focus, can belong to another material.
			if (_graph == null || _several || material == null || material.Id != _materialId)
				return;

			// The section already shows the value, and a rebuild would take the focus away,
			// unless the value changes which inputs are shown.
			_xml = _graph.WithValue(node, socket, value);
			if (!material.SetGraphValue(_xml, node, socket, value))
				Application.Instance.AsyncInvoke(() => Refresh(true));
			else if (rebuild)
			{
				// From the edited XML: with the graph open in Grasshopper 2 the material has not changed yet.
				var materialId = material.Id;
				Application.Instance.AsyncInvoke(() =>
				{
					if (!IsDisposed && _materialId == materialId)
						Rebuild(material);
				});
			}
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

		protected override bool HiddenWhenUnlinked => true;

		public override LocalizeStringPair Caption => new LocalizeStringPair("Volume", LOC.STR("Volume"));
	}

	public sealed class CyclesGraphDisplacementSection : CyclesGraphOutputSection
	{
		protected override string OutputSocket => "displacement";

		protected override bool HiddenWhenUnlinked => true;

		public override LocalizeStringPair Caption => new LocalizeStringPair("Displacement", LOC.STR("Displacement"));
	}
}
