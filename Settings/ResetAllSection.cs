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
	public class ResetAllSection: Section
	{
		private LocalizeStringPair m_caption;
		private Button m_resetall;

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
		public ResetAllSection()
		{
			m_caption = new LocalizeStringPair("Reset section", Localization.LocalizeString("Reset settings", 43));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
		}



		private void InitializeComponents()
		{
			m_resetall = new Button()
			{
				Text = Localization.LocalizeString("Reset all Cycles settings to their defaults", 44),
				Width = 75,
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
									new TableRow( new TableCell(m_resetall, true)),
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
			m_resetall.Click += M_resetall_Click;
		}

		public event EventHandler Reset;

		private void M_resetall_Click(object sender, EventArgs e)
		{
			RcCore.It.AllSettings.DefaultSettings();
			Reset?.Invoke(this, EventArgs.Empty);
		}
	}
}
