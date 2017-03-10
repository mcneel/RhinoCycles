using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino;
using Rhino.DocObjects;
using Rhino.UI;

namespace RhinoCycles.Settings
{
	public class ViewportPropertiesPage : ObjectPropertiesPage
	{
		private Bitmap m_icon;
		public ViewportPropertiesPage()
		{
			CollapsibleSectionHolder = new CollapsibleSectionUIPanel();
			CollapsibleSectionHolder.ViewDataChanged += CollapsibleSectionHolder_ViewDataChanged;
			m_icon = new Bitmap(32, 32);
			var brush = new SolidBrush(Color.Chocolate);

			using (Graphics g = Graphics.FromImage(m_icon))
			{
				g.FillRectangle(brush, new Rectangle(Point.Empty, m_icon.Size));
			}
		}

		private void CollapsibleSectionHolder_ViewDataChanged(object sender, EventArgs e)
		{
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

		public void UserDataAvailable(ViewportSettings vud)
		{
			CollapsibleSectionHolder.UserdataAvailable(vud);
		}

		public override string EnglishPageTitle => "Cycles";

		public override Icon Icon => null;

		public override PropertyPageType PageType => PropertyPageType.View;

		public override object PageControl => CollapsibleSectionHolder;

		private CollapsibleSectionUIPanel CollapsibleSectionHolder { get; }
	}
}
