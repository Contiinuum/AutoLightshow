using MelonLoader;
using System.Reflection;

namespace AutoLightshow
{
    public static class Config
    {
        public const string Category = "AutoLightshow";

        public static bool enabled;
        public static bool intenseLights;
        public static bool enableStroboChains;
        public static float minAllowedBrightness;
        public static float maxAllowedBrightness;



        public static void RegisterConfig()
        {
            MelonPrefs.RegisterBool(Category, nameof(enabled), true, "Enables AutoLightshow.");
            MelonPrefs.RegisterBool(Category, nameof(intenseLights), true, "Makes the Lightshow more intense.");
            MelonPrefs.RegisterBool(Category, nameof(enableStroboChains), true, "Strobes the Lights on chains");
            MelonPrefs.RegisterFloat(Category, nameof(minAllowedBrightness), 0f, "Minimum brightness allowed for the lightshow. [0, 0.5, 0.1, 0]{P}");
            MelonPrefs.RegisterFloat(Category, nameof(maxAllowedBrightness), 0.8f, "Maximum brightness allowed for the lightshow. [0.5, 1, 0.1, 0.8]{P}");


            OnModSettingsApplied();
        }

        public static void OnModSettingsApplied()
        {
            foreach (var fieldInfo in typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (fieldInfo.FieldType == typeof(bool)) fieldInfo.SetValue(null, MelonPrefs.GetBool(Category, fieldInfo.Name));
                else if (fieldInfo.FieldType == typeof(float)) fieldInfo.SetValue(null, MelonPrefs.GetFloat(Category, fieldInfo.Name));
            }
        }
    }
}
