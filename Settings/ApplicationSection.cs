/**
Copyright 2014-2020 Robert McNeel and Associates

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
using Rhino.UI.Controls;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore.Settings
{
	///<summary>
	/// Base class for all the sections
	///</summary>
	public abstract class ApplicationSection : EtoCollapsibleSection
	{
		protected int m_table_padding = 10;

		protected uint m_doc_serialnumber;
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="for_app">Pass in 'true' if sections display application settings. 'false' means
		/// viewport-specific settings.</param>
		public ApplicationSection(uint doc_serial) {
			m_doc_serialnumber = doc_serial;
			vps = RcCore.It.AllSettings;
		}

		ApplicationAndDocumentSettings vps;

		/// <summary>
		/// Access to settings related to viewport/sessions through IViewportSettings
		/// </summary>
		public IAllSettings Settings => vps;

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

		public void Show(IAllSettings vud)
		{
			_hidden = false;
			Visible = true;
			EngineSettingsReceived?.Invoke(this, new EngineSettingsReceivedArgs(vud));
		}

		public class EngineSettingsReceivedArgs : EventArgs
		{
			public IAllSettings AllSettings { get; }
			public EngineSettingsReceivedArgs(IAllSettings vud)
			{
				AllSettings = vud;
			}
		}
		protected event EventHandler<EngineSettingsReceivedArgs> EngineSettingsReceived;

		public override bool Hidden => _hidden;
	};
}
