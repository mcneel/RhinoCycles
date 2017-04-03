using System;
using Eto.Forms;
using Rhino.UI.Controls;

namespace RhinoCycles.Settings
{
	public class CollapsibleSectionUIPanel : Panel
	{
		/// <summary>
		/// Returns the ID of this panel.
		/// </summary>
		public static Guid PanelId
		{
			get
			{
				return typeof(CollapsibleSectionUIPanel).GUID;
			}
		}

		/// <summary>
		/// Public constructor
		/// </summary>
		public CollapsibleSectionUIPanel()
		{
			InitializeComponents();
			InitializeLayout();
		}

		private EtoCollapsibleSectionHolder m_holder;
		private void InitializeComponents()
		{
			m_holder = new EtoCollapsibleSectionHolder();

		}

		private void InitializeLayout()
		{
			// Create holder for sections. The holder can expand/collaps sections and
			// displays a title for each section

			// Create two sections
			AddUserdataSection section0 = new AddUserdataSection();

			section0.ViewDataChanged += Section0_ViewDataChanged;
			IntegratorSection section1 = new IntegratorSection();
			SessionSection section2 = new SessionSection();
			DeviceSection section3 = new DeviceSection();

			// Populate the holder with sections
			m_holder.Add(section0);
			m_holder.Add(section1);
			m_holder.Add(section2);
			m_holder.Add(section3);

			// Create a tablelayout that contains the holder and add it to the UI
			// Content
			TableLayout tableLayout = new TableLayout()
			{
				Rows =
				{
					m_holder
				}
			};

			Content = tableLayout;
		}

		public event EventHandler ViewDataChanged;

		private void Section0_ViewDataChanged(object sender, EventArgs e)
		{
			ViewDataChanged?.Invoke(sender, e);
		}

		public void NoUserdataAvailable()
		{
			(m_holder.SectionAt(0) as Section)?.Show(null);
			(m_holder.SectionAt(1) as Section)?.Hide();
			(m_holder.SectionAt(2) as Section)?.Hide();
			(m_holder.SectionAt(3) as Section)?.Hide();
			m_holder.Invalidate(true);
			Invalidate(true);
		}
		public void UserdataAvailable(ViewportSettings vud)
		{
			(m_holder.SectionAt(0) as Section)?.Hide();
			(m_holder.SectionAt(1) as Section)?.Show(vud);
			(m_holder.SectionAt(2) as Section)?.Show(vud);
			(m_holder.SectionAt(3) as Section)?.Show(vud);
			m_holder.Invalidate(true);
			Invalidate(true);
		}
	}
}
