using MelonLoader;
using System.Reflection;

namespace AutoLightshow
{
    public static class Config
    {
        public const string Category = "AutoLightshow";

        public static bool enabled;
        public static bool pulseMode;
        public static bool enableStroboChains;
        public static bool enablePsychedelia;
        public static float intensity;
        public static float minBrightness;
        public static float maxBrightness;



        public static void RegisterConfig()
        {
            MelonPrefs.RegisterBool(Category, nameof(enabled), true, "Enables AutoLightshow.");
            MelonPrefs.RegisterBool(Category, nameof(pulseMode), false, "Pulses the lights, similar to how a sound visualizer works.");
            MelonPrefs.RegisterBool(Category, nameof(enableStroboChains), true, "Strobes the Lights on chains.");
            MelonPrefs.RegisterBool(Category, nameof(enablePsychedelia), true, "Allows the Lightshow to use Psychedelia.");
            MelonPrefs.RegisterFloat(Category, nameof(intensity), 1f, "Controls how intense the lightshow is. [0.1, 1, 0.1, 1]{P}");
            MelonPrefs.RegisterFloat(Category, nameof(minBrightness), 0f, "Minimum brightness allowed for the lightshow. [0, 0.5, 0.1, 0]{P}");
            MelonPrefs.RegisterFloat(Category, nameof(maxBrightness), 0.8f, "Maximum brightness allowed for the lightshow. [0.5, 1, 0.1, 0.8]{P}");


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
