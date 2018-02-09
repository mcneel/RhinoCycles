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
using Rhino;
using Rhino.UI.Controls;
using RhinoCyclesCore;
using RhinoCyclesCore.Core;

namespace RhinoCycles.Settings
{
	///<summary>
	/// Base class for all the sections
	///</summary>
	public abstract class Section : EtoCollapsibleSection
	{
		protected int m_table_padding = 10;

		public readonly uint m_doc_serialnumber = 0;

		protected readonly bool m_for_app = false;
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="for_app">Pass in 'true' if sections display application settings. 'false' means
		/// viewport-specific settings.</param>
		public Section(bool for_app, uint doc_serial) {
			m_for_app = for_app;
			m_doc_serialnumber = doc_serial;
		}

		/// <summary>
		/// Access to settings related to viewport/sessions through IViewportSettings
		/// </summary>
		public IViewportSettings Settings
		{
			get
			{

				IViewportSettings vud;
				if (!m_for_app && RcCore.It.EngineSettings.AllowViewportSettingsOverride)
				{
					vud = Plugin.GetActiveViewportSettings(m_doc_serialnumber);
				}
				else
				{
					vud = RcCore.It.EngineSettings;
				}
				return vud;
			}
		}

		public virtual void DisplayData()
		{
		}

		public virtual void EnableDisableControls()
		{
		}

		private bool _hidden;
		public void Hide()
		{
			_hidden = true;
			Visible = false;
		}

		public void Show(IViewportSettings vud)
		{
			_hidden = false;
			Visible = true;
			ViewportSettingsReceived?.Invoke(this, new ViewportSettingsReceivedEventArgs(vud));
		}

		public class ViewportSettingsReceivedEventArgs : EventArgs
		{
			public IViewportSettings ViewportSettings { get; }
			public ViewportSettingsReceivedEventArgs(IViewportSettings vud)
			{
				ViewportSettings = vud;
			}
		}
		protected event EventHandler<ViewportSettingsReceivedEventArgs> ViewportSettingsReceived;

		public override bool Hidden => _hidden;
	};
}
