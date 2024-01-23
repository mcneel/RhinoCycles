/**
Copyright 2014-2024 Robert McNeel and Associates

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
using Eto.Forms;
using Rhino.UI;
using System;
using static RhinoCyclesCore.RenderEngine;

namespace RhinoCyclesCore.Settings
{
	///<summary>
	/// The UI implementation for advanced settings section in Rendering panel
	///</summary>
	public class AdvancedSettingsSection : Section
	{
		private readonly LocalizeStringPair m_caption;
		private NumericStepper StepperSeed;
		private NumericStepper StepperMaxBounces;
		private NumericStepper StepperMaxDiffuseBounces;
		private NumericStepper StepperMaxGlossyBounces;
		private NumericStepper StepperMaxVolumeBounces;
		private NumericStepper StepperMaxTransmissionBounces;
		private NumericStepper StepperMaxTransparencyBounces;
		private NumericStepper StepperSamples;
		private CheckBox CheckboxUseSamples;
		private DropDown ListboxTextureBakeQuality;

		public override LocalizeStringPair Caption => m_caption;

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight
		{
			get
			{
				float dpi = ParentWindow != null ? ParentWindow.LogicalPixelSize : 1.0f;
				// Content.Height, use 450 for now
				int height = (int)(450 * dpi);
				return height;
			}
		}

		Guid m_pluginId;

		public override Guid PlugInId => m_pluginId;

		public Label LblSeed { get; set; }
		public Label LblSamples { get; set; }
		public Label LblMaxBounces { get; set; }
		public Label LblMaxDiffuseBounces { get; set; }
		public Label LblMaxGlossyBounces { get; set; }
		public Label LblMaxVolumeBounces { get; set; }
		public Label LblMaxTransmissionBounces { get; set; }
		public Label LblMaxTransparencyBounces { get; set; }
		public Label LblTextureBakeQuality { get; set; }
		public StackLayout MainLayout { get; set; }

		/// <summary>
		/// Local copy of Settings.Samples. Needed since we need it on
		/// the Unload event. At this point Settings is invalid, so
		/// that can't be used.
		/// </summary>
		private int Samples { get; set; } = 0;


		///<summary>
		/// Constructor for IntegratorSection
		///</summary>
		public AdvancedSettingsSection(Guid pluginId)
		{
			m_pluginId = pluginId;
			m_caption = Localization.LocalizeCommandOptionValue("Rhino Render Advanced Settings", 38);
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
		}

		public override void HolderVisible(bool visible)
		{
			if (visible)
			{
				HookListeners();
				DisplayData();
			}
			else
				UnHookListeners();
		}

		private bool m_hook = false;
		private void HookListeners()
		{
			if (!m_hook)
			{
				ViewModelActivated += IntegratorSection_ViewModelActivated;
				DataChanged += AdvancedSettingsSection_DataChanged;

				m_hook = true;
			}
		}

		private void UnHookListeners()
		{
			if (m_hook)
			{
				ViewModelActivated -= IntegratorSection_ViewModelActivated;
				DataChanged -= AdvancedSettingsSection_DataChanged;

				m_hook = false;
			}
		}

		private void AdvancedSettingsSection_DataChanged(object sender, Rhino.UI.Controls.DataSource.EventArgs e)
		{
			if(e.DataType == Rhino.UI.Controls.DataSource.ProviderIds.RhinoSettings)
				DisplayData();
		}

		private void SettingsForProperties_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			DisplayData();
		}

		private void IntegratorSection_ViewModelActivated(object sender, EventArgs e)
		{
			DataContext = ViewModel;
			DisplayData();
		}

		public override void DisplayData()
		{
			try
			{
				IntegratorSection_ViewportSettingsReceived(this, new EngineSettingsReceivedArgs(Settings));
			}
			catch (Exception)
			{

			}
		}

		private void IntegratorSection_ViewportSettingsReceived(object sender, EngineSettingsReceivedArgs e)
		{
			if (e.AllSettings != null)
			{
				UnregisterControlEvents();
				StepperSeed.Value = e.AllSettings.Seed;
				CheckboxUseSamples.Checked = e.AllSettings.UseDocumentSamples;
				StepperSamples.Value = e.AllSettings.Samples;
				StepperMaxBounces.Value = e.AllSettings.MaxBounce;
				StepperMaxDiffuseBounces.Value = e.AllSettings.MaxDiffuseBounce;
				StepperMaxGlossyBounces.Value = e.AllSettings.MaxGlossyBounce;
				StepperMaxVolumeBounces.Value = e.AllSettings.MaxVolumeBounce;
				StepperMaxTransmissionBounces.Value = e.AllSettings.MaxTransmissionBounce;
				StepperMaxTransparencyBounces.Value = e.AllSettings.TransparentMaxBounce;
				ListboxTextureBakeQuality.SelectedIndex = e.AllSettings.TextureBakeQuality;
				RegisterControlEvents();
			}
		}

		private void InitializeComponents()
		{
			LblSeed = new Label()
			{
				Text = Localization.LocalizeString("Seed", 4),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperSeed = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.Seed,
			};

			LblSamples = new Label()
			{
				Text = Localization.LocalizeString("Samples", 6),
				VerticalAlignment = VerticalAlignment.Center,
			};
			StepperSamples = new NumericStepper()
			{
				Value = 0,
				MaximumDecimalPlaces = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				Width = 75,
				Tag = AdvancedSettings.Samples,
			};

			CheckboxUseSamples = new CheckBox()
			{
				Checked = false,
				Text = Localization.LocalizeString("Override Production Render Quality", 1),
				ToolTip = Localization.LocalizeString("Check to override render quality setting. If unchecked affects only viewport sample count", 2),
			};

			LblMaxBounces = new Label()
			{
				Text = Localization.LocalizeString("Maximum bounces", 22),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperMaxBounces = new NumericStepper()
			{
				Value = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.MaxBounce,
			};

			LblMaxDiffuseBounces = new Label()
			{
				Text = Localization.LocalizeString("Diffuse", 31),
				ToolTip = Localization.LocalizeString("Maximum Diffuse Bounces", 15),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperMaxDiffuseBounces = new NumericStepper()
			{
				Value = 0,
				ToolTip = Localization.LocalizeString("Maximum Diffuse Bounces", 15),
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.MaxDiffuseBounce,
			};

			LblMaxGlossyBounces = new Label()
			{
				Text = Localization.LocalizeString("Glossy", 32),
				ToolTip = Localization.LocalizeString("Maximum Glossy Bounces", 16),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperMaxGlossyBounces = new NumericStepper()
			{
				Value = 0,
				ToolTip = Localization.LocalizeString("Maximum Glossy Bounces", 16),
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.MaxGlossyBounce,
			};

			LblMaxVolumeBounces = new Label()
			{
				Text = Localization.LocalizeString("Volume", 33),
				ToolTip = Localization.LocalizeString("Maximum Volume Bounces", 17),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperMaxVolumeBounces = new NumericStepper()
			{
				Value = 0,
				ToolTip = Localization.LocalizeString("Maximum Volume Bounces", 17),
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.MaxVolumeBounce,
			};

			LblMaxTransmissionBounces = new Label()
			{
				Text = Localization.LocalizeString("Transmission", 23),
				ToolTip = Localization.LocalizeString("Maximum Transmission Bounces", 18),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperMaxTransmissionBounces = new NumericStepper()
			{
				Value = 0,
				ToolTip = Localization.LocalizeString("Maximum Transmission Bounces", 18),
				MaxValue = int.MaxValue,
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.MaxTransmissionBounce,
			};

			LblMaxTransparencyBounces = new Label()
			{
				Text = Localization.LocalizeString("Transparency", 29),
				ToolTip = Localization.LocalizeString("Maximum Transparency Bounces", 30),
				VerticalAlignment = VerticalAlignment.Center,
			};

			StepperMaxTransparencyBounces = new NumericStepper()
			{
				Value = 0,
				ToolTip = Localization.LocalizeString("Maximum Transparency Bounces", 37),
				MinValue = 0,
				MaximumDecimalPlaces = 0,
				Width = 75,
				Tag = AdvancedSettings.TransparentMaxBounce,
			};
			LblTextureBakeQuality = new Label()
			{
				Text = Localization.LocalizeString("Texture Bake Quality", 3),
				VerticalAlignment = VerticalAlignment.Center,
			};
			ListboxTextureBakeQuality = new DropDown()
			{
				Items = {
					Localization.LocalizeString("Low", 8),
					Localization.LocalizeString("Standard", 9),
					Localization.LocalizeString("High", 10),
					Localization.LocalizeString("Ultra", 11),
					Localization.LocalizeString("Disable", 77),
				}
			};

		}


		private void InitializeLayout()
		{
			var bounceTableTitle = new TableLayout()
			{
				Rows = 
				{
					new TableRow(Localization.LocalizeString("Ray Bounces", 35), new Rhino.UI.Controls.Divider())
				}
			};

			var bounceTable = new TableLayout()
			{
				Padding = new Eto.Drawing.Padding(14, 0, 0, 0),
				Spacing = new Eto.Drawing.Size(5, 5),
				Rows =
				{
					new TableRow(LblMaxBounces, StepperMaxBounces),
					new TableRow(LblMaxDiffuseBounces, StepperMaxDiffuseBounces),
					new TableRow(LblMaxGlossyBounces, StepperMaxGlossyBounces),
					new TableRow(LblMaxTransmissionBounces, StepperMaxTransmissionBounces),
					new TableRow(LblMaxVolumeBounces, StepperMaxVolumeBounces),
					new TableRow(LblMaxTransparencyBounces, StepperMaxTransparencyBounces),
				}
			};

			var bounceMainTable = new TableLayout()
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				ToolTip = Localization.LocalizeString("Settings controlling the bounce limits\nfor different types of rays.", 36),
				Rows = 
				{
					new TableRow(bounceTableTitle),
					new TableRow(bounceTable),
				}
			};

			var sampleTableTitle = new TableLayout()
			{
				Rows = 
				{
					new TableRow(Localization.LocalizeString("Session", 12), new Rhino.UI.Controls.Divider())
				}
			};

			var sampleTableStpper = new TableLayout()
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				Rows =
				{
					new TableRow(LblSamples, StepperSamples),
				}
			};

			var sampleTable = new TableLayout()
			{
				Padding = new Eto.Drawing.Padding(14, 0, 0, 0),
				Spacing = new Eto.Drawing.Size(5, 5),
				Rows =
				{
					new TableRow(sampleTableStpper),
					new TableRow(CheckboxUseSamples),
				}
			};

			var sampleMainTable = new TableLayout()
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				ToolTip = Localization.LocalizeString("Settings for controlling Cycles in a session", 20),
				Rows = 
				{
					new TableRow(sampleTableTitle),
					new TableRow(sampleTable),
				}
			};


			var textureTableTitle = new TableLayout()
			{
				Rows = 
				{
					new TableRow(Localization.LocalizeString("Texture Baking", 21), new Rhino.UI.Controls.Divider())
				}
			};

			var textureTable = new TableLayout()
			{
				Padding = new Eto.Drawing.Padding(14, 0, 0, 0),
				Spacing = new Eto.Drawing.Size(5, 5),
				Rows =
				{
					new TableRow(LblTextureBakeQuality, ListboxTextureBakeQuality),
				}
			};

			var textureMainTable = new TableLayout()
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				ToolTip = Localization.LocalizeString("Setting for controlling texture bake resolution", 28),
				Rows = 
				{
					new TableRow(textureTableTitle),
					new TableRow(textureTable),
				}
			};

			var seedTableTitle = new TableLayout()
			{
				Rows = 
				{
					new TableRow(Localization.LocalizeString("Seed", 4), new Rhino.UI.Controls.Divider())
				}
			};

			var seedTable = new TableLayout()
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				Padding = new Eto.Drawing.Padding(14, 0, 0, 0),
				Rows =
				{
				  new TableRow(new TableCell(StepperSeed, true)) 
				}
			};

			var seedMainTable = new TableLayout()
			{
				Spacing = new Eto.Drawing.Size(5, 5),
				ToolTip = Localization.LocalizeString("Set the seed for the random number generator.", 34),
				Rows = 
				{
					new TableRow(seedTableTitle),
					new TableRow(seedTable),
				}
			};

			MainLayout = new StackLayout()
			{
				// Padding around the table
				Padding = new Eto.Drawing.Padding(3, 5, 3, 0),
				// Spacing between table cells
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				MinimumSize = new Eto.Drawing.Size(200, 400),
				Spacing = 10,
				Items =
				{
				TableLayout.Horizontal(10,
						seedMainTable
					),
					TableLayout.Horizontal(10,
						sampleMainTable
					),
					TableLayout.Horizontal(10,
						bounceMainTable
					),
					TableLayout.Horizontal(10,
						textureMainTable
					),
				}
			};
			Content = MainLayout;
		}

		private void RegisterControlEvents()
		{
			CheckboxUseSamples.CheckedChanged += CheckboxUseSamples_CheckedChanged;
			ListboxTextureBakeQuality.SelectedIndexChanged += ListboxTextureBakeQuality_SelectedIndexChanged;
			StepperSamples.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperSeed.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperMaxBounces.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperMaxDiffuseBounces.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperMaxGlossyBounces.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperMaxVolumeBounces.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperMaxTransmissionBounces.ValueChanged += IntegratorSettingValueChangedHandler;
			StepperMaxTransparencyBounces.ValueChanged += IntegratorSettingValueChangedHandler;
		}

		private void ListboxTextureBakeQuality_SelectedIndexChanged(object sender, EventArgs e)
		{
			var vud = Settings;
			if (vud == null) return;
			vud.TextureBakeQuality = ListboxTextureBakeQuality.SelectedIndex;
		}

		private void CheckboxUseSamples_CheckedChanged(object sender, EventArgs e)
		{
			var vud = Settings;
			if (vud == null) return;

			vud.UseDocumentSamples = CheckboxUseSamples.Checked.GetValueOrDefault(false);
		}

		private void UnregisterControlEvents()
		{
			CheckboxUseSamples.CheckedChanged -= CheckboxUseSamples_CheckedChanged;
			StepperSamples.ValueChanged -= IntegratorSettingValueChangedHandler;
			ListboxTextureBakeQuality.SelectedIndexChanged -= ListboxTextureBakeQuality_SelectedIndexChanged;
			StepperSeed.ValueChanged -= IntegratorSettingValueChangedHandler;
			StepperMaxBounces.ValueChanged -= IntegratorSettingValueChangedHandler;
			StepperMaxDiffuseBounces.ValueChanged -= IntegratorSettingValueChangedHandler;
			StepperMaxGlossyBounces.ValueChanged -= IntegratorSettingValueChangedHandler;
			StepperMaxVolumeBounces.ValueChanged -= IntegratorSettingValueChangedHandler;
			StepperMaxTransmissionBounces.ValueChanged -= IntegratorSettingValueChangedHandler;
			StepperMaxTransparencyBounces.ValueChanged -= IntegratorSettingValueChangedHandler;
		}

		private void IntegratorSettingValueChangedHandler(object sender, EventArgs e)
		{
			var vud = Settings;
			if (vud == null) return;

			if (!(sender is NumericStepper ns)) return;
			var setting = (AdvancedSettings)ns.Tag;

			switch (setting)
			{
				case AdvancedSettings.Seed:
					vud.Seed = (int)ns.Value;
					break;
				case AdvancedSettings.Samples:
					vud.Samples = (int)ns.Value;
					Samples = (int)ns.Value;
					break;
				case AdvancedSettings.MaxBounce:
					vud.MaxBounce = (int)ns.Value;
					break;
				case AdvancedSettings.MaxDiffuseBounce:
					vud.MaxDiffuseBounce = (int)ns.Value;
					break;
				case AdvancedSettings.MaxGlossyBounce:
					vud.MaxGlossyBounce = (int)ns.Value;
					break;
				case AdvancedSettings.MaxTransmissionBounce:
					vud.MaxTransmissionBounce = (int)ns.Value;
					break;
				case AdvancedSettings.MaxVolumeBounce:
					vud.MaxVolumeBounce = (int)ns.Value;
					break;
				case AdvancedSettings.TransparentMaxBounce:
					vud.TransparentMaxBounce = (int)ns.Value;
					break;
				default:
					throw new ArgumentException("Unknown IntegratorSetting encountered");
			}
		}

	}
}
