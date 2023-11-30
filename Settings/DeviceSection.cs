/**
Copyright 2014-2021 Robert McNeel and Associates

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
using Rhino.Render;
using Rhino.Runtime;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static RhinoCyclesCore.RenderEngine;

namespace RhinoCyclesCore.Settings
{
	public class SelectionChangedEventArgs : EventArgs
	{
		public DeviceItem DeviceItem { get; private set; }
		public SelectionChangedEventArgs(DeviceItem di) 
		{
			DeviceItem = di;
		}
	}
	public class GridDevicePage : TabPage
	{
		private GridView m_gv;

		public GridView Grid => m_gv;

		private ObservableCollection<DeviceItem> m_col;

		public ObservableCollection<DeviceItem> Collection => m_col;

		public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

		public GridDevicePage()
		{
			m_col = new ObservableCollection<DeviceItem>();
			m_col.CollectionChanged += M_col_CollectionChanged;
			m_gv = new GridView { DataStore = m_col, ShowHeader = false };
			m_gv.Columns.Add(new GridColumn {
				DataCell = new CheckBoxCell { Binding = Binding.Property<DeviceItem, bool?>(r => r.Selected) },
				HeaderText = "Use",
				Editable = false,
				Expand = false,
			});
			m_gv.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Property<DeviceItem, string>(r => r.Text) },
				HeaderText = "Device",
				Expand = false,
			});
			m_gv.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Property<DeviceItem, string>(r => "\t\t") },
				HeaderText = "Filler",
				Width = 100,
				Expand = true,
			});
			m_gv.Columns.Add(new GridColumn {
				DataCell = new CheckBoxCell { Binding = Binding.Property<DeviceItem, bool?>(r => r.Ready) },
				HeaderText = "Ready",
				Editable = false,
				Expand = false,
			});
			Content = new StackLayout
			{
				Spacing = 5,
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Items = {
					new StackLayoutItem(control: m_gv, expand: true)
				}
			};
		}

		public void ClearSelection()
		{
			foreach(var di in m_col)
			{
				di.Selected = false;
			}
		}

		public void SetSelected(DeviceItem seldi)
		{
			foreach(var di in m_col)
			{
				if(di.Id == seldi.Id) {
					di.Selected = true;
				}
			}
		}

		public string DeviceSelectionString()
		{
			var selid = (from d in m_col where d.Selected select d.Id).DefaultIfEmpty(-1).First();
			var str = $"{selid}";

			return str;

		}

		public void RegisterEventHandlers()
		{
			m_gv.CellClick += M_gv_CellClick;
			foreach(var di in m_col)
			{
				di.PropertyChanged += Di_PropertyChanged;
			}
		}

		private void M_gv_CellClick(object sender, GridCellMouseEventArgs e)
		{
			if(e.Row>-1)
			{
				if(e.Item is DeviceItem i)
				{
					i.Selected = !i.Selected;
				}
			}
		}

		public void UnregisterEventHandlers()
		{
			m_gv.CellClick -= M_gv_CellClick;
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
					if (i is DeviceItem di)
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
				SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(di));
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

		int _id;
		public int Id { get {
				return _id;
			}
			set
			{
				_id = value;
				Device = ccl.Device.GetDevice(_id);
			}
		}

		public bool Ready { get; set; }

		public ccl.Device Device { get; private set; }

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string memberName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
		}
	}
	///<summary>
	/// The UI implementation of device section
	///</summary>
	public class DeviceSection : ApplicationSection
	{
		private LocalizeStringPair m_caption;
		private TabControl m_tc;
		private Label m_lb_curdev;
		private Label m_curdev;
		private GridDevicePage m_tabpage_cpu;
		private GridDevicePage m_tabpage_cuda;
		private GridDevicePage m_tabpage_optix;
		private GridDevicePage m_tabpage_metal;
		private GridDevicePage m_tabpage_hip;
		private GridDevicePage m_tabpage_oneapi;
		private ccl.Device m_currentDevice;
		private Label m_lb_threadcount;
		private Slider m_threadcount;
		private Label m_lb_threadcount_currentval;
		private Label m_lb_gpusdisabled_message;
		private Button m_btn_enablegpus;

		public override LocalizeStringPair Caption
		{
			get { return m_caption; }
		}

		///<summary>
		/// The Heigth of the section
		///</summary>
		public override int SectionHeight => Content.Height;

		public override bool Hidden
		{
			get
			{
				return false;
			}
		}

		///<summary>
		/// Constructor for SectionOne
		///</summary>
		public DeviceSection(uint doc_serial) : base(doc_serial)
		{
			RcCore.It.InitialisationCompleted += It_InitialisationCompleted;
			RcCore.It.DeviceKernelReady += It_DeviceKernelReady;
			m_caption = new LocalizeStringPair("Device settings", Localization.LocalizeString("Device settings", 14));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			EngineSettingsReceived += DeviceSection_EngineSettingsReceivedHandler;
		}

		private void It_DeviceKernelReady(object sender, EventArgs e)
		{
			DeviceSection_EngineSettingsReceivedHandler(this, new EngineSettingsReceivedArgs(Settings));
		}

		protected override void OnShown(EventArgs e)
		{
			IDocumentSettings vud = Utilities.GetEngineDocumentSettings(m_doc_serialnumber);
			var rd = ActiveDevice(vud);
			if (rd.IsCpu) m_tc.SelectedPage = m_tabpage_cpu;
			if (ccl.Device.CudaAvailable() && (rd.IsCuda || rd.IsMultiCuda)) m_tc.SelectedPage = m_tabpage_cuda;
			if (ccl.Device.OptixAvailable() && (rd.IsOptix|| rd.IsMultiOptix)) m_tc.SelectedPage = m_tabpage_optix;
			if (ccl.Device.MetalAvailable() && rd.IsMetal) m_tc.SelectedPage = m_tabpage_metal;
			if (ccl.Device.HipAvailable() && rd.IsHip) m_tc.SelectedPage = m_tabpage_hip;
			if (ccl.Device.OneApiAvailable() && rd.IsOneApi) m_tc.SelectedPage = m_tabpage_oneapi;
			base.OnShown(e);
		}

		private static ccl.Device ActiveDevice(IDocumentSettings vud)
		{
			var	rd = RcCore.It.AllSettings.RenderDevice;
			return rd;
		}
		private static void SetupDeviceData(IDocumentSettings vud, ObservableCollection<DeviceItem> lb, ccl.DeviceType t)
		{
			var rd = ActiveDevice(vud);
			lb.Clear();
			foreach (var d in ccl.Device.Devices)
			{
				if (d.Type == t)
				{
					var deviceCheck = RcCore.It.IsDeviceReady(d);
					lb.Add(new DeviceItem { Text = d.NiceName, Selected = rd.HasId(d.Id), Id = (int)d.Id, Ready = deviceCheck.isDeviceReady });
				}
			}
		}

		public override void DisplayData()
		{
			DeviceSection_EngineSettingsReceivedHandler(this, new EngineSettingsReceivedArgs(Settings));
		}

		private void It_InitialisationCompleted(object sender, EventArgs e)
		{
			DeviceSection_EngineSettingsReceivedHandler(this, new EngineSettingsReceivedArgs(Settings));
		}

		private void ActivateDevicePage(IDocumentSettings vud)
		{
			var rd = ActiveDevice(vud);
			if (ccl.Device.CudaAvailable() && (rd.IsCuda || rd.IsMultiCuda)) m_tc.SelectedPage = m_tabpage_cuda;
			else if (ccl.Device.OptixAvailable() && (rd.IsOptix || rd.IsMultiOptix)) m_tc.SelectedPage = m_tabpage_optix;
			else if (ccl.Device.MetalAvailable() && rd.IsMetal) m_tc.SelectedPage = m_tabpage_metal;
			else if (ccl.Device.HipAvailable() && rd.IsHip) m_tc.SelectedPage = m_tabpage_hip;
			else if (ccl.Device.OneApiAvailable() && rd.IsOneApi) m_tc.SelectedPage = m_tabpage_oneapi;
			else m_tc.SelectedPage = m_tabpage_cpu;
		}

		private void DeviceSection_EngineSettingsReceivedHandler(object sender, EngineSettingsReceivedArgs e)
		{
			m_currentDevice = RcCore.It.AllSettings.RenderDevice;

			Application.Instance.AsyncInvoke(() => {
				if (e.AllSettings != null) {
					SuspendLayout();
					UnRegisterControlEvents();
					ShowDeviceData();
					SetupDeviceData(e.AllSettings, m_tabpage_cpu.Collection, ccl.DeviceType.Cpu);
					if(ccl.Device.CudaAvailable()) SetupDeviceData(e.AllSettings, m_tabpage_cuda.Collection, ccl.DeviceType.Cuda);
					if(ccl.Device.OptixAvailable()) SetupDeviceData(e.AllSettings, m_tabpage_optix.Collection, ccl.DeviceType.Optix);
					if(ccl.Device.MetalAvailable()) SetupDeviceData(e.AllSettings, m_tabpage_metal.Collection, ccl.DeviceType.Metal);
					if(ccl.Device.HipAvailable()) SetupDeviceData(e.AllSettings, m_tabpage_hip.Collection, ccl.DeviceType.Hip);
					if(ccl.Device.OneApiAvailable()) SetupDeviceData(e.AllSettings, m_tabpage_oneapi.Collection, ccl.DeviceType.OneApi);
					ActivateDevicePage(e.AllSettings);
					m_lb_threadcount.Visible = m_currentDevice.IsCpu;
					m_lb_threadcount_currentval.Visible = m_currentDevice.IsCpu;
					m_threadcount.Visible = m_currentDevice.IsCpu;
					m_threadcount.Value = e.AllSettings.Threads;
					m_lb_gpusdisabled_message.Visible = Utilities.GpusDisabled;
					m_btn_enablegpus.Visible = Utilities.GpusDisabled;
					int utilPerc = (int)((float)e.AllSettings.Threads / Utilities.GetSystemProcessorCount() * 100.0f);
					m_lb_threadcount_currentval.Text = $"(\u2248{utilPerc} %)";
					RegisterControlEvents();
					ResumeLayout();
				}
			});
		}

		private void InitializeComponents()
		{
			m_tc = new TabControl();

			m_tabpage_cpu = new GridDevicePage { Text = "CPU", Image = Rhino.Resources.Assets.Rhino.Eto.Icons.TryGet(Rhino.Resources.ResourceIds.Svg_CPUSvg, new Eto.Drawing.Size(16, 16)), ToolTip = Localization.LocalizeString("Show all the render devices in the Cpu category.", 24)};
			m_tabpage_cuda = new GridDevicePage { Text = "CUDA", Image = Rhino.Resources.Assets.Rhino.Eto.Icons.TryGet(Rhino.Resources.ResourceIds.Svg_CUDASvg, new Eto.Drawing.Size(16, 16)), ToolTip = Localization.LocalizeString("Show all the render devices in the Cuda category.\nThese are the NVidia graphics and compute cards.", 25) };
			m_tabpage_optix = new GridDevicePage { Text = "Optix", Image = Rhino.Resources.Assets.Rhino.Eto.Icons.TryGet(Rhino.Resources.ResourceIds.Svg_OPTIXSvg, new Eto.Drawing.Size(16, 16)), ToolTip = Localization.LocalizeString("Show all the render devices in the Optix category.\nThese are the NVidia graphics and compute cards from Maxwell architecture and newer.", 41) };
			m_tabpage_metal = new GridDevicePage { Text = "Metal", Image = Rhino.Resources.Assets.Rhino.Eto.Icons.TryGet(Rhino.Resources.ResourceIds.Svg_AppleMetalLogoSvg, new Eto.Drawing.Size(16, 16)), ToolTip = Localization.LocalizeString("Show all the render devices in the Metal category.\nThese include GPU devices on MacOS systems.", 26) };
			m_tabpage_hip = new GridDevicePage { Text = "HIP", Image = Rhino.Resources.Assets.Rhino.Eto.Icons.TryGet(Rhino.Resources.ResourceIds.Svg_HIPSvg, new Eto.Drawing.Size(16, 16)), ToolTip = Localization.LocalizeString("Show all the render devices in the HIP category.\nThese include AMD GPU and supported devices.", 78) };
			m_tabpage_oneapi = new GridDevicePage { Text = "OneAPI", Image = Rhino.Resources.Assets.Rhino.Eto.Icons.TryGet(Rhino.Resources.ResourceIds.Svg_OneAPISvg, new Eto.Drawing.Size(16, 16)), ToolTip = Localization.LocalizeString("Show all the render devices in the OneAPI category.\nThese include Intel GPU and supported devices.", 79) };

			m_tc.Pages.Add(m_tabpage_cpu);
			if(ccl.Device.CudaAvailable()) {
				m_tc.Pages.Add(m_tabpage_cuda);
			}
			if(ccl.Device.OptixAvailable()) {
				m_tc.Pages.Add(m_tabpage_optix);
			}
			if(ccl.Device.MetalAvailable()) {
				m_tc.Pages.Add(m_tabpage_metal);
			}
			if(ccl.Device.HipAvailable()) {
				m_tc.Pages.Add(m_tabpage_hip);
			}
			if(ccl.Device.OneApiAvailable()) {
				m_tc.Pages.Add(m_tabpage_oneapi);
			}

			m_lb_curdev = new Label { Text = Localization.LocalizeString("Current render device:", 27) };
			m_curdev = new Label { Text = "...", Wrap = WrapMode.Word };

			m_threadcount = new Slider()
			{
				SnapToTick = true,
				TickFrequency = 1,
				Value = 1,
				MaxValue = Utilities.GetSystemProcessorCount(),
				MinValue = 1,
				Width = 130,
				Orientation = Orientation.Horizontal
			};
			m_lb_threadcount = new Label { Text = Localization.LocalizeString("Cpu Utilization", 13), ToolTip = Localization.LocalizeString("Utilization percentage of Cpu to use when set as render device", 42) };
			m_lb_threadcount_currentval = new Label { Text = "-" };

			m_lb_gpusdisabled_message = new Label
			{
				Text = Localization.LocalizeString("GPU detection is disabled. Press the 'Enable GPU detection' button and restart Rhino.", 72)
			};
			m_btn_enablegpus = new Button
			{
				Text = Localization.LocalizeString("Enable GPU detection", 73),
				ToolTip = Localization.LocalizeString("Press to enable GPU detection, then restart Rhino", 74)
			};

		}


		private void InitializeLayout()
		{
			StackLayout layout = new StackLayout()
			{
				Padding = 10,
				Spacing = 5,
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				Orientation = Orientation.Vertical,
				Items =
				{
					TableLayout.HorizontalScaled(15, m_lb_curdev, m_curdev),
					new StackLayoutItem(m_tc, true),
					TableLayout.HorizontalScaled(15, m_lb_threadcount, m_threadcount, m_lb_threadcount_currentval),
					TableLayout.HorizontalScaled(15, m_lb_gpusdisabled_message, m_btn_enablegpus),
				}
			};
			Content = layout;
		}

		private void RegisterControlEvents()
		{
			m_tabpage_cpu.SelectionChanged += DeviceSelectionChanged;
			m_tabpage_cpu.RegisterEventHandlers();
			if (ccl.Device.CudaAvailable())
			{
				m_tabpage_cuda.SelectionChanged += DeviceSelectionChanged;
				m_tabpage_cuda.RegisterEventHandlers();
			}
			if (ccl.Device.OptixAvailable())
			{
				m_tabpage_optix.SelectionChanged += DeviceSelectionChanged;
				m_tabpage_optix.RegisterEventHandlers();
			}
			if (ccl.Device.MetalAvailable())
			{
				m_tabpage_metal.SelectionChanged += DeviceSelectionChanged;
				m_tabpage_metal.RegisterEventHandlers();
			}
			if (ccl.Device.HipAvailable())
			{
				m_tabpage_hip.SelectionChanged += DeviceSelectionChanged;
				m_tabpage_hip.RegisterEventHandlers();
			}
			if (ccl.Device.OneApiAvailable())
			{
				m_tabpage_oneapi.SelectionChanged += DeviceSelectionChanged;
				m_tabpage_oneapi.RegisterEventHandlers();
			}
			m_threadcount.ValueChanged += M_threadcount_ValueChanged;
			m_btn_enablegpus.Click += m_btn_enablegpus_Clicked;
		}

		private void m_btn_enablegpus_Clicked(object sender, EventArgs e)
		{
			Utilities.EnableGpus();
			Eto.Forms.MessageBox.Show(Localization.LocalizeString("GPU detection has been enabled. Please restart Rhino.", 75), Eto.Forms.MessageBoxType.Information);
		}

		private void M_threadcount_ValueChanged(object sender, EventArgs e)
		{
			Settings.Threads = (int)m_threadcount.Value;
			Application.Instance.AsyncInvoke(() => {
				UnRegisterControlEvents();
				int utilPerc = (int)((float)Settings.Threads / Utilities.GetSystemProcessorCount() * 100.0f);
				m_lb_threadcount_currentval.Text = $"(\u2248{utilPerc} %)";
				RegisterControlEvents();
			});
		}

		private void ShowDeviceData()
		{
			var nodev = "-";
			if(m_currentDevice!=null)
			{
				if(m_currentDevice.Type != ccl.DeviceType.Optix)
					m_curdev.Text = $"{m_currentDevice.NiceName} ({m_currentDevice.Type})";
				else
					m_curdev.Text = $"{m_currentDevice.NiceName}";
			}
			else
			{
				m_curdev.Text = nodev;
			}
		}

		private void HandleResetClick(object sender, EventArgs e)
		{
			var vud = Utilities.GetEngineDocumentSettings(m_doc_serialnumber);
			if (vud != null)
			{
				vud.SelectedDeviceStr = RcCore.It.AllSettings.SelectedDeviceStr;
				vud.IntermediateSelectedDeviceStr = vud.SelectedDeviceStr;
				It_InitialisationCompleted(this, EventArgs.Empty);
			}
		}

		private void DeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			UnRegisterControlEvents();

			if (sender is GridDevicePage senderpage)
			{
				foreach (var page in m_tc.Pages)
				{
					if (page is GridDevicePage p) {
						p.ClearSelection();
						if(page == sender) {
							p.SetSelected(e.DeviceItem);
						}
					}
				}
				var vud = Settings;
				if (vud != null)
				{
					var dev = ccl.Device.DeviceFromString(senderpage.DeviceSelectionString());
					vud.IntermediateSelectedDeviceStr = dev.DeviceString;
					vud.SelectedDeviceStr = vud.IntermediateSelectedDeviceStr;
				}
			}

			RegisterControlEvents();

			It_InitialisationCompleted(this, EventArgs.Empty);
		}

		private void UnRegisterControlEvents()
		{
			m_tabpage_cpu.SelectionChanged -= DeviceSelectionChanged;
			m_tabpage_cpu.UnregisterEventHandlers();
			if (ccl.Device.CudaAvailable())
			{
				m_tabpage_cuda.SelectionChanged -= DeviceSelectionChanged;
				m_tabpage_cuda.UnregisterEventHandlers();
			}
			if (ccl.Device.OptixAvailable())
			{
				m_tabpage_optix.SelectionChanged -= DeviceSelectionChanged;
				m_tabpage_optix.UnregisterEventHandlers();
			}
			if (ccl.Device.MetalAvailable())
			{
				m_tabpage_metal.SelectionChanged -= DeviceSelectionChanged;
				m_tabpage_metal.UnregisterEventHandlers();
			}
			if (ccl.Device.HipAvailable())
			{
				m_tabpage_hip.SelectionChanged -= DeviceSelectionChanged;
				m_tabpage_hip.UnregisterEventHandlers();
			}
			if (ccl.Device.OneApiAvailable())
			{
				m_tabpage_oneapi.SelectionChanged -= DeviceSelectionChanged;
				m_tabpage_oneapi.UnregisterEventHandlers();
			}
			m_threadcount.ValueChanged -= M_threadcount_ValueChanged;
			m_btn_enablegpus.Click -= m_btn_enablegpus_Clicked;
		}
	}
}
