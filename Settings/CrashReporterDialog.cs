/**
Copyright 2021 Robert McNeel and Associates

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
using System.Diagnostics;
using Eto.Forms;
using Eto.Drawing;
using Rhino;
using Rhino.ApplicationSettings;
using Rhino.Runtime;
using Rhino.UI;
using System.ComponentModel;

namespace RhinoCyclesCore.Settings
{
	public class CrashReporterDialog : Dialog
	{
		private Size m_default_size = Size.Empty;

		public Size DefaultSize
		{
			get { return m_default_size; }
			set { m_default_size = value; }
		}

		public CrashReporterDialog(string title, string explanation)
		{
			InitializeLayout(title, explanation);
			RegisterControlEvents();
			if (HostUtils.RunningOnWindows)
			{
				BackgroundColor = AppearanceSettings.GetPaintColor(PaintColor.PanelBackground).ToEto();
			}
		}

		private Label m_crashExplanation;
		private LinkButton m_linkToUrl;
		private Button m_Ok;

		private void InitializeLayout(string title, string explanation)
		{
			Title = title;
			m_crashExplanation = new Label()
			{
				Wrap = WrapMode.Word,
				Text = explanation,
			};
			m_linkToUrl = new LinkButton()
			{
				Text = LOC.STR("More information and potential solutions"),
			};
			m_Ok = new Button()
			{
				Text = LOC.STR("OK")
			};
			StackLayout layout = new StackLayout()
			{
				// Padding around the table
				Padding = new Eto.Drawing.Padding(20, 10, 20, 15),
				// Spacing between table cells
				Spacing = 10,
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Items =
				{
					m_crashExplanation,
					m_linkToUrl,
					null,
					m_Ok,
				}
			};

			Content = layout;

		}
		private void LinkClicked(object sender, EventArgs args)
		{
			Process.Start("https://wiki.mcneel.com/rhino/render_error");
		}

		private void OkClicked(object sender, EventArgs args)
		{
			Close();
		}

		private void RegisterControlEvents(){
			m_linkToUrl.Click += LinkClicked;
			m_Ok.Click += OkClicked;
		}

		private void UnregisterControlEvents(){
			m_linkToUrl.Click -= LinkClicked;
			m_Ok.Click -= OkClicked;
		}
		protected override void OnLoadComplete(EventArgs e)
		{
			base.OnLoadComplete(e);

			if (m_default_size.Equals(Size.Empty))
				Size = new Eto.Drawing.Size(350, 250);
			else
				Size = m_default_size;
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			UnregisterControlEvents();
			base.OnClosing(e);
		}
	}
}