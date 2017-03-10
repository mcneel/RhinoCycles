using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
