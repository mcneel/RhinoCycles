/**
Copyright 2014-2015 Robert McNeel and Associates

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
using ccl;

namespace RhinoCycles
{
	public class CyclesView
	{
		public Transform Transform { get; set; }
		public double LensLength { get; set; }
		public double Diagonal { get; set; }
		public double Vertical { get; set; }
		public double Horizontal { get; set; }
		public double ViewAspectRatio { get; set; }
		public CameraType Projection { get; set; }

		public int Width { get; set; }
		public int Height { get; set; }

		public ViewPlane Viewplane { get; set; }
		public bool TwoPoint { get; set; }
		public uint Crc { get; set; }
	}
}
