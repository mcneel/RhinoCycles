/**
Copyright 2014-2017 Robert McNeel and Associates

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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore
{
	/// <summary>
	/// Helper class to hold background/world shader related data and state
	/// </summary>
	public class CyclesBackground : IDisposable
	{
		/// <summary>
		/// True if ChangeQueue modified the background
		/// </summary>
		public bool Modified { get; set; }

		/// <summary>
		/// Get background style.
		/// </summary>
		public BackgroundStyle BackgroundFill { get; set; } = BackgroundStyle.SolidColor;
		/// <summary>
		/// Solid color or top color for gradient
		/// </summary>
		public Color color1;
		/// <summary>
		/// Bottom color for gradient
		/// </summary>
		public Color color2;
		/// <summary>
		/// Environment used for background (360deg environment)
		/// </summary>
		public RenderEnvironment BackgroundEnvironment;
		/// <summary>
		/// Custom environment used for skylight. If not set, background_environment will be used.
		/// </summary>
		public RenderEnvironment SkylightEnvironment;
		/// <summary>
		/// Custom environment to use for reflections. If not set, use background_environment,
		/// solid color or gradient, whichever is used as background
		/// </summary>
		public RenderEnvironment ReflectionEnvironment;

		/// <summary>
		/// Hold texture data for background (360deg)
		/// </summary>
		public CyclesTextureImage BgTexture { get; set; } = new CyclesTextureImage();
		/// <summary>
		/// Background color, used if bg is no image
		/// </summary>
		public Color BgColor { get; set; } = Color.Empty;
		/// <summary>
		/// True if bg env texture is available and background fill is set to Environment.
		/// </summary>
		public bool HasBgEnvTexture => BgTexture.HasTextureImage && BackgroundFill == BackgroundStyle.Environment;
		public float HasBgEnvTextureAsFloat => HasBgEnvTexture ? 1.0f : 0.0f;

		public float BgStrength => HasBgEnvTexture ? BgTexture.Amount : 1.0f;

		/// <summary>
		/// Hold texture data for skylight
		/// </summary>
		public CyclesTextureImage SkyTexture { get; set; } = new CyclesTextureImage();
		/// <summary>
		/// Sky color, used if sky has no image
		/// </summary>
		public Color SkyColor { get; set; } = Color.Empty;

		public float SkyStrength => HasSkyEnvTexture ? SkyTexture.Amount : 1.0f;

		private float gamma = 1.0f;
		public float Gamma
		{
			get { return gamma; }
			set {
				if (Math.Abs(value - gamma) > 0.00001f)
				{
					Modified = true;
				}
				gamma = value;
			}
		}

		public string Xml { get; set; } = ""; 

		public bool PlanarProjection { get; set; } = false;

		/// <summary>
		/// True if skylight is used.
		/// </summary>
		public bool HasSky => HasSkyEnvTexture || SkyColor != Color.Empty;
		public bool HasSkyEnvTexture => SkyTexture.HasTextureImage;
		public float HasSkyEnvTextureAsFloat => HasSkyEnvTexture ? 1.0f : 0.0f;
		/// <summary>
		/// True if skylight is enabled
		/// </summary>
		public bool SkylightEnabled { get; set; } = false;
		public float SkylightEnabledAsFloat => SkylightEnabled ? 1.0f : 0.0f;

		/// <summary>
		/// Hold texture data for reflection
		/// </summary>
		public CyclesTextureImage ReflectionTexture { get; set; } = new CyclesTextureImage();
		/// <summary>
		/// Reflection color, used if refl has no image
		/// </summary>
		public Color ReflectionColor { get; set; } = Color.Empty;

		/// <summary>
		/// True if custom reflection env is used
		/// </summary>
		public bool HasRefl => HasReflEnvTexture || ReflectionColor != Color.Empty;
		public bool HasReflEnvTexture => ReflectionTexture.HasTextureImage;
		public float HasReflEnvTextureAsFloat => HasSkyEnvTexture ? 1.0f : 0.0f;

		public bool UseCustomReflectionEnvironment => HasRefl;
		public float UseCustomReflectionEnvironmentAsFloat => UseCustomReflectionEnvironment ? 1.0f : 0.0f;

		public float ReflStrength => HasReflEnvTexture ? ReflectionTexture.Amount : 1.0f;

		/// <summary>
		/// Hold texture data for wallpaper
		/// </summary>
		public CyclesTextureImage Wallpaper { get; set; } = new CyclesTextureImage();
		/// <summary>
		/// Solid bg color to show outside of wallpaper when not set to Stretch to Fit
		/// </summary>
		public Color WallpaperSolid { get; set; } = Color.Empty;

		private readonly Guid id = Guid.NewGuid();
		private bool m_old_hidden;
		private bool m_old_grayscale;
		private bool m_old_scaletofit;
		private string m_old_wallpaper = "";

		/// <summary>
		/// Same as <see cref="HandleWallpaper(ViewInfo, bool)"/>, but re-use old scaletofit setting
		/// </summary>
		/// <param name="view"></param>
		public void HandleWallpaper(ViewInfo view)
		{
			HandleWallpaper(view, m_old_scaletofit);
		}

		public void HandleWallpaper(ViewInfo view, bool scaleToFit)
		{
			var file = Rhino.Render.Utilities.FindFile(RhinoDoc.ActiveDoc, view.WallpaperFilename);

			bool modifiedWallpaper = false;
			modifiedWallpaper |= m_old_hidden != view.WallpaperHidden | m_old_grayscale != view.ShowWallpaperInGrayScale;
			modifiedWallpaper |= m_old_scaletofit != scaleToFit;
			modifiedWallpaper |= !m_old_wallpaper.Equals(file);
			Modified |= modifiedWallpaper;

			if (string.IsNullOrEmpty(file) || !File.Exists(file))
			{
				Rhino.RhinoApp.OutputDebugString($"{file} not found, clearing and returning\n");
				Wallpaper.Clear();
				WallpaperSolid = color1;
				return;
			}

			if(!modifiedWallpaper) { return; }

			m_old_hidden = view.WallpaperHidden;
			m_old_grayscale = view.ShowWallpaperInGrayScale;
			m_old_scaletofit = scaleToFit;
			m_old_wallpaper = file ?? "";
			var crc = Rhino.RhinoMath.CRC32(27, System.Text.Encoding.UTF8.GetBytes(m_old_wallpaper));
			Rhino.RhinoApp.OutputDebugString($"Handling wallpaper, reading {file}\n");
			try
			{
				int near, far;
				var screenport = view.Viewport.GetScreenPort(out near, out far);
				var bottom = screenport.Bottom;
				var top = screenport.Top;
				var left = screenport.Left;
				var right = screenport.Right;

				// color matrix used for conversion to gray scale
				ColorMatrix cmgray = new ColorMatrix(
					new[]{
						new[] { 0.3f, 0.3f, 0.3f, 0.0f, 0.0f},
						new[] { .59f, .59f, .59f, 0.0f, 0.0f},
						new[] { .11f, .11f, .11f, 0.0f, 0.0f},
						new[] { 0.0f, 0.0f, 0.0f, 1.0f, 0.0f},
						new[] { 0.0f, 0.0f, 0.0f, 0.0f, 1.0f}
					}
				);
				ColorMatrix cmcolor = new ColorMatrix(
					new[]{
						new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f},
						new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f},
						new[] { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f},
						new[] { 0.0f, 0.0f, 0.0f, 1.0f, 0.0f},
						new[] { 0.0f, 0.0f, 0.0f, 0.0f, 1.0f}
					}
				);
				ImageAttributes attr = new ImageAttributes();
				attr.SetColorMatrix(view.ShowWallpaperInGrayScale ? cmgray : cmcolor);

				var w = Math.Abs(right - left);
				var h = Math.Abs(bottom - top);
				var viewport_ar = (float)w / h;
				Bitmap bm = new Bitmap(file);
				var image_ar = (float)bm.Width / bm.Height;

				int nw = 0;
				int nh = 0;
				int x = 0;
				int y = 0;
				if (image_ar > viewport_ar)
				{
					x = 0;
					nw = w;
					nh = (int)(w / image_ar);
					y = (int)(h * 0.5 - nh * 0.5);
				}
				else
				{
					y = 0;
					nh = h;
					nw = (int)(h * image_ar);
					x = (int)(w * 0.5 - nw * 0.5);
				}

				Bitmap newBitmap = new Bitmap(w, h);

				var col = Color.Aqua;
				if (color1 != Color.Empty) col = color1;
				var brush = new SolidBrush(col);
				var p = new Point(x, y);
				var bmsize = new Size(nw, nh);
				if (scaleToFit)
				{
					bmsize = new Size(w, h);
					p = new Point(0, 0);
				}
				using (Graphics g = Graphics.FromImage(newBitmap))
				{
					g.FillRectangle(brush, new Rectangle(Point.Empty, newBitmap.Size));
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					if (!view.WallpaperHidden)
					{
						g.DrawImage(bm, new Rectangle(p, bmsize), 0, 0, bm.Width, bm.Height, GraphicsUnit.Pixel, attr);
					}
				}
				var wallpaperbm = BitmapConverter.ReadByteBitmapFromBitmap(crc, newBitmap.Size.Width, newBitmap.Size.Height, newBitmap);
				wallpaperbm.ApplyGamma(Gamma);
				Wallpaper.TexByte = wallpaperbm.Data;
				if (RcCore.It.EngineSettings.SaveDebugImages) wallpaperbm.SaveBitmaps();
				Wallpaper.TexWidth = newBitmap.Width;
				Wallpaper.TexHeight = newBitmap.Height;
				Wallpaper.Name =
					$"{file}_{newBitmap.Width}x{newBitmap.Height}_{view.WallpaperHidden}_{view.ShowWallpaperInGrayScale}_{scaleToFit}_{id}";
			}
			catch (Exception e)
			{
				Rhino.RhinoApp.OutputDebugString($"wallpaper failure: {e.Message}.\n");
				Wallpaper.Clear();
			}
		}

		/// <summary>
		/// Read texture data and bg color from environments
		/// </summary>
		public void HandleEnvironments(RenderEnvironment.Usage usage)
		{
			SimulatedEnvironment simenv;
			switch (usage)
			{
				case RenderEnvironment.Usage.Background:

					if (BackgroundEnvironment != null)
					{
						simenv = BackgroundEnvironment.SimulateEnvironment(true);
						if (simenv != null)
						{
							BgColor = simenv.BackgroundColor;
						}
						BitmapConverter.EnvironmentBitmapFromEvaluator(BackgroundEnvironment, BgTexture, Gamma, PlanarProjection);
					}
					else
					{
						BgColor = Color.Empty;
						BgTexture.Clear();
					}
					break;
				case RenderEnvironment.Usage.Skylighting:
					if (SkylightEnvironment != null)
					{
						simenv = SkylightEnvironment.SimulateEnvironment(true);
						if (simenv != null)
						{
							SkyColor = simenv.BackgroundColor;
						}
						BitmapConverter.EnvironmentBitmapFromEvaluator(SkylightEnvironment, SkyTexture, Gamma, false);
					}
					else
					{
						SkyColor = Color.Empty;
						SkyTexture.Clear();
					}
					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					if (ReflectionEnvironment != null)
					{
						simenv = ReflectionEnvironment.SimulateEnvironment(true);
						if (simenv != null)
						{
							ReflectionColor = simenv.BackgroundColor;
						}
						BitmapConverter.EnvironmentBitmapFromEvaluator(ReflectionEnvironment, ReflectionTexture, Gamma, false);
					}
					else
					{
						ReflectionColor = Color.Empty;
						ReflectionTexture.Clear();
					}
					break;
			}
		}

		/// <summary>
		/// Reset modified flag.
		/// </summary>
		public void Reset()
		{
			Modified = false;
		}

		public void Dispose()
		{
			ReflectionTexture.Clear();
			BgTexture.Clear();
			SkyTexture.Clear();
			Wallpaper.Clear();
		}
	}
}
