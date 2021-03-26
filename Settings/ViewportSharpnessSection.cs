
/**
Copyright 2014-2021 Robert McNeel and Associates

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
using Eto.Forms;
using Rhino.Render;
using Rhino.Runtime;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static RhinoCyclesCore.RenderEngine;

namespace RhinoCyclesCore.Settings
{
	public class ViewportSharpnessSection : ApplicationSection
	{
		private LocalizeStringPair m_caption;
		public override LocalizeStringPair Caption
		{
			get { return m_caption; }
		}

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => Content.Height;

		public override bool Hidden
		{
			get
			{
				return false;
			}
		}

		public ViewportSharpnessSection(uint doc_serial) : base(doc_serial)
		{
			m_caption = new LocalizeStringPair("Viewport resolution sharpness", LOC.STR("Viewport resolution sharpness"));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
		}

		private Label m_labelSharpness;

		private Label m_labelSharpnessPixelized;
		private Slider m_sliderSharpness;
		private Label m_labelSharpnessPerfect;


		private void InitializeComponents() {
			m_labelSharpness = new Label { Text = LOC.STR("Sharpness"), ToolTip=LOC.STR("Sharpness of rendering in viewport and thumbnail previews.") };
			m_labelSharpnessPixelized = new Label { Text = LOC.STR("Pixelized (fastest)"), ToolTip=LOC.STR("Rendering happens faster, but results are more pixelized. Also affects thumbnail previews.") };
			m_sliderSharpness = new Slider()
			{
				SnapToTick = true,
				TickFrequency = 1,
				Value = 1,
				MaxValue = 10,
				MinValue = 1,
				Width = 130,
				Orientation = Orientation.Horizontal
			};
			m_labelSharpnessPerfect = new Label { Text = LOC.STR("Pixel perfect (slowest)"), ToolTip=LOC.STR("Rendering happens slower, and results are pixel perfect. Also affects thumbnail previews.")  };
		}

		private void InitializeLayout()
		{
			StackLayout layout = new StackLayout()
			{
				Padding = 10,
				Spacing = 5,
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Orientation = Orientation.Vertical,
				Items =
				{
					new StackLayoutItem(m_labelSharpness, true),
					TableLayout.HorizontalScaled(15, m_labelSharpnessPixelized, m_sliderSharpness, m_labelSharpnessPerfect),
				}
			};
			Content = layout;
		}

		public override void DisplayData()
		{
			UnRegisterControlEvents();

			m_sliderSharpness.Value = 11 - (int)Settings.DpiScale;

			RegisterControlEvents();
		}


		private void sharpnessValueChanged(object sender, EventArgs e)
		{
			/* values [1,10]. Lowest means least, highest most sharp.
			 * Since this sets PixelSize lets flip the values.
			 */
			Settings.DpiScale = (float)(11-(int)m_sliderSharpness.Value);
		}

		private void RegisterControlEvents()
		{
			m_sliderSharpness.ValueChanged += sharpnessValueChanged;
		}
		private void UnRegisterControlEvents()
		{
			m_sliderSharpness.ValueChanged -= sharpnessValueChanged;
		}
	}
}