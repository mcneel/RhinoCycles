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

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// The Edit button of a Node-Based material, which opens its graph in Grasshopper 2.
	/// </summary>
	public class NodeBasedMaterialSection : EtoContentUISection3
	{
		public NodeBasedMaterialSection()
		{
			var edit = new Button { Text = LOC.STR("Edit in Grasshopper") };
			edit.Click += OnEditClick;

			Content = new TableLayout
			{
				Padding = RhinoLayout.Padding(RhinoLayout.PaddingType.Table),
				Spacing = RhinoLayout.Spacing(RhinoLayout.SpacingType.Table),
				Rows = { new TableRow(new TableCell(edit), new TableCell(null, true)), null }
			};
		}

		public override Guid ViewModelId => Guid.Empty;

		public override LocalizeStringPair Caption => new LocalizeStringPair("Nodes", LOC.STR("Nodes"));

		public override int SectionHeight => (int)Math.Ceiling(Content.GetPreferredSize(new Eto.Drawing.SizeF(Width > 0 ? Width : 300, float.PositiveInfinity)).Height);

		private void OnEditClick(object sender, EventArgs e)
		{
			var materialId = SelectedMaterialId();
			if (materialId != Guid.Empty && !NodeBasedMaterial.EditInGrasshopper(materialId))
				Rhino.RhinoApp.WriteLine(LOC.STR("The Cycles components for Grasshopper 2 are not installed, so this graph cannot be edited."));
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
