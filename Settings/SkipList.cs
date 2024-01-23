/**
Copyright 2014-2024 Robert McNeel and Associates

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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

	/// <summary>
	/// Collection of GpuDevice instances
	/// </summary>
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


	/// <summary>
	/// Representation of a GPU device name that needs to be found, and zero or
	/// more substrings to test against in case the DeviceName is found.
	/// </summary>
	public class GpuDevice
	{
		/// <summary>
		/// The shortest possible part of device name to initially test against,
		/// for instance "Intel"
		/// </summary>
		public string DeviceName;

		/// <summary>
		/// Collection of substrings. If more than one is in the collection at
		/// least one must be found
		/// </summary>
		public SubStrings SubStrings;
		public GpuDevice() { }
		/// <summary>
		/// Construct a GpuDevice given the name and a list of zero or more strings
		/// that will function as sub strings
		///
		/// To compare against the string "Intel HD Graphics 530" one would create
		/// a GpuDevice with name "Intel" and list of substrings containing one
		/// entry "530".
		///
		/// If more Intel devices should be found, say "Intel HD Graphics 630" one
		/// would create a GpuDevice with name "Intel" and a list of substrings
		/// containing the strings "530" and "630".
		/// </summary>
		/// <param name="name">Shortest part of GPU device to for initial test</param>
		/// <param name="subStrings">List of zero or more strings for secondary test</param>
		public GpuDevice(string name, List<string> subStrings)
		{
			DeviceName = name;
			SubStrings = new SubStrings();
			foreach(string substr in subStrings) {
				SubStrings.Add(new SubString(substr));
			}
		}
	}


	/// <summary>
	/// SkipList helps maintaining a list of device and substring collections that
	/// can be used to determine whether a given string for a device should trigger
	/// skipping of OpenCL initialization.
	/// </summary>
	public class SkipList
	{
		/// <summary>
		/// All device strings and substrings collection.
		/// </summary>
		GpuDevices skipDevice = new GpuDevices();

		private Type GpuDevicesType = typeof(GpuDevices);

		private void InitializeSkipList() {
				skipDevice.Clear();
				skipDevice.Add(new GpuDevice("Intel", new List<string>() { "530" }));
				WriteSkipList();
		}

		private void ReadSkipList() {
			try {
				using(FileStream fs = new FileStream(DbPath, FileMode.Open)) {
					GpuDevices skipList = xmlMarshal.Deserialize(fs) as GpuDevices;
					skipDevice = skipList;
				}
			} catch (Exception) {
				InitializeSkipList();
			}
		}

		private void WriteSkipList() {
			var directory = Path.GetDirectoryName(DbPath);
			if(!Directory.Exists(directory)) {
				Directory.CreateDirectory(directory);
			}
			try {
				using(TextWriter tw = new StreamWriter(DbPath)) {
					xmlMarshal.Serialize(tw, skipDevice);
				}
			} catch (Exception) {}
		}

		public SkipList() {
			xmlMarshal = XmlSerializer.FromTypes(new List<Type>(){GpuDevicesType}.ToArray()).First();
		}


		/// <summary>
		/// Set the path to the folder that will contain the skip list serialization.
		/// </summary>
		/// <param name="name">Path to the folder for skip list</param>
		public void SetDbPath(string name) {
			DbPath = name;
		}

		private string DbPath;

		private XmlSerializer xmlMarshal;

		/// <summary>
		/// Construct a new SkipList instance using the given path name of folder to
		/// save the skip list to.
		/// </summary>
		/// <param name="path"></param>
		public SkipList(string path) {
			xmlMarshal = XmlSerializer.FromTypes(new List<Type>(){GpuDevicesType}.ToArray()).First();
			SetDbPath(Path.Combine(path, "skiplist.xml"));
			if (File.Exists(DbPath)) {
				ReadSkipList();
			} else {
				InitializeSkipList();
			}
		}

		/// <summary>
		/// Given the string see if any gpu device name is contained within and
		/// if there are substrings for the device name if any of them exist.
		///
		/// </summary>
		/// <param name="entry">Full device name to test with, for instance "Intel HD Graphics 530"</param>
		/// <returns>true if the entry satisfies any of the entries</returns>
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
