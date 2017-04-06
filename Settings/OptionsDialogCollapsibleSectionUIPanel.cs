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
			ApplicationSection applicationSection = new ApplicationSection(true);
			IntegratorSection integratorSection = new IntegratorSection(true);
			SessionSection sessionSection = new SessionSection(true);
			DeviceSection deviceSection = new DeviceSection(true);
			applicationSection.DisplayData();
			deviceSection.DisplayData();
			integratorSection.DisplayData();
			sessionSection.DisplayData();
			m_holder.Add(applicationSection);
			m_holder.Add(integratorSection);
			m_holder.Add(sessionSection);
			m_holder.Add(deviceSection);

			Content = m_holder;
		}
	}
}
