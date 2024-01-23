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

using Rhino.PlugIns;
using System.Reflection;
using System.Runtime.InteropServices;

// Plug-In title and Guid are extracted from the following two attributes
[assembly: AssemblyTitle("RhinoCycles")]
[assembly: Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97")]

// Plug-in Description Attributes - all of these are optional
[assembly: PlugInDescription(DescriptionType.Address, "-")]
[assembly: PlugInDescription(DescriptionType.Country, "Finland")]
[assembly: PlugInDescription(DescriptionType.Email, "nathan@mcneel.com")]
[assembly: PlugInDescription(DescriptionType.Phone, "-")]
[assembly: PlugInDescription(DescriptionType.Fax, "-")]
[assembly: PlugInDescription(DescriptionType.Organization, "McNeel")]
[assembly: PlugInDescription(DescriptionType.UpdateUrl, "http://www.rhino3d.com/download")]
[assembly: PlugInDescription(DescriptionType.WebSite, "http://www.rhino3d.com")]

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyDescription("RhinoCycles")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("RhinoCycles")]
[assembly: AssemblyCopyright("Copyright ©  2014-2024")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
