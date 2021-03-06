using HarmonyLib;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using System;

namespace AutoLightshow
{
    internal static class Hooks
    {
#pragma warning disable IDE0060, IDE0051

        [HarmonyPatch(typeof(OptionsMenu), "ShowPage", new Type[] { typeof(OptionsMenu.Page)})]
        private static class PatchShowPage
        {
            private static void Postfix(OptionsMenu __instance, OptionsMenu.Page page)
            {
                if(page == OptionsMenu.Page.Main)
                    AutoLightshowMod.SetUserBrightness(RenderSettings.skybox.GetFloat("_Exposure"), RenderSettings.reflectionIntensity);
            }
        }

        [HarmonyPatch(typeof(SongCues), "LoadCues")]
        private static class PatchLoadCues
        {
            private static void Postfix(SongCues __instance)
            {
                if (KataConfig.I.practiceMode) return;
                if (!Config.enabled) return;
                if (TutorialFlow.I.mIsTutorialSong) return;
                AutoLightshowMod.StartLightshow();
            }
        }

        [HarmonyPatch(typeof(EnvironmentLoader), "SwitchEnvironment")]
        private static class PatchSwitchEnvironment
        {
            private static void Postfix(EnvironmentLoader __instance)
            {
                if (!Config.enabled) return;
                
                MelonCoroutines.Start(AutoLightshowMod.ISetDefaultArenaBrightness());
                
            }
        }

        [HarmonyPatch(typeof(InGameUI), "Restart")]
        private static class PatchRestart
        {
            private static void Prefix(InGameUI __instance)
            {
                if (KataConfig.I.practiceMode) return;
                AutoLightshowMod.Reset(); //true
            }
        }

        [HarmonyPatch(typeof(InGameUI), "ReturnToSongList")]
        private static class PatchReturnToSongList
        {
            private static void Postfix(InGameUI __instance)
            {
                if (KataConfig.I.practiceMode) return;
                AutoLightshowMod.Reset();
            }
        }

        [HarmonyPatch(typeof(InGameUI), "GoToFailedPage")]
        private static class PatchGoToFailedPage
        {
            private static void Postfix(InGameUI __instance)
            {
                if (KataConfig.I.practiceMode) return;
                AutoLightshowMod.Reset();
            }
        }

        [HarmonyPatch(typeof(InGameUI), "GoToResultsPage")]
        private static class PatchGoToResultsPage
        {
            private static void Postfix(InGameUI __instance)
            {
                if (KataConfig.I.practiceMode) return;
                AutoLightshowMod.Reset();
            }
        }

        [HarmonyPatch(typeof(LaunchPanel), "Back")]
        private static class PatchLaunchPanelBack
        {
            private static void Postfix(LaunchPanel __instance)
            {
                AutoLightshowMod.Reset();
            }
        }
        [HarmonyPatch(typeof(MenuState), "SetState", typeof(MenuState.State))]
        private static class PatchSetState
        {
            private static void PostFix(MenuState __instance, MenuState.State state)
            {
                if (state == MenuState.State.SongPage)
                    AutoLightshowMod.Reset();
            }
        }
    }
}