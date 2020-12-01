using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RhinoCyclesCore.Settings
{
	public class SubStrings : ICollection
	{
		public string CollectionName;
		private ArrayList empArray = new ArrayList();

		public SubString this[int index]
		{
			get { return (SubString)empArray[index]; }
		}

		public void CopyTo(Array a, int index)
		{
			empArray.CopyTo(a, index);
		}
		public int Count
		{
			get { return empArray.Count; }
		}
		public object SyncRoot
		{
			get { return this; }
		}
		public bool IsSynchronized
		{
			get { return false; }
		}
		public IEnumerator GetEnumerator()
		{
			return empArray.GetEnumerator();
		}

		public void Add(SubString newSubString)
		{
			empArray.Add(newSubString);
		}
	}

	public class SubString
	{
		public string Value;
		public SubString() { }
		public SubString(string name)
		{
			Value = name;
		}
	}
	public class GpuDevices : ICollection
	{
		public string CollectionName;
		private ArrayList empArray = new ArrayList();

		public GpuDevice this[int index]
		{
			get { return (GpuDevice)empArray[index]; }
		}

		public void CopyTo(Array a, int index)
		{
			empArray.CopyTo(a, index);
		}
		public int Count
		{
			get { return empArray.Count; }
		}
		public object SyncRoot
		{
			get { return this; }
		}
		public bool IsSynchronized
		{
			get { return false; }
		}
		public IEnumerator GetEnumerator()
		{
			return empArray.GetEnumerator();
		}

		public void Add(GpuDevice newGpuDevice)
		{
			empArray.Add(newGpuDevice);
		}

		public void Clear()
		{
			empArray.Clear();
		}
	}

	public class GpuDevice
	{
		public string DeviceName;
		public SubStrings SubStrings;
		public GpuDevice() { }
		public GpuDevice(string name, List<string> subStrings)
		{
			DeviceName = name;
			SubStrings = new SubStrings();
			foreach(string substr in subStrings) {
				SubStrings.Add(new SubString(substr));
			}
		}
	}

	public class SkipList
	{
		GpuDevices skipDevice = new GpuDevices();

		private void InitializeSkipList() {
				skipDevice.Clear();
				skipDevice.Add(new GpuDevice("Intel", new List<string>() { "530" }));
				WriteSkipList();
		}

		private void ReadSkipList() {
			try {
				XmlSerializer xml = new XmlSerializer(typeof(GpuDevices));
				using(FileStream fs = new FileStream(DbPath, FileMode.Open)) {
					GpuDevices skipList = xml.Deserialize(fs) as GpuDevices;
					skipDevice = skipList;
					fs.Close();
				}
			} catch (Exception) {
				InitializeSkipList();
			}
		}

		private void WriteSkipList() {
			XmlSerializer xml = new XmlSerializer(typeof(GpuDevices));
			var directory = Path.GetDirectoryName(DbPath);
			if(!Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}
			try {
				using(TextWriter tw = new StreamWriter(DbPath)) {
					xml.Serialize(tw, skipDevice);
					tw.Close();
				}
			} catch (Exception) {}
		}

		public SkipList() {

		}

		public void SetDbPath(string name) {
			DbPath = name;
		}

		private string DbPath;

		public SkipList(string path) {
			SetDbPath(Path.Combine(path, "skiplist.xml"));
			if (File.Exists(DbPath)) {
				ReadSkipList();
			} else {
				InitializeSkipList();
			}
		}

		public bool Hit(string entry) {
			foreach(GpuDevice key in skipDevice) {
				if(entry.Contains(key.DeviceName)) {
					if (key.SubStrings.Count == 0)
						return true;
					foreach(SubString substr in key.SubStrings) {
						if (entry.Contains(substr.Value))
							return true;
					}
				}
			}
			return false;
		}


	}
}
