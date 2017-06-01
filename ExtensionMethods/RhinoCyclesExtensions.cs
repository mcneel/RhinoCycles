using System;
using System.Drawing;

namespace RhinoCyclesCore.ExtensionMethods
{
	public static class SizeExtensions
	{
		public static void Deconstruct(this Size size, out int x, out int y)
		{
			x = size.Width;
			y = size.Height;
		}
	}
}