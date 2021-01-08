using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;

namespace AutoLightshow
{
    public static class Integrations
    {
        public static bool arenaLoaderFound = false;
        public static void LookForIntegrations()
        {
            foreach (MelonMod mod in MelonHandler.Mods)
            {
                if (mod.Assembly.GetName().Name == "ArenaLoader")
                {
                    var scoreVersion = new Version(mod.Info.Version);
                    var lastUnsupportedVersion = new Version("0.2.1");
                    var result = scoreVersion.CompareTo(lastUnsupportedVersion);
                    if (result > 0)
                    {
                        arenaLoaderFound = true;
                        MelonLogger.Log("Arena Loader found");

                    }
                    else
                    {
                        MelonLogger.Log("Arena Loader version not compatible. Update Arena Loader to use it with Authorable modifiers.");
                        arenaLoaderFound = false;
                    }
                }               
            }
        }
    }
}
