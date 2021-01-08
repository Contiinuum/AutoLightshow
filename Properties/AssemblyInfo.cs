using System.Resources;
using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;
using AutoLightshow;

[assembly: AssemblyTitle(AutoLightshowMod.BuildInfo.Name)]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany(AutoLightshowMod.BuildInfo.Company)]
[assembly: AssemblyProduct(AutoLightshowMod.BuildInfo.Name)]
[assembly: AssemblyCopyright("Created by " + AutoLightshowMod.BuildInfo.Author)]
[assembly: AssemblyTrademark(AutoLightshowMod.BuildInfo.Company)]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
//[assembly: Guid("")]
[assembly: AssemblyVersion(AutoLightshowMod.BuildInfo.Version)]
[assembly: AssemblyFileVersion(AutoLightshowMod.BuildInfo.Version)]
[assembly: NeutralResourcesLanguage("en")]
[assembly: MelonInfo(typeof(AutoLightshowMod), AutoLightshowMod.BuildInfo.Name, AutoLightshowMod.BuildInfo.Version, AutoLightshowMod.BuildInfo.Author, AutoLightshowMod.BuildInfo.DownloadLink)]
[assembly: MelonOptionalDependencies("ArenaLoader")]


// Create and Setup a MelonModGame to mark a Mod as Universal or Compatible with specific Games.
// If no MelonModGameAttribute is found or any of the Values for any MelonModGame on the Mod is null or empty it will be assumed the Mod is Universal.
// Values for MelonModGame can be found in the Game's app.info file or printed at the top of every log directly beneath the Unity version.
[assembly: MelonGame(null, null)]