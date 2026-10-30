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
using System.Globalization;
using System.Linq;

using ccl;
using Grasshopper2.Types.Colour;
using Rhino.Geometry;

namespace CyclesComponents
{
	/// <summary>
	/// Turns Grasshopper values into Cycles XML attribute text for a socket.
	/// </summary>
	internal static class CyclesValues
	{
		internal static bool TryFormat(ShaderNodeSocketInfo socket, object value, out string text, out string problem)
		{
			text = null;
			problem = null;

			switch (socket.Type)
			{
				case "float":
					if (TryNumber(value, out var number)) { text = Format(number); return true; }
					break;

				case "int":
				case "uint":
					if (TryNumber(value, out var integer)) { text = ((long)Math.Round(integer)).ToString(CultureInfo.InvariantCulture); return true; }
					break;

				case "boolean":
					if (value is bool flag) { text = flag ? "true" : "false"; return true; }
					if (TryNumber(value, out var truth)) { text = truth != 0.0 ? "true" : "false"; return true; }
					break;

				case "color":
				case "color2":
					if (TryColour(value, out var r, out var g, out var b)) { text = Format(r, g, b); return true; }
					if (TryNumber(value, out var grey)) { text = Format(grey, grey, grey); return true; }
					break;

				case "vector":
				case "point":
				case "normal":
					if (TryVector(value, out var x, out var y, out var z)) { text = Format(x, y, z); return true; }
					if (TryNumber(value, out var all)) { text = Format(all, all, all); return true; }
					break;

				case "point2":
					if (value is Point2d p2) { text = Format(p2.X, p2.Y); return true; }
					if (value is Vector2d v2) { text = Format(v2.X, v2.Y); return true; }
					if (TryVector(value, out var u, out var v, out _)) { text = Format(u, v); return true; }
					break;

				case "string":
					text = value?.ToString() ?? string.Empty;
					return true;

				case "enum":
					return TryEnum(socket, value, out text, out problem);
			}

			problem = $"A {value?.GetType().Name ?? "null"} cannot set the {socket.Type} input '{socket.UiName}'.";
			return false;
		}

		private static bool TryEnum(ShaderNodeSocketInfo socket, object value, out string text, out string problem)
		{
			text = null;
			problem = null;

			var name = value?.ToString()?.Trim() ?? string.Empty;
			foreach (var entry in socket.EnumValues)
			{
				if (string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase))
				{
					text = entry.Key;
					return true;
				}
			}

			/* A number picks a value by its position, as an index into the list. */
			if (TryNumber(value, out var index) && index >= 0 && index < socket.EnumValues.Count)
			{
				text = socket.EnumValues[(int)index].Key;
				return true;
			}

			problem = $"'{name}' is not a value of '{socket.UiName}'. Use one of: {EnumNames(socket)}.";
			return false;
		}

		internal static string EnumNames(ShaderNodeSocketInfo socket)
		{
			return string.Join(", ", socket.EnumValues.Select(e => e.Key));
		}

		private static bool TryNumber(object value, out double number)
		{
			switch (value)
			{
				case double d: number = d; return true;
				case float f: number = f; return true;
				case int i: number = i; return true;
				case long l: number = l; return true;
				case decimal m: number = (double)m; return true;
				case bool b: number = b ? 1.0 : 0.0; return true;
			}

			number = 0.0;
			return false;
		}

		/* Cycles colours are scene linear; Grasshopper colours are sRGB. */
		private static bool TryColour(object value, out double r, out double g, out double b)
		{
			Eto.Drawing.Color eto;
			switch (value)
			{
				case Colour colour: eto = colour.ToEto(); break;
				case Eto.Drawing.Color c: eto = c; break;
				case System.Drawing.Color gdi: eto = Eto.Drawing.Color.FromArgb(gdi.R, gdi.G, gdi.B, gdi.A); break;
				default:
					r = g = b = 0.0;
					return false;
			}

			r = SrgbToLinear(eto.R);
			g = SrgbToLinear(eto.G);
			b = SrgbToLinear(eto.B);
			return true;
		}

		private static double SrgbToLinear(double c)
		{
			return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
		}

		private static bool TryVector(object value, out double x, out double y, out double z)
		{
			switch (value)
			{
				case Vector3d v: x = v.X; y = v.Y; z = v.Z; return true;
				case Point3d p: x = p.X; y = p.Y; z = p.Z; return true;
				case Vector3f vf: x = vf.X; y = vf.Y; z = vf.Z; return true;
				case Point3f pf: x = pf.X; y = pf.Y; z = pf.Z; return true;
			}

			x = y = z = 0.0;
			return false;
		}

		private static string Format(params double[] values)
		{
			return string.Join(" ", values.Select(v => v.ToString("G9", CultureInfo.InvariantCulture)));
		}
	}
}
