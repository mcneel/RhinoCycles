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
using RhinoCyclesCore;
using RhinoCyclesCore.Core;

namespace RhinoCycles.Settings
{
	public class ViewportCollapsibleSectionUIPanel : Panel
	{
		/// <summary>
		/// Returns the ID of this panel.
		/// </summary>
		public static Guid PanelId
		{
			get
			{
				return typeof(ViewportCollapsibleSectionUIPanel).GUID;
			}
		}

		/// <summary>
		/// Public constructor
		/// </summary>
		public ViewportCollapsibleSectionUIPanel()
		{
			InitializeComponents();
			InitializeLayout();
			RcCore.It.EngineSettings.ApplicationSettingsChanged += EngineSettings_ApplicationSettingsChanged;
		}

		private void EngineSettings_ApplicationSettingsChanged(object sender, ApplicationChangedEventArgs e)
		{
			if (!e.Settings.AllowViewportSettingsOverride)
			{
				Prevented();
			}
			else
			{
				var vud = m_addUserDataSection.Settings;
				Allowed(vud);
			}
		}

		private EtoCollapsibleSectionHolder m_holder;
		private AddUserdataSection m_addUserDataSection;
		private IntegratorSection m_integratorSection;
		private SessionSection m_sessionSection;
		private DeviceSection m_deviceSection;
		private void InitializeComponents()
		{
			m_holder = new EtoCollapsibleSectionHolder();
			m_addUserDataSection = new AddUserdataSection(false);
			m_integratorSection = new IntegratorSection(false);
			m_sessionSection = new SessionSection(false);
			m_deviceSection = new DeviceSection(false);
		}

		private void InitializeLayout()
		{
			m_addUserDataSection.ViewDataChanged += AddUserData_ViewDataChanged;

			m_addUserDataSection.DisplayData();
			m_deviceSection.DisplayData();
			m_integratorSection.DisplayData();
			m_sessionSection.DisplayData();

			m_holder.Add(m_addUserDataSection);
			m_holder.Add(m_integratorSection);
			m_holder.Add(m_sessionSection);
			m_holder.Add(m_deviceSection);



			Content = m_holder;
		}

		public event EventHandler ViewDataChanged;

		private void AddUserData_ViewDataChanged(object sender, EventArgs e)
		{
			ViewDataChanged?.Invoke(sender, e);
		}

		private void Prevented()
		{
			m_addUserDataSection?.Show(null);
			m_integratorSection?.Hide();
			m_sessionSection?.Hide();
			m_deviceSection?.Hide();
		}

		private void Allowed(IViewportSettings vud)
		{
			m_addUserDataSection?.Hide();
			m_integratorSection?.Show(vud);
			m_sessionSection?.Show(vud);
			m_deviceSection?.Show(vud);
		}

		public void NoUserdataAvailable()
		{
			Prevented();
		}

		public void UserdataAvailable(IViewportSettings vud)
		{
			if (RcCore.It.EngineSettings.AllowViewportSettingsOverride)
			{
				Allowed(vud);
			}
			else
			{
				Prevented();
			}
		}
	}
}
