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

using GrasshopperIO;
using Grasshopper2.Components;
using Grasshopper2.Parameters;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;

namespace CyclesComponents
{
	/// <summary>
	/// Writes the XML of a Material Output into a "Cycles Graph" render material. Material Output
	/// writes the material itself now; this is kept for documents saved before, which still
	/// write through it.
	/// </summary>
	[IoId("00000000-e98c-4231-9011-d063c3a7bcf7"), Grasshopper2.Author("Lars")]
	[Obsolete("Material Output writes the material itself.")]
	public sealed class CyclesMaterialComponent : Component
	{
		#region constructor
		public CyclesMaterialComponent()
			: base(new Nomen("Cycles Material", "Write a Cycles shader graph into a Rhino render material. The same material is updated on every solution.", NodeCatalog.Chapter, "Output"))
		{
			// Render contents belong to the UI thread.
			Threading = ThreadingState.UiSingleThreaded;
		}

		public CyclesMaterialComponent(IReader reader) : base(reader)
		{
			Threading = ThreadingState.UiSingleThreaded;
		}

		protected override void AddInputs(InputAdder inputs)
		{
			inputs.AddText("XML", "X", "The shader graph in Cycles XML, from Material Output.");
			inputs.AddText("Name", "N", "Name of the render material.", Access.Item, Requirement.MayBeMissing);
		}

		protected override void AddOutputs(OutputAdder outputs)
		{
			outputs.AddText("Material", "M", "Id of the render material.");
		}
		#endregion

		#region implementation
		protected override IIcon IconInternal => NodeCatalog.Icon("cycles_material");

		protected override void Process(IDataAccess access)
		{
			// Old documents Grasshopper restores from an autosave would otherwise make a new material each time.
			if (!GraphEditing.IsMaterialDocument(Document))
			{
				access.AddRemark("Not writing", "Cycles Material only writes the material its graph was opened from with Edit.");
				return;
			}

			access.GetItem(0, out string xml);
			access.GetItem(1, out string name);

			GraphEditing.OnUiThread(() =>
			{
				var material = GraphEditing.WriteMaterial(this, access, xml, name, true);
				if (material != null)
					access.SetItem(0, material.Id.ToString());
			});
		}
		#endregion
	}
}
