using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Harmony;
using ArenaLoader;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AutoLightshow
{
    public class AutoLightshowMod : MelonMod
    {
        public static bool isEnabled => Config.enabled;

        private const float intensityNormExp = .5f;
        private const float intensityWeight = 1.5f;
        private const float threshholdIncrement = .225f;

        private static object faderToken;
        private static object fadeToBlackToken;
        private static float fadeToBlackStartTick;
        private static float fadeToBlackEndTick;
        private static float fadeToBlackExposure;
        private static float fadeToBlackReflection;
        private static object lightshowToken;
        private static object psyToken;
        private static bool active = false;

        private static float defaultPsychadeliaPhaseSeconds = 14.28f;
        private static float psychadeliaTimer = 0.0f;
        private static float lastPsyTimer = 0f;

        private static float userArenaBrightness = .5f;
        private static float userArenaReflection = 1f;

        private static float defaultArenaBrightness = .5f;
       
        private static float maxBrightness;
        private const float fadeOutTime = 360f;
        private static float mapIntensity;

        private static int startIndex = 0;
        private static List<BrightnessEvent> brightnessEvents = new List<BrightnessEvent>();

        public static class BuildInfo
        {
            public const string Name = "AutoLightshow";  // Name of the Mod.  (MUST BE SET)
            public const string Author = "Continuum"; // Author of the Mod.  (Set as null if none)
            public const string Company = null; // Company that made the Mod.  (Set as null if none)
            public const string Version = "1.5.0"; // Version of the Mod.  (MUST BE SET)
            public const string DownloadLink = null; // Download Link for the Mod.  (Set as null if none)
        }
        
        public override void OnApplicationStart()
        {
            HarmonyInstance instance = HarmonyInstance.Create("AutoLightshow");
            Integrations.LookForIntegrations();
            Config.RegisterConfig();
        }

        public override void OnModSettingsApplied()
        {
            Config.OnModSettingsApplied();
        }

        public static void EnableMod(bool enable)
        {
            Config.enabled = enable;
        }
        private static float CalculateIntensity(float startTick, float endTick, List<SongCues.Cue> cues)
        {
            float intensity = 0f;
            bool indexSet = false;
            for(int i = startIndex; i < cues.Count; i++)
            {
                SongCues.Cue cue = SongCues.I.mCues.cues[i];
                if (cue.tick >= endTick) break;
                if (cue.tick >= startTick && cue.tick < endTick)
                {
                    intensity += GetTargetAmount((Hitsound)cue.velocity, cue.behavior);
                    if (!indexSet)
                    {
                        indexSet = true;
                        startIndex = i;
                    }
                }
                   
            }
           
            intensity /= AudioDriver.TickSpanToMs(SongDataHolder.I.songData, startTick, endTick);
           
            intensity *= 1000f;
            return intensity;
        }

        private static async void PrepareLightshow()
        {
            float oldExposure = RenderSettings.skybox.GetFloat("_Exposure");
            brightnessEvents.Clear();
            ArenaLoaderMod.CurrentSkyboxExposure = oldExposure;
            List<SongCues.Cue> cues = SongCues.I.mCues.cues.ToList();
            for (int i = cues.Count - 1; i >= 0; i--)
            {
                if (cues[i].behavior == Target.TargetBehavior.Dodge) cues.RemoveAt(i);
                else if (!Config.enableStroboChains && cues[i].behavior == Target.TargetBehavior.Chain) cues.RemoveAt(i);               
            }
            await PrepareAsync(cues);
            lightshowToken = MelonCoroutines.Start(BetterLightshow());
        }

        private static async Task PrepareAsync(List<SongCues.Cue> cues)
        {
            float offset = cues[0].tick % 120;
            float span = 7680f;
            float sectionStart = cues[0].tick;
            float sectionEnd = sectionStart + span;
            float previousSectionBrightness = maxBrightness * .5f;
            ExposureState expState = ExposureState.Dark;
            
            List<Section> sections = new List<Section>();
            int numSections = Mathf.FloorToInt(cues.Last().tick / span) + 1;
            startIndex = 0;
            for(int i = 0; i < numSections; i++)
            {
                CalculateSections(cues, sections, sectionStart, sectionEnd, threshholdIncrement);
                sectionStart = sectionEnd;
                sectionEnd += span;
            }
            startIndex = 0;
            sections.Sort((section1, section2) => section1.start.CompareTo(section2.start));
            foreach (Section section in sections)
            {
                List<SongCues.Cue> sectionCues = new List<SongCues.Cue>();

                float sectionBrightness = 0f;
                float sectionTargetBrightness = GetSectionTargetBrightness(expState, section.intensity);
                foreach (SongCues.Cue cue in cues)
                {
                    if (cue.tick > section.end) break;
                    if (cue.tick >= section.start && cue.tick < section.end)
                    {
                        sectionCues.Add(cue);
                        sectionBrightness += GetTargetAmount((Hitsound)cue.velocity, cue.behavior);
                    }
                }
                if (sectionBrightness != 0f && sectionCues.Count > 0f)
                {
                    sectionCues.Sort((cue1, cue2) => cue1.tick.CompareTo(cue2.tick));

                    foreach (SongCues.Cue cue in sectionCues)
                    {
                        if (cue.nextCue is null) break;
                        brightnessEvents.Add(new BrightnessEvent(((GetTargetAmount((Hitsound)cue.velocity, cue.behavior) / sectionBrightness) * (sectionTargetBrightness - previousSectionBrightness)), cue.tick, cue.nextCue.tick, section.intensity));
                    }
                    expState = expState == ExposureState.Light ? ExposureState.Dark : ExposureState.Light;
                }
                previousSectionBrightness = sectionTargetBrightness;
            }
            brightnessEvents.Sort((event1, event2) => event1.startTick.CompareTo(event2.startTick));
            await Task.CompletedTask;
        }

        private static void CalculateSections(List<SongCues.Cue> cues, List<Section> sections, float start, float end, float threshhold, float startIndex = 0)
        {
            float intensity = CalculateIntensity(start, end, cues);          
            intensity = intensity / Mathf.Pow(mapIntensity, intensityNormExp);
            intensity = (float)Math.Tanh(intensityWeight * intensity);
            float span = end - start;
            if (span > 480f)
            {
                if(threshhold >= (float)Math.Tanh(intensityWeight * intensity))
                {
                    sections.Add(new Section(start, end, intensity));
                    return;
                }
                else
                {
                    threshhold += threshholdIncrement;
                    CalculateSections(cues, sections, start, end - span / 2, threshhold, startIndex);
                    CalculateSections(cues, sections, end - span / 2, end, threshhold, startIndex);
                }                
            }
            else
            {
                sections.Add(new Section(start, end, intensity));
            }
        }

        private static float GetSectionTargetBrightness(ExposureState state, float intensity)
        {
            float sign = state == ExposureState.Light ? -1 : 1;
            return (float)(.5f + sign * Math.Tanh(intensityWeight * intensity) / 2) * maxBrightness;
        }

        public static void StartLightshow()
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            StopLightshow();
            maxBrightness = defaultArenaBrightness * Config.maxBrightness;
            mapIntensity = CalculateIntensity(SongCues.I.mCues.cues.First().tick, SongCues.I.mCues.cues.Last().tick, SongCues.I.mCues.cues.ToList());
            active = true;
            if (!Config.pulseMode) faderToken = MelonCoroutines.Start(Fade(960f, maxBrightness * .5f));
            Task.Run(() => PrepareLightshow());           
            //lightshowToken = MelonCoroutines.Start(BetterLightshow());

            if (Config.pulseMode)
            {       
                
                fadeToBlackStartTick = AudioDriver.I.mCachedTick;
                fadeToBlackEndTick = fadeToBlackStartTick + (fadeOutTime / mapIntensity);
                fadeToBlackExposure = RenderSettings.skybox.GetFloat("_Exposure");
                fadeToBlackReflection = RenderSettings.reflectionIntensity;
                fadeToBlackToken = MelonCoroutines.Start(FadeToBlack());
            }
            
        }

        private static IEnumerator BetterLightshow()
        {
            
            List<SongCues.Cue> cues = SongCues.I.mCues.cues.ToList();
            while (active)
            {
                if (brightnessEvents.Count == 0) yield break;
                if(brightnessEvents[0].startTick <= AudioDriver.I.mCachedTick)
                {
                    if (!Config.pulseMode)
                    {
                        MelonCoroutines.Stop(faderToken);
                        faderToken = MelonCoroutines.Start(BetterFade(brightnessEvents[0].endTick, brightnessEvents[0].brightness));
                    }
                    else
                    {
                        fadeToBlackStartTick = brightnessEvents[0].startTick;
                        fadeToBlackEndTick = fadeToBlackStartTick + (fadeOutTime / mapIntensity);
                        float curr = RenderSettings.skybox.GetFloat("_Exposure") + brightnessEvents[0].brightness;
                        float newAmount = curr > maxBrightness ? maxBrightness : curr;
                        fadeToBlackExposure = newAmount;
                        fadeToBlackReflection = newAmount;
                    }
                    brightnessEvents.RemoveAt(0);
                }

                if (cues.Count == 0) yield break;
                SongCues.Cue cue = cues[0];
                if (cue.nextCue is null) yield break;
                if (cue.tick <= AudioDriver.I.mCachedTick)
                {
                    HandlePsychedelia(cue);
                    if(Config.pulseMode) HandlePulse(cue);
                    cues.RemoveAt(0);
                }
                
                yield return new WaitForSecondsRealtime(.01f);
            }
        }

        private static void HandlePsychedelia(SongCues.Cue cue)
        {
            if (Config.enablePsychedelia)
            {
                if (cue.behavior == Target.TargetBehavior.Melee || cue.behavior == Target.TargetBehavior.ChainStart || cue.behavior == Target.TargetBehavior.Hold)
                {

                    List<float> ticks = new List<float>();
                    if (cue.behavior == Target.TargetBehavior.ChainStart) LookForEndOfChain(cue, ticks);
                    else LookForLastBehavior(cue, new Target.TargetBehavior[] { Target.TargetBehavior.Melee }, ticks);

                    if (cue.behavior == Target.TargetBehavior.Hold && cue.tickLength >= 1920f)
                    {
                        MelonCoroutines.Stop(psyToken);
                        psyToken = MelonCoroutines.Start(DoPsychedelia(AudioDriver.I.mCachedTick + cue.tickLength - 960f));
                    }
                    else if (ticks[0] - cue.tick >= 1920f && cue.behavior == Target.TargetBehavior.Melee)
                    {
                        MelonCoroutines.Stop(psyToken);
                        psyToken = MelonCoroutines.Start(DoPsychedelia(ticks[0] - 960f));
                    }
                }
            }
        }

        private static void HandlePulse(SongCues.Cue cue)
        {
            float amount = GetTargetAmount((Hitsound)cue.velocity, cue.behavior) * 2f;
            fadeToBlackStartTick = AudioDriver.I.mCachedTick;
            fadeToBlackEndTick = fadeToBlackStartTick + (fadeOutTime / mapIntensity);
            float curr = RenderSettings.skybox.GetFloat("_Exposure") + amount;
            float newAmount = curr > maxBrightness ? maxBrightness : curr;
            fadeToBlackExposure = newAmount;
            fadeToBlackReflection = newAmount;
        }

        public static IEnumerator ISetDefaultArenaBrightness()
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) yield break;
            while (EnvironmentLoader.I.IsSwitching())
            {
                yield return new WaitForSecondsRealtime(.2f);
            }
            defaultArenaBrightness = RenderSettings.skybox.GetFloat("_Exposure");
        }

        public static void SetUserBrightness(float brightness, float reflection)
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            userArenaBrightness = brightness;
            userArenaReflection = reflection;
        }

        /*private static IEnumerator DoLightshow()
        {
            float oldExposure = RenderSettings.skybox.GetFloat("_Exposure");
            ArenaLoaderMod.CurrentSkyboxExposure = oldExposure;
            List<SongCues.Cue> cues = SongCues.I.mCues.cues.ToList();
            for (int i = cues.Count - 1; i >= 0; i--)
            {              
                if (cues[i].behavior == Target.TargetBehavior.Dodge) cues.RemoveAt(i);
            }
               
            ExposureState state = ExposureState.Light;
            while (active)
            {
                if (cues.Count == 0) yield break;
                SongCues.Cue cue = cues[0];
                if (cue.nextCue is null) yield break;
                if (cue.tick <= AudioDriver.I.mCachedTick)
                {
                    if (cue.behavior != Target.TargetBehavior.Chain && cue.behavior != Target.TargetBehavior.ChainStart && cue.velocity == (int)Hitsound.ChainNode)
                    {
                        cues.RemoveAt(0);
                        continue;
                    }
                    if (cue.nextCue.tick == cue.tick)
                    {
                        if (cue.behavior == Target.TargetBehavior.Melee) cues.RemoveAt(1);
                        else
                        {
                            cues.RemoveAt(0);
                            continue;
                        }
                           
                    }
                    if (!Config.enableStroboChains)
                    {
                        if (cue.behavior == Target.TargetBehavior.Chain)
                        {
                            cues.RemoveAt(0);
                            continue;
                        }      
                    }
                    if(cue.behavior != Target.TargetBehavior.Melee && cue.velocity == (int)Hitsound.Melee)
                    {
                        cues.RemoveAt(0);
                        continue;
                    }

                    float diff = cue.nextCue.tick - cue.tick;
                    float end = cue.nextCue.tick - (diff / 4f);
                    bool isLight = state == ExposureState.Light;
                    float target = isLight ? Config.minBrightness : defaultArenaBrightness * Config.maxBrightness;
                    if (cue.behavior == Target.TargetBehavior.Chain) target = isLight ? (Config.maxBrightness / 100) * 35f : (Config.maxBrightness / 100) * 65f;
                    float threshhold = SongDataHolder.I.songData.GetTempo(AudioDriver.I.mCachedTick) >= 150 ? 2f : 1f;
                    if (cue.behavior == Target.TargetBehavior.Standard || cue.behavior == Target.TargetBehavior.Vertical || cue.behavior == Target.TargetBehavior.Horizontal)
                    {
                        if (cue.tick == cue.nextCue.tick)
                        {
                            if(cue.nextCue.behavior == cue.behavior)
                            {
                                if (cue.nextCue.velocity == (int)Hitsound.Percussion || cue.nextCue.velocity == (int)Hitsound.Snare)
                                {
                                    cues.RemoveAt(0);
                                    continue;
                                }
                                else
                                {
                                    cues.RemoveAt(1);
                                }
                            }
                            else
                            {
                                cues.RemoveAt(0);
                                continue;
                            }
                           
                        }
                        else if (diff <= 240f)
                        {
                            if(cue.nextCue.behavior == cue.behavior)
                            {
                                if (cue.nextCue.velocity != (int)Hitsound.Percussion && cue.nextCue.velocity != (int)Hitsound.Snare)
                                {
                                    cues.RemoveAt(1);
                                }
                            }                            
                        }

                    }
                    else if (cue.behavior == Target.TargetBehavior.Chain)
                    {
                        if(cue.velocity == (int)Hitsound.ChainNode)
                        {
                            
                            if (diff >= 240) //(diff >= 240 * threshhold
                            {
                                cues.RemoveAt(0);
                                continue;
                            }
                        }                       
                    }

                    if (Config.enablePsychedelia)
                    {
                        if (cue.behavior == Target.TargetBehavior.Melee || cue.behavior == Target.TargetBehavior.ChainStart || cue.behavior == Target.TargetBehavior.Hold)
                        {

                            //if (cue.behavior == Target.TargetBehavior.Melee) LookForLastBehavior(cue, Target.TargetBehavior.Melee, tick);
                            //else LookForLastBehavior(cue, Target.TargetBehavior.ChainStart, tick);
                            List<float> ticks = new List<float>();
                            if (cue.behavior == Target.TargetBehavior.ChainStart) LookForEndOfChain(cue, ticks);
                            //else LookForLastBehavior(cue, new Target.TargetBehavior[] { Target.TargetBehavior.Melee, Target.TargetBehavior.Hold }, ticks);
                            else LookForLastBehavior(cue, new Target.TargetBehavior[] { Target.TargetBehavior.Melee }, ticks);

                            if (cue.behavior == Target.TargetBehavior.Hold && cue.tickLength >= 1920f)
                            {
                                MelonCoroutines.Stop(psyToken);
                                psyToken = MelonCoroutines.Start(DoPsychedelia(AudioDriver.I.mCachedTick + cue.tickLength - 960f));
                            }
                            else if (ticks[0] - cue.tick >= 1920f && cue.behavior == Target.TargetBehavior.Melee)//&& !(cue.behavior == Target.TargetBehavior.Hold && ticks[0] > AudioDriver.I.mCachedTick + cue.tickLength)
                            {
                                MelonCoroutines.Stop(psyToken);
                                psyToken = MelonCoroutines.Start(DoPsychedelia(ticks[0] - 960f));
                            }

                        }
                    }
                    

                    bool smallFade = false;
                    MelonCoroutines.Stop(faderToken);
                    if (!Config.pulseMode)
                    {
                        if (cue.behavior != Target.TargetBehavior.Melee)
                        {
                            if ((cue.velocity == (int)Hitsound.Kick || cue.velocity == (int)Hitsound.ChainStart || cue.velocity == (int)Hitsound.Snare) && (cue.behavior == Target.TargetBehavior.Standard || cue.behavior == Target.TargetBehavior.Vertical || cue.behavior == Target.TargetBehavior.Horizontal))
                            {
                                float amnt = GetTargetAmount((Hitsound)cue.velocity, cue.behavior);
                                target = RenderSettings.skybox.GetFloat("_Exposure") + (isLight ? -amnt : amnt);

                                if (target > maxBrightness)
                                {
                                    target = maxBrightness;
                                }
                                else if (target < 0f)
                                {
                                    target = 0f;
                                }
                                else
                                {
                                    smallFade = true;
                                }
                                faderToken = MelonCoroutines.Start(Fade(end, target));
                            }
                            else
                            {
                                if (cue.behavior == Target.TargetBehavior.Hold || cue.velocity == (int)Hitsound.Snare) end = cue.nextCue.tick;
                                else if (cue.velocity == (int)Hitsound.Percussion) end = cue.tick + 120f;
                                float curr = RenderSettings.skybox.GetFloat("_Exposure");
                                if (curr >= maxBrightness * .5f * Config.intensity)
                                {
                                    target = 0f;
                                    isLight = true;
                                }
                                else
                                {
                                    target = maxBrightness * Config.intensity;
                                    isLight = false;
                                }
                                faderToken = MelonCoroutines.Start(Fade(end, target));
                            }

                        }
                        else
                        {
                            if (RenderSettings.skybox.GetFloat("_Exposure") >= maxBrightness * .5f)
                            {
                                target = 0f;
                                isLight = true;
                            }
                            else
                            {
                                target = maxBrightness;
                                isLight = false;
                            }
                            faderToken = MelonCoroutines.Start(Fade(cue.tick + 60f, target));
                            //InstantLightChange(target);
                        }
                        if (!smallFade) state = isLight ? ExposureState.Dark : ExposureState.Light;
                    }
                    else
                    {
                        HandlePulse(cue);
                    }

                    cues.RemoveAt(0);
                }
                yield return new WaitForSecondsRealtime(.01f);
            }
            yield return null;
        }*/

        private static float GetTargetAmount(Hitsound hitsound, Target.TargetBehavior behavior)
        {
            float amount = 0f;
            switch (hitsound)
            {
                case Hitsound.ChainNode:
                    amount = (maxBrightness / 100f) * 5f;
                    break;
                case Hitsound.ChainStart:
                    amount = (maxBrightness / 100f) * 20f;
                    break;
                case Hitsound.Kick:
                    amount = (maxBrightness / 100f) * 30f;
                    break;
                case Hitsound.Snare:
                    amount = (maxBrightness / 100f) * 40f;
                    break;
                case Hitsound.Percussion:
                    amount = (maxBrightness / 100f) * 60f;
                    break;
                case Hitsound.Melee:
                    amount = (maxBrightness / 100f) * 80f;
                    break;
                default:
                    break;
            }
            if (behavior == Target.TargetBehavior.Melee && hitsound != Hitsound.Melee) amount = (maxBrightness / 100f) * 80f;
            return amount * Config.intensity * .5f;
        }

        private static void LookForEndOfChain(SongCues.Cue cue, List<float> ticks)
        {
            if(cue.nextCue is null)
            {
                ticks.Add(cue.tick);
                return;
            }
                   
            if(cue.nextCue.behavior == Target.TargetBehavior.Chain)
            {
                LookForEndOfChain(cue.nextCue, ticks);
                return;
            }
            
            ticks.Add(cue.nextCue.tick);
        }

        private static void LookForLastBehavior(SongCues.Cue cue, Target.TargetBehavior[] behaviors, List<float> ticks)
        {
            if (cue.nextCue is null)
            {
                ticks.Add(cue.tick);
                return;
            }

            for (int i = 0; i < behaviors.Length; i++)
            {
                if (cue.nextCue.behavior == behaviors[i])
                {
                    if (cue.behavior == Target.TargetBehavior.Hold && cue.nextCue.behavior == Target.TargetBehavior.Hold && cue.tick == cue.nextCue.tick) continue;
                    if (cue.behavior == Target.TargetBehavior.Melee && cue.nextCue.behavior == Target.TargetBehavior.Melee && cue.tick == cue.nextCue.tick) continue;
                    LookForLastBehavior(cue.nextCue, behaviors, ticks);
                    return;
                }
            }
            
            ticks.Add(cue.nextCue.tick);
        }

        private static IEnumerator Fade(float endTick, float targetExposure)
        {
            float oldExposure = RenderSettings.skybox.GetFloat("_Exposure");
            float oldReflection = RenderSettings.reflectionIntensity;
            ArenaLoaderMod.CurrentSkyboxExposure = oldExposure;
            float startTick = AudioDriver.I.mCachedTick;
            while (true)
            {
                float percentage = ((AudioDriver.I.mCachedTick - startTick) * 100f) / (endTick - startTick);
                float currentExp = Mathf.Lerp(oldExposure, targetExposure, percentage / 100f);
                float currentRef = Mathf.Lerp(oldReflection, targetExposure, percentage / 100f);
                RenderSettings.skybox.SetFloat("_Exposure", currentExp);
                ArenaLoaderMod.CurrentSkyboxReflection = 0f;
                ArenaLoaderMod.ChangeReflectionStrength(currentRef);
                ArenaLoaderMod.CurrentSkyboxExposure = currentExp;
                yield return new WaitForSecondsRealtime(.01f);
            }
        }

        private static IEnumerator BetterFade(float endTick, float targetExposure)
        {
            float oldExposure = RenderSettings.skybox.GetFloat("_Exposure");
            float oldReflection = RenderSettings.reflectionIntensity;
            ArenaLoaderMod.CurrentSkyboxExposure = oldExposure;
            float startTick = AudioDriver.I.mCachedTick;
            targetExposure += oldExposure;
            while (true)
            {
                float percentage = ((AudioDriver.I.mCachedTick - startTick) * 100f) / (endTick - startTick);
                float currentExp = Mathf.Lerp(oldExposure, targetExposure, percentage / 100f);
                float currentRef = Mathf.Lerp(oldReflection, targetExposure, percentage / 100f);
                RenderSettings.skybox.SetFloat("_Exposure", currentExp);
                ArenaLoaderMod.CurrentSkyboxReflection = 0f;
                ArenaLoaderMod.ChangeReflectionStrength(currentRef);
                ArenaLoaderMod.CurrentSkyboxExposure = currentExp;
                yield return new WaitForSecondsRealtime(.01f);
            }
        }

        private static IEnumerator FadeToBlack()
        {
            ArenaLoaderMod.CurrentSkyboxExposure = fadeToBlackExposure;
            while (true)
            {
                float percentage = ((AudioDriver.I.mCachedTick - fadeToBlackStartTick) * 100f) / (fadeToBlackEndTick - fadeToBlackStartTick);
                if(percentage >= 0)
                {
                    float currentExp = Mathf.Lerp(fadeToBlackExposure, 0f, percentage / 100f);
                    float currentRef = Mathf.Lerp(fadeToBlackReflection, 0f, percentage / 100f);
                    RenderSettings.skybox.SetFloat("_Exposure", currentExp);
                    ArenaLoaderMod.CurrentSkyboxReflection = 0f;
                    ArenaLoaderMod.ChangeReflectionStrength(currentRef);
                    ArenaLoaderMod.CurrentSkyboxExposure = currentExp;
                }                
                yield return new WaitForSecondsRealtime(.01f);
            }
        }

        /*private static IEnumerator Pulse(float endTick, float amount)
        {
            float oldExposure = RenderSettings.skybox.GetFloat("_Exposure");
            float oldReflection = RenderSettings.reflectionIntensity;
            float targetExposure = oldExposure + amount;
            if (targetExposure > Config.maxBrightness) targetExposure = Config.maxBrightness;
            ArenaLoaderMod.CurrentSkyboxExposure = oldExposure;
            float startTick = AudioDriver.I.mCachedTick;
            while (true)
            {
                float percentage = ((AudioDriver.I.mCachedTick - startTick) * 100f) / (endTick - startTick);
                float currentExp = Mathf.Lerp(oldExposure, targetExposure, percentage / 100f);
                float currentRef = Mathf.Lerp(oldReflection, targetExposure, percentage / 100f);
                RenderSettings.skybox.SetFloat("_Exposure", currentExp);
                ArenaLoaderMod.CurrentSkyboxReflection = 0f;
                ArenaLoaderMod.ChangeReflectionStrength(currentRef);
                ArenaLoaderMod.CurrentSkyboxExposure = currentExp;               
                yield return new WaitForSecondsRealtime(.01f);
            }
        }*/

        private static IEnumerator DoPsychedelia(float end)
        {
            psychadeliaTimer = lastPsyTimer;
            while (active)
            {
                float tick = AudioDriver.I.mCachedTick;
                float amount = SongDataHolder.I.songData.GetTempo(tick) / 50f;
                float phaseTime = defaultPsychadeliaPhaseSeconds / amount;

                if (psychadeliaTimer <= phaseTime)
                {

                    psychadeliaTimer += Time.deltaTime;

                    float forcedPsychedeliaPhase = psychadeliaTimer / phaseTime;
                    GameplayModifiers.I.mPsychedeliaPhase = forcedPsychedeliaPhase;
                }
                else
                {
                    psychadeliaTimer = 0;
                }
                if (tick > end)
                {
                    lastPsyTimer = psychadeliaTimer;
                    psychadeliaTimer = 0;
                    yield break;
                }
                   
                yield return new WaitForSecondsRealtime(0.01f);
            }
        }

        private static void ResetArenaValues()
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            RenderSettings.skybox.SetFloat("_Exposure", userArenaBrightness);
            ArenaLoaderMod.CurrentSkyboxExposure = userArenaBrightness;
            ArenaLoaderMod.CurrentSkyboxReflection = userArenaReflection;
            ArenaLoaderMod.ChangeReflectionStrength(userArenaReflection);
        }

        public static void StopLightshow()
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            active = false;
            MelonCoroutines.Stop(faderToken);
            MelonCoroutines.Stop(lightshowToken);
            MelonCoroutines.Stop(fadeToBlackToken);
            MelonCoroutines.Stop(psyToken);
            ResetArenaValues();
        }

        public static void Reset(string caller, bool restart = false)
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            StopLightshow();
            ResetArenaValues();
            lastPsyTimer = 0f;
            fadeToBlackEndTick = 0f;
            fadeToBlackStartTick = 0f;
            fadeToBlackExposure = 0f;
            fadeToBlackReflection = 0f;
            //if (restart) StartLightshow();
        }

        private enum ExposureState
        {
            Dark,
            Light
        }

        private enum Hitsound
        {
            ChainStart = 1,
            ChainNode = 2,
            Melee = 3,
            Kick = 20,
            Percussion = 60,
            Snare = 120
        }

        public struct BrightnessEvent
        {
            public float brightness;
            public float startTick;
            public float endTick;
            public float intensity;

            public BrightnessEvent(float _brightness, float _startTick, float _endTick, float _intensity)
            {
                brightness = _brightness;
                startTick = _startTick;
                endTick = _endTick;
                intensity = _intensity;
            }
        }

        public struct Section
        {
            public float start;
            public float end;
            public float intensity;

            public Section(float _start, float _end, float _intensity)
            {
                start = _start;
                end = _end;
                intensity = _intensity;
            }
        }
    }
}

















































































