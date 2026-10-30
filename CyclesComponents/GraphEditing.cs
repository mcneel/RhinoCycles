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

using Eto.Drawing;
using GrasshopperIO;
using GrasshopperIO.DataBase;
using Grasshopper2.Components;
using Grasshopper2.Doc;
using Grasshopper2.Framework;
using Grasshopper2.Parameters;
using Grasshopper2.UI;
using Grasshopper2.UI.Canvas;
using Rhino;
using Rhino.Render;

namespace CyclesComponents
{
	/// <summary>
	/// The Grasshopper 2 document a Cycles Graph material carries, and opening it for editing.
	/// RhinoCycles' Edit button calls <see cref="EditMaterial"/> by reflection.
	/// </summary>
	public static class GraphEditing
	{
		/* RhinoCyclesCore.Materials.CyclesGraphMaterial and its fields, which this plugin does not reference. */
		internal static readonly Guid MaterialType = new Guid("E5B2C7F2-C65B-44DB-923E-A3EB96EE60DC");
		internal const string GraphXmlField = "cycles-graph-xml";
		internal const string SourceField = "cycles-graph-source";
		internal const string DocumentField = "cycles-graph-document";

		/* Documents opened from a material, by material id. */
		private static readonly Dictionary<Guid, Document> g_open = new Dictionary<Guid, Document>();
		private static bool g_watchingDocuments;

		/// <summary>
		/// Open the graph of a Cycles Graph material in the Grasshopper 2 editor, or bring it
		/// forward when it is open already. A material without a document gets a new one with
		/// a Material Output feeding a Cycles Material bound to it.
		/// </summary>
		/// <returns>false if the material or the editor could not be found.</returns>
		public static bool EditMaterial(Guid materialId)
		{
			var material = RhinoDoc.ActiveDoc?.RenderMaterials.FirstOrDefault(m => m.Id == materialId);
			if (material is null || material.TypeId != MaterialType)
				return false;

			var editor = Editor.ShowEditor();
			if (editor is null)
				return false;

			if (!g_watchingDocuments)
			{
				editor.Documents.Changed += DocumentsChanged;
				g_watchingDocuments = true;
			}

			if (g_open.TryGetValue(materialId, out var open) && editor.Documents.Index(open) >= 0)
			{
				editor.Documents.Push(open);
				return true;
			}

			var doc = ReadDocument(material) ?? ImportGraphDocument(material) ?? NewGraphDocument(material);
			doc.File.FallbackName = material.Name;
			// The material holds it, so closing the tab need not offer to save a file.
			doc.Unmodify();

			g_open[materialId] = doc;
			editor.Documents.Push(doc);
			return true;
		}

		/// <summary>
		/// Whether this document was opened from a material, which then stores it.
		/// </summary>
		internal static bool IsMaterialDocument(Document doc)
		{
			return doc != null && g_open.Values.Any(d => ReferenceEquals(d, doc));
		}

		internal static byte[] Serialise(Document doc)
		{
			var node = Node.Create("Document");
			doc.Store(node, FileContents.Small | FileContents.Projection);
			return IO.WriteNodeToByteArray(node, default);
		}

		private static Document ReadDocument(RenderMaterial material)
		{
			if (!material.Fields.TryGetValue(DocumentField, out byte[] bytes) || bytes is null || bytes.Length == 0)
				return null;

			try
			{
				var reader = IO.ReadNodeFromByteArray(bytes, default);
				return reader is null ? null : new Document(reader);
			}
			catch (Exception ex)
			{
				Grasshopper2.Diagnostics.Logger.Add(ex, $"{nameof(GraphEditing)}.{nameof(ReadDocument)}()");
				return null;
			}
		}

		private static Document NewGraphDocument(RenderMaterial material)
		{
			var doc = Document.NewInactiveDocument();

			var output = ObjectProxies.FindByType(typeof(CyclesMaterialOutput)).Emit();
			doc.Objects.Add(output, new PointF(400, 100));
			AddMaterialWriter(doc, (Component)output, material, true);

			return doc;
		}

		/// <summary>
		/// The graph built from the material's XML, or null if it has none.
		/// </summary>
		private static Document ImportGraphDocument(RenderMaterial material)
		{
			if (!material.Fields.TryGetValue(GraphXmlField, out string xml) || string.IsNullOrWhiteSpace(xml))
				return null;

			var result = GraphImport.Import(xml);

			// A lossy graph would overwrite the material's XML on the first solution, so it is
			// only connected to the material when everything came across.
			var complete = result.Problems.Count == 0;
			AddMaterialWriter(result.Document, result.Output, material, complete);

			RhinoApp.WriteLine(string.Format("Imported the graph of '{0}' into Grasshopper 2: {1} nodes.",
				material.Name, result.Document.Objects.Count - 2));
			if (!complete)
			{
				foreach (var problem in result.Problems)
					RhinoApp.WriteLine("  " + problem);
				RhinoApp.WriteLine("  Material Output is not connected to Cycles Material, so the material keeps its XML until you connect them.");
			}

			return result.Document;
		}

		/// <summary>
		/// Add a Cycles Material component after the output, bound to this material rather than a new one.
		/// </summary>
		private static void AddMaterialWriter(Document doc, Component output, RenderMaterial material, bool connect)
		{
			var writer = ObjectProxies.FindByType(typeof(CyclesMaterialComponent)).Emit();
			var pivot = output.Attributes.Pivot;
			doc.Objects.Add(writer, new PointF(pivot.X + 300, pivot.Y));
			if (connect)
				Connections.Connect(output.Parameters.Output(0), ((Component)writer).Parameters.Input(0));

			material.BeginChange(RenderContent.ChangeContexts.Program);
			material.Fields.Set(SourceField, writer.InstanceId.ToString());
			material.EndChange();
		}

		/// <summary>
		/// Store the document in the material if it changed.
		/// </summary>
		internal static void StoreDocument(RenderMaterial material, Document doc)
		{
			var bytes = Serialise(doc);
			if (material.Fields.TryGetValue(DocumentField, out byte[] existing) && existing != null && existing.SequenceEqual(bytes))
				return;

			material.BeginChange(RenderContent.ChangeContexts.Program);
			material.Fields.Set(DocumentField, bytes);
			material.EndChange();
		}

		private static void DocumentsChanged(object sender, DocumentBagEventArgs e)
		{
			if (e.Type != DocumentBagEventType.Removed)
				return;

			// Keep what changed since the last solution, such as moved components.
			foreach (var pair in g_open.Where(p => ReferenceEquals(p.Value, e.Stack.Root)).ToList())
			{
				var material = RhinoDoc.ActiveDoc?.RenderMaterials.FirstOrDefault(m => m.Id == pair.Key);
				if (material != null)
					StoreDocument(material, pair.Value);
				g_open.Remove(pair.Key);
			}
		}
	}
}
