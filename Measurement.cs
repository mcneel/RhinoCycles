namespace RhinoCycles
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
