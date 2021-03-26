
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
	public class ViewportResponsivenessSection : ApplicationSection
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

		public ViewportResponsivenessSection(uint doc_serial) : base(doc_serial)
		{
			m_caption = new LocalizeStringPair("Viewport responsiveness", LOC.STR("Viewport responsiveness"));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
		}

		private Label m_labelResponsiveness;

		private Label m_labelResponsivenessFast;
		private Slider m_sliderResponsiveness;
		private Label m_labelResponsivenessSlow;

		private void InitializeComponents() {

			m_labelResponsiveness = new Label { Text = LOC.STR("Response"), ToolTip=LOC.STR("The responsiveness of the viewport when tumbling or making changes.") };
			m_labelResponsivenessFast = new Label { Text = LOC.STR("Faster (coarser start)"), ToolTip=LOC.STR("Faster response, but the initial results are more pixelized.") };
			m_sliderResponsiveness = new Slider()
			{
				SnapToTick = true,
				TickFrequency = 1,
				Value = 1,
				MaxValue = 10,
				MinValue = 1,
				Width = 130,
				Orientation = Orientation.Horizontal
			};
			m_labelResponsivenessSlow = new Label { Text = LOC.STR("Slower (sharper start)"), ToolTip=LOC.STR("Slower response, but initial results are less pixelized or even not at all pixelized.") };
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
					new StackLayoutItem(m_labelResponsiveness, true),
					TableLayout.HorizontalScaled(15, m_labelResponsivenessFast, m_sliderResponsiveness, m_labelResponsivenessSlow),
				}
			};
			Content = layout;
		}

		public override void DisplayData()
		{
			UnRegisterControlEvents();

			if(!Settings.UseStartResolution) {
				m_sliderResponsiveness.Value = 10;
			} else {
				switch(Settings.StartResolution) {
					case 8:
						m_sliderResponsiveness.Value = 1;
						break;
					case 12:
						m_sliderResponsiveness.Value = 2;
						break;
					case 16:
						m_sliderResponsiveness.Value = 3;
						break;
					case 20:
						m_sliderResponsiveness.Value = 4;
						break;
					case 32:
						m_sliderResponsiveness.Value = 5;
						break;
					case 64:
						m_sliderResponsiveness.Value = 6;
						break;
					case 128:
						m_sliderResponsiveness.Value = 7;
						break;
					case 256:
						m_sliderResponsiveness.Value = 8;
						break;
					case 512:
						m_sliderResponsiveness.Value = 9;
						break;
					default:
						m_sliderResponsiveness.Value = 10;
						break;
				}
			}
			RegisterControlEvents();
		}

		private void responsivenessValueChanged(object sender, EventArgs e)
		{ /* values [1,10]. Lowest means fastest response, but also most pixelized
		   * This sets the StartResolution. Lower number means more pixelized.
			 */
			int startResolution = 0;
			bool useStartResolution = true;
			switch((int)m_sliderResponsiveness.Value) {
				case 1:
					startResolution = 8;
					break;
				case 2:
					startResolution = 12;
					break;
				case 3:
					startResolution = 16;
					break;
				case 4:
					startResolution = 20;
					break;
				case 5:
					startResolution = 32;
					break;
				case 6:
					startResolution = 64;
					break;
				case 7:
					startResolution = 128;
					break;
				case 8:
					startResolution = 256;
					break;
				case 9:
					startResolution = 512;
					break;
				default:
					startResolution = 1024*20;
					useStartResolution = false;
					break;

			}
			Settings.UseStartResolution = useStartResolution;
			Settings.StartResolution = startResolution;
		}

		private void RegisterControlEvents()
		{
			m_sliderResponsiveness.ValueChanged += responsivenessValueChanged;
		}
		private void UnRegisterControlEvents()
		{
			m_sliderResponsiveness.ValueChanged -= responsivenessValueChanged;
		}
	}
}