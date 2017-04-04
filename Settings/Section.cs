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

namespace RhinoCycles.Settings
{
	///<summary>
	/// Base class for all the sections
	///</summary>
	public abstract class Section : EtoCollapsibleSection
	{
		protected int m_table_padding = 10;

		public virtual void DisplayData(RhinoDoc doc)
		{
		}

		public virtual void EnableDisableControls()
		{
		}

		private bool _hidden;
		public void Hide()
		{
			_hidden = true;
		}

		public void Show(ViewportSettings vud)
		{
			_hidden = false;
			ViewportSettingsReceived?.Invoke(this, new ViewportSettingsReceivedEventArgs(vud));
		}

		public class ViewportSettingsReceivedEventArgs : EventArgs
		{
			public ViewportSettings ViewportSettings { get; }
			public ViewportSettingsReceivedEventArgs(ViewportSettings vud)
			{
				ViewportSettings = vud;
			}
		}
		protected event EventHandler<ViewportSettingsReceivedEventArgs> ViewportSettingsReceived;

		public override bool Hidden => _hidden;
	};
}
