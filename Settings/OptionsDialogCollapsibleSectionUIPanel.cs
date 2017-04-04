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
using Rhino.UI.Controls;

namespace RhinoCycles.Settings
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

		/// <summary>
		/// Public constructor
		/// </summary>
		public OptionsDialogCollapsibleSectionUIPanel()
		{
			InitializeComponents();
			InitializeLayout();
		}

		private EtoCollapsibleSectionHolder m_holder;
		private void InitializeComponents()
		{
			m_holder = new EtoCollapsibleSectionHolder();

		}

		private void InitializeLayout()
		{
			// Create holder for sections. The holder can expand/collaps sections and
			// displays a title for each section

			IntegratorSection section1 = new IntegratorSection(true);
			section1.DisplayData();
			SessionSection section2 = new SessionSection(true);
			section2.DisplayData();
			DeviceSection section3 = new DeviceSection(true);
			section3.DisplayData();
			m_holder.Add(section1);
			m_holder.Add(section2);
			m_holder.Add(section3);

			// Create a tablelayout that contains the holder and add it to the UI
			// Content
			TableLayout tableLayout = new TableLayout()
			{
				Rows =
				{
					m_holder
				}
			};

			Content = tableLayout;
		}

		public event EventHandler ViewDataChanged;

		private void Section0_ViewDataChanged(object sender, EventArgs e)
		{
			ViewDataChanged?.Invoke(sender, e);
		}
	}
}
