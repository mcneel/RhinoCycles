using System;
using Eto.Forms;
using Rhino;
using Rhino.UI;

namespace RhinoCycles.Settings
{
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class AddUserdataSection: Section
	{
		private LocalizeStringPair m_caption;
		private Button m_button;

		public override LocalizeStringPair Caption => m_caption;

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => Content.Height;

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public AddUserdataSection()
		{
			m_caption = new LocalizeStringPair("Override Cycles settings", "Override Cycles settings");
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
		}

		private void InitializeComponents()
		{
			m_button = new Button()
			{
				Text = "Open settings..."
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
										new TableRow(m_button),
								}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_button.Click += OnButtonClick;
		}

		private void UnRegisterControlEvents()
		{
			m_button.Click -= OnButtonClick;
		}


		public event EventHandler ViewDataChanged;

		private void OnButtonClick(object sender, EventArgs e)
		{
			RhinoApp.RunScript("_TestAddViewportSettings", false);
			ViewDataChanged?.Invoke(this, EventArgs.Empty);
		}

	}
}
