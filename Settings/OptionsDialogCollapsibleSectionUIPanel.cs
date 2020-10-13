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

		//IntegratorSection m_integratorSection;
		SessionSection m_sessionSection;
		DeviceSection m_deviceSection;
		private void InitializeLayout()
		{
			//m_integratorSection = new IntegratorSection(Rhino.PlugIns.PlugIn.IdFromName("Rhino Render"), true, 0);
			m_sessionSection = new SessionSection(0);
			m_deviceSection = new DeviceSection(0);
			//m_holder.Add(m_integratorSection);
			m_holder.Add(m_sessionSection);
			m_holder.Add(m_deviceSection);
			UpdateSections();

			Content = m_holder;
		}

		private void ResetAllSection_Reset(object sender, EventArgs e)
		{
			UpdateSections();
		}

		public void UpdateSections()
		{
			m_deviceSection.DisplayData();
			//m_integratorSection.DisplayData();
			m_sessionSection.DisplayData();
		}
	}
}
