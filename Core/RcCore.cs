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

using ccl;
using Rhino;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace RhinoCyclesCore.Core
{
	public sealed class RcCore
	{
		#region helper functions to get relative path between two paths
		public static string GetRelativePath(string fromPath, string toPath)
		{
			bool hit = false;
			int l = 1;
			// find length of common path.
			if (toPath.StartsWith(fromPath))
			{
				hit = true;
				l = fromPath.Length + 1;
			} else {
				while (!hit)
				{
					if (l > fromPath.Length) break;

					string ss = fromPath.Substring(0, l);
					if (!toPath.StartsWith(ss))
					{
						hit = true;
						break;
					}
					l++;
				}
			}

			if (!hit) throw new ArgumentException("Paths must have common start");

			// we found a hit, now determine the relative jump
			string remainder = fromPath.Substring(l - 1);
			string toremainder = toPath.Substring(l - 1);
			var sp = remainder.Split(System.IO.Path.DirectorySeparatorChar);
			List<string> relp = new List<string>(sp);
			relp = relp.FindAll(x => x.Length > 0);
			for(int i = 0; i < relp.Count; i++)
			{
				relp[i] = "..";
			}

			// add the path of the remainder in toPath
			relp.Add(toremainder);

			// combine into final relative path.
			var relpstr = System.IO.Path.Combine(relp.ToArray());
			// add a dot if string starts with a directory separator
			if (relpstr.StartsWith(System.IO.Path.DirectorySeparatorChar.ToString())) relpstr = "." + relpstr;

			return relpstr;
		}

		#endregion


		public void InitializeResourceManager()
		{
			Properties.Resources.Culture = CultureInfo.InvariantCulture;
		}

		public void TriggerInitialisationCompleted(object sender)
		{
			InitialisationCompleted?.Invoke(sender, EventArgs.Empty);
		}

		/// <summary>
		/// Event signalling that CCSycles initialisation has been completed.
		/// </summary>
		public event EventHandler InitialisationCompleted;
		/// <summary>
		/// Flag to keep track of CSycles initialisation
		/// </summary>
		public bool Initialised { get; set; }

		public bool AppInitialised { get; set; }

		/// <summary>
		/// Get the path used to look up .cubins (absolute)
		/// </summary>
		public string KernelPath { get; set; }

		/// <summary>
		/// Get the path where runtime created data like compiled kernels and BVH caches are stored.
		/// </summary>
		public string DataUserPath { get; set; }

		/// <summary>
		/// Get the path used to look up .cubins (relative)
		/// </summary>
		public string KernelPathRelative { get; set; }

		public string PluginPath { get; set; }

		public string AppPath { get; set; }

		public ApplicationAndDocumentSettings AllSettings { get; }

		private RcCore() {
			AppInitialised = false;
			if(AllSettings == null)
				AllSettings = new ApplicationAndDocumentSettings();
		}

		public static RcCore It { get; } = new RcCore();

		public static void OutputDebugString(string msg)
		{
#if OUTPUTDEBUGSTRINGS
			RhinoApp.OutputDebugString(msg);
#endif
		}

		ConcurrentDictionary<uint, Session> sessions = new ConcurrentDictionary<uint, Session>();
		/// <summary>
		/// Shut down Cycles on all levels. Wait for all active session to complete.
		/// </summary>
		public void Shutdown() {
			int count;
			int timer = 0;
			while((count = sessions.Count) > 0 ) {
				if(timer%50==0)
					RhinoApp.OutputDebugString($"Number of sessions we wait for {count}\n");
				Thread.Sleep(10);
				timer++;
			}
			RhinoApp.OutputDebugString($"All sessions cleaned up\n");
			CSycles.shutdown();
		}

		private readonly object sessionsLock = new object();
		/// <summary>
		/// Create a ccl.Session and register with central system so we can later ensure
		/// we wait on all sessions to fully complete before shutting down CSycles.
		///
		/// Sessions created with this function have to be released/destroyed using
		/// the function ReleaseSession
		/// </summary>
		/// <param name="client"></param>
		/// <param name="sessionParameters"></param>
		/// <returns></returns>
		public Session CreateSession(Client client, SessionParameters sessionParameters) {
			lock (sessionsLock)
			{
				var session = new Session(client, sessionParameters);

				if (sessions.ContainsKey(session.Id))
				{
					RhinoApp.OutputDebugString($"Session {session.Id} already exists\n");
				}

				sessions[session.Id] = session;
				RhinoApp.OutputDebugString($"Created session {session.Id}.\n");

				return session;
			}
		}

		/// <summary>
		/// Release and destroy session created by CreateSession.
		/// </summary>
		/// <param name="session"></param>
		public void ReleaseSession(Session session) {
			lock (sessionsLock)
			{
				if (sessions.ContainsKey(session.Id))
				{
					RhinoApp.OutputDebugString($"Releasing session {session.Id}.\n");
					Session tempSession;
					while (!sessions.TryRemove(session.Id, out tempSession))
					{
						Thread.Sleep(10);
					}
					if (tempSession != null)
					{
						tempSession.EndRun();
						tempSession.Destroy();
					}
					RhinoApp.OutputDebugString($"Session {session.Id} released.\n");

				}
			}
		}
	}
}
