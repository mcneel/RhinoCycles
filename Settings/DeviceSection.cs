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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RhinoCycles.Settings
{
	public class SelectionChangedEventArgs : EventArgs
	{
		public ObservableCollection<DeviceItem> Collection { get; private set; }
		public SelectionChangedEventArgs(ObservableCollection<DeviceItem> col)
		{
			Collection = col;
		}
	}

	public class GridDevicePage : TabPage
	{
		private GridView m_gv;

		public GridView Grid => m_gv;

		private ObservableCollection<DeviceItem> m_col;

		public ObservableCollection<DeviceItem> Collection => m_col;

		public event EventHandler SelectionChanged;

		public GridDevicePage()
		{
			m_col = new ObservableCollection<DeviceItem>();
			m_col.CollectionChanged += M_col_CollectionChanged;
			m_gv = new GridView { DataStore = m_col };
			m_gv.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Property<DeviceItem, string>(r => r.Text) },
				HeaderText = "Device"
			});

			m_gv.Columns.Add(new GridColumn {
				DataCell = new CheckBoxCell { Binding = Binding.Property<DeviceItem, bool?>(r => r.Selected) },
				HeaderText = "Use",
				Editable = true
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

		public void RegisterEventHandlers()
		{
			foreach(var di in m_col)
			{
				di.PropertyChanged += Di_PropertyChanged;
			}
		}

		public void UnregisterEventHandlers()
		{
			foreach(var di in m_col)
			{
				di.PropertyChanged -= Di_PropertyChanged;
			}
		}

		private void M_col_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if(e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
			{
				foreach(var i in e.NewItems)
				{
					var di = i as DeviceItem;
					if(di!=null)
					{
						di.PropertyChanged -= Di_PropertyChanged;
						di.PropertyChanged += Di_PropertyChanged;
					}
				}
			}
		}

		private void Di_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			var di = sender as DeviceItem;
			if (e.PropertyName.CompareTo("Selected") == 0)
			{
				SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(m_col));
			}
		}
	}

	public class DeviceItem : INotifyPropertyChanged
	{
		public string Text { get; set; }

		private bool _selected;
		public bool Selected
		{
			get { return _selected; }
			set
			{
				if(value!=_selected)
				{
					_selected = value;
					OnPropertyChanged();
				}
			}
		}

		ccl.Device _dev;
		int _id;
		public int Id { get {
				return _id;
			}
			set
			{
				_id = value;
				_dev = ccl.Device.GetDevice(_id);
			}
		}

		public ccl.Device Device => _dev;

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string memberName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
		}
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
				SuspendLayout();
				UnRegisterControlEvents();
				SetupListbox(vud, m_tabpage_cpu.Collection, ccl.DeviceType.CPU);
				SetupListbox(vud, m_tabpage_cuda.Collection, ccl.DeviceType.CUDA);
				SetupListbox(vud, m_tabpage_opencl.Collection, ccl.DeviceType.OpenCL);
				RegisterControlEvents();
				ResumeLayout();
			}
			);
		}

		private void DeviceSection_ViewportSettingsReceived(object sender, ViewportSettingsReceivedEventArgs e)
		{
			if (e.ViewportSettings != null)
			{
				SuspendLayout();
				UnRegisterControlEvents();
				SetupListbox(e.ViewportSettings, m_tabpage_cpu.Collection, ccl.DeviceType.CPU);
				SetupListbox(e.ViewportSettings, m_tabpage_cuda.Collection, ccl.DeviceType.CUDA);
				SetupListbox(e.ViewportSettings, m_tabpage_opencl.Collection, ccl.DeviceType.OpenCL);
				var rd = ActiveDevice(e.ViewportSettings);
				if (rd.IsCuda || rd.IsMultiCuda) m_tc.SelectedPage = m_tabpage_cuda;
				else if (rd.IsOpenCl || rd.IsMultiOpenCl) m_tc.SelectedPage = m_tabpage_opencl;
				else m_tc.SelectedPage = m_tabpage_cpu;
				RegisterControlEvents();
				ResumeLayout();
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
			m_tabpage_cpu.SelectionChanged += deviceSelectionChanged;
			m_tabpage_cpu.RegisterEventHandlers();
			m_tabpage_cuda.SelectionChanged += deviceSelectionChanged;
			m_tabpage_cuda.RegisterEventHandlers();
			m_tabpage_opencl.SelectionChanged += deviceSelectionChanged;
			m_tabpage_opencl.RegisterEventHandlers();
		}

		private void deviceSelectionChanged(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		private void UnRegisterControlEvents()
		{
			m_tabpage_cpu.SelectionChanged -= deviceSelectionChanged;
			m_tabpage_cpu.UnregisterEventHandlers();
			m_tabpage_cuda.SelectionChanged -= deviceSelectionChanged;
			m_tabpage_cuda.UnregisterEventHandlers();
			m_tabpage_opencl.SelectionChanged -= deviceSelectionChanged;
			m_tabpage_opencl.UnregisterEventHandlers();
		}
	}
}
