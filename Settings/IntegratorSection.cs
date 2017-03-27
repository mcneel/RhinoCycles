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
using Rhino.UI;
using RhinoCycles.Viewport;
using Rhino;
using static RhinoCyclesCore.RenderEngine;

namespace RhinoCycles.Settings
{
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class IntegratorSection: Section
	{
		private LocalizeStringPair m_caption;
		private Label m_seed_lb;
		private NumericStepper m_seed;

		private Label m_diffusesamples_lb;
		private NumericStepper m_diffusesamples;

		private Label m_glossysamples_lb;
		private NumericStepper m_glossysamples;

		private Label m_transmissionsamples_lb;
		private NumericStepper m_transmissionsamples;

		private Label m_minbounce_lb;
		private NumericStepper m_minbounce;

		private Label m_maxbounce_lb;
		private NumericStepper m_maxbounce;

		private Label m_maxdiffusebounce_lb;
		private NumericStepper m_maxdiffusebounce;

		private Label m_maxglossybounce_lb;
		private NumericStepper m_maxglossybounce;

		private Label m_maxvolumebounce_lb;
		private NumericStepper m_maxvolumebounce;

		private Label m_maxtransmissionbounce_lb;
		private NumericStepper m_maxtransmissionbounce;

		public override LocalizeStringPair Caption => m_caption;

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => this.Content.Height;

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public IntegratorSection()
		{
			m_caption = new LocalizeStringPair("Integrator settings", Localization.LocalizeString("Integrator settings", 3));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			ViewportSettingsReceived += IntegratorSection_ViewportSettingsReceived;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			var layout = Content as TableLayout;
			if (layout == null) return;
			layout.SetColumnScale(0, true);
			layout.SetColumnScale(1, true);
		}

		private void IntegratorSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				UnregisterControlEvents();
				m_seed.Value = e.ViewportSettings.Seed;
				m_diffusesamples.Value = e.ViewportSettings.DiffuseSamples;
				m_glossysamples.Value = e.ViewportSettings.GlossySamples;
				m_transmissionsamples.Value = e.ViewportSettings.TransmissionSamples;
				m_minbounce.Value = e.ViewportSettings.MinBounce;
				m_maxbounce.Value = e.ViewportSettings.MaxBounce;
				m_maxdiffusebounce.Value = e.ViewportSettings.MaxDiffuseBounce;
				m_maxglossybounce.Value = e.ViewportSettings.MaxGlossyBounce;
				m_maxvolumebounce.Value = e.ViewportSettings.MaxVolumeBounce;
				m_maxtransmissionbounce.Value = e.ViewportSettings.MaxTransmissionBounce;
				RegisterControlEvents();
			}
		}

		private void InitializeComponents()
		{
			m_seed_lb = new Label()
			{
				Text = Localization.LocalizeString("Seed", 4),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_seed = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.Seed,
			};

			m_diffusesamples_lb = new Label()
			{
				Text = Localization.LocalizeString("Diffuse Samples", 10),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_diffusesamples = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.DiffuseSamples,
			};

			m_glossysamples_lb = new Label()
			{
				Text = Localization.LocalizeString("Glossy Samples", 11),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_glossysamples = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.GlossySamples,
			};

			m_transmissionsamples_lb = new Label()
			{
				Text = Localization.LocalizeString("Transmission Samples", 12),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_transmissionsamples = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.TransmissionSamples,
			};

			m_minbounce_lb = new Label()
			{
				Text = Localization.LocalizeString("Minimum Bounces", 13),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_minbounce = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.MinBounce,
			};

			m_maxbounce_lb = new Label()
			{
				Text = Localization.LocalizeString("Maximum Bounces", 14),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_maxbounce = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.MaxBounce,
			};

			m_maxdiffusebounce_lb = new Label()
			{
				Text = Localization.LocalizeString("Maximum Diffuse Bounces", 15),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_maxdiffusebounce = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.MaxDiffuseBounce,
			};

			m_maxglossybounce_lb = new Label()
			{
				Text = Localization.LocalizeString("Maximum Glossy Bounces", 16),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_maxglossybounce = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.MaxGlossyBounce,
			};

			m_maxvolumebounce_lb = new Label()
			{
				Text = Localization.LocalizeString("Maximum Volume Bounces", 17),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_maxvolumebounce = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.MaxVolumeBounce,
			};

			m_maxtransmissionbounce_lb = new Label()
			{
				Text = Localization.LocalizeString("Maximum Transmission Bounces", 18),
				VerticalAlignment = VerticalAlignment.Center,
			};

			m_maxtransmissionbounce = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = IntegratorSetting.MaxTransmissionBounce,
			};

		}


		private void InitializeLayout()
		{
			TableLayout layout = new TableLayout()
			{
				// Padding around the table
				Padding = new Eto.Drawing.Padding(3, 5, 3, 0),
				// Spacing between table cells
				Spacing = new Eto.Drawing.Size(15, 5),
				Rows =
					{
						new TableRow(m_seed_lb, m_seed),
						//new TableRow(m_diffusesamples_lb, m_diffusesamples),
						//new TableRow(m_glossysamples_lb, m_glossysamples),
						//new TableRow(m_transmissionsamples_lb, m_transmissionsamples),
						new TableRow(m_minbounce_lb, m_minbounce),
						new TableRow(m_maxbounce_lb, m_maxbounce),
						new TableRow(m_maxdiffusebounce_lb, m_maxdiffusebounce),
						new TableRow(m_maxglossybounce_lb, m_maxglossybounce),
						new TableRow(m_maxtransmissionbounce_lb, m_maxtransmissionbounce),
						new TableRow(m_maxvolumebounce_lb, m_maxvolumebounce),
					}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_seed.ValueChanged += M_seed_ValueChanged;
			m_diffusesamples.ValueChanged += M_seed_ValueChanged;
			m_glossysamples.ValueChanged += M_seed_ValueChanged;
			m_transmissionsamples.ValueChanged += M_seed_ValueChanged;
			m_minbounce.ValueChanged += M_seed_ValueChanged;
			m_maxbounce.ValueChanged += M_seed_ValueChanged;
			m_maxdiffusebounce.ValueChanged += M_seed_ValueChanged;
			m_maxglossybounce.ValueChanged += M_seed_ValueChanged;
			m_maxvolumebounce.ValueChanged += M_seed_ValueChanged;
			m_maxtransmissionbounce.ValueChanged += M_seed_ValueChanged;
		}

		private void UnregisterControlEvents()
		{
			m_seed.ValueChanged -= M_seed_ValueChanged;
			m_diffusesamples.ValueChanged -= M_seed_ValueChanged;
			m_glossysamples.ValueChanged -= M_seed_ValueChanged;
			m_transmissionsamples.ValueChanged -= M_seed_ValueChanged;
			m_minbounce.ValueChanged -= M_seed_ValueChanged;
			m_maxbounce.ValueChanged -= M_seed_ValueChanged;
			m_maxdiffusebounce.ValueChanged -= M_seed_ValueChanged;
			m_maxglossybounce.ValueChanged -= M_seed_ValueChanged;
			m_maxvolumebounce.ValueChanged -= M_seed_ValueChanged;
			m_maxtransmissionbounce.ValueChanged -= M_seed_ValueChanged;
		}


		private void ChangeIntegratorSetting(IntegratorSetting setting, int value)
		{
			var rvp = RhinoDoc.ActiveDoc.Views.ActiveView.RealtimeDisplayMode as RenderedViewport;
			if (rvp == null) return;
			var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) return;

			rvp.TriggerViewportSettingsChanged(vud);
		}
		private void M_seed_ValueChanged(object sender, EventArgs e)
		{
			var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) return;

			var ns = sender as NumericStepper;
			if (ns == null) return;
			var setting = (IntegratorSetting)ns.Tag;

			switch (setting)
			{
				case IntegratorSetting.Seed:
					vud.Seed = (int)ns.Value;
					break;
				case IntegratorSetting.DiffuseSamples:
					vud.DiffuseSamples = (int)ns.Value;
					break;
				case IntegratorSetting.GlossySamples:
					vud.GlossySamples = (int)ns.Value;
					break;
				case IntegratorSetting.TransmissionSamples:
					vud.TransmissionSamples = (int)ns.Value;
					break;
				case IntegratorSetting.MinBounce:
					vud.MinBounce = (int)ns.Value;
					break;
				case IntegratorSetting.MaxBounce:
					vud.MaxBounce = (int)ns.Value;
					break;
				case IntegratorSetting.MaxDiffuseBounce:
					vud.MaxDiffuseBounce = (int)ns.Value;
					break;
				case IntegratorSetting.MaxGlossyBounce:
					vud.MaxGlossyBounce = (int)ns.Value;
					break;
				case IntegratorSetting.MaxTransmissionBounce:
					vud.MaxTransmissionBounce = (int)ns.Value;
					break;
				case IntegratorSetting.MaxVolumeBounce:
					vud.MaxVolumeBounce = (int)ns.Value;
					break;
			}

			ChangeIntegratorSetting(setting, (int)ns.Value);
		}

	}
}
