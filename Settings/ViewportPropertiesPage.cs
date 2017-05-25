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
using Rhino;
using Rhino.DocObjects;
using Rhino.UI;
using RhinoCyclesCore;
using System;
using System.Drawing;

namespace RhinoCycles.Settings
{
	public class ViewportPropertiesPage : ObjectPropertiesPage
	{
		public ViewportPropertiesPage()
		{
			CollapsibleSectionHolder = new ViewportCollapsibleSectionUIPanel();
			CollapsibleSectionHolder.ViewDataChanged += CollapsibleSectionHolder_ViewDataChanged;
		}

		public override bool OnActivate(bool active)
		{
			if(active) CollapsibleSectionHolder_ViewDataChanged(null, EventArgs.Empty);
			return base.OnActivate(active);
		}

		private void CollapsibleSectionHolder_ViewDataChanged(object sender, EventArgs e)
		{
			if (RhinoDoc.ActiveDoc == null || RhinoDoc.ActiveDoc.Views.ActiveView == null) return;

			var vi = new ViewInfo(RhinoDoc.ActiveDoc.Views.ActiveView.ActiveViewport);
			var vpi = vi.Viewport;
			var vud = vpi.UserData.Find(typeof (ViewportSettings)) as ViewportSettings;
			if (vud != null)
			{
				UserDataAvailable(vud);
			}
			else
			{
				NoUserDataAvailable();
			}
		}

		public void NoUserDataAvailable()
		{
			CollapsibleSectionHolder.NoUserdataAvailable();
		}

		public void UserDataAvailable(IViewportSettings vud)
		{
			CollapsibleSectionHolder.UserdataAvailable(vud);
		}

		public override string EnglishPageTitle => "Cycles";
		public override string LocalPageTitle => Localization.LocalizeString("Cycles", 7);

		public override Icon PageIcon(Size sizeInPixels) => new Icon(Properties.Resources.Cycles_viewport_properties, sizeInPixels);

		public override PropertyPageType PageType => PropertyPageType.View;

		public override object PageControl => CollapsibleSectionHolder;

	  public override bool ShouldDisplay(ObjectPropertiesPageEventArgs e)
	  {
			if (RhinoDoc.ActiveDoc == null || RhinoDoc.ActiveDoc.Views.ActiveView == null) return false;

			if (!RhinoCyclesCore.Core.RcCore.It.EngineSettings.AllowViewportSettingsOverride) return false;

			var dm = RhinoDoc.ActiveDoc.Views.ActiveView.RealtimeDisplayMode as Viewport.RenderedViewport;
			return dm != null;
		}

		private ViewportCollapsibleSectionUIPanel CollapsibleSectionHolder { get; }
	}
}
