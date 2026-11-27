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
using LOC = Rhino.UI.LOC;

namespace CyclesComponents
{
	/// <summary>
	/// The Grasshopper 2 document a Node-Based material carries, and opening it for editing.
	/// RhinoCycles' Edit button calls <see cref="EditMaterial"/> by reflection.
	/// </summary>
	public static class GraphEditing
	{
		/* RhinoCyclesCore.Materials.NodeBasedMaterial and its fields, which this plugin does not reference. */
		internal static readonly Guid MaterialType = new Guid("E5B2C7F2-C65B-44DB-923E-A3EB96EE60DC");
		internal const string GraphXmlField = "cycles-graph-xml";
		internal const string SourceField = "cycles-graph-source";
		internal const string DocumentField = "cycles-graph-gh2-document";
		internal const string EditsField = "cycles-graph-edits";

		/* The shader of a new graph. NodeBasedMaterial renders the same while its graph is empty. */
		private const string DefaultShader = "principled_bsdf";

		/* Documents opened from a material, by material id, and the Rhino document each material is
		 * in, which on the Mac need not be the active one. */
		private static readonly Dictionary<Guid, Document> g_open = new Dictionary<Guid, Document>();
		private static readonly Dictionary<Guid, uint> g_openIn = new Dictionary<Guid, uint>();
		/* The documents in g_open, for the solver thread, which must not read g_open. */
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<Document, bool> g_openDocuments =
			new System.Collections.Concurrent.ConcurrentDictionary<Document, bool>();
		private static bool g_watchingDocuments;

		private static RhinoDoc RhinoDocOf(Guid materialId)
		{
			return g_openIn.TryGetValue(materialId, out var serial) ? RhinoDoc.FromRuntimeSerialNumber(serial) : RhinoDoc.ActiveDoc;
		}

		private static void Forget(Guid materialId)
		{
			if (g_open.TryGetValue(materialId, out var doc))
				g_openDocuments.TryRemove(doc, out _);
			g_open.Remove(materialId);
			g_openIn.Remove(materialId);
		}

