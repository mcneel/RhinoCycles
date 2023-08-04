/**
Copyright 2014-2021 Robert McNeel and Associates

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
using Rhino.UI;
using RhinoCyclesCore.Core;
using System.Drawing;
using Rhino.Resources;

namespace RhinoCyclesCore.Settings
{
	public class OptionsDialogPage : Rhino.UI.OptionsDialogPage
	{
		public OptionsDialogPage() : base("Rhino Render")
		{
			CollapsibleSectionHolder = new OptionsDialogCollapsibleSectionUIPanel(this);
		}

		public override object PageControl => CollapsibleSectionHolder;

		public override bool ShowApplyButton => false;
		public override bool ShowDefaultsButton => true;
		public override string LocalPageTitle => Localization.LocalizeString("Rhino Render", 7);
		public override void OnHelp() => RhinoHelp.Show("options/cycles.htm");

		System.Drawing.Image g_page_image = null;
		public override System.Drawing.Image PageImage => g_page_image ?? (g_page_image = Rhino.Resources.Assets.Rhino.SystemDrawing.Bitmaps.TryGet(Rhino.Resources.ResourceIds.Svg_CyclesViewportPropertiesSvg, new System.Drawing.Size(48, 48)));

		public override void OnDefaults()
		{
			RcCore.It.AllSettings.DefaultSettings();
			CollapsibleSectionHolder.UpdateSections();
		}
		private OptionsDialogCollapsibleSectionUIPanel CollapsibleSectionHolder { get; }
	}
}
