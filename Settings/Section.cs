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
using System.Collections.Concurrent;
using Rhino.UI.Controls;
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore.Settings
{
	///<summary>
	/// Base class for all the sections
	///</summary>
	public abstract class Section : EtoCollapsibleSection
	{
		protected int m_table_padding = 10;

		DocumentSettingsModel dsm;
		/// <summary>
		/// Constructor
		/// </summary>
		public Section() {
			dsm = new DocumentSettingsModel(this);
		}

		/// <summary>
		/// Access to settings
		/// </summary>
		public IAllSettings Settings => dsm;

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
