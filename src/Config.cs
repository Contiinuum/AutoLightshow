using MelonLoader;
using System.Reflection;

namespace AutoLightshow
{
    public static class Config
    {
        public const string Category = "AutoLightshow";

        public static bool enabled;
        public static bool alternativeLightshow;
        public static bool enableStroboChains;
        public static bool enablePsychedelia;
        public static float intensity;
        public static float minBrightness;
        public static float maxBrightness;



        public static void RegisterConfig()
        {
            MelonPreferences.CreateEntry(Category, nameof(enabled), true, "Enables AutoLightshow.");
            MelonPreferences.CreateEntry(Category, nameof(alternativeLightshow), false, "Uses an alternative lightshow");
            MelonPreferences.CreateEntry(Category, nameof(enableStroboChains), true, "Strobes the Lights on chains.");
            MelonPreferences.CreateEntry(Category, nameof(enablePsychedelia), true, "Allows the Lightshow to use Psychedelia.");
            MelonPreferences.CreateEntry(Category, nameof(intensity), 1f, "Controls how intense the lightshow is. [0.1, 1, 0.1, 1]{P}");
            MelonPreferences.CreateEntry(Category, nameof(minBrightness), 0f, "Minimum brightness allowed for the lightshow. [0, 0.5, 0.1, 0]{P}");
            MelonPreferences.CreateEntry(Category, nameof(maxBrightness), 0.8f, "Maximum brightness allowed for the lightshow. [0.5, 1, 0.1, 0.8]{P}");


            OnPreferencesSaved();
        }

        public static void OnPreferencesSaved()
        {
            foreach (var fieldInfo in typeof(Config).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (fieldInfo.FieldType == typeof(bool)) fieldInfo.SetValue(null, MelonPreferences.GetEntryValue<bool>(Category, fieldInfo.Name));
                else if (fieldInfo.FieldType == typeof(float)) fieldInfo.SetValue(null, MelonPreferences.GetEntryValue<float>(Category, fieldInfo.Name));
            }
        }
    }
}
