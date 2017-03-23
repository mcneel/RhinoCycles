

using System;
using Rhino.Render;
/**
Copyright 2014-2017 Robert McNeel and Associates

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
using System.Collections.Generic;
using System.Linq;

namespace RhinoCyclesCore.Database
{
	public class FocalBlur
	{
		private static int _runningSerial;
		private readonly int _serial;
		public FocalBlur(RenderSettings rs)
		{
			_serial = _runningSerial++;
			UseFocalBlur = rs.FocalBlurMode == RenderSettings.FocalBlurModes.Manual;
			FocalDistance = (float)rs.FocalBlurDistance;
			FocalAperture = (float)rs.FocalBlurAperture;

			if (!UseFocalBlur)
			{
				FocalAperture = 0.0f;
				FocalDistance = 10.0f;
			}
		}

		public FocalBlur()
		{
			_serial = _runningSerial++;
			UseFocalBlur = false;
			FocalDistance = 10.0f;
			FocalAperture = 0.0f;
		}

		public bool UseFocalBlur { get; set; }
		public float FocalDistance { get; set; }
		public float FocalAperture { get; set; }

		public override bool Equals(object obj)
		{
			var fb = obj as FocalBlur;
			if (fb == null) return false;

			return fb.UseFocalBlur == UseFocalBlur &&
					Math.Abs(fb.FocalAperture - FocalAperture) < 0.000001 &&
					Math.Abs(fb.FocalDistance - FocalDistance) < 0.000001;
		}

		public override int GetHashCode()
		{
			return _serial;
		}
	}

	public class CameraDatabase
	{
		/// <summary>
		/// record view changes to push to cycles
		/// </summary>
		private readonly List<CyclesView> _cqViewChanges = new List<CyclesView>();

		/// <summary>
		/// Return true if ChangeQueue mechanism recorded viewport changes.
		/// </summary>
		/// <returns></returns>
		public bool HasChanges()
		{
			return _cqViewChanges.Any() || _focalBlurModified;
		}

		/// <summary>
		/// Clear view change queue
		/// </summary>
		public void ResetViewChangeQueue()
		{
			_cqViewChanges.Clear();
			_focalBlurModified = false;
		}

		/// <summary>
		/// Record view change
		/// </summary>
		/// <param name="t">view info</param>
		public void AddViewChange(CyclesView t)
		{
			_cqViewChanges.Add(t);
		}

		/// <summary>
		/// Get latest CyclesView recorded.
		/// </summary>
		/// <returns></returns>
		public CyclesView LatestView()
		{
			return _cqViewChanges.LastOrDefault();
		}

		public FocalBlur GetBlur()
		{
			return _focalBlur;
		}

		private FocalBlur _focalBlur = new FocalBlur();
		private bool _focalBlurModified;

		public bool HandleBlur(RenderSettings rs)
		{
			var fb = new FocalBlur(rs);
			var rc = false;

			if(!_focalBlur.Equals(fb))
			{
				_focalBlur = fb;
				_focalBlurModified = true;
				rc = true;
			}

			return rc;
		}
	}
}
