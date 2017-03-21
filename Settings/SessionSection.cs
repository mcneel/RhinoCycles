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
	public class SessionSection: Section
	{
		private LocalizeStringPair m_caption;
		private Label m_button_lb;
		private NumericStepper m_samples;

		public override LocalizeStringPair Caption
		{
			get { return m_caption; }
		}

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight
		{
			get
			{
				return this.Content.Height;
			}
		}

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public SessionSection()
		{
			m_caption = new LocalizeStringPair("Session settings", Localization.LocalizeString("Session settings", 5));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			ViewportSettingsReceived += SessionSection_ViewportSettingsReceived;
		}

		private void SessionSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				m_samples.Value = e.ViewportSettings.Samples;
			}
		}

		private void InitializeComponents()
		{
			m_button_lb = new Label()
			{
				Text = Localization.LocalizeString("Samples", 6),
				VerticalAlignment = VerticalAlignment.Center,
			};
			m_samples = new NumericStepper()
			{
				Value = 0,
				MaximumDecimalPlaces = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
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
										new TableRow(m_button_lb, m_samples),
								}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_samples.ValueChanged += M_samples_ValueChanged;
		}

		private void M_samples_ValueChanged(object sender, EventArgs e)
		{
			var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) return;

			vud.Samples = (int)m_samples.Value;
		}

		private void UnRegisterControlEvents()
		{
		}
	}
}
