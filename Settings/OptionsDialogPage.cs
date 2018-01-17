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
using RhinoCyclesCore.Core;
using System;
using System.Drawing;

namespace RhinoCycles.Settings
{
	public class OptionsDialogPage : Rhino.UI.OptionsDialogPage
	{
    public OptionsDialogPage() : base("Cycles")
		{
			CollapsibleSectionHolder = new OptionsDialogCollapsibleSectionUIPanel();
		}

		public override object PageControl => CollapsibleSectionHolder;

		public override bool ShowApplyButton => false;
		public override bool ShowDefaultsButton => true;
    public override string LocalPageTitle => Localization.LocalizeString ("Cycles", 7);

    public override Image PageImage {
      get {
        var icon = Properties.Resources.Cycles_viewport_properties;
        return icon.ToBitmap ();
      }
    }

		public override void OnDefaults()
		{
			RcCore.It.EngineSettings.DefaultSettings();
			CollapsibleSectionHolder.UpdateSections();
		}
		private OptionsDialogCollapsibleSectionUIPanel CollapsibleSectionHolder { get; }
	}
}
