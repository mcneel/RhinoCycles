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
using System.Xml.Linq;

using ccl;
using Eto.Drawing;
using GrasshopperIO;
using GrasshopperIO.DataBase;
using Grasshopper2.Components;
using Grasshopper2.Data;
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
		internal const string EditsField = "cycles-graph-edits";

		/* The shader of a new graph. CyclesGraphMaterial renders the same while its graph is empty. */
		private const string DefaultShader = "principled_bsdf";

		/* Documents opened from a material, by material id. */
		private static readonly Dictionary<Guid, Document> g_open = new Dictionary<Guid, Document>();
		private static bool g_watchingDocuments;

		/// <summary>
		/// Open the graph of a Cycles Graph material in the Grasshopper 2 editor, or bring it
		/// forward when it is open already. A material without a document gets a new one with
		/// a Principled BSDF into a Material Output that writes to it.
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

			var doc = ReadDocument(material);
			if (doc != null && !ApplyPendingEdits(material, doc))
			{
				RhinoApp.WriteLine(string.Format("'{0}' was changed in the material editor in a way its Grasshopper 2 graph cannot follow, so the graph is rebuilt from the material.", material.Name));
				doc = null;
			}
			if (doc is null)
			{
				var hasXml = material.Fields.TryGetValue(GraphXmlField, out string xml) && !string.IsNullOrWhiteSpace(xml);
				doc = hasXml ? ImportGraphDocument(material, xml) : NewGraphDocument(material);

				// XML that does not parse is left as it is; the command line says why.
				if (doc is null)
					return true;
			}
			ClearPendingEdits(material);
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
			return MaterialOf(doc).HasValue;
		}

		/// <summary>
		/// The id of the material this document was opened from, or null for any other document.
		/// </summary>
		internal static Guid? MaterialOf(Document doc)
		{
			foreach (var pair in g_open)
				if (doc != null && ReferenceEquals(pair.Value, doc))
					return pair.Key;
			return null;
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

			// Laid out as GraphImport does: top-aligned columns, placed by their centre.
			float Centre(Component c) => 50f + 0.5f * GraphImport.RowHeight * (Math.Max(c.Parameters.InputCount, c.Parameters.OutputCount) + 1);

			var output = (CyclesMaterialOutput)ObjectProxies.FindByType(typeof(CyclesMaterialOutput)).Emit();
			doc.Objects.Add(output, new PointF(50f + GraphImport.ColumnWidth, Centre(output)));

			// A Principled BSDF into Surface, as a new material starts in Blender.
			if (ObjectProxies.FindById(NodeCatalog.ProxyId(DefaultShader))?.Emit() is Component shader)
			{
				doc.Objects.Add(shader, new PointF(50f, Centre(shader)));
				Connections.Connect(shader.Parameters.Output(0), output.Parameters.Input(0));
			}

			Bind(material, output);

			return doc;
		}

		/// <summary>
		/// The graph built from the material's XML, or null if the XML does not parse.
		/// </summary>
		private static Document ImportGraphDocument(RenderMaterial material, string xml)
		{
			var result = GraphImport.Import(xml);
			if (result.Output is null)
			{
				RhinoApp.WriteLine(string.Format("Cannot open '{0}' in Grasshopper 2. {1}", material.Name, string.Join(" ", result.Problems)));
				return null;
			}

			// A lossy graph would overwrite the material's XML on the first solution, so the
			// output only writes when everything came across.
			var complete = result.Problems.Count == 0;
			result.Output.Paused = !complete;
			Bind(material, result.Output);

			RhinoApp.WriteLine(string.Format("Imported the graph of '{0}' into Grasshopper 2: {1} nodes.",
				material.Name, result.Document.Objects.Count - 1));
			if (!complete)
			{
				foreach (var problem in result.Problems)
					RhinoApp.WriteLine("  " + problem);
				RhinoApp.WriteLine("  The material keeps its XML until you use Write to Material on Material Output.");
			}

			return result.Document;
		}

		/// <summary>
		/// Make the output write to this material rather than a new one.
		/// </summary>
		private static void Bind(RenderMaterial material, CyclesMaterialOutput output)
		{
			output.BoundMaterial = material.Id;
			material.BeginChange(RenderContent.ChangeContexts.Program);
			material.Fields.Set(SourceField, output.InstanceId.ToString());
			material.EndChange();
		}

		/// <summary>
		/// Whether the document has a Cycles Material, which wrote materials before Material Output did.
		/// </summary>
		internal static bool HasCyclesMaterial(Document doc)
		{
#pragma warning disable CS0618 // Obsolete, but still in documents saved before.
			return doc != null && doc.Objects.ForwardsOfT<CyclesMaterialComponent>().Any();
#pragma warning restore CS0618
		}

		/// <summary>
		/// Whether this output writes the material its document was opened from: the output the
		/// material is bound to, or the first one when that is gone. Others write nothing.
		/// </summary>
		internal static bool WritesOpenedMaterial(CyclesMaterialOutput output)
		{
			var id = MaterialOf(output.Document);
			var material = id.HasValue ? RhinoDoc.ActiveDoc?.RenderMaterials.FirstOrDefault(m => m.Id == id.Value) : null;
			var source = material != null && material.Fields.TryGetValue(SourceField, out string s) ? s : null;

			var outputs = output.Document.Objects.ForwardsOfT<CyclesMaterialOutput>().ToList();
			var writer = outputs.FirstOrDefault(o => o.InstanceId.ToString() == source) ?? outputs.FirstOrDefault();
			return ReferenceEquals(writer, output);
		}

		/// <summary>
		/// Run a material write on the UI thread, which RDK requires. A component with one iteration
		/// solves on the solver thread whatever its Threading says.
		/// </summary>
		internal static void OnUiThread(Action write)
		{
			Eto.Forms.Application.Instance.Invoke(write);
		}

		/// <summary>
		/// Write a graph into the Cycles Graph material of a writer, creating it on the first solution
		/// and updating the same one after that. The material also gets the writer's document.
		/// </summary>
		/// <param name="opened">Write to the material the document was opened from, if there is one.
		/// Otherwise the material is the one whose source is the writer.</param>
		/// <returns>The material, or null if there is none; the writer then says why.</returns>
		internal static RenderMaterial WriteMaterial(Component writer, IDataAccess access, string xml, string name, bool opened)
		{
			var doc = RhinoDoc.ActiveDoc;
			if (doc is null)
			{
				access.AddError("No document", "There is no active Rhino document to add the material to.");
				return null;
			}

			// A duplicated material shares the writer's id, so the opened one is looked for first.
			var source = writer.InstanceId.ToString();
			var openedId = opened ? MaterialOf(writer.Document) : null;
			var material = (openedId.HasValue ? doc.RenderMaterials.FirstOrDefault(m => m.Id == openedId.Value && m.TypeId == MaterialType) : null)
				?? doc.RenderMaterials.FirstOrDefault(m => m.TypeId == MaterialType &&
					m.Fields.TryGetValue(SourceField, out string s) && s == source);

			if (material is null)
			{
				material = RenderContent.Create(doc, MaterialType) as RenderMaterial;
				if (material is null)
				{
					access.AddError("No Cycles Graph material", "RhinoCycles is not loaded, or is too old to have the Cycles Graph material.");
					return null;
				}
				if (string.IsNullOrWhiteSpace(name))
					name = "Cycles Graph";
			}

			var current = material.Fields.TryGetValue(GraphXmlField, out string existing) ? existing : null;
			var currentSource = material.Fields.TryGetValue(SourceField, out string bound) ? bound : null;
			var rename = !string.IsNullOrWhiteSpace(name) && material.Name != name;
			var hasDocument = material.Fields.TryGetValue(DocumentField, out byte[] stored) && stored?.Length > 0;
			if (current != xml || currentSource != source || rename || !hasDocument)
			{
				// One change for graph and document, so a running Raytraced restarts once.
				material.BeginChange(RenderContent.ChangeContexts.Program);
				if (rename)
					material.Name = name;
				material.Fields.Set(GraphXmlField, xml ?? string.Empty);
				material.Fields.Set(SourceField, source);
				material.Fields.Set(DocumentField, Serialise(writer.Document));
				// The document is the graph now, so earlier changes waiting for it are moot.
				material.Fields.Set(EditsField, string.Empty);
				material.EndChange();

				if (IsMaterialDocument(writer.Document))
					writer.Document.Unmodify();
			}

			return material;
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

		/// <summary>
		/// Close the graph opened from a material whose graph was replaced, such as by pasted XML,
		/// without storing it back, so Edit imports the new graph. RhinoCycles calls this by reflection.
		/// </summary>
		public static void ReleaseMaterial(Guid materialId)
		{
			if (!g_open.TryGetValue(materialId, out var doc))
				return;

			// Forgotten first, so closing the tab does not store it in the material again.
			g_open.Remove(materialId);
			Editor.Instance?.Documents.Pop(doc, false);
		}

		#region edits from the material editor
		/* What ApplyEdit returns. */
		private const int NoOpenGraph = 0;
		private const int EditApplied = 1;
		private const int GraphKeepsValue = 2;

		private enum EditResult { Applied, Linked, Missing }

		/// <summary>
		/// Pass a value the material editor changed in the XML on to the open Grasshopper 2 graph
		/// that writes the material, which then writes the material again. RhinoCycles calls
		/// this by reflection.
		/// </summary>
		/// <returns>0 if no open graph writes the material, so the change waits in the material
		/// for <see cref="EditMaterial"/>; 1 if the graph took it; 2 if the graph keeps its own value.</returns>
		public static int ApplyEdit(Guid materialId, string node, string socket, string value)
		{
			var doc = OpenGraph(materialId);
			if (doc is null)
				return NoOpenGraph;

			var result = SetValue(doc, node, socket, value);
			if (result == EditResult.Linked)
				RhinoApp.WriteLine(string.Format("'{0}' of {1} is fed by a wire in Grasshopper 2, so it keeps the value the graph gives it.", socket, node));
			else if (result == EditResult.Missing)
				RhinoApp.WriteLine(string.Format("The open Grasshopper 2 graph has no '{0}' of {1}, so the material keeps the graph's value.", socket, node));

			// Written again from the graph either way, so the material shows what the graph holds.
			foreach (var output in doc.Objects.ForwardsOfT<CyclesMaterialOutput>())
				output.Expire();
			doc.Solution.Start();
			return result == EditResult.Applied ? EditApplied : GraphKeepsValue;
		}

		/// <summary>
		/// The open document that writes the material: the one opened from it, or one whose
		/// Material Output the material names as its source.
		/// </summary>
		private static Document OpenGraph(Guid materialId)
		{
			var editor = Editor.Instance;
			if (editor is null)
				return null;

			if (g_open.TryGetValue(materialId, out var opened) && editor.Documents.Index(opened) >= 0)
				return opened;

			var material = RhinoDoc.ActiveDoc?.RenderMaterials.FirstOrDefault(m => m.Id == materialId);
			var source = material != null && material.Fields.TryGetValue(SourceField, out string s) ? s : null;
			if (string.IsNullOrEmpty(source))
				return null;

			// A duplicated material shares its source with the original, whose document writes only that one.
			return editor.Documents.All.Select(stack => stack.Root).FirstOrDefault(d => d != null && !IsMaterialDocument(d) &&
				d.Objects.ForwardsOfT<CyclesMaterialOutput>().Any(o => o.InstanceId.ToString() == source));
		}

		/// <summary>
		/// Set the input of the component an XML node came from, as the material editor changed it.
		/// </summary>
		private static EditResult SetValue(Document doc, string node, string socketName, string text)
		{
			var component = doc.Objects.ForwardsOfT<CyclesNodeComponent>()
				.FirstOrDefault(c => CyclesXmlWriter.NodeName(c.NodeType, c.InstanceId) == node);
			var socket = component is null ? null : ShaderNodeTypes.Find(component.NodeType)?.Inputs.FirstOrDefault(s => s.Name == socketName);
			var parameter = socket is null ? null : GraphImport.FindParameter(component, component.NodeType, socketName, true);
			var value = parameter is null ? null : GraphImport.ParseValue(socket, text);
			if (value is null)
				return EditResult.Missing;

			// A wire wins over a value.
			if (parameter.Inputs.Count > 0)
				return EditResult.Linked;

			parameter.PersistentDataWeak = Garden.ITreeFromList(new[] { value });
			parameter.Expire();
			return EditResult.Applied;
		}

		/// <summary>
		/// Changes the material editor made while the document was closed, onto its components.
		/// </summary>
		/// <returns>false if one no longer matches a component, so the document cannot follow the XML.</returns>
		private static bool ApplyPendingEdits(RenderMaterial material, Document doc)
		{
			var edits = PendingEdits(material);
			if (edits is null)
				return false;

			foreach (var (node, socket, value) in edits)
			{
				switch (SetValue(doc, node, socket, value))
				{
					case EditResult.Missing:
						return false;
					case EditResult.Linked:
						RhinoApp.WriteLine(string.Format("'{0}' of {1} is fed by a wire in Grasshopper 2, so the change from the material editor is not kept.", socket, node));
						break;
				}
			}
			return true;
		}

		/// <summary>
		/// The material's changes waiting for its document, oldest first, or null if they do not parse.
		/// </summary>
		private static List<(string Node, string Socket, string Value)> PendingEdits(RenderMaterial material)
		{
			var edits = new List<(string Node, string Socket, string Value)>();
			if (!material.Fields.TryGetValue(EditsField, out string text) || string.IsNullOrWhiteSpace(text))
				return edits;

			try
			{
				foreach (var set in XElement.Parse("<edits>" + text + "</edits>").Elements("set"))
				{
					var node = (string)set.Attribute("node");
					var socket = (string)set.Attribute("socket");
					var value = (string)set.Attribute("value");
					if (node is null || socket is null || value is null)
						return null;
					edits.Add((node, socket, value));
				}
				return edits;
			}
			catch (System.Xml.XmlException)
			{
				return null;
			}
		}

		private static void ClearPendingEdits(RenderMaterial material)
		{
			if (!material.Fields.TryGetValue(EditsField, out string text) || string.IsNullOrEmpty(text))
				return;

			material.BeginChange(RenderContent.ChangeContexts.Program);
			material.Fields.Set(EditsField, string.Empty);
			material.EndChange();
		}
		#endregion

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
