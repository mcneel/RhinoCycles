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

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// Inputs a node's settings leave unused, as Cycles compiles the node, so the material editor
	/// can leave them out the way Blender does. Socket and enum names are Cycles' own.
	/// </summary>
	internal static class CyclesInputUse
	{
		private sealed class Rule
		{
			internal Rule(string socket, string[] settings, Func<Func<string, string>, bool> used)
			{
				Socket = socket;
				Settings = settings;
				Used = used;
			}

			internal string Socket { get; }
			internal string[] Settings { get; }
			internal Func<Func<string, string>, bool> Used { get; }
		}

		private static readonly Dictionary<string, List<Rule>> g_rules = Build();

		/// <summary>
		/// False if the node's settings leave the input unused.
		/// </summary>
		/// <param name="nodeType">Cycles node type, e.g. "noise_texture".</param>
		/// <param name="socket">Input socket name, e.g. "w".</param>
		/// <param name="setting">The node's value of a setting by socket name, or null while a link sets it.</param>
		internal static bool IsUsed(string nodeType, string socket, Func<string, string> setting)
		{
			if (nodeType == null || !g_rules.TryGetValue(nodeType, out var rules))
				return true;

			return rules.Where(r => r.Socket == socket).All(r => r.Used(setting));
		}

		/// <summary>
		/// True if the input is a setting other inputs of the node depend on.
		/// </summary>
		internal static bool ChangesUse(string nodeType, string socket)
		{
			return nodeType != null && g_rules.TryGetValue(nodeType, out var rules) && rules.Any(r => r.Settings.Contains(socket));
		}

		private static Dictionary<string, List<Rule>> Build()
		{
			var all = new Dictionary<string, List<Rule>>(StringComparer.Ordinal);

			void Add(string nodeTypes, string sockets, string[] settings, Func<Func<string, string>, bool> used)
			{
				foreach (var nodeType in nodeTypes.Split(' '))
				{
					if (!all.TryGetValue(nodeType, out var rules))
						all[nodeType] = rules = new List<Rule>();
					rules.AddRange(sockets.Split(' ').Select(s => new Rule(s, settings, used)));
				}
			}
			// A setting a link decides could be anything, so its inputs stay.
			void If(string nodeTypes, string sockets, string setting, params string[] values) =>
				Add(nodeTypes, sockets, new[] { setting }, s => !(s(setting) is string v) || values.Contains(v));
			void Unless(string nodeTypes, string sockets, string setting, params string[] values) =>
				Add(nodeTypes, sockets, new[] { setting }, s => !(s(setting) is string v) || !values.Contains(v));

			Unless("noise_texture white_noise_texture voronoi_texture", "vector", "dimensions", "1D");
			If("noise_texture white_noise_texture voronoi_texture", "w", "dimensions", "1D", "4D");
			If("noise_texture", "offset", "type", "hybrid_multifractal", "ridged_multifractal", "hetero_terrain");
			If("noise_texture", "gain", "type", "hybrid_multifractal", "ridged_multifractal");
			If("noise_texture", "use_normalize", "type", "fBM");

			// Edges and sphere radii are measured straight, as is any distance in 1D.
			Unless("voronoi_texture", "metric exponent", "dimensions", "1D");
			Unless("voronoi_texture", "metric exponent", "feature", "distance_to_edge", "n_sphere_radius");
			If("voronoi_texture", "exponent", "metric", "minkowski");
			If("voronoi_texture", "smoothness", "feature", "smooth_f1");
			Unless("voronoi_texture", "detail roughness lacunarity use_normalize", "feature", "n_sphere_radius");

			If("wave_texture", "bands_direction", "wave_type", "bands");
			If("wave_texture", "rings_direction", "wave_type", "rings");
			If("gabor_texture", "orientation_2d", "type", "2D");
			If("gabor_texture", "orientation_3d", "type", "3D");
			If("image_texture", "projection_blend", "projection", "box");

			If("metallic_bsdf", "color edge_tint", "fresnel_type", "f82");
			If("metallic_bsdf", "ior k", "fresnel_type", "physical_conductor");

			// Thin Wall, an int read as atoi reads it, replaces subsurface scattering with a lobe that keeps only the anisotropy.
			bool? ThinWall(Func<string, string> s) => s("thin_wall") is string t ? double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && (int)d != 0 : (bool?)null;
			Add("principled_bsdf", "subsurface_radius subsurface_scale", new[] { "thin_wall" }, s => ThinWall(s) != true);
			Add("principled_bsdf", "subsurface_ior", new[] { "thin_wall", "subsurface_method" }, s => ThinWall(s) != true && s("subsurface_method") == "random_walk_skin");
			Add("principled_bsdf", "subsurface_anisotropy", new[] { "thin_wall", "subsurface_method" }, s => ThinWall(s) != false || s("subsurface_method") != "burley");
			Unless("subsurface_scattering", "subsurface_ior subsurface_anisotropy", "method", "burley");
			Unless("subsurface_scattering", "subsurface_roughness", "method", "burley", "random_walk_skin");

			If("principled_hair_bsdf", "color", "parametrization", "Direct coloring");
			If("principled_hair_bsdf", "melanin melanin_redness tint random_color", "parametrization", "Melanin concentration");
			If("principled_hair_bsdf", "absorption_coefficient", "parametrization", "Absorption coefficient");

			// Preetham and Hosek-Wilkie are precomputed from the sun direction; the others model the atmosphere.
			If("sky_texture", "sun_direction turbidity", "sky_type", "preetham", "hosek_wilkie");
			If("sky_texture", "ground_albedo", "sky_type", "hosek_wilkie");
			If("sky_texture", "sun_disc sun_size sun_intensity sun_elevation sun_rotation altitude air_density aerosol_density ozone_density",
				"sky_type", "single_scattering", "multiple_scattering");

			If("scatter_volume volume_coefficients", "anisotropy", "phase", "Henyey-Greenstein", "Draine");
			If("scatter_volume volume_coefficients", "IOR backscatter", "phase", "Fournier-Forand");
			If("scatter_volume volume_coefficients", "alpha", "phase", "Draine");
			If("scatter_volume volume_coefficients", "diameter", "phase", "Mie");

			Unless("math", "value2", "math_type",
				"sqrt", "inversesqrt", "absolute", "radians", "degrees", "round", "floor", "ceil", "fraction", "trunc", "sign", "exponent",
				"sine", "cosine", "tangent", "sinh", "cosh", "tanh", "arcsine", "arccosine", "arctangent");
			If("math", "value3", "math_type", "multiply_add", "wrap", "compare", "smoothmin", "smoothmax");
			Unless("vector_math", "vector2", "math_type",
				"length", "scale", "normalize", "round", "floor", "ceil", "fraction", "absolute", "sign", "sine", "cosine", "tangent");
			If("vector_math", "vector3", "math_type", "faceforward", "multiply_add", "wrap");
			If("vector_math", "scale", "math_type", "refract", "scale");

			If("map_range vector_map_range", "steps", "range_type", "stepped");
			If("mapping", "location", "mapping_type", "point", "texture");
			If("vector_rotate", "rotation", "rotate_type", "euler_xyz");
			If("vector_rotate", "axis", "rotate_type", "axis");
			Unless("vector_rotate", "angle", "rotate_type", "euler_xyz");

			If("normal_map", "attribute base", "space", "tangent");
			If("vector_displacement", "attribute", "space", "tangent");
			If("tangent", "axis", "direction_type", "radial");
			If("tangent", "attribute", "direction_type", "uv_map");

			return all;
		}
	}
}
