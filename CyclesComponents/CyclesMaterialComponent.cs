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
using System.Linq;

using GrasshopperIO;
using Grasshopper2.Components;
using Grasshopper2.Parameters;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;
using Rhino;
using Rhino.Render;

namespace CyclesComponents
{
	/// <summary>
	/// Writes a Cycles shader graph into a "Cycles Graph" render material in the Rhino document,
	/// creating it on the first solution and updating the same one after that. The material
	/// also gets this document, so its Edit button can open the graph again.
	/// </summary>
	[IoId("00000000-e98c-4231-9011-d063c3a7bcf7"), Grasshopper2.Author("Lars")]
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
			access.GetItem(0, out string xml);
			access.GetItem(1, out string name);

			var doc = RhinoDoc.ActiveDoc;
			if (doc is null)
			{
				access.AddError("No document", "There is no active Rhino document to add the material to.");
				return;
			}

			// One material per component, found again by the component's id.
			var source = InstanceId.ToString();
			var material = doc.RenderMaterials.FirstOrDefault(m => m.TypeId == GraphEditing.MaterialType &&
				m.Fields.TryGetValue(GraphEditing.SourceField, out string s) && s == source);

			if (material is null)
			{
				material = RenderContent.Create(doc, GraphEditing.MaterialType) as RenderMaterial;
				if (material is null)
				{
					access.AddError("No Cycles Graph material", "RhinoCycles is not loaded, or is too old to have the Cycles Graph material.");
					return;
				}
				if (string.IsNullOrWhiteSpace(name))
					name = "Cycles Graph";
			}

			var current = material.Fields.TryGetValue(GraphEditing.GraphXmlField, out string existing) ? existing : null;
			var rename = !string.IsNullOrWhiteSpace(name) && material.Name != name;
			var hasDocument = material.Fields.TryGetValue(GraphEditing.DocumentField, out byte[] stored) && stored?.Length > 0;
			if (current != xml || rename || !hasDocument)
			{
				// One change for graph and document, so a running Raytraced restarts once.
				material.BeginChange(RenderContent.ChangeContexts.Program);
				if (rename)
					material.Name = name;
				material.Fields.Set(GraphEditing.GraphXmlField, xml ?? string.Empty);
				material.Fields.Set(GraphEditing.SourceField, source);
				material.Fields.Set(GraphEditing.DocumentField, GraphEditing.Serialise(Document));
				material.EndChange();

				if (GraphEditing.IsMaterialDocument(Document))
					Document.Unmodify();
			}

			access.SetItem(0, material.Id.ToString());
		}
		#endregion
	}
}
