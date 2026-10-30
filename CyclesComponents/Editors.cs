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
using System.Linq;

using Grasshopper2.Parameters;
using Grasshopper2.Parameters.Special;
using Grasshopper2.Types.Colour;
using Grasshopper2.Types.Functions;
using Grasshopper2.Types.Functions.Standard;
using Rhino.Geometry;

namespace CyclesComponents
{
	/// <summary>
	/// Canvas editors for the inputs a value cannot be typed into: a Gradient Editor for a
	/// colour ramp, a Function Editor for each curve.
	/// </summary>
	internal static class Editors
	{
		/* Their heights in rows of 20 pixels, as their attributes lay them out. */
		private const int GradientRows = 2;
		private const int FunctionRows = 6;

		internal static int Rows(AbstractParameter editor)
		{
			return editor is FunctionEditorObject ? FunctionRows : GradientRows;
		}

		internal static GripGradient DefaultRamp()
		{
			return new GripGradient(Progression.Linear, Space3.RgbLinear,
				new GradientGrip(0.0, Colour.FromRgbLinear(0.0, 0.0, 0.0)),
				new GradientGrip(1.0, Colour.FromRgbLinear(1.0, 1.0, 1.0)));
		}

		/// <summary>
		/// A straight curve over the range, which leaves its channel as it is. Points added in the
		/// editor make it smooth, as in Blender.
		/// </summary>
		internal static Function StraightCurve(double minX, double maxX)
		{
			return new InterpolationFunction(InterpolationAlgorithm.CubicMonotone, new[] { new Point2d(minX, minX), new Point2d(maxX, maxX) });
		}

		internal static GradientEditorObject ForRamp(string code, GripGradient gradient)
		{
			return new GradientEditorObject(code, gradient ?? DefaultRamp());
		}

		/// <summary>
		/// A Function Editor framed on the curve over the range, rather than its default -2 to 2.
		/// </summary>
		internal static FunctionEditorObject ForCurve(string code, Function curve, double minX, double maxX)
		{
			curve = curve ?? StraightCurve(minX, maxX);
			var editor = new FunctionEditorObject(code, curve);

			var ys = Enumerable.Range(0, 65).Select(i => curve.Y(minX + i * (maxX - minX) / 64)).Where(y => !double.IsNaN(y) && !double.IsInfinity(y)).ToList();
			var y0 = Math.Min(minX, ys.Count > 0 ? ys.Min() : minX);
			var y1 = Math.Max(maxX, ys.Count > 0 ? ys.Max() : maxX);
			var padX = 0.05 * Math.Max(maxX - minX, 1e-6);
			var padY = 0.05 * Math.Max(y1 - y0, 1e-6);
			editor.Editor.SetFrameLimits(minX - padX, maxX + padX, y0 - padY, y1 + padY, markFresh: true);
			return editor;
		}
	}
}
