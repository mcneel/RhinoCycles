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

		ApplicationSection m_applicationSection;
		IntegratorSection m_integratorSection;
		SessionSection m_sessionSection;
		DeviceSection m_deviceSection;
		//ResetAllSection m_resetAllSection;
		private void InitializeLayout()
		{
			m_applicationSection = new ApplicationSection(true);
			m_integratorSection = new IntegratorSection(true);
			m_sessionSection = new SessionSection(true);
			m_deviceSection = new DeviceSection(true);
			//m_resetAllSection = new ResetAllSection(true);
			//m_resetAllSection.Reset += ResetAllSection_Reset;
			//m_applicationSection.DisplayData();
			//m_deviceSection.DisplayData();
			//m_integratorSection.DisplayData();
			//m_sessionSection.DisplayData();
			m_holder.Add(m_applicationSection);
			m_holder.Add(m_integratorSection);
			m_holder.Add(m_sessionSection);
			m_holder.Add(m_deviceSection);
			UpdateSections();
			//m_holder.Add(m_resetAllSection);

			Content = m_holder;
		}

		private void ResetAllSection_Reset(object sender, EventArgs e)
		{
			UpdateSections();
		}

		public void UpdateSections()
		{
			m_applicationSection.DisplayData();
			m_deviceSection.DisplayData();
			m_integratorSection.DisplayData();
			m_sessionSection.DisplayData();
		}
	}
}
