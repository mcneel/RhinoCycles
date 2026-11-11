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

using GrasshopperIO;
using Grasshopper2.Components;
using Grasshopper2.Parameters;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;
using Grasshopper2.UI.InputPanel;

namespace CyclesComponents
{
	/// <summary>
	/// The end of a Cycles shader graph: writes the graph as Cycles XML into a "Cycles Graph"
	/// render material, created on the first solution and updated after that. The material
	/// also gets this document, so its Edit button can open the graph again.
	/// </summary>
	[IoId("00000000-9b72-468f-9c8b-88878374dda5"), Grasshopper2.Author("Lars")]
	public sealed class CyclesMaterialOutput : Component
	{
		/* Input sockets of Cycles' output node, in parameter order. */
		private static readonly string[] g_outputSockets = { "surface", "volume", "displacement" };

		/* After XML. Documents from before it existed lack it. */
		private const int MaterialOutput = 1;

		#region constructor
		public CyclesMaterialOutput()
			: base(new Nomen("Material Output", "Write a Cycles shader graph into a Rhino render material. The same material is updated on every solution.", NodeCatalog.Chapter, "Output"))
		{
			// Render contents belong to the UI thread.
			Threading = ThreadingState.UiSingleThreaded;
		}

		public CyclesMaterialOutput(IReader reader) : base(reader)
		{
			Threading = ThreadingState.UiSingleThreaded;
			Paused = reader.HasItem(nameof(Paused)) && reader.Boolean(nameof(Paused));
			BoundMaterial = reader.HasItem(nameof(BoundMaterial)) && Guid.TryParse(reader.String(nameof(BoundMaterial)), out var bound) ? bound : Guid.Empty;
		}

		public override void Store(IWriter writer)
		{
			base.Store(writer);
			writer.Boolean(nameof(Paused), Paused);
			writer.String(nameof(BoundMaterial), BoundMaterial.ToString());
		}

		protected override void AddInputs(InputAdder inputs)
		{
			inputs.AddGeneric("Surface", g_outputSockets[0], "Shader for the surface.", Access.Item, Requirement.MayBeMissing);
			inputs.AddGeneric("Volume", g_outputSockets[1], "Shader for the volume.", Access.Item, Requirement.MayBeMissing);
			inputs.AddGeneric("Displacement", g_outputSockets[2], "Displacement vector.", Access.Item, Requirement.MayBeMissing);
		}

		protected override void AddOutputs(OutputAdder outputs)
		{
			outputs.AddText("XML", "X", "The shader graph in Cycles XML.");
			outputs.AddText("Material", "M", "Id of the render material.");
		}
		#endregion

		#region properties
		/// <summary>
		/// Not writing to the material, so it keeps its graph. Set after an incomplete import,
		/// which would otherwise replace the material's graph with what came across.
		/// </summary>
		internal bool Paused { get; set; }

		/// <summary>
		/// The material whose graph this is, which it writes only while opened from it; empty
		/// for a graph made in Grasshopper, which writes a material of its own.
		/// </summary>
		internal Guid BoundMaterial { get; set; }
		#endregion

		#region implementation
		protected override IIcon IconInternal => NodeCatalog.Icon("material_output");

		public override void AppendToInputPanel(InputPanel panel)
		{
			base.AppendToInputPanel(panel);

			if (!Paused)
				return;

			using (panel.BeginCategory("Material"))
			{
				var bar = panel.AddBar(false);
				bar.AddPushButton(null, new Nomen("Write to Material", "Replace the material's graph with this one, losing what the import could not bring across.", "Material", "Material"), Resume);
			}
		}

		private void Resume()
		{
			Paused = false;
			Expire();
			Document?.Solution.Start();
		}

		protected override void Process(IDataAccess access)
		{
			var connected = new List<KeyValuePair<string, CyclesSocket>>();
			for (var i = 0; i < g_outputSockets.Length; i++)
			{
				if (!access.GetItem(i, out object value) || value is null)
					continue;

				if (value is CyclesSocket socket)
					connected.Add(new KeyValuePair<string, CyclesSocket>(g_outputSockets[i], socket));
				else
					access.AddWarning("Not a Cycles socket", $"{access.NameIn(i)} takes the output of a Cycles node, not a {value.GetType().Name}.");
			}

			if (connected.Count == 0)
			{
				access.AddRemark("Nothing connected", "Connect a Cycles shader to Surface or Volume.");
				return;
			}

			var xml = CyclesXmlWriter.Write(connected);
			access.SetItem(0, xml);

			GraphEditing.OnUiThread(() => Write(access, xml));
		}

		private void Write(IDataAccess access, string xml)
		{
			// Documents from before Material Output wrote materials still write through Cycles Material.
			if (GraphEditing.HasCyclesMaterial(Document))
				return;

			if (Paused)
			{
				access.AddWarning("Not writing", "The import was incomplete, so the material keeps its own graph. Use Write to Material on this node to replace it.");
				return;
			}

			var opened = GraphEditing.IsMaterialDocument(Document);

			// Such as a copy Grasshopper restores from an autosave, which would make a new material.
			if (!opened && BoundMaterial != Guid.Empty)
			{
				access.AddRemark("Not writing", "This graph belongs to a material, which it writes only when opened with Edit in the material editor.");
				return;
			}

			if (opened && !GraphEditing.WritesOpenedMaterial(this))
			{
				access.AddRemark("Not writing", "Another Material Output writes the material of this graph.");
				return;
			}

			// The material is named in the material editor.
			var material = GraphEditing.WriteMaterial(this, access, xml, null, opened);
			if (material != null && Parameters.OutputCount > MaterialOutput)
				access.SetItem(MaterialOutput, material.Id.ToString());
		}
		#endregion
	}
}
