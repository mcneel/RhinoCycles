

using Rhino.Render;
/**
Copyright 2014-2016 Robert McNeel and Associates

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
		public FocalBlur(RenderSettings rs)
		{
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
					fb.FocalAperture == FocalAperture &&
					fb.FocalDistance == FocalDistance;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}

	public class CameraDatabase
	{
		/// <summary>
		/// record view changes to push to cycles
		/// </summary>
		private readonly List<CyclesView> m_cq_view_changes = new List<CyclesView>();

		/// <summary>
		/// Return true if ChangeQueue mechanism recorded viewport changes.
		/// </summary>
		/// <returns></returns>
		public bool HasChanges()
		{
			return m_cq_view_changes.Any() || m_fb_modified;
		}

		/// <summary>
		/// Clear view change queue
		/// </summary>
		public void ResetViewChangeQueue()
		{
			m_cq_view_changes.Clear();
			m_fb_modified = false;
		}

		/// <summary>
		/// Record view change
		/// </summary>
		/// <param name="t">view info</param>
		public void AddViewChange(CyclesView t)
		{
			m_cq_view_changes.Add(t);
		}

		/// <summary>
		/// Get latest CyclesView recorded.
		/// </summary>
		/// <returns></returns>
		public CyclesView LatestView()
		{
			return m_cq_view_changes.LastOrDefault();
		}

		public FocalBlur GetBlur()
		{
			return m_fb;
		}

		private FocalBlur m_fb = new FocalBlur();
		private bool m_fb_modified = false;

		public void HandleBlur(RenderSettings rs)
		{
			var fb = new FocalBlur(rs);

			if(m_fb!=fb)
			{
				m_fb = fb;
				m_fb_modified = true;
			}
		}
	}
}
