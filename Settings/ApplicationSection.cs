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
using Eto.Forms;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;

namespace RhinoCycles.Settings
{
	///<summary>
	/// Application level settings
	///</summary>
	public class ApplicationSection: Section
	{
		private LocalizeStringPair m_caption;
		private Label m_allowdeviceoverride_lb;
		private CheckBox m_allowdeviceoverride;
		private Label m_allowviewportsettingsoverride_lb;
		private CheckBox m_allowviewportsettingsoverride;

		public override LocalizeStringPair Caption
		{
			get { return m_caption; }
		}

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => Content.Height;

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public ApplicationSection(bool for_app) : base(for_app)
		{
			RcCore.It.InitialisationCompleted += It_InitialisationCompleted;
			m_caption = new LocalizeStringPair("Application-wide", Localization.LocalizeString("Application-wide", 22));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			ViewportSettingsReceived += ApplicationSection_ViewportSettingsReceived;
		}

		private void It_InitialisationCompleted(object sender, EventArgs e)
		{
			Application.Instance.AsyncInvoke(() =>
			{
				SuspendLayout();
				DisplayData();
				ResumeLayout();
			}
			);
		}

		public override void DisplayData()
		{
			ApplicationSection_ViewportSettingsReceived(this, new ViewportSettingsReceivedEventArgs(Settings));
		}

		private void ApplicationSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				UnRegisterControlEvents();
				m_allowviewportsettingsoverride.Checked = RcCore.It.EngineSettings.AllowViewportSettingsOverride;
				m_allowdeviceoverride.Checked = RcCore.It.EngineSettings.AllowViewportSettingsOverride ? e.ViewportSettings.AllowSelectedDeviceOverride : false;

				m_allowdeviceoverride.Enabled = RcCore.It.EngineSettings.AllowViewportSettingsOverride;
				RegisterControlEvents();
			}
		}

		private void InitializeComponents()
		{
			m_allowdeviceoverride_lb = new Label()
			{
				Text = Localization.LocalizeString("Allow device override in viewport", 23),
				VerticalAlignment = VerticalAlignment.Center,
			};
			m_allowdeviceoverride = new CheckBox()
			{
				Checked = false,
			};
			m_allowviewportsettingsoverride_lb = new Label()
			{
				Text = Localization.LocalizeString("Allow viewport settings override", 37),
				VerticalAlignment = VerticalAlignment.Center,
			};
			m_allowviewportsettingsoverride = new CheckBox()
			{
				Checked = false,
			};
		}


		private void InitializeLayout()
		{
			StackLayout layout = new StackLayout()
			{
				// Padding around the table
				Padding = new Eto.Drawing.Padding(3, 5, 3, 0),
				// Spacing between table cells
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Items =
				{
					TableLayout.HorizontalScaled(10, 
						new Panel() {
							Padding = 10,
							Content = new TableLayout() {
								Spacing = new Eto.Drawing.Size(1, 5),
								Rows = {
									new TableRow( new TableCell(m_allowviewportsettingsoverride_lb, true), new TableCell(m_allowviewportsettingsoverride, true)),
									new TableRow( m_allowdeviceoverride_lb, m_allowdeviceoverride),
								}
							}
						}
					)
				}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_allowdeviceoverride.CheckedChanged += M_ValueChanged;
			m_allowviewportsettingsoverride.CheckedChanged += M_ValueChanged;
		}

		private void M_ValueChanged(object sender, EventArgs e)
		{
			var vud = RcCore.It.EngineSettings;
			if (vud == null) return;

			vud.AllowViewportSettingsOverride = m_allowviewportsettingsoverride.Checked.HasValue ? m_allowviewportsettingsoverride.Checked.Value : false;
			vud.AllowSelectedDeviceOverride = m_allowdeviceoverride.Checked.HasValue ? m_allowdeviceoverride.Checked.Value : false;

			DisplayData();
		}

		private void UnRegisterControlEvents()
		{
			m_allowdeviceoverride.CheckedChanged -= M_ValueChanged;
			m_allowviewportsettingsoverride.CheckedChanged -= M_ValueChanged;
		}
	}
}
