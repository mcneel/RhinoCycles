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
using Eto.Forms;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;

namespace RhinoCyclesCore.Settings
{
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class SessionSection: ApplicationSection
	{
		private readonly LocalizeStringPair m_caption;
		private Label m_throttlems_lb;
		private NumericStepper m_throttlems;

		public override LocalizeStringPair Caption
		{
			get { return m_caption; }
		}

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => MainLayout.Height;

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public SessionSection(uint doc_serial) : base(doc_serial)
		{
			RcCore.It.InitialisationCompleted += It_InitialisationCompleted;
			m_caption = new LocalizeStringPair("Session settings", Localization.LocalizeString("Session settings", 5));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			EngineSettingsReceived += SessionSection_EngineSettingsReceivedHandler;
			ViewModelActivated += SessionSection_ViewModelActivated; ;
		}

		private void SessionSection_ViewModelActivated(object sender, EventArgs e)
		{
			DataContext = ViewModel;
			DisplayData();
		}

		private void It_InitialisationCompleted(object sender, EventArgs e)
		{
			Application.Instance.AsyncInvoke(() =>
			{
				var vud = Settings;
				if (vud == null) return;
				SuspendLayout();
				UnRegisterControlEvents();
				m_throttlems.Value = vud.ThrottleMs;
				RegisterControlEvents();
				ResumeLayout();
			}
			);
		}

		public override void DisplayData()
		{
			SessionSection_EngineSettingsReceivedHandler(this, new EngineSettingsReceivedArgs(Settings));
		}
		private void SessionSection_EngineSettingsReceivedHandler(object sender, EngineSettingsReceivedArgs e)
		{
			if (e.AllSettings != null)
			{
				UnRegisterControlEvents();
				m_throttlems.Value = e.AllSettings.ThrottleMs;
				RegisterControlEvents();
			}
		}

		private void InitializeComponents()
		{
			m_throttlems_lb = new Label()
			{
				Text = Localization.LocalizeString("Throttle (in ms)", 19),
				VerticalAlignment = VerticalAlignment.Center,
			};
			m_throttlems = new NumericStepper()
			{
				Value = 0,
				MaximumDecimalPlaces = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				Width = 75,
			};
		}


		StackLayout MainLayout;
		private void InitializeLayout()
		{
			MainLayout = new StackLayout()
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
									new TableRow(m_throttlems_lb, m_throttlems),
								}
							}
						}
					)
				}
			};
			Content = MainLayout;
		}

		private void RegisterControlEvents()
		{
			m_throttlems.ValueChanged += M_ValueChanged;
		}

		private void M_ValueChanged(object sender, EventArgs e)
		{
			var vud = Settings;
			if (vud == null) return;

			vud.ThrottleMs = (int)m_throttlems.Value;

		}

		private void UnRegisterControlEvents()
		{
			m_throttlems.ValueChanged -= M_ValueChanged;
		}
	}
}
