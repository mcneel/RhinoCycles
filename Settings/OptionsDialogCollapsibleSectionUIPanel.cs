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
using Rhino.UI;
using Rhino.UI.Controls;

namespace RhinoCyclesCore.Settings
{
	public class OptionsDialogCollapsibleSectionUIPanel : Panel
	{
		/// <summary>
		/// Returns the ID of this panel.
		/// </summary>
		public static Guid PanelId
		{
			get
			{
				return typeof(OptionsDialogCollapsibleSectionUIPanel).GUID;
			}
		}

		OptionsDialogPage mParent { get; set; }

		/// <summary>
		/// Public constructor
		/// </summary>
		public OptionsDialogCollapsibleSectionUIPanel(OptionsDialogPage parent)
		{
			mParent = parent;
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
		}

		private EtoCollapsibleSectionHolder m_holder;
		private void InitializeComponents()
		{
			mNoteAboutAdvancedSettings= new Label()
			{
				Text = Localization.LocalizeString("For Rhino Render Advanced Settings please see the", 45),
				VerticalAlignment = VerticalAlignment.Center,
			};
			mLinkToRenderPage = new LinkButton()
			{
				Text = Localization.LocalizeString("Document Properties Render Page", 46)
		};
			m_holder = new EtoCollapsibleSectionHolder();
		}

		private Label mNoteAboutAdvancedSettings;
		private LinkButton mLinkToRenderPage;
		SessionSection m_sessionSection;
		DeviceSection m_deviceSection;
		StackLayout MainLayout;
		private void InitializeLayout()
		{
			m_sessionSection = new SessionSection(0);
			m_deviceSection = new DeviceSection(0);
			m_holder.Add(m_sessionSection);
			m_holder.Add(m_deviceSection);
			UpdateSections();

			MainLayout = new StackLayout()
			{
				// Padding around the table
				Padding = new Eto.Drawing.Padding(3, 5, 3, 0),
				// Spacing between table cells
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Items =
				{
					TableLayout.HorizontalScaled(0,
						new Panel() {
							Padding = 10,
							Content = new TableLayout() {
								Spacing = new Eto.Drawing.Size(1, 5),
								Rows = {
									new TableRow(mNoteAboutAdvancedSettings, mLinkToRenderPage),
								}
							}
						}
					),
					m_holder,
				}
			};

			Content = MainLayout;
		}

		private void LinkToButtonPageClicked(object sender, EventArgs args)
		{
			mParent.SetActivePageTo("Render", true);
		}

		private void RegisterControlEvents() {
			mLinkToRenderPage.Click += LinkToButtonPageClicked;
		}

		private void UnRegisterControlEvents() {
			mLinkToRenderPage.Click -= LinkToButtonPageClicked;
		}

		private void ResetAllSection_Reset(object sender, EventArgs e)
		{
			UpdateSections();
		}

		public void UpdateSections()
		{
			m_deviceSection.DisplayData();
			m_sessionSection.DisplayData();
		}
	}
}
