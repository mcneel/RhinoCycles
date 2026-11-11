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

using ccl.ShaderNodes.Sockets;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// A Cycles shader graph in Cycles' own XML, as the Grasshopper 2 Cycles components write it.
	/// Any node type Cycles has can be used, as the graph is built by ccycles, not csycles.
	/// </summary>
	[Guid("E5B2C7F2-C65B-44DB-923E-A3EB96EE60DC")]
	/* Elevated, so with TestShowPrivateContent on it is in the short Create New list, not only under More Types. */
	[CustomRenderContent(IsPrivate = true, IsElevated = true)]
	public class CyclesGraphMaterial : RenderMaterial, ICyclesMaterial
	{
		/* Field names are shared with the Grasshopper 2 Cycles components, which set them by name. */
		public const string GraphXmlField = "cycles-graph-xml";
		private const string GraphXmlPrompt = "Graph XML";
		public const string SourceField = "cycles-graph-source";
		/* The Grasshopper 2 document the graph came from, for editing it again. */
		public const string DocumentField = "cycles-graph-document";
		/* Values the material editor changed in the XML that the document does not have yet. */
		public const string EditsField = "cycles-graph-edits";

		/* While the graph is empty, the Principled BSDF a new graph starts with in Grasshopper 2. */
		internal const string EmptyGraphXml = "<principled_bsdf name=\"principled_bsdf\" />" +
		                                     "<connect from=\"principled_bsdf BSDF\" to=\"output surface\" />";

		private static readonly Dictionary<Size, Bitmap> material_icons = new Dictionary<Size, Bitmap>();

		private string _graphXml = string.Empty;

		/* Set while this class changes the graph itself, which is not a replacement. */
		[ThreadStatic]
		private static bool g_changing;

		static CyclesGraphMaterial()
		{
			RenderContent.ContentFieldChanged += OnContentFieldChanged;
		}

		/// <summary>
		/// XML typed or pasted into the Graph XML field replaces the graph. Grasshopper 2 writes
		/// the field in the Program context, and the material's own changes are flagged.
		/// </summary>
		private static void OnContentFieldChanged(object sender, RenderContentFieldChangedEventArgs e)
		{
			// RDK names the field by its prompt here.
			var field = e.FieldName;
			if (g_changing || e.ChangeContext != ChangeContexts.UI || (field != GraphXmlField && field != GraphXmlPrompt) || !(e.Content is CyclesGraphMaterial changed))
				return;

			// Not while the content is in the middle of changing.
			var id = changed.Id;
			var doc = changed.DocumentAssoc;
			Eto.Forms.Application.Instance.AsyncInvoke(() =>
			{
				if (doc?.RenderMaterials.FirstOrDefault(m => m.Id == id) is CyclesGraphMaterial material &&
				    material.HasGraphOrigin() && material.Fields.TryGetValue(GraphXmlField, out string xml))
					material.ReplaceGraph(xml);
			});
		}

		/// <summary>
		/// Whether the graph came from a Grasshopper 2 document, which a replaced graph drops.
		/// </summary>
		private bool HasGraphOrigin()
		{
			return (Fields.TryGetValue(DocumentField, out byte[] document) && document?.Length > 0) ||
			       (Fields.TryGetValue(SourceField, out string source) && !string.IsNullOrEmpty(source)) ||
			       (Fields.TryGetValue(EditsField, out string edits) && !string.IsNullOrEmpty(edits));
		}

		public CyclesGraphMaterial()
		{
			Fields.Add(GraphXmlField, string.Empty, GraphXmlPrompt);
			var source = Fields.Add(SourceField, string.Empty, "Source");
			var document = Fields.Add(DocumentField, new byte[0]);
			var edits = Fields.Add(EditsField, string.Empty, "Edits");

			/* Hiding a field refreshes the content UI, which throws off the UI thread. The RDK
			 * copies materials there for previews; those copies never get a UI. */
			if (!Rhino.RhinoApp.InvokeRequired)
			{
				source.IsHiddenInAutoUI = true;
				document.IsHiddenInAutoUI = true;
				edits.IsHiddenInAutoUI = true;
			}
		}

		public override string TypeName => "Cycles Graph";

		public override string TypeDescription => "A Cycles shader graph in Cycles XML (Grasshopper)";

		public override bool Icon(Size size, out Bitmap bitmap)
		{
			bitmap = null;
			if (!material_icons.TryGetValue(size, out var icon))
			{
				Image img = Rhino.Resources.Assets.Rhino.SystemDrawing.Bitmaps.TryGet(Rhino.Resources.ResourceIds.Svg_CyclesMaterialSvg, new Size(48, 48));
				if (img == null)
					return false;
				icon = new Bitmap(img);
				material_icons.Add(size, icon);
			}

			// The caller owns the bitmap and disposes it, so it gets a copy of the cached one.
			bitmap = new Bitmap(icon);
			return true;
		}

		public override bool VirtualIcon(Size size, out Bitmap bitmap)
		{
			return Icon(size, out bitmap);
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddUserInterfaceSection(new CyclesGraphMaterialSection());
			AddUserInterfaceSection(new CyclesGraphSurfaceSection());
			AddUserInterfaceSection(new CyclesGraphVolumeSection());
			AddUserInterfaceSection(new CyclesGraphDisplacementSection());
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		/// <summary>
		/// Change one value of the graph, as the material editor does: in the open Grasshopper 2
		/// graph that writes this material if there is one, otherwise in the XML, kept as an edit
		/// for the stored document until Edit opens it.
		/// </summary>
		/// <param name="xml">The graph with the value changed.</param>
		/// <returns>false if the open graph keeps its own value.</returns>
		internal bool SetGraphValue(string xml, string node, string socket, string value)
		{
			switch (ApplyEditInGrasshopper(Id, node, socket, value))
			{
				case 1: return true;
				case 2: return false;
			}

			var edits = Fields.TryGetValue(EditsField, out string text) ? text : string.Empty;
			g_changing = true;
			try
			{
				BeginChange(ChangeContexts.UI);
				Fields.Set(GraphXmlField, xml);
				Fields.Set(EditsField, CyclesGraphXml.AddEdit(edits, node, socket, value));
				EndChange();
			}
			finally
			{
				g_changing = false;
			}
			return true;
		}

		/// <summary>
		/// Replace the whole graph, such as with XML copied from Blender. Its Grasshopper 2 document
		/// and waiting edits belong to the old graph, so they go, and Edit imports the XML.
		/// </summary>
		internal void ReplaceGraph(string xml)
		{
			FindGraphEditing()?.GetMethod("ReleaseMaterial", new[] { typeof(Guid) })?.Invoke(null, new object[] { Id });

			g_changing = true;
			try
			{
				BeginChange(ChangeContexts.UI);
				Fields.Set(GraphXmlField, xml ?? string.Empty);
				Fields.Set(SourceField, string.Empty);
				Fields.Set(DocumentField, new byte[0]);
				Fields.Set(EditsField, string.Empty);
				EndChange();
			}
			finally
			{
				g_changing = false;
			}
		}

		/// <summary>
		/// GraphEditing.ApplyEdit, if the Grasshopper 2 Cycles components are loaded: 0 when no
		/// open graph writes the material, 1 when the graph took the value, 2 when it keeps its own.
		/// </summary>
		private static int ApplyEditInGrasshopper(Guid materialId, string node, string socket, string value)
		{
			var apply = FindGraphEditing()?.GetMethod("ApplyEdit", new[] { typeof(Guid), typeof(string), typeof(string), typeof(string) });
			return apply == null ? 0 : (int)apply.Invoke(null, new object[] { materialId, node, socket, value });
		}

		/// <summary>
		/// Open the material's graph in Grasshopper 2, starting it if needed. The Grasshopper 2
		/// Cycles components do the work; RhinoCycles does not reference Grasshopper 2.
		/// </summary>
		/// <returns>false if the Cycles components for Grasshopper 2 are not installed.</returns>
		public static bool EditInGrasshopper(Guid materialId)
		{
			var editing = FindGraphEditing();
			if (editing == null && Rhino.RhinoDoc.ActiveDoc is Rhino.RhinoDoc doc)
			{
				// RunScript only posts the script while Rhino waits for input; this runs GH2 now.
				Rhino.RhinoApp.ExecuteCommand(doc, "_GH2");
				editing = FindGraphEditing();
			}

			var edit = editing?.GetMethod("EditMaterial", new[] { typeof(Guid) });
			return edit != null && (bool)edit.Invoke(null, new object[] { materialId });
		}

		private static Type FindGraphEditing()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => a.GetName().Name == "G2CyclesComponents")
				?.GetType("CyclesComponents.GraphEditing");
		}

		/// <summary>
		/// A physically based material for display modes that do not render Cycles, read from the
		/// shader on Surface; see <see cref="CyclesGraphLook"/>.
		/// </summary>
		public override void SimulateMaterial(ref Material simulatedMaterial, RenderTexture.TextureGeneration tg)
		{
			base.SimulateMaterial(ref simulatedMaterial, tg);

			var xml = Fields.TryGetValue(GraphXmlField, out string text) && !string.IsNullOrWhiteSpace(text) ? text : EmptyGraphXml;
			var graph = CyclesGraphXml.Parse(xml);
			var look = graph == null ? new CyclesGraphLook() : CyclesGraphLook.FromGraph(graph);

			// Rhino's material colours are gamma-encoded; the graph's are linear.
			var baseColour = ToSrgb(look.BaseColor);
			simulatedMaterial.ToPhysicallyBased();
			var pbr = simulatedMaterial.PhysicallyBased;
			pbr.BaseColor = baseColour;
			pbr.Metallic = Clamp(look.Metallic);
			pbr.Roughness = Clamp(look.Roughness);
			pbr.Alpha = Clamp(look.Alpha);
			pbr.Opacity = 1.0 - Clamp(look.Transmission);
			pbr.OpacityIOR = look.Ior;
			pbr.OpacityRoughness = Clamp(look.Roughness);
			pbr.Clearcoat = Clamp(look.Coat);
			pbr.ClearcoatRoughness = Clamp(look.CoatRoughness);
			pbr.Sheen = Clamp(look.Sheen);
			pbr.Subsurface = Clamp(look.Subsurface);
			pbr.SubsurfaceScatteringColor = baseColour;
			pbr.Emission = ToSrgb(look.Emission);

			if (tg != RenderTexture.TextureGeneration.Disallow && !string.IsNullOrEmpty(look.BaseColorImage) && System.IO.File.Exists(look.BaseColorImage))
				pbr.SetTexture(new Rhino.DocObjects.Texture { FileName = look.BaseColorImage }, Rhino.DocObjects.TextureType.PBR_BaseColor);

			simulatedMaterial.DiffuseColor = baseColour.AsSystemColor();
			simulatedMaterial.Name = Name;
		}

		private static Rhino.Display.Color4f ToSrgb(float[] linear)
		{
			float S(float c)
			{
				c = Math.Max(0f, Math.Min(1f, c));
				return (float)(c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055);
			}
			return new Rhino.Display.Color4f(S(linear[0]), S(linear[1]), S(linear[2]), 1f);
		}

		private static double Clamp(double x)
		{
			return Math.Max(0.0, Math.Min(1.0, x));
		}

		public float Gamma { get; set; }

		public ShaderBody.CyclesMaterial MaterialType => ShaderBody.CyclesMaterial.CustomRenderMaterial;

		public void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn)
		{
			if (Fields.TryGetValue(GraphXmlField, out string xml))
				_graphXml = xml ?? string.Empty;
		}

		public string MaterialXml => _graphXml;

		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			var xml = string.IsNullOrWhiteSpace(_graphXml) ? EmptyGraphXml : _graphXml;
			var parsed = ccl.Shader.GraphFromCyclesXml(sh, xml, out var problems);
			if (!string.IsNullOrEmpty(problems))
				RcCore.It.AddLogString($"Cycles Graph material '{Name}': {problems}");
			return parsed;
		}

		public ClosureSocket GetClosureSocket(ccl.Shader sh)
		{
			// The graph is built by ccycles and has no C# nodes.
			return null;
		}

		public Converters.BitmapConverter BitmapConverter { get; set; }
	}
}
