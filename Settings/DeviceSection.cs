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
			m_gv = new GridView { DataStore = m_col, ShowHeader = false };
			m_gv.Columns.Add(new GridColumn {
				DataCell = new CheckBoxCell { Binding = Binding.Property<DeviceItem, bool?>(r => r.Selected) },
				HeaderText = "Use",
				Editable = false
			});
			m_gv.Columns.Add(new GridColumn {
				DataCell = new TextBoxCell { Binding = Binding.Property<DeviceItem, string>(r => r.Text) },
				HeaderText = "Device"
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

		public void ClearSelection()
		{
			foreach(var di in m_col)
			{
				di.Selected = false;
			}
		}

		public string DeviceSelectionString()
		{
			var str = string.Join(",", (from d in m_col where d.Selected select d.Id).ToList());

			return string.IsNullOrEmpty(str) ? "-1" : str;

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
				SelectionChanged?.Invoke(this, EventArgs.Empty);
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
			m_caption = new LocalizeStringPair("Device settings", Localization.LocalizeString("Device settings", 14));
			InitializeComponents();
			InitializeLayout();
			RegisterControlEvents();
			EngineSettingsReceived += DeviceSection_EngineSettingsReceivedHandler;
		}

		protected override void OnShown(EventArgs e)
		{
			IDocumentSettings vud = Utilities.GetEngineDocumentSettings(m_doc_serialnumber);
			var rd = ActiveDevice(vud);
			if (rd.IsCpu) m_tc.SelectedPage = m_tabpage_cpu;
			if (rd.IsCuda || rd.IsMultiCuda) m_tc.SelectedPage = m_tabpage_cuda;
			if (rd.IsOptix|| rd.IsMultiOptix) m_tc.SelectedPage = m_tabpage_optix;
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
					lb.Add(new DeviceItem { Text = d.NiceName, Selected = rd.HasId(d.Id), Id = (int)d.Id });
				}
			}
		}

		public override void DisplayData()
		{
			DeviceSection_EngineSettingsReceivedHandler(this, new EngineSettingsReceivedArgs(Settings));
		}

		private void It_InitialisationCompleted(object sender, EventArgs e)
		{
			m_currentDevice = Settings.RenderDevice;
			DeviceSection_EngineSettingsReceivedHandler(this, new EngineSettingsReceivedArgs(Settings));

			Application.Instance.AsyncInvoke(() =>
			{

			}
			);
		}

		private void ActivateDevicePage(IDocumentSettings vud)
		{
			var rd = ActiveDevice(vud);
			if (rd.IsCuda || rd.IsMultiCuda) m_tc.SelectedPage = m_tabpage_cuda;
			else if (rd.IsOptix || rd.IsMultiOptix) m_tc.SelectedPage = m_tabpage_optix;
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
					SetupDeviceData(e.AllSettings, m_tabpage_cuda.Collection, ccl.DeviceType.Cuda);
					SetupDeviceData(e.AllSettings, m_tabpage_optix.Collection, ccl.DeviceType.Optix);
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

			m_tabpage_cpu = new GridDevicePage { Text = "CPU", Image = Eto.Drawing.Icon.FromResource("RhinoCyclesCore.Icons.CPU.ico").WithSize(16, 16)  , ToolTip = Localization.LocalizeString("Show all the render devices in the Cpu category.", 24)};
			m_tabpage_cuda = new GridDevicePage { Text = "CUDA", Image = Eto.Drawing.Icon.FromResource("RhinoCyclesCore.Icons.CUDA.ico").WithSize(16, 16), ToolTip = Localization.LocalizeString("Show all the render devices in the Cuda category.\nThese are the NVidia graphics and compute cards.", 25) };
			m_tabpage_optix = new GridDevicePage { Text = "Optix", Image = Eto.Drawing.Icon.FromResource("RhinoCyclesCore.Icons.CUDA.ico").WithSize(16, 16), ToolTip = Localization.LocalizeString("Show all the render devices in the Optix category.\nThese are the NVidia graphics and compute cards from Maxwell architecture and newer.", 41) };
			m_tc.Pages.Add(m_tabpage_cpu);
			m_tc.Pages.Add(m_tabpage_cuda);
			m_tc.Pages.Add(m_tabpage_optix);

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
			m_tabpage_cuda.SelectionChanged += DeviceSelectionChanged;
			m_tabpage_cuda.RegisterEventHandlers();
			m_tabpage_optix.SelectionChanged += DeviceSelectionChanged;
			m_tabpage_optix.RegisterEventHandlers();
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

		private void DeviceSelectionChanged(object sender, EventArgs e)
		{
			UnRegisterControlEvents();

			if (sender is GridDevicePage senderpage)
			{
				foreach (var page in m_tc.Pages)
				{
					if (page is GridDevicePage p && p != sender) p.ClearSelection();
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
			m_tabpage_cuda.SelectionChanged -= DeviceSelectionChanged;
			m_tabpage_cuda.UnregisterEventHandlers();
			m_tabpage_optix.SelectionChanged -= DeviceSelectionChanged;
			m_tabpage_optix.UnregisterEventHandlers();
			m_threadcount.ValueChanged -= M_threadcount_ValueChanged;
			m_btn_enablegpus.Click -= m_btn_enablegpus_Clicked;
		}
	}
}
