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
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using ccl;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;

namespace CyclesComponents
{
	/// <summary>
	/// What the Cycles node registry does not say: which nodes to show, under which
	/// section and name, and which of their sockets make sense as parameters.
	/// </summary>
	internal static class NodeCatalog
	{
		internal const string Chapter = "Cycles";

		/* Node types Cycles adds or uses internally. */
		private static readonly HashSet<string> g_hidden = new HashSet<string>(StringComparer.Ordinal)
		{
			"output", "aov_output", "mix_closure_weight",
		};

		private static readonly HashSet<string> g_acronyms = new HashSet<string>(StringComparer.Ordinal)
		{
			"aov", "bsdf", "bw", "hsv", "ies", "ior", "rgb", "uv", "uvw", "xyz",
		};

		private static readonly Dictionary<string, string> g_sections = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["emission"] = "Shader", ["holdout"] = "Shader", ["add_closure"] = "Shader", ["mix_closure"] = "Shader",
			["background_shader"] = "Shader", ["subsurface_scattering"] = "Shader", ["absorption_volume"] = "Shader",
			["scatter_volume"] = "Shader", ["principled_volume"] = "Shader", ["volume_coefficients"] = "Shader",

			["texture_coordinate"] = "Input", ["geometry"] = "Input", ["attribute"] = "Input", ["light_path"] = "Input",
			["object_info"] = "Input", ["camera_info"] = "Input", ["layer_weight"] = "Input", ["fresnel"] = "Input",
			["uvmap"] = "Input", ["tangent"] = "Input", ["vertex_color"] = "Input", ["wireframe"] = "Input",
			["ambient_occlusion"] = "Input", ["bevel"] = "Input", ["hair_info"] = "Input", ["particle_info"] = "Input",
			["point_info"] = "Input", ["volume_info"] = "Input", ["light_falloff"] = "Input", ["scene_time"] = "Input",
			["raycast"] = "Input", ["value"] = "Input", ["color"] = "Input",

			["mix"] = "Colour", ["mix_color"] = "Colour", ["hsv"] = "Colour", ["invert"] = "Colour", ["gamma"] = "Colour",
			["brightness_contrast"] = "Colour", ["rgb_curves"] = "Colour",

			["mapping"] = "Vector", ["normal_map"] = "Vector", ["bump"] = "Vector", ["displacement"] = "Vector",
			["vector_displacement"] = "Vector", ["normal"] = "Vector", ["set_normal"] = "Vector",
			["vector_math"] = "Vector", ["vector_rotate"] = "Vector", ["vector_transform"] = "Vector",
			["vector_curves"] = "Vector", ["vector_map_range"] = "Vector", ["mix_vector"] = "Vector",
			["mix_vector_non_uniform"] = "Vector", ["radial_tiling"] = "Vector",
		};

		/// <summary>
		/// Node types to offer as components, sorted by name.
		/// </summary>
		internal static IEnumerable<ShaderNodeTypeInfo> VisibleTypes()
		{
			return ShaderNodeTypes.All.Where(t => !g_hidden.Contains(t.Name) && !t.Name.StartsWith("convert_", StringComparison.Ordinal));
		}

		/* rgb_ramp's colour and alpha tables, edited together as one gradient. */
		internal const string RampSocket = "ramp";
		internal const string RampAlphaSocket = "ramp_alpha";

		/* The curves behind a curve node's table, as Blender has them. */
		private static readonly (string Name, string Code)[] g_colourCurves = { ("Combined", "C"), ("Red", "R"), ("Green", "G"), ("Blue", "B") };
		private static readonly (string Name, string Code)[] g_vectorCurves = { ("X", "X"), ("Y", "Y"), ("Z", "Z") };
		private static readonly (string Name, string Code)[] g_floatCurve = { ("Curve", "curve") };

		/// <summary>
		/// Sockets that become component inputs. Arrays, transforms and node references
		/// need editors of their own, so they are left out for now; the colour ramp is a
		/// gradient, and curve tables are a function per channel.
		/// </summary>
		internal static IEnumerable<ShaderNodeSocketInfo> VisibleInputs(ShaderNodeTypeInfo nodeType)
		{
			return nodeType.Inputs.Where(s => !s.IsInternal && (IsSettable(s.Type) || IsRamp(s) || CurveChannels(s) != null));
		}

		internal static bool IsRamp(ShaderNodeSocketInfo socket)
		{
			return socket.Type == "array_color" && socket.Name == RampSocket;
		}

		/// <summary>
		/// The function inputs a curve table is made from, one per channel, or null for any other
		/// socket. rgb_curves has Blender's combined curve first, applied before red, green and blue.
		/// </summary>
		internal static (string Name, string Code)[] CurveChannels(ShaderNodeSocketInfo socket)
		{
			switch (socket.Type)
			{
				case "array_color" when socket.Name == "curves": return g_colourCurves;
				case "array_vector" when socket.Name == "curves": return g_vectorCurves;
				case "array_float" when socket.Name == "curve": return g_floatCurve;
				default: return null;
			}
		}

		private static bool IsSettable(string socketType)
		{
			return !socketType.StartsWith("array_", StringComparison.Ordinal) && socketType != "transform" && socketType != "node";
		}

		internal static string DisplayName(string nodeType)
		{
			var words = nodeType.Split('_').Select(w => g_acronyms.Contains(w)
				? w.ToUpperInvariant()
				: CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w));
			return string.Join(" ", words);
		}

		internal static string Section(string nodeType)
		{
			if (nodeType.StartsWith("rhino_", StringComparison.Ordinal)) return "Rhino";
			if (g_sections.TryGetValue(nodeType, out var section)) return section;
			if (nodeType.EndsWith("_bsdf", StringComparison.Ordinal)) return "Shader";
			if (nodeType.EndsWith("_texture", StringComparison.Ordinal)) return "Texture";
			return "Converter";
		}

		internal static Nomen Nomen(string nodeType)
		{
			return new Nomen(DisplayName(nodeType), $"The Cycles '{nodeType}' shader node.", Chapter, Section(nodeType));
		}

		internal static IIcon Icon(string nodeType)
		{
			return StandardIcons.LetterDisc(DisplayName(nodeType));
		}

		/// <summary>
		/// A stable proxy id per node type, so palettes and recent lists survive restarts.
		/// </summary>
		internal static Guid ProxyId(string nodeType)
		{
			using (var md5 = MD5.Create())
			{
				var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes("cycles-shader-node:" + nodeType));
				/* The 00000000 prefix Grasshopper's own ids use. */
				bytes[0] = bytes[1] = bytes[2] = bytes[3] = 0;
				return new Guid(bytes);
			}
		}

		/// <summary>
		/// The parameter name for a socket, unique among its siblings. Texture mapping
		/// sockets share display names with the texture's own ("Scale", "Type").
		/// </summary>
		internal static string ParameterName(ShaderNodeSocketInfo socket, IReadOnlyList<ShaderNodeSocketInfo> siblings)
		{
			var name = BaseParameterName(socket);
			var clashes = siblings.Any(s => !ReferenceEquals(s, socket) && BaseParameterName(s) == name);
			return clashes ? $"{name} ({socket.Name})" : name;
		}

		private static string BaseParameterName(ShaderNodeSocketInfo socket)
		{
			return socket.Name.StartsWith("tex_mapping.", StringComparison.Ordinal) ? "Mapping " + socket.UiName : socket.UiName;
		}
	}
}
