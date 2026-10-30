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
	/// The Edit button of a Cycles Graph material, which opens its graph in Grasshopper 2.
	/// </summary>
	public class CyclesGraphMaterialSection : EtoContentUISection3
	{
		public CyclesGraphMaterialSection()
		{
			var edit = new Button { Text = LOC.STR("Edit in Grasshopper") };
			edit.Click += OnEditClick;

			Content = new TableLayout
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				Rows =
				{
					new TableRow(new Label { Text = LOC.STR("The shader graph of this material is edited in Grasshopper 2."), Wrap = WrapMode.Word }),
					new TableRow(edit),
					null
				}
			};
		}

		public override Guid ViewModelId => Guid.Empty;

		public override LocalizeStringPair Caption => new LocalizeStringPair("Shader Graph", LOC.STR("Shader Graph"));

		public override int SectionHeight => 70;

		private void OnEditClick(object sender, EventArgs e)
		{
			var collection = ViewModel.GetData(Rhino.UI.Controls.DataSource.ProviderIds.ContentSelection, false, false) as RenderContentCollection;
			if (collection == null)
				return;

			Guid materialId;
			using (var iterator = collection.Iterator())
				materialId = iterator.First()?.Id ?? Guid.Empty;

			if (materialId != Guid.Empty && !CyclesGraphMaterial.EditInGrasshopper(materialId))
				Rhino.RhinoApp.WriteLine(LOC.STR("The Cycles components for Grasshopper 2 are not installed, so this graph cannot be edited."));
		}
	}
}
