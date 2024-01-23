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
namespace RhinoCyclesCore
{
	/// <summary>
	/// Very simple rolling average measuring. This approximates
	/// average over the last thousand measurements. Use when there
	/// is no need for exact average.
	/// </summary>
	public class Measurement
	{
		public double Alpha { get; set; }
		public double Max { get; set; }
		public double Min { get; set; }
		public double Avg { get; set; }
		public ulong Count { get; set; }

		/// <summary>
		/// Constructor
		/// </summary>
		public Measurement()
		{
			Alpha = 1.0/1000.0;
			Reset();
		}

		/// <summary>
		/// Reset the measurements.
		/// </summary>
		public void Reset()
		{
			Max = double.MinValue;
			Min = double.MaxValue;
			Avg = 0.0;
			Count = 0;
		}

		/// <summary>
		/// Add new value to be averaged in.
		/// Might update Min and/or Max.
		/// Increases Count
		/// </summary>
		/// <param name="value">The value to be averaged in.</param>
		public void Add(double value)
		{
			if (value < Min) Min = value;
			if (value > Max) Max = value;

			Avg = Alpha*value + (1.0 - Alpha)*Avg;
			Count++;
		}
	}
}
