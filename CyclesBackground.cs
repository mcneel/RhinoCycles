/**
Copyright 2014-2024 Robert McNeel and Associates

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

using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.Runtime;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace RhinoCyclesCore
{
	/// <summary>
	/// Helper class to hold background/world shader related data and state
	/// </summary>
	public class CyclesBackground : IDisposable
	{

		BitmapConverter _bitmapConverter;
		uint _docsrn;
		public CyclesBackground(BitmapConverter bitmapConverter, uint docsrn)
		{
			_bitmapConverter = bitmapConverter;
			_docsrn = docsrn;
		}

		ccl.float4 _tst = new ccl.float4(0.0f, 0.0f, 0.0f);
		/// <summary>
		/// True if ChangeQueue modified the background
		/// </summary>
		public bool Modified { get; set; }

		public bool PreviewBg { get; set; } = false;

		/// <summary>
		/// Get background style.
		/// </summary>
		public BackgroundStyle BackgroundFill { get; set; } = BackgroundStyle.SolidColor;
		/// <summary>
		/// Solid color or top color for gradient
		/// </summary>
		public Color Color1 { get; set; } = Color.Empty;
		public ccl.float4 Color1AsFloat4 =>
			BackgroundFill == BackgroundStyle.Environment ?
				BgColorAs4float : (Color1.Equals(Color.Empty) ?
									_tst : RenderEngine.CreateFloat4(Color1) ^ Gamma);
		/// <summary>
		/// Bottom color for gradient
		/// </summary>
		public Color Color2 { get; set; } = Color.Empty;
		public ccl.float4 Color2AsFloat4 => Color2.Equals(Color.Empty) ? _tst : RenderEngine.CreateFloat4(Color2) ^ Gamma;
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
		public ccl.float4 BgColorAs4float => BgColor.Equals(Color.Empty) ? _tst : RenderEngine.CreateFloat4(BgColor) ^ Gamma;
		/// <summary>
		/// True if bg env texture is available and background fill is set to Environment.
		/// </summary>
		public bool HasBgEnvTexture => (BgTexture.HasTextureImage || BgTexture.HasProcedural) && BackgroundFill == BackgroundStyle.Environment;
		public float HasBgEnvTextureAsFloat => HasBgEnvTexture || (Wallpaper.HasTextureImage && BackgroundFill == BackgroundStyle.WallpaperImage) ? 1.0f : 0.0f;

		public float BgStrength => HasBgEnvTexture ? BgTexture.Strength: 1.0f;

		public bool UseGradient => BackgroundFill == BackgroundStyle.Gradient;
		public float UseGradientAsFloat => UseGradient ? 1.0f : 0.0f;

		public bool EnabledLights { get; set; }
		public float EnabledLightsAsFloat => EnabledLights ? 1.0f : 0.0f;
		public float InvertEnabledLightsAsFloat => EnabledLights ? 0.0f: 1.0f;

		// For RH-81548 it is important we do not pass on 1.0f in the old !EnabledLights && SkylightEnabled case.
		// It turns out we can always just return SkyStrength here.
		public float NonSkyEnvStrengthFactor => SkyStrength; //!EnabledLights && SkylightEnabled ? SkyStrength : 1.0f;

		/// <summary>
		/// Hold texture data for skylight
		/// </summary>
		public CyclesTextureImage SkyTexture { get; set; } = new CyclesTextureImage();
		/// <summary>
		/// Sky color, used if sky has no image
		/// </summary>
		public Color SkyColor { get; set; } = Color.Empty;
		public ccl.float4 SkyColorAs4float =>
			SkyColor.Equals(Color.Empty) ?
				((
					(BackgroundFill == BackgroundStyle.SolidColor || BackgroundFill == BackgroundStyle.Gradient) &&
					SkylightEnabled) ?
					Color1AsFloat4: _tst
				)
			: RenderEngine.CreateFloat4(SkyColor) ^ Gamma;

		public float UseSkyColorAsFloat => (SkyColor.Equals(Color.Empty) || HasSkyEnvTexture) ? 0.0f : 1.0f;
		public float SkyStrength => SkylightEnabled ? ( HasSkyEnvTexture ? SkyTexture.Strength : BgStrength) : 0.0f;

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
		public bool HasSkyEnvTexture => SkyTexture.HasTextureImage || SkyTexture.HasProcedural;
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
		public ccl.float4 ReflectionColorAs4float => ReflectionColor.Equals(Color.Empty) ? _tst : RenderEngine.CreateFloat4(ReflectionColor) ^ Gamma;

		/// <summary>
		/// True if custom reflection env is used
		/// </summary>
		public bool HasRefl => HasReflEnvTexture || ReflectionColor != Color.Empty;
		public bool HasReflEnvTexture => ReflectionTexture.HasTextureImage || ReflectionTexture.HasProcedural;
		public float HasReflEnvTextureAsFloat => HasReflEnvTexture ? 1.0f : 0.0f;

		public bool UseCustomReflectionEnvironment => HasRefl;
		public float UseCustomReflectionEnvironmentAsFloat => UseCustomReflectionEnvironment ? 1.0f : 0.0f;

		public float ReflStrength => HasReflEnvTexture ? ReflectionTexture.Strength: 1.0f;

		public bool NoCustomsWithSkylightEnabled
		{
			get
			{
				return !HasRefl && !HasSky && SkylightEnabled;
			}
		}
		public bool NoCustomsWithSkylightDisabled
		{
			get
			{
				return !HasRefl && !HasSky && !SkylightEnabled;
			}
		}

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
				RcCore.OutputDebugString($"{file} not found, clearing and returning\n");
				Wallpaper.Clear();
				WallpaperSolid = Color1;
				return;
			}

			if(!modifiedWallpaper) { return; }

			m_old_hidden = view.WallpaperHidden;
			m_old_grayscale = view.ShowWallpaperInGrayScale;
			m_old_scaletofit = scaleToFit;
			m_old_wallpaper = file ?? "";
			RcCore.OutputDebugString($"Handling wallpaper, reading {file}\n");
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
				if (Color1 != Color.Empty) col = Color1;
				var brush = new SolidBrush(col);
				var p = new Point(x, y);
				var bmsize = new Size(nw, -nh);
				if (scaleToFit)
				{
					bmsize = new Size(w, -h);
					p = new Point(0, 0);
				}
				using (Graphics g = Graphics.FromImage(newBitmap))
				{
					g.FillRectangle(brush, new Rectangle(Point.Empty, newBitmap.Size));
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					if (!view.WallpaperHidden)
					{
						g.TranslateTransform(0, -bmsize.Height);
						g.DrawImage(bm, new Rectangle(p, bmsize), 0, 0, bm.Width, bm.Height, GraphicsUnit.Pixel, attr);
					}
				}
				var wallpaperName = $"{file}_{newBitmap.Width}x{newBitmap.Height}_{view.WallpaperHidden}_{view.ShowWallpaperInGrayScale}_{scaleToFit}_{id}.png";
				var crc = Rhino.RhinoMath.CRC32(27, System.Text.Encoding.UTF8.GetBytes(wallpaperName));
				if (HostUtils.RunningOnOSX)
				{
					newBitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
				}
				const int ExifOrientationId = 0x112;
				List<int> propertyIdList = new List<int>(bm.PropertyIdList);
				// Read orientation tag
				if (propertyIdList.Contains(ExifOrientationId))
				{
					var prop = bm.GetPropertyItem(ExifOrientationId);
					var orient = BitConverter.ToInt16(prop.Value, 0);
					switch(orient)
					{
						case 1:
							newBitmap.RotateFlip(RotateFlipType.RotateNoneFlipNone);
							break;
						case 2:
							newBitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);
							break;
						case 3:
							newBitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
							break;
						case 4:
							newBitmap.RotateFlip(RotateFlipType.Rotate180FlipX);
							break;
						case 5:
							newBitmap.RotateFlip(RotateFlipType.Rotate90FlipX);
							break;
						case 6:
							newBitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);
							break;
						case 7:
							newBitmap.RotateFlip(RotateFlipType.Rotate270FlipX);
							break;
						case 8:
							newBitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
							break;
						default:
							newBitmap.RotateFlip(RotateFlipType.RotateNoneFlipNone);
							break;
					}
				}
				var tmpFolder = System.IO.Path.GetTempPath();
				var newwallpaperFn = System.IO.Path.Combine(tmpFolder, wallpaperName);
				newBitmap.Save(newwallpaperFn);
				Wallpaper.Filename = newwallpaperFn;
				Wallpaper.ProjectionMode = TextureProjectionMode.Screen;
			}
			catch (Exception e)
			{
				RcCore.OutputDebugString($"wallpaper failure: {e.Message}.\n");
				Wallpaper.Clear();
			}
		}

#pragma warning disable 618
		// This is supposed to be a global utility but I don't know where to put it. Sorry. JohnC.
		internal static RenderSettings.EnvironmentUsage EnvUsageToUsage(RenderEnvironment.Usage usage)
		{
			switch (usage)
			{
			default:
			case RenderEnvironment.Usage.Background:              return RenderSettings.EnvironmentUsage.Background;
			case RenderEnvironment.Usage.Skylighting:             return RenderSettings.EnvironmentUsage.Skylighting;
			case RenderEnvironment.Usage.ReflectionAndRefraction: return RenderSettings.EnvironmentUsage.Reflection;
			}
		}
#pragma warning restore 618

		/// <summary>
		/// Read texture data and bg color from environments
		/// </summary>
		[Obsolete("Use the one that takes RenderSettings.EnvironmentUsage")]
		public void HandleEnvironments(RenderEnvironment.Usage usage)
		{
			HandleEnvironments(EnvUsageToUsage(usage));
		}

		List<Guid> _specialIds = new List<Guid> {
			new Guid("6A4D9BEE-5B02-4BB6-9764-5B407240731A"), // HDRLS Environment
			new Guid("ABC95D68-BD66-4EB5-A72A-E3FA6C58CCC3"), // HDRLS Texture
		};
		private bool _IsHdrLsEnvironment(RenderEnvironment env)
		{
			return _specialIds.Contains(env.TypeId);
		}

		/// <summary>
		/// Read texture data and bg color from environments
		/// </summary>
		public void HandleEnvironments(RenderSettings.EnvironmentUsage usage)
		{
			SimulatedEnvironment simenv;
			switch (usage)
			{
				case RenderSettings.EnvironmentUsage.Background:
					{
						BgTexture.Clear();
						if (BackgroundEnvironment != null)
						{
							simenv = BackgroundEnvironment.SimulateEnvironment(true);
							string envfn = "";
							if (simenv != null)
							{
								BgColor = simenv.BackgroundColor;
								if(simenv.BackgroundImage != null)
								{
									if(File.Exists(simenv.BackgroundImage.Filename))
									{
										envfn = simenv.BackgroundImage.Filename;
										if(_IsHdrLsEnvironment(BackgroundEnvironment))
										{
											BgTexture.IsLinear = true;
										}
									}
								}
							}

							_bitmapConverter.EnvironmentBitmapFromEvaluator(BackgroundEnvironment, BgTexture, Gamma, _docsrn, envfn);
						}
						else
						{
							BgColor = Color.Empty;
							BgTexture.Clear();
						}
					}
					break;
				case RenderSettings.EnvironmentUsage.Skylighting:
					{
						SkyTexture.Clear();
						if (SkylightEnvironment != null)
						{
							simenv = SkylightEnvironment.SimulateEnvironment(true);
							string envfn = "";
							if (simenv != null)
							{
								SkyColor = simenv.BackgroundColor;
								if(simenv.BackgroundImage != null)
								{
									if (File.Exists(simenv.BackgroundImage.Filename))
									{
										envfn = simenv.BackgroundImage.Filename;
										if(_IsHdrLsEnvironment(SkylightEnvironment))
										{
											SkyTexture.IsLinear = true;
										}
									}
								}
							}
							_bitmapConverter.EnvironmentBitmapFromEvaluator(SkylightEnvironment, SkyTexture, Gamma, _docsrn, envfn);
						}
						else
						{
							SkyColor = Color.Empty;
							SkyTexture.Clear();
						}
					}
					break;
				case RenderSettings.EnvironmentUsage.Reflection:
					{
						ReflectionTexture.Clear();
						if (ReflectionEnvironment != null)
						{
							simenv = ReflectionEnvironment.SimulateEnvironment(true);
							string envfn = "";
							if (simenv != null)
							{
								ReflectionColor = simenv.BackgroundColor;
								if(simenv.BackgroundImage != null)
								{
									if (File.Exists(simenv.BackgroundImage.Filename))
									{
										envfn = simenv.BackgroundImage.Filename;
										if(_IsHdrLsEnvironment(ReflectionEnvironment))
										{
											ReflectionTexture.IsLinear = true;
										}
									}
								}
							}
							_bitmapConverter.EnvironmentBitmapFromEvaluator(ReflectionEnvironment, ReflectionTexture, Gamma, _docsrn, envfn);
						}
						else
						{
							ReflectionColor = Color.Empty;
							ReflectionTexture.Clear();
						}
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
			ReflectionTexture?.Dispose();
			ReflectionTexture = null;
			BgTexture?.Dispose();
			BgTexture = null;
			SkyTexture?.Dispose();
			SkyTexture = null;
			Wallpaper?.Dispose();
			Wallpaper = null;

			BackgroundEnvironment?.Dispose();
			BackgroundEnvironment = null;
			SkylightEnvironment?.Dispose();
			SkylightEnvironment = null;
			ReflectionEnvironment?.Dispose();
			ReflectionEnvironment = null;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder("CyclesBackground:");
			var props = typeof(CyclesBackground).GetProperties();
			foreach(var prop in props) {
				sb.Append($"\t{prop.Name} := {prop.GetValue(this)}\n");
			}
			sb.Append("---------\n");
			if(HasBgEnvTexture && BgTexture.HasFloatImage)
			{
				var a = BgTexture.TexFloat.ToArray();

				var tenperc = a.Length / 10;
				sb.Append($"\t --> {a[tenperc]}");
				a = null;
			}
			return sb.ToString();
		}
	}
}
