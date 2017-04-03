using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eto.Forms;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System.Windows.Threading;
using Rhino;
using RhinoCycles.Viewport;

namespace RhinoCycles.Settings
{
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class SessionSection: Section
	{
		private LocalizeStringPair m_caption;
		private Label m_samples_lb;
		private NumericStepper m_samples;
		private Label m_throttlems_lb;
		private NumericStepper m_throttlems;
		private Label m_device_lb;
		private ListBox m_devices;

		public override LocalizeStringPair Caption
		{
			get { return m_caption; }
		}

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => Content.Height;

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public SessionSection()
		{
			RcCore.It.InitialisationCompleted += It_InitialisationCompleted;
			m_caption = new LocalizeStringPair("Session settings", Localization.LocalizeString("Session settings", 5));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			ViewportSettingsReceived += SessionSection_ViewportSettingsReceived;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			var layout = Content as TableLayout;
			if (layout == null) return;
			layout.SetColumnScale(0, true);
			layout.SetColumnScale(1, true);
		}

		private void It_InitialisationCompleted(object sender, EventArgs e)
		{
			Application.Instance.AsyncInvoke(() =>
			{
				var vud = Plugin.GetActiveViewportSettings();
				SuspendLayout();
				m_devices.Items.Clear();
				foreach (var d in ccl.Device.Devices)
				{
					m_devices.Items.Add(d.Name);
				}
				if (vud != null)
				{
					var rd = RcCore.It.EngineSettings.RenderDevice;
					m_devices.SelectedIndex = (int)rd.Id;
				}
				ResumeLayout();
			}
			);
		}

		private void SessionSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				UnRegisterControlEvents();
				m_samples.Value = e.ViewportSettings.Samples;
				m_throttlems.Value = e.ViewportSettings.ThrottleMs;
				RegisterControlEvents();
			}
		}

		private void InitializeComponents()
		{
			m_samples_lb = new Label()
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
			m_throttlems_lb = new Label()
			{
				Text = Localization.LocalizeString("Throttle (in ms)", 19),
				VerticalAlignment = VerticalAlignment.Center,
			};
			m_throttlems = new NumericStepper()
			{
				Value = 0,
				MaximumDecimalPlaces = 0,
				MaxValue = int.MaxValue,
				MinValue = 0,
				Width = 75,
			};
			m_device_lb = new Label()
			{
				Text = Localization.LocalizeString("Device", 20),
				VerticalAlignment = VerticalAlignment.Top,
			};
			m_devices = new ListBox();
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
							new TableRow(m_samples_lb, m_samples),
							new TableRow(m_throttlems_lb, m_throttlems),
							//new TableRow(m_device_lb, m_devices),
					}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_samples.ValueChanged += M_ValueChanged;
			m_throttlems.ValueChanged += M_ValueChanged;
		}

		private void M_ValueChanged(object sender, EventArgs e)
		{
			var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) return;

			vud.Samples = (int)m_samples.Value;
			vud.ThrottleMs = (int)m_throttlems.Value;

			var rvp = RhinoDoc.ActiveDoc.Views.ActiveView.RealtimeDisplayMode as RenderedViewport;
			if (rvp == null) return;

			rvp.TriggerViewportSettingsChanged(vud);
			rvp.ChangeSamples(vud.Samples);
			rvp.SignalRedraw();
		}

		private void UnRegisterControlEvents()
		{
			m_samples.ValueChanged -= M_ValueChanged;
			m_throttlems.ValueChanged -= M_ValueChanged;
		}
	}
}
