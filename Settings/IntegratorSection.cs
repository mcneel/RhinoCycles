using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;
using Rhino.UI;

namespace RhinoCycles.Settings
{
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class IntegratorSection: Section
	{
		private LocalizeStringPair m_caption;
		private Label m_button_lb;
		private NumericStepper m_seed;

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

		private void IntegratorSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				m_seed.Value = e.ViewportSettings.Seed;
			}
		}

		private void InitializeComponents()
		{
			m_button_lb = new Label()
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
			};

		}


		private void InitializeLayout()
		{
			TableLayout layout = new TableLayout()
			{
				// Padding around the table
				Padding = 10,
				// Spacing between table cells
				Spacing = new Eto.Drawing.Size(15, 15),
				Rows =
								{
										new TableRow(m_button_lb, m_seed),
								}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_seed.ValueChanged += M_seed_ValueChanged;
		}

		private void M_seed_ValueChanged(object sender, EventArgs e)
		{
			var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) return;

			vud.Seed = (int)m_seed.Value;
		}

		private void UnRegisterControlEvents()
		{
		}

	}
}
