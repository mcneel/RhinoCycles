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

using Eto.Forms;
using Rhino.Render;
using Rhino.UI;
using Rhino.UI.Controls;
using System;
using System.Linq;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// The Edit button of a Cycles Graph material, which opens its graph in Grasshopper 2.
	/// </summary>
	public class CyclesGraphMaterialSection : EtoContentUISection3
	{
		public CyclesGraphMaterialSection()
		{
			var edit = new Button { Text = LOC.STR("Edit in Grasshopper") };
			edit.Click += OnEditClick;
			var paste = new Button { Text = LOC.STR("Paste XML") };
			paste.Click += OnPasteClick;

			Content = new TableLayout
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				Rows =
				{
					new TableRow(new Label { Text = LOC.STR("Nodes and links are edited in Grasshopper 2. Values can also be changed below."), Wrap = WrapMode.Word }),
					new TableRow(new TableLayout(new TableRow(new TableCell(edit, true), new TableCell(paste, true))) { Spacing = new Eto.Drawing.Size(5, 0) }),
					null
				}
			};
		}

		public override Guid ViewModelId => Guid.Empty;

		public override LocalizeStringPair Caption => new LocalizeStringPair("Shader Graph", LOC.STR("Shader Graph"));

		public override int SectionHeight => 70;

		private void OnEditClick(object sender, EventArgs e)
		{
			var materialId = SelectedMaterialId();
			if (materialId != Guid.Empty && !CyclesGraphMaterial.EditInGrasshopper(materialId))
				Rhino.RhinoApp.WriteLine(LOC.STR("The Cycles components for Grasshopper 2 are not installed, so this graph cannot be edited."));
		}

		/// <summary>
		/// Replace the graph with Cycles XML from the clipboard, such as copied from Blender.
		/// </summary>
		private void OnPasteClick(object sender, EventArgs e)
		{
			var materialId = SelectedMaterialId();
			var material = Rhino.RhinoDoc.ActiveDoc?.RenderMaterials.FirstOrDefault(m => m.Id == materialId) as CyclesGraphMaterial;
			if (material == null)
				return;

			string xml;
			using (var clipboard = new Clipboard())
				xml = clipboard.Text;

			var graph = CyclesGraphXml.Parse(xml);
			if (graph == null || graph.IsEmpty)
			{
				Rhino.RhinoApp.WriteLine(LOC.STR("The clipboard holds no Cycles XML to paste."));
				return;
			}

			material.ReplaceGraph(xml);
		}

		private Guid SelectedMaterialId()
		{
			if (!(ViewModel.GetData(Rhino.UI.Controls.DataSource.ProviderIds.ContentSelection, false, false) is RenderContentCollection collection))
				return Guid.Empty;

			using (var iterator = collection.Iterator())
				return iterator.First()?.Id ?? Guid.Empty;
		}
	}
}
