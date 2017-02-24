/**
Copyright 2014-2016 Robert McNeel and Associates

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
		/// OpenCL doesn't properly support HDRi textures in the environment,
		/// so read them as byte textures instead.
		/// </summary>
		public bool m_float_as_byte;

		/// <summary>
		/// True if ChangeQueue modified the background
		/// </summary>
		public bool modified;

		/// <summary>
		/// True if skylight is enabled
		/// </summary>
		public bool skylight_enabled;

		/// <summary>
		/// Get background style.
		/// </summary>
		public BackgroundStyle background_fill = BackgroundStyle.SolidColor;
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
		public RenderEnvironment background_environment;
		/// <summary>
		/// Custom environment used for skylight. If not set, background_environment will be used.
		/// </summary>
		public RenderEnvironment skylight_environment;
		/// <summary>
		/// Custom environment to use for reflections. If not set, use background_environment,
		/// solid color or gradient, whichever is used as background
		/// </summary>
		public RenderEnvironment reflection_environment;

		/// <summary>
		/// Hold texture data for background (360deg)
		/// </summary>
		public CyclesTextureImage bg = new CyclesTextureImage();
		/// <summary>
		/// Background color, used if bg is no image
		/// </summary>
		public Color bg_color = Color.Empty;
		
		/// <summary>
		/// Hold texture data for skylight
		/// </summary>
		public CyclesTextureImage sky = new CyclesTextureImage();
		/// <summary>
		/// Sky color, used if sky has no image
		/// </summary>
		public Color sky_color = Color.Empty;

		private float gamma = 1.0f;
		public float Gamma
		{
			get { return gamma; }
			set {
				if (Math.Abs(value - gamma) > 0.00001f)
				{
					modified = true;
				}
				gamma = value;
			}
		}

		public string Xml = "";

		public bool PlanarProjection = false;

		/// <summary>
		/// True if skylight is used.
		/// </summary>
		public bool HasSky => sky.HasTextureImage || sky_color != Color.Empty;

		/// <summary>
		/// Hold texture data for reflection
		/// </summary>
		public CyclesTextureImage refl = new CyclesTextureImage();
		/// <summary>
		/// Reflection color, used if refl has no image
		/// </summary>
		public Color refl_color = Color.Empty;

		/// <summary>
		/// True if custom reflection env is used
		/// </summary>
		public bool HasRefl => refl.HasTextureImage || refl_color != Color.Empty;

		/// <summary>
		/// Hold texture data for wallpaper
		/// </summary>
		public CyclesTextureImage wallpaper = new CyclesTextureImage();
		/// <summary>
		/// Solid bg color to show outside of wallpaper when not set to Stretch to Fit
		/// </summary>
		public Color wallpaper_solid = Color.Empty;

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
			modified |= modifiedWallpaper;

			if (string.IsNullOrEmpty(file) || !File.Exists(file))
			{
				Rhino.RhinoApp.OutputDebugString($"{file} not found, clearing and returning\n");
				wallpaper.Clear();
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
				wallpaper.TexByte = wallpaperbm.Corrected;
				if (RcCore.It.EngineSettings.SaveDebugImages) wallpaperbm.SaveBitmaps();
				wallpaper.TexWidth = newBitmap.Width;
				wallpaper.TexHeight = newBitmap.Height;
				wallpaper.Name =
					$"{file}_{newBitmap.Width}x{newBitmap.Height}_{view.WallpaperHidden}_{view.ShowWallpaperInGrayScale}_{scaleToFit}_{id}";
			}
			catch (Exception e)
			{
				Rhino.RhinoApp.OutputDebugString($"wallpaper failure: {e.Message}.\n");
				wallpaper.Clear();
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

					if (background_environment != null)
					{
						simenv = background_environment.SimulateEnvironment(true);
						if (simenv != null)
						{
							bg_color = simenv.BackgroundColor;
						}
					}
					else
					{
						bg_color = Color.Empty;
						bg.Clear();
					}
					BitmapConverter.EnvironmentBitmapFromEvaluator(background_environment, bg, Gamma, m_float_as_byte, PlanarProjection);
					break;
				case RenderEnvironment.Usage.Skylighting:
					bool resampled = false;
					if (skylight_environment != null)
					{
						simenv = skylight_environment.SimulateEnvironment(true);
						if (simenv != null)
						{
							sky_color = simenv.BackgroundColor;
						}

						var skylight_copy = skylight_environment.MakeCopy() as RenderEnvironment;
						if (skylight_copy != null)
						{
							var original_render_texture = skylight_copy.FindChild("texture");
							if (original_render_texture != null)
							{
								var resampler = RenderContentType.NewContentFromTypeId(new System.Guid("71D5FEEF-4144-4133-8C38-1EEF2BC851F1"));
								resampler.Name = "RESAMPLER"; // give name so we can see in debug if it doesn't get freed properly.
								var render_texture = original_render_texture.MakeCopy() as RenderTexture;

								resampler.BeginChange(RenderContent.ChangeContexts.Ignore);
								resampler.SetParameter("u-division-count", 64);
								resampler.SetParameter("v-division-count", 64);
								resampler.SetParameter("blur-on", true);
								resampler.SetParameter("blur-radius", 0.2);
								resampler.SetChild(render_texture, "texture");
								resampler.EndChange();

								skylight_copy.BeginChange(RenderContent.ChangeContexts.Ignore);
								skylight_copy.SetChild(resampler, "texture");
								skylight_copy.EndChange();

								BitmapConverter.EnvironmentBitmapFromEvaluator(skylight_copy, sky, Gamma, m_float_as_byte, false);
								resampled = true;

								render_texture.Dispose();
								resampler.Dispose();
							}
							skylight_copy.Dispose();
						}
					}
					else
					{
						sky_color = Color.Empty;
						sky.Clear();
					}
					if (!resampled)
						BitmapConverter.EnvironmentBitmapFromEvaluator(skylight_environment, sky, Gamma, m_float_as_byte, false);

					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					if (reflection_environment != null)
					{
						simenv = reflection_environment.SimulateEnvironment(true);
						if (simenv != null)
						{
							refl_color = simenv.BackgroundColor;
						}
					}
					else
					{
						refl_color = Color.Empty;
						refl.Clear();
					}
					BitmapConverter.EnvironmentBitmapFromEvaluator(reflection_environment, refl, Gamma, m_float_as_byte, false);
					break;
			}
		}

		/// <summary>
		/// Reset modified flag.
		/// </summary>
		public void Reset()
		{
			modified = false;
		}

		public void Dispose()
		{
			refl.Clear();
			bg.Clear();
			sky.Clear();
			wallpaper.Clear();
		}
	}
}
