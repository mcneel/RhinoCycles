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
using Eto.Forms;
using Rhino;
using Rhino.UI;
using Rhino.DocObjects;
using RhinoCyclesCore.Core;

namespace RhinoCycles.Settings
{
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class AddUserdataSection: Section
	{
		private LocalizeStringPair m_caption;
		private Button m_button;
		private Label m_nooverride;

		public override LocalizeStringPair Caption => m_caption;

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => Content.Height;

		public override bool Collapsible => false;
		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public AddUserdataSection(bool for_app) : base(for_app)
		{
			m_caption = new LocalizeStringPair("Override View-specific Cycles settings", Localization.LocalizeString("Override View-specific Cycles settings", 1));
			InitializeComponents();
			InitializeLayout();
			RegisterEvents();
		}

		private void InitializeComponents()
		{
			m_button = new Button()
			{
				Text = Localization.LocalizeString("Override settings...", 2),
				Visible = RcCore.It.EngineSettings.AllowViewportSettingsOverride,
			};
			m_nooverride = new Label()
			{
				Text = LOC.STR("Override not enabled in application settings"),
				Visible = !RcCore.It.EngineSettings.AllowViewportSettingsOverride,
			};
		}


		private void InitializeLayout()
		{
			TableLayout layout = new TableLayout()
			{
				// Padding around the table
				Padding = 10,
				// Spacing between table cells
				Spacing = new Eto.Drawing.Size(15, 15),
				Rows =
				{
					new TableRow(m_button),
					new TableRow(m_nooverride),
				}
			};
			Content = layout;
		}

		private void RegisterEvents()
		{
			RcCore.It.EngineSettings.ApplicationSettingsChanged += EngineSettings_ApplicationSettingsChanged;
			m_button.Click += OnButtonClick;
		}

		private void EngineSettings_ApplicationSettingsChanged(object sender, RhinoCyclesCore.ApplicationChangedEventArgs e)
		{
			m_button.Visible = e.Settings.AllowViewportSettingsOverride;
			m_nooverride.Visible = !e.Settings.AllowViewportSettingsOverride;
		}

		private void UnregisterEvents()
		{
			RcCore.It.EngineSettings.ApplicationSettingsChanged -= EngineSettings_ApplicationSettingsChanged;
			m_button.Click -= OnButtonClick;
		}


		public event EventHandler ViewDataChanged;

		private void OnButtonClick(object sender, EventArgs e)
		{
			var vi = new ViewInfo(RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport);
			var vpi = vi.Viewport;

			var vud = vpi.UserData.Find(typeof (ViewportSettings)) as ViewportSettings;

			if (vud == null)
			{
				var nvud = new ViewportSettings();
				vpi.UserData.Add(nvud);
			}
			ViewDataChanged?.Invoke(this, EventArgs.Empty);
		}

	}
}
