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
using Rhino.UI;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// A Cycles shader graph in Cycles' own XML, as the Grasshopper 2 Cycles components write it.
	/// Any node type Cycles has can be used, as the graph is built by ccycles, not csycles.
	/// </summary>
	[Guid("E5B2C7F2-C65B-44DB-923E-A3EB96EE60DC")]
	[CustomRenderContent(IsElevated = true)]
	public class NodeBasedMaterial : RenderMaterial, ICyclesMaterial
	{
		/* Field names are shared with the Grasshopper 2 Cycles components, which set them by name. */
		public const string GraphXmlField = "cycles-graph-xml";
		private const string GraphXmlPrompt = "Cycles XML";
		public const string SourceField = "cycles-graph-source";
		/* The Grasshopper 2 document the graph came from, for editing it again. Base64 text, as RDK
		 * undo cannot restore a byte array field. */
		public const string DocumentField = "cycles-graph-gh2-document";
		/* Where materials saved before kept that document, as bytes. Only read; it keeps its type,
		 * as Debug builds assert when a field read from a file changes type. */
		public const string BytesDocumentField = "cycles-graph-document";
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

		static NodeBasedMaterial()
		{
			RenderContent.ContentFieldChanged += OnContentFieldChanged;
		}

		/// <summary>
		/// XML typed or pasted into the Cycles XML field replaces the graph. Grasshopper 2 writes
		/// the field in the Program context, and the material's own changes are flagged.
		/// </summary>
		private static void OnContentFieldChanged(object sender, RenderContentFieldChangedEventArgs e)
		{
			// RDK names the field by its prompt here.
			var field = e.FieldName;
			if (g_changing || e.ChangeContext != ChangeContexts.UI || (field != GraphXmlField && field != GraphXmlPrompt) || !(e.Content is NodeBasedMaterial changed))
				return;

			// Not while the content is in the middle of changing.
			var id = changed.Id;
			var doc = changed.DocumentAssoc;
			Eto.Forms.Application.Instance.AsyncInvoke(() =>
			{
				if (!(doc?.RenderMaterials.FirstOrDefault(m => m.Id == id) is NodeBasedMaterial material) ||
				    !material.Fields.TryGetValue(GraphXmlField, out string xml))
					return;

				// A whole material file, as the Blender add-on copies it, gives its graph.
				var graph = GraphFromMaterialFile(xml);
				if (graph != null)
					material.ReplaceGraph(graph);
				else if (material.HasGraphOrigin())
					material.ReplaceGraph(xml);
			});
		}

		/// <summary>
		/// The graph in the XML of a Node-Based material file (.rmtl), or null if the text is not one.
		/// </summary>
		internal static string GraphFromMaterialFile(string text)
		{
			var body = Regex.Replace(text ?? string.Empty, "<\\?xml[^>]*\\?>", string.Empty).TrimStart();
			if (!body.StartsWith("<xml", StringComparison.Ordinal))
				return null;

			try
			{
				var material = XElement.Parse(body).Element("material");
				var parameter = material?.Descendants("parameter").FirstOrDefault(p => (string)p.Attribute("name") == GraphXmlField);
				return parameter?.Value;
			}
			catch (System.Xml.XmlException)
			{
				return null;
			}
		}

		/// <summary>
		/// Whether the graph came from a Grasshopper 2 document, which a replaced graph drops.
		/// </summary>
		private bool HasGraphOrigin()
		{
			return (Fields.TryGetValue(DocumentField, out string document) && !string.IsNullOrEmpty(document)) ||
			       (Fields.TryGetValue(BytesDocumentField, out byte[] bytes) && bytes?.Length > 0) ||
			       (Fields.TryGetValue(SourceField, out string source) && !string.IsNullOrEmpty(source)) ||
			       (Fields.TryGetValue(EditsField, out string edits) && !string.IsNullOrEmpty(edits));
		}

		public NodeBasedMaterial()
		{
			Fields.Add(GraphXmlField, string.Empty, GraphXmlPrompt);
			var source = Fields.Add(SourceField, string.Empty, "Source");
			var document = Fields.Add(DocumentField, string.Empty);
			var bytesDocument = Fields.Add(BytesDocumentField, new byte[0]);
			var edits = Fields.Add(EditsField, string.Empty, "Edits");

			/* Hiding a field refreshes the content UI, which throws off the UI thread. The RDK
			 * copies materials there for previews; those copies never get a UI. */
			if (!Rhino.RhinoApp.InvokeRequired)
			{
				source.IsHiddenInAutoUI = true;
				document.IsHiddenInAutoUI = true;
				bytesDocument.IsHiddenInAutoUI = true;
				edits.IsHiddenInAutoUI = true;
			}
		}

		public override string TypeName => LOC.STR("Node-Based");

		public override string TypeDescription => LOC.STR("A Cycles shader node graph, edited in Grasshopper 2");

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
			AddUserInterfaceSection(new NodeBasedMaterialSection());
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
			var doc = DocumentAssoc;
			var undo = doc?.BeginUndoRecord(LOC.STR("Node-Based Value")) ?? 0;
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
				if (undo != 0)
					doc.EndUndoRecord(undo);
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

			// Its own undo step, as the field edit has ended by now.
			var doc = DocumentAssoc;
			var undo = doc?.BeginUndoRecord(LOC.STR("Replace Node-Based Graph")) ?? 0;
			g_changing = true;
			try
			{
				BeginChange(ChangeContexts.UI);
				Fields.Set(GraphXmlField, xml ?? string.Empty);
				Fields.Set(SourceField, string.Empty);
				Fields.Set(DocumentField, string.Empty);
				if (Fields.TryGetValue(BytesDocumentField, out byte[] bytes) && bytes?.Length > 0)
					Fields.Set(BytesDocumentField, new byte[0]);
				Fields.Set(EditsField, string.Empty);
				EndChange();
			}
			finally
			{
				g_changing = false;
				if (undo != 0)
					doc.EndUndoRecord(undo);
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
		/// <returns>false if the graph did not open; the command line says why.</returns>
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
			if (edit == null)
			{
				Rhino.RhinoApp.WriteLine(LOC.STR("The Cycles components for Grasshopper 2 are not installed, so this graph cannot be edited."));
				return false;
			}

			if ((bool)edit.Invoke(null, new object[] { materialId }))
				return true;

			Rhino.RhinoApp.WriteLine(LOC.STR("Grasshopper 2 could not open the graph: the material is not a Node-Based material in the active document, or Grasshopper 2 did not start."));
			return false;
		}

		private static Type FindGraphEditing()
		{
			return AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => a.GetName().Name == "G2CyclesComponents")
				?.GetType("CyclesComponents.GraphEditing");
		}

		/// <summary>
		/// Changing another material to Node-Based starts the graph from that material: a Principled
		/// BSDF with the values Raytraced renders it with. Textures are not carried over.
		/// </summary>
		public override MatchDataResult MatchData(RenderContent oldContent)
		{
			var result = base.MatchData(oldContent);
			if (oldContent is NodeBasedMaterial || !(oldContent is RenderMaterial old))
				return result;

			var xml = GraphFromMaterial(old);
			if (xml == null)
				return result;

			g_changing = true;
			try
			{
				Fields.Set(GraphXmlField, xml);
			}
			finally
			{
				g_changing = false;
			}
			return MatchDataResult.Some;
		}

		/* RhinoFullNxt's IOR for the specular of an opaque material. */
		private const double DielectricIor = 1.5;

		/// <summary>
		/// A Principled BSDF graph from a material's physically based values, converted as
		/// RhinoFullNxt converts them for Raytraced.
		/// </summary>
		private static string GraphFromMaterial(RenderMaterial old)
		{
			var material = old.ToMaterial(RenderTexture.TextureGeneration.Skip);
			if (material == null)
				return null;
			var wasPhysicallyBased = material.IsPhysicallyBased;
			if (!wasPhysicallyBased)
				material.ToPhysicallyBased();
			var pbr = material.PhysicallyBased;
			if (pbr == null)
				return null;

			var doc = old.DocumentAssoc ?? Rhino.RhinoDoc.ActiveDoc;
			var gamma = PreProcessGamma(doc);
			double[] Linear(Rhino.Display.Color4f c) => new[] { Math.Pow(Math.Max(0f, c.R), gamma), Math.Pow(Math.Max(0f, c.G), gamma), Math.Pow(Math.Max(0f, c.B), gamma) };
			double[] Mix(double[] a, double[] b, double t) => new[] { a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]), a[2] + t * (b[2] - a[2]) };
			double[] Tint(double amount, double[] colour) => Mix(new[] { 1.0, 1.0, 1.0 }, colour, Clamp(amount));

			var productPreset = doc != null &&
				RenderPresetHelpers.ProductPreset(Utilities.GetEngineDocumentSettings(doc.RuntimeSerialNumber)) == RenderPresetHelpers.Presets.Product;
			if (productPreset && wasPhysicallyBased && CyclesShader.WhatMaterial(old, material) == CyclesShader.ProbableMaterial.Gem)
				return GemGraph(Linear(pbr.BaseColor), Clamp(pbr.OpacityRoughness), pbr.OpacityIOR);

			var transmission = Clamp(1.0 - pbr.Opacity);
			var subsurface = Clamp(pbr.Subsurface);

			// 4.x has no SSS colour, so it is mixed into the base colour.
			var baseColour = Mix(Linear(pbr.BaseColor), Linear(pbr.SubsurfaceScatteringColor), subsurface * (1.0 - transmission));
			// 4.x has no transmission roughness or IOR of its own; both follow transmission.
			var roughness = Clamp(pbr.Roughness);
			roughness += transmission * (Clamp(pbr.OpacityRoughness) - roughness);
			var ior = DielectricIor + transmission * (pbr.OpacityIOR - DielectricIor);
			var radius = pbr.SubsurfaceScatteringRadius;

			// Except under the Product preset, Raytraced lets shadow rays through as much as the
			// material transmits, tinted by its base colour, and cuts alpha out around that.
			var colouredShadow = transmission > 0.0 && doc != null && !productPreset;
			var alpha = Clamp(pbr.Alpha);

			var node = new XElement("principled_bsdf",
				new XAttribute("name", "principled_bsdf"),
				new XAttribute("base_color", Format(baseColour)),
				new XAttribute("metallic", Format(Clamp(pbr.Metallic))),
				new XAttribute("roughness", Format(roughness)),
				new XAttribute("ior", Format(ior)),
				new XAttribute("alpha", Format(colouredShadow ? 1.0 : alpha)),
				new XAttribute("specular_ior_level", Format(pbr.Specular)),
				new XAttribute("specular_tint", Format(Tint(pbr.SpecularTint, baseColour))),
				new XAttribute("anisotropic", Format(Clamp(pbr.Anisotropic))),
				new XAttribute("anisotropic_rotation", Format(pbr.AnisotropicRotation)),
				new XAttribute("transmission_weight", Format(transmission)),
				new XAttribute("sheen_weight", Format(Clamp(pbr.Sheen))),
				new XAttribute("sheen_tint", Format(Tint(pbr.SheenTint, baseColour))),
				// Rhino draws its clearcoat at a quarter; Coat Weight is unscaled.
				new XAttribute("coat_weight", Format(0.25 * Clamp(pbr.Clearcoat))),
				new XAttribute("coat_roughness", Format(Clamp(pbr.ClearcoatRoughness))),
				new XAttribute("subsurface_method", "random_walk_skin"),
				new XAttribute("subsurface_weight", Format(subsurface > 0.0 ? 1.0 : 0.0)),
				new XAttribute("subsurface_scale", Format(subsurface)),
				new XAttribute("subsurface_radius", Format(radius, radius, radius)));

			// Rhino folds the emission strength into the colour.
			var strength = old.Fields.TryGetValue("intensity", out double intensity) ? intensity
				: old.Fields.TryGetValue("emission-multiplier", out double multiplier) ? multiplier : 0.0;
			if (strength > 0.0)
			{
				var e = pbr.Emission;
				var unscaled = new Rhino.Display.Color4f((float)(e.R / strength), (float)(e.G / strength), (float)(e.B / strength), 1f);
				node.Add(new XAttribute("emission_color", Format(Linear(unscaled))));
				node.Add(new XAttribute("emission_strength", Format(strength)));
			}

			// Anisotropy follows the UV map's tangent, as in Raytraced.
			var nodes = new List<XElement>
			{
				new XElement("tangent", new XAttribute("name", "uv_tangent"), new XAttribute("direction_type", "uv_map"), new XAttribute("attribute", "uvmap1")),
				node,
			};
			var links = new List<(string From, string To)> { ("uv_tangent tangent", "principled_bsdf tangent") };
			var surface = "principled_bsdf BSDF";

			if (colouredShadow)
			{
				// One transmission for the BSDF and the shadow, as in Raytraced.
				nodes.Add(new XElement("value", new XAttribute("name", "transmission"), new XAttribute("value", Format(transmission))));
				nodes.Add(new XElement("light_path", new XAttribute("name", "light_path")));
				nodes.Add(new XElement("math", new XAttribute("name", "coloured_shadow_switch"), new XAttribute("math_type", "multiply")));
				nodes.Add(new XElement("transparent_bsdf", new XAttribute("name", "coloured_shadow"), new XAttribute("color", Format(Linear(pbr.BaseColor)))));
				// Fac is linked; 0 is what camera rays see, which the Rendered mode look follows.
				nodes.Add(new XElement("mix_closure", new XAttribute("name", "coloured_shadow_mix"), new XAttribute("fac", "0")));
				links.Add(("transmission value", "principled_bsdf transmission_weight"));
				links.Add(("transmission value", "coloured_shadow_switch value2"));
				links.Add(("light_path is_shadow_ray", "coloured_shadow_switch value1"));
				links.Add(("coloured_shadow_switch value", "coloured_shadow_mix fac"));
				links.Add(("principled_bsdf BSDF", "coloured_shadow_mix closure1"));
				links.Add(("coloured_shadow BSDF", "coloured_shadow_mix closure2"));
				surface = "coloured_shadow_mix closure";

				if (alpha < 1.0)
				{
					nodes.Add(new XElement("transparent_bsdf", new XAttribute("name", "alpha_cutter"), new XAttribute("color", "1 1 1")));
					nodes.Add(new XElement("mix_closure", new XAttribute("name", "alpha_cutter_mix"), new XAttribute("fac", Format(alpha))));
					links.Add(("alpha_cutter BSDF", "alpha_cutter_mix closure1"));
					links.Add((surface, "alpha_cutter_mix closure2"));
					surface = "alpha_cutter_mix closure";
				}
			}
			links.Add((surface, "output surface"));

			return GraphXml(nodes, links);
		}

		/// <summary>
		/// RhinoFullNxt's gem for the Product preset: glass split into red, green and blue at IORs
		/// spread by the dispersion, with the base colour's glass for other rays than the camera's.
		/// </summary>
		private static string GemGraph(double[] baseColour, double roughness, double opacityIor)
		{
			var baseIor = Math.Max(opacityIor, 1.001);
			var dispersion = 0.045 * Math.Max(1.0 - roughness, 0.0);
			XElement Glass(string name, string colour, double ior) => new XElement("glass_bsdf", new XAttribute("name", name),
				new XAttribute("color", colour), new XAttribute("IOR", Format(ior)), new XAttribute("roughness", Format(roughness)));

			var nodes = new List<XElement>
			{
				Glass("gem_red_channel", "1 0 0", Math.Max(baseIor - dispersion, 1.001)),
				Glass("gem_green_channel", "0 1 0", baseIor),
				Glass("gem_blue_channel", "0 0 1", baseIor + dispersion),
				Glass("gem_core", Format(baseColour), baseIor),
				new XElement("add_closure", new XAttribute("name", "gem_add_rg_channels")),
				new XElement("add_closure", new XAttribute("name", "gem_add_rgb_channels")),
				new XElement("light_path", new XAttribute("name", "gem_light_path")),
				// Fac is linked; the Rendered mode look follows the base colour's glass.
				new XElement("mix_closure", new XAttribute("name", "gem_camera_ray_mix"), new XAttribute("fac", "0")),
				new XElement("mix_closure", new XAttribute("name", "gem_final_mix"), new XAttribute("fac", "0.75")),
			};
			var links = new List<(string From, string To)>
			{
				("gem_red_channel BSDF", "gem_add_rg_channels closure1"),
				("gem_green_channel BSDF", "gem_add_rg_channels closure2"),
				("gem_add_rg_channels closure", "gem_add_rgb_channels closure1"),
				("gem_blue_channel BSDF", "gem_add_rgb_channels closure2"),
				("gem_light_path is_camera_ray", "gem_camera_ray_mix fac"),
				("gem_core BSDF", "gem_camera_ray_mix closure1"),
				("gem_add_rgb_channels closure", "gem_camera_ray_mix closure2"),
				("gem_camera_ray_mix closure", "gem_final_mix closure1"),
				("gem_add_rgb_channels closure", "gem_final_mix closure2"),
				("gem_final_mix closure", "output surface"),
			};
			return GraphXml(nodes, links);
		}

		private static string GraphXml(IEnumerable<XElement> nodes, IEnumerable<(string From, string To)> links)
		{
			return string.Concat(nodes.Select(n => n.ToString(SaveOptions.DisableFormatting))) +
			       string.Concat(links.Select(l => new XElement("connect", new XAttribute("from", l.From), new XAttribute("to", l.To)).ToString(SaveOptions.DisableFormatting)));
		}

		/// <summary>
		/// The gamma Raytraced applies to material colours; see ChangeDatabase.PreProcessGamma.
		/// </summary>
		private static double PreProcessGamma(Rhino.RhinoDoc doc)
		{
			if (doc == null)
				return 2.2;
			using (var lw = doc.RenderSettings.LinearWorkflow)
				return lw.PreProcessColors || lw.PreProcessTextures ? lw.PreProcessGamma : 1.0;
		}

		private static string Format(params double[] values)
		{
			return string.Join(" ", values.Select(v => ((float)v).ToString("G6", CultureInfo.InvariantCulture)));
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

			// Rendered mode reflects with the legacy fields.
			pbr.SynchronizeLegacyMaterial();
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

		/* Where a node sits in a node editor, which Cycles would report as an input it does not have. */
		private static readonly Regex g_position = new Regex("\\s+ui_position\\s*=\\s*(\"[^\"]*\"|'[^']*')", RegexOptions.Compiled);

		public bool GetShader(ccl.Shader sh, bool finalize)
		{
			var xml = g_position.Replace(string.IsNullOrWhiteSpace(_graphXml) ? EmptyGraphXml : _graphXml, string.Empty);
			var parsed = ccl.Shader.GraphFromCyclesXml(sh, xml, out var problems);
			if (!string.IsNullOrEmpty(problems))
				RcCore.It.AddLogString($"Node-Based material '{Name}': {problems}");
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
