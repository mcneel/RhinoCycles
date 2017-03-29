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
using System.Collections.ObjectModel;

namespace RhinoCycles.Settings
{
	public class GridDevicePage : TabPage
	{
		private GridView m_gv;

		private ObservableCollection<DeviceItem> m_col;

		public ObservableCollection<DeviceItem> Collection { get { return m_col; } }

		public GridDevicePage()
		{
			m_col = new ObservableCollection<DeviceItem>();
			m_gv = new GridView { DataStore = m_col };
			m_gv.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Property<DeviceItem, string>(r => r.Text) },
				HeaderText = "Device"
			});

			m_gv.Columns.Add(new GridColumn {
				DataCell = new CheckBoxCell { Binding = Binding.Property<DeviceItem, bool?>(r => r.Selected) },
				HeaderText = "Use"
			});
			Content = new StackLayout
			{
				Spacing = 5,
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Items = {
					new StackLayoutItem(m_gv, true)
				}
			};
		}
	}

	public class DeviceItem
	{
		public string Text { get; set; }
		public bool Selected { get; set; }
		public int Id { get; set; }
	}
	///<summary>
	/// The UI implementation of of Section one
	///</summary>
	public class DeviceSection: Section
	{
		private LocalizeStringPair m_caption;
		private TabControl m_tc;
		private GridDevicePage m_tabpage_cpu;
		private GridDevicePage m_tabpage_cuda;
		private GridDevicePage m_tabpage_opencl;

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
				return Content.Height;
			}
		}

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public DeviceSection()
		{
			RcCore.It.InitialisationCompleted += It_InitialisationCompleted;
			m_caption = new LocalizeStringPair("Device settings", LOC.STR("Device settings"));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			ViewportSettingsReceived += DeviceSection_ViewportSettingsReceived;
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);
			var vud = Plugin.GetActiveViewportSettings();
			var rd = ActiveDevice(vud);
			if (rd.IsCpu) m_tc.SelectedPage = m_tabpage_cpu;
			if (rd.IsCuda || rd.IsMultiCuda) m_tc.SelectedPage = m_tabpage_cuda;
			if (rd.IsOpenCl || rd.IsMultiOpenCl) m_tc.SelectedPage = m_tabpage_opencl;
		}

		private static ccl.Device ActiveDevice(ViewportSettings vud)
		{
			ccl.Device rd;
			if (vud == null)
			{
				rd = RcCore.It.EngineSettings.RenderDevice;
			}
			else
			{
				rd = ccl.Device.DeviceFromString(vud.SelectedDevice);
			}
			return rd;
		}
		private static void SetupListbox(ViewportSettings vud, ObservableCollection<DeviceItem> lb, ccl.DeviceType t)
		{
			var rd = ActiveDevice(vud);
			lb.Clear();
			foreach (var d in ccl.Device.Devices)
			{
				if (d.Type == t)
				{
					lb.Add(new DeviceItem { Text = d.NiceName, Selected = rd.EqualsId(d.Id), Id = (int)d.Type });
				}
			}
		}

		private void It_InitialisationCompleted(object sender, EventArgs e)
		{
			Application.Instance.AsyncInvoke(() =>
			{
				var vud = Plugin.GetActiveViewportSettings();
				UnRegisterControlEvents();
				SuspendLayout();
				SetupListbox(vud, m_tabpage_cpu.Collection, ccl.DeviceType.CPU);
				SetupListbox(vud, m_tabpage_cuda.Collection, ccl.DeviceType.CUDA);
				SetupListbox(vud, m_tabpage_opencl.Collection, ccl.DeviceType.OpenCL);
				ResumeLayout();
				RegisterControlEvents();
			}
			);
		}

		private void DeviceSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				UnRegisterControlEvents();
				RegisterControlEvents();
			}
		}

		private void InitializeComponents()
		{
			m_tc = new TabControl();
			m_tabpage_cpu = new GridDevicePage { Text = "CPU" };
			m_tabpage_cuda = new GridDevicePage { Text = "CUDA" };
			m_tabpage_opencl = new GridDevicePage { Text = "OpenCL" };
			m_tc.Pages.Add(m_tabpage_cpu);
			m_tc.Pages.Add(m_tabpage_cuda);
			m_tc.Pages.Add(m_tabpage_opencl);
		}


		private void InitializeLayout()
		{
			StackLayout layout = new StackLayout()
			{
				// Padding around the table
				Padding = 10,
				// Spacing between table cells
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Items =
				{
					new StackLayoutItem(m_tc, true),
				}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			//m_samples.ValueChanged += M_ValueChanged;
			//m_throttlems.ValueChanged += M_ValueChanged;
		}

		private void M_ValueChanged(object sender, EventArgs e)
		{
			/*var vud = Plugin.GetActiveViewportSettings();
			if (vud == null) return;

			vud.Samples = (int)m_samples.Value;
			vud.ThrottleMs = (int)m_throttlems.Value;

			var rvp = RhinoDoc.ActiveDoc.Views.ActiveView.RealtimeDisplayMode as RenderedViewport;
			if (rvp == null) return;

			rvp.TriggerViewportSettingsChanged(vud);
			rvp.ChangeSamples(vud.Samples);
			rvp.SignalRedraw();*/
		}

		private void UnRegisterControlEvents()
		{
			//m_samples.ValueChanged -= M_ValueChanged;
			//m_throttlems.ValueChanged -= M_ValueChanged;
		}
	}
}