		/// <summary>
		/// Open the graph of a Node-Based material in the Grasshopper 2 editor, or bring it
		/// forward when it is open already. A material without a document gets a new one with
		/// a Principled BSDF into a Material Output that writes to it.
		/// </summary>
		/// <returns>false if the material or the editor could not be found.</returns>
		public static bool EditMaterial(Guid materialId)
		{
			var material = RhinoDoc.ActiveDoc?.RenderMaterials.FirstOrDefault(m => m.Id == materialId);
			if (material is null || material.TypeId != MaterialType)
				return false;

			// Throws here, before anything opens, when ccycles is missing or too old to list its nodes.
			_ = ShaderNodeTypes.All;

			var editor = Editor.ShowEditor();
			if (editor is null)
				return false;

			if (!g_watchingDocuments)
			{
				editor.Documents.Changed += DocumentsChanged;
				RhinoDoc.BeginSaveDocument += BeginSaveDocument;
				RhinoDoc.CloseDocument += CloseDocument;
				g_watchingDocuments = true;
			}

			if (g_open.TryGetValue(materialId, out var open) && editor.Documents.Index(open) >= 0)
			{
				editor.Documents.Push(open);
				return true;
			}

			// The graph that writes the material is still open, so that one rather than a copy of it.
			var writing = SourceGraph(editor, materialId);
			if (writing != null)
			{
				editor.Documents.Push(writing);
				return true;
			}

			// Paused when the graph lacks what the material has: changes it cannot place, which the
			// XML has, or a stored document that does not read, kept until Write to Material.
			bool pause;
			var doc = ReadDocument(material);
			if (doc != null)
				pause = !ApplyPendingEdits(material, doc);
			else
			{
				pause = HasDocument(material);
				if (pause)
					RhinoApp.WriteLine(string.Format(LOC.STR("The Grasshopper 2 document stored in '{0}' cannot be read, so the graph is rebuilt from the material's XML. Its Material Output is paused, so the material keeps that document until you use Write to Material."), material.Name));

				var hasXml = material.Fields.TryGetValue(GraphXmlField, out string xml) && !string.IsNullOrWhiteSpace(xml);
				doc = hasXml ? ImportGraphDocument(material, xml) : NewGraphDocument(material);

				// XML that does not parse is left as it is; the command line says why.
				if (doc is null)
					return true;
			}

			// Every output here writes this material only, so none makes a new one in the next
			// document after File > New, or in an autosave copy.
			var outputs = doc.Objects.ForwardsOfT<CyclesMaterialOutput>().ToList();
			foreach (var output in outputs.Where(o => o.BoundMaterial == Guid.Empty))
				output.BoundMaterial = material.Id;

			// The material's own copy is its graph from now on, paused or not, so the file the graph
			// came from no longer writes it.
			var writer = WriterOf(outputs, material);
			if (writer != null && !(SourceOf(material, out var braced) == writer.InstanceId && braced))
				Bind(material, writer);

			// The document has the waiting changes now, unless it lacks some; those wait on, and it does not write.
			if (!pause)
				ClearPendingEdits(material);
			else if (writer != null)
				writer.Paused = true;

			doc.File.FallbackName = material.Name;
			// The material holds it, so closing the tab need not offer to save a file.
			doc.Unmodify();

			if (g_open.TryGetValue(materialId, out var previous))
				g_openDocuments.TryRemove(previous, out _);
			g_open[materialId] = doc;
			g_openDocuments[doc] = true;
			g_openIn[materialId] = RhinoDoc.ActiveDoc.RuntimeSerialNumber;
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
		/// <see cref="IsMaterialDocument"/> for the solver thread.
		/// </summary>
		internal static bool IsMaterialDocumentAnyThread(Document doc)
		{
			return doc != null && g_openDocuments.ContainsKey(doc);
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

		/// <summary>
		/// The document as the material stores it: base64 text, which RDK undo can restore.
		/// </summary>
		internal static string Serialise(Document doc)
		{
			return Serialise(doc, true);
		}

		/* With or without the canvas view, which alone is no change to the graph. */
		private static string Serialise(Document doc, bool view)
		{
			var node = Node.Create("Document");
			doc.Store(node, view ? FileContents.Small | FileContents.Projection : FileContents.Small);
			return Convert.ToBase64String(IO.WriteNodeToByteArray(node, default));
		}

		/// <summary>
		/// Whether the material stores this graph, wherever its canvas was panned or zoomed to.
		/// </summary>
		private static bool StoresGraph(RenderMaterial material, Document doc, string text)
		{
			var stored = StoredDocument(material);
			if (stored is null)
				return false;
			if (stored == text)
				return true;

			var storedDoc = ReadDocument(material);
			return storedDoc != null && Serialise(storedDoc, false) == Serialise(doc, false);
		}

		/// <summary>
		/// The stored document's base64 text, or null if there is none.
		/// </summary>
		private static string StoredDocument(RenderMaterial material)
		{
			return material.Fields.TryGetValue(DocumentField, out string text) && !string.IsNullOrEmpty(text) ? text : null;
		}

		private static bool HasDocument(RenderMaterial material)
		{
			return StoredDocument(material) != null;
		}

		private static Document ReadDocument(RenderMaterial material)
		{
			var text = StoredDocument(material);
			if (text is null)
				return null;

			try
			{
				var reader = IO.ReadNodeFromByteArray(Convert.FromBase64String(text), default);
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
				RhinoApp.WriteLine(string.Format(LOC.STR("Cannot open '{0}' in Grasshopper 2. {1}"), material.Name, string.Join(" ", result.Problems)));
				return null;
			}

			// A lossy graph would overwrite the material's XML on the first solution, so the
			// output only writes when everything came across.
			var complete = result.Problems.Count == 0;
			result.Output.Paused = !complete;
			Bind(material, result.Output);

			RhinoApp.WriteLine(string.Format(LOC.STR("Imported the graph of '{0}' into Grasshopper 2: {1} nodes."),
				material.Name, result.Document.Objects.Count - 1));
			foreach (var ignored in result.Ignored)
				RhinoApp.WriteLine("  " + ignored);
			if (!complete)
			{
				foreach (var problem in result.Problems)
					RhinoApp.WriteLine("  " + problem);
				RhinoApp.WriteLine("  " + LOC.STR("The material keeps its XML until you use Write to Material on Material Output."));
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
			try
			{
				material.Fields.Set(SourceField, SourceText(output.InstanceId, true));
			}
			finally
			{
				material.EndChange();
			}
		}

		/* Material documents write the source braced, graphs outside one plain. A graph outside one,
		 * such as the file the material came from, no longer writes a material with a braced source. */
		private static string SourceText(Guid outputId, bool opened)
		{
			return outputId.ToString(opened ? "B" : "D");
		}

		/// <summary>
		/// The id of the output the material names as its source, or empty if there is none.
		/// </summary>
		/// <param name="opened">Whether a material document wrote it.</param>
		private static Guid SourceOf(RenderMaterial material, out bool opened)
		{
			var text = material.Fields.TryGetValue(SourceField, out string s) ? s : null;
			opened = text != null && text.StartsWith("{", StringComparison.Ordinal);
			return Guid.TryParse(text, out var id) ? id : Guid.Empty;
		}

		/// <summary>
		/// The output of a material document that writes the material: the one the material names
		/// as its source, or the first one when that is gone.
		/// </summary>
		private static CyclesMaterialOutput WriterOf(IReadOnlyList<CyclesMaterialOutput> outputs, RenderMaterial material)
		{
			var source = material is null ? Guid.Empty : SourceOf(material, out _);
			return outputs.FirstOrDefault(o => o.InstanceId == source) ?? outputs.FirstOrDefault();
		}

		/// <summary>
		/// Whether this output writes the material its document was opened from: the output the
		/// material is bound to, or the first one when that is gone. Others write nothing.
		/// </summary>
		internal static bool WritesOpenedMaterial(CyclesMaterialOutput output)
		{
			var id = MaterialOf(output.Document);
			var material = id.HasValue ? RhinoDocOf(id.Value)?.RenderMaterials.FirstOrDefault(m => m.Id == id.Value) : null;

			var outputs = output.Document.Objects.ForwardsOfT<CyclesMaterialOutput>().ToList();
			return ReferenceEquals(WriterOf(outputs, material), output);
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
		/// Write a graph into the Node-Based material of a writer, creating it on the first solution
		/// and updating the same one after that. The material also gets the writer's document.
		/// </summary>
		/// <param name="opened">Write to the material the document was opened from, if there is one.
		/// Otherwise the material is the one whose source is the writer.</param>
		/// <param name="rewrite">Write even if nothing changed, so changes waiting for the document go.</param>
		/// <returns>The material, or null if there is none; the writer then says why.</returns>
		internal static RenderMaterial WriteMaterial(Component writer, IDataAccess access, string xml, bool opened, bool rewrite)
		{
			var source = SourceText(writer.InstanceId, opened);
			var openedId = opened ? MaterialOf(writer.Document) : null;
			var doc = openedId.HasValue ? RhinoDocOf(openedId.Value) : RhinoDoc.ActiveDoc;
			if (doc is null)
			{
				access.AddError("No document", "There is no active Rhino document to add the material to.");
				return null;
			}

			RenderMaterial material;
			if (openedId.HasValue)
			{
				// Only the material it was opened from, not a duplicate sharing the writer's id.
				material = doc.RenderMaterials.FirstOrDefault(m => m.Id == openedId.Value && m.TypeId == MaterialType);
				if (material is null)
				{
					access.AddWarning("No material", "The material this graph was opened from is no longer in its Rhino document.");
					return null;
				}
			}
			else
			{
				// Once Edit has written the material, its own copy is the graph to change, not this one.
				material = MaterialOfSource(doc, writer.InstanceId);
				var edited = false;
				if (material != null)
					SourceOf(material, out edited);
				if (edited)
				{
					access.AddRemark("Not writing", "The material of this graph is now edited with Edit in the material editor, so this graph no longer writes it.");
					return null;
				}
			}

			// A new material's name; after that it is named in the material editor.
			string name = null;
			if (material is null)
			{
				material = RenderContent.Create(doc, MaterialType) as RenderMaterial;
				if (material is null)
				{
					access.AddError("No Node-Based material", "RhinoCycles is not loaded, or is too old to have the Node-Based material.");
					return null;
				}
				name = "Node-Based";
			}

			var current = material.Fields.TryGetValue(GraphXmlField, out string existing) ? existing : null;
			var currentSource = material.Fields.TryGetValue(SourceField, out string bound) ? bound : null;
			var rename = !string.IsNullOrWhiteSpace(name) && material.Name != name;
			if (rewrite || current != xml || currentSource != source || rename || !HasDocument(material))
			{
				var document = Serialise(writer.Document);

				// One change for graph and document, so a running Raytraced restarts once.
				material.BeginChange(RenderContent.ChangeContexts.Program);
				try
				{
					if (rename)
						material.Name = name;
					material.Fields.Set(GraphXmlField, xml ?? string.Empty);
					material.Fields.Set(SourceField, source);
					material.Fields.Set(DocumentField, document);
					// The document is the graph now, so earlier changes waiting for it are moot.
					material.Fields.Set(EditsField, string.Empty);
				}
				finally
				{
					material.EndChange();
				}

				if (IsMaterialDocument(writer.Document))
					writer.Document.Unmodify();
			}

			return material;
		}

		/// <summary>
		/// The first material whose source is this output, however it was written. A writer outside
		/// a material document writes it, unless a material document wrote it last.
		/// </summary>
		private static RenderMaterial MaterialOfSource(RhinoDoc doc, Guid outputId)
		{
			return outputId == Guid.Empty ? null : doc?.RenderMaterials.FirstOrDefault(m => m.TypeId == MaterialType && SourceOf(m, out _) == outputId);
		}

		/// <summary>
		/// Store the document in the material if it changed, and the XML's node positions if
		/// components moved since the graph last wrote it.
		/// </summary>
		internal static void StoreDocument(RenderMaterial material, Document doc)
		{
			// Such as after Change Type, which keeps the material's id.
			if (material.TypeId != MaterialType)
				return;

			var outputs = doc.Objects.ForwardsOfT<CyclesMaterialOutput>().ToList();
			var writer = WriterOf(outputs, material);

			// A stored document that does not read stays until Write to Material replaces it.
			if ((writer is null || writer.Paused) && HasDocument(material) && ReadDocument(material) is null)
				return;

			var text = Serialise(doc);

			// A paused output has not written the XML, whose nodes keep their own names.
			string placed = null;
			if (writer != null && !writer.Paused && material.Fields.TryGetValue(GraphXmlField, out string xml))
				placed = CyclesXmlWriter.WithPositions(xml, doc, writer);

			if (placed == null && StoresGraph(material, doc, text))
				return;

			material.BeginChange(RenderContent.ChangeContexts.Program);
			try
			{
				material.Fields.Set(DocumentField, text);
				if (placed != null)
					material.Fields.Set(GraphXmlField, placed);
			}
			finally
			{
				material.EndChange();
			}
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
			Forget(materialId);
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
		/// for <see cref="EditMaterial"/>, though a paused graph shows it; 1 if the graph took it;
		/// 2 if the graph keeps its own value.</returns>
		public static int ApplyEdit(Guid materialId, string node, string socket, string value)
		{
			var doc = OpenGraph(materialId);
			if (doc is null)
				return NoOpenGraph;

			var result = SetValue(doc, node, socket, value);

			// A paused graph does not write the material, so the material takes the change too.
			if (!WritesMaterial(doc, materialId))
			{
				if (result == EditResult.Applied)
					doc.Solution.Start();
				else
					RhinoApp.WriteLine(string.Format(LOC.STR("'{0}' of {1} is changed in the material only, as the paused Grasshopper 2 graph cannot take it. Write to Material would undo the change."), socket, node));
				return NoOpenGraph;
			}

			if (result == EditResult.Linked)
				RhinoApp.WriteLine(string.Format(LOC.STR("'{0}' of {1} is fed by a wire in Grasshopper 2, so it keeps the value the graph gives it."), socket, node));
			else if (result == EditResult.Missing)
				RhinoApp.WriteLine(string.Format(LOC.STR("The open Grasshopper 2 graph has no '{0}' of {1}, so the material keeps the graph's value."), socket, node));

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

			return SourceGraph(editor, materialId);
		}

		/// <summary>
		/// Whether an open graph of the material writes it: not when the output that would is
		/// paused, or gone. A graph outside a material document is open only if it writes.
		/// </summary>
		private static bool WritesMaterial(Document doc, Guid materialId)
		{
			if (!IsMaterialDocument(doc))
				return true;

			var material = RhinoDocOf(materialId)?.RenderMaterials.FirstOrDefault(m => m.Id == materialId);
			var writer = WriterOf(doc.Objects.ForwardsOfT<CyclesMaterialOutput>().ToList(), material);
			return writer != null && !writer.Paused;
		}

		/// <summary>
		/// An open document, not opened from a material, whose Material Output writes the material
		/// as its source, such as the graph that made it.
		/// </summary>
		private static Document SourceGraph(Editor editor, Guid materialId)
		{
			var rhinoDoc = RhinoDoc.ActiveDoc;
			var material = rhinoDoc?.RenderMaterials.FirstOrDefault(m => m.Id == materialId && m.TypeId == MaterialType);
			if (material is null)
				return null;

			// Written from a material document since, so no graph outside one writes it.
			var source = SourceOf(material, out var opened);
			if (source == Guid.Empty || opened)
				return null;

			// A duplicated material shares its source with the original, and the graph writes only the first.
			if (MaterialOfSource(rhinoDoc, source)?.Id != materialId)
				return null;

			// Only an output that writes: not one that belongs to a material, such as in a restored autosave.
			return editor.Documents.All.Select(stack => stack.Root).FirstOrDefault(d => d != null && !IsMaterialDocument(d) &&
				d.Objects.ForwardsOfT<CyclesMaterialOutput>().Any(o => o.InstanceId == source && !o.Paused && o.BoundMaterial == Guid.Empty));
		}

		/// <summary>
		/// Set the input of the component an XML node came from, as the material editor changed it.
		/// </summary>
		private static EditResult SetValue(Document doc, string node, string socketName, string text)
		{
			var components = doc.Objects.ForwardsOfT<CyclesNodeComponent>().ToList();
			var component = components.FirstOrDefault(c => CyclesXmlWriter.NodeName(c.NodeType, c.InstanceId) == node);

			// A paused import has not rewritten the XML, whose nodes keep the names the import labelled them with.
			if (component is null)
			{
				var labelled = components.Where(c => c.LabelText == node).ToList();
				component = labelled.Count == 1 ? labelled[0] : null;
			}
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
		/// The command line lists those the document cannot take.
		/// </summary>
		/// <returns>false if one no longer matches a component, or they do not parse, so the
		/// document lacks a change the XML has.</returns>
		private static bool ApplyPendingEdits(RenderMaterial material, Document doc)
		{
			var edits = PendingEdits(material);
			var missing = new List<string>();
			if (edits is null)
				missing.Add(LOC.STR("Changes that cannot be read."));
			else
			{
				foreach (var (node, socket, value) in edits)
				{
					switch (SetValue(doc, node, socket, value))
					{
						case EditResult.Missing:
							missing.Add(string.Format(LOC.STR("'{0}' of {1} set to {2}."), socket, node, value));
							break;
						case EditResult.Linked:
							RhinoApp.WriteLine(string.Format(LOC.STR("'{0}' of {1} is fed by a wire in Grasshopper 2, so the change from the material editor is not kept."), socket, node));
							break;
					}
				}
			}
			if (missing.Count == 0)
				return true;

			RhinoApp.WriteLine(string.Format(LOC.STR("'{0}' was changed in the material editor in ways its Grasshopper 2 graph cannot follow, so its Material Output is paused. The graph does not have:"), material.Name));
			foreach (var change in missing)
				RhinoApp.WriteLine("  " + change);
			RhinoApp.WriteLine("  " + LOC.STR("The material keeps its XML until you use Write to Material on Material Output."));
			return false;
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
			try
			{
				material.Fields.Set(EditsField, string.Empty);
			}
			finally
			{
				material.EndChange();
			}
		}
		#endregion

		private static void DocumentsChanged(object sender, DocumentBagEventArgs e)
		{
			if (e.Type != DocumentBagEventType.Removed)
				return;

			// Keep what changed since the last solution, such as moved components.
			foreach (var pair in g_open.Where(p => ReferenceEquals(p.Value, e.Stack.Root)).ToList())
			{
				var material = RhinoDocOf(pair.Key)?.RenderMaterials.FirstOrDefault(m => m.Id == pair.Key && m.TypeId == MaterialType);
				Forget(pair.Key);
				if (material is null)
					continue;

				// Not out of the event, so Grasshopper 2 still closes the tab.
				try
				{
					StoreDocument(material, pair.Value);
				}
				catch (Exception ex)
				{
					RhinoApp.WriteLine(string.Format(LOC.STR("The Grasshopper 2 graph of '{0}' could not be stored in the material: {1}"), material.Name, ex.Message));
				}
			}
		}

		/// <summary>
		/// Store the open graphs in their materials, so the file has what the tabs show.
		/// </summary>
		private static void BeginSaveDocument(object sender, DocumentSaveEventArgs e)
		{
			foreach (var pair in g_open.ToList())
			{
				var material = e.Document?.RenderMaterials.FirstOrDefault(m => m.Id == pair.Key && m.TypeId == MaterialType);
				if (material is null)
					continue;

				try
				{
					StoreDocument(material, pair.Value);
				}
				catch (Exception ex)
				{
					Grasshopper2.Diagnostics.Logger.Add(ex, $"{nameof(GraphEditing)}.{nameof(BeginSaveDocument)}()");
				}
			}
		}

		/// <summary>
		/// Forget the graphs opened from a closing document's materials. Their tabs stay as ordinary
		/// documents, whose Material Output belongs to a material and so writes nothing.
		/// </summary>
		private static void CloseDocument(object sender, DocumentEventArgs e)
		{
			foreach (var id in g_open.Keys.ToList())
				if (g_openIn.TryGetValue(id, out var serial) ? serial == e.Document?.RuntimeSerialNumber : e.Document?.RenderMaterials.Any(m => m.Id == id) == true)
					Forget(id);
		}
	}
}
