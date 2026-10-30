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
		public const string SourceField = "cycles-graph-source";
		/* The Grasshopper 2 document the graph came from, for editing it again. */
		public const string DocumentField = "cycles-graph-document";

		/* Shown while the graph is empty, rather than a black object. */
		private const string EmptyGraphXml = "<diffuse_bsdf name=\"d\" color=\"0.5 0.5 0.5\" />" +
		                                     "<connect from=\"d BSDF\" to=\"output surface\" />";

		private static readonly Dictionary<Size, Bitmap> material_icons = new Dictionary<Size, Bitmap>();

		private string _graphXml = string.Empty;

		public CyclesGraphMaterial()
		{
			Fields.Add(GraphXmlField, string.Empty, "Graph XML");
			var source = Fields.Add(SourceField, string.Empty, "Source");
			var document = Fields.Add(DocumentField, new byte[0]);

			/* Hiding a field refreshes the content UI, which throws off the UI thread. The RDK
			 * copies materials there for previews; those copies never get a UI. */
			if (!Rhino.RhinoApp.InvokeRequired)
			{
				source.IsHiddenInAutoUI = true;
				document.IsHiddenInAutoUI = true;
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
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		/// <summary>
		/// Open the material's graph in Grasshopper 2, starting it if needed. The Grasshopper 2
		/// Cycles components do the work; RhinoCycles does not reference Grasshopper 2.
		/// </summary>
		/// <returns>false if the Cycles components for Grasshopper 2 are not installed.</returns>
		public static bool EditInGrasshopper(Guid materialId)
		{
			var editing = FindGraphEditing();
			if (editing == null)
			{
				Rhino.RhinoApp.RunScript("_-GH2 _Enter", false);
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

		/* The colour of the first BSDF-ish colour input, for viewports that do not render Cycles. */
		private static readonly Regex g_colour = new Regex("\\b(?:base_color|color)=\"([-+0-9.eE]+) ([-+0-9.eE]+) ([-+0-9.eE]+)\"", RegexOptions.Compiled);

		public override void SimulateMaterial(ref Material simulatedMaterial, RenderTexture.TextureGeneration tg)
		{
			base.SimulateMaterial(ref simulatedMaterial, tg);

			var colour = Color.FromArgb(204, 204, 204);
			if (Fields.TryGetValue(GraphXmlField, out string xml) && !string.IsNullOrEmpty(xml))
			{
				var match = g_colour.Match(xml);
				if (match.Success)
					colour = Color.FromArgb(ToSrgbByte(match.Groups[1].Value), ToSrgbByte(match.Groups[2].Value), ToSrgbByte(match.Groups[3].Value));
			}

			simulatedMaterial.DiffuseColor = colour;
			simulatedMaterial.Name = Name;
		}

		private static int ToSrgbByte(string linear)
		{
			var c = double.Parse(linear, CultureInfo.InvariantCulture);
			c = Math.Max(0.0, Math.Min(1.0, c));
			var srgb = c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
			return (int)Math.Round(srgb * 255.0);
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
