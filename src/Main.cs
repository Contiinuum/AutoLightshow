using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Harmony;
using ArenaLoader;
using System.Collections;
using System.Linq;

namespace AutoLightshow
{
    public class AutoLightshowMod : MelonMod
    {

        private static object faderToken;
        private static object lightshowToken;
        private static object psyToken;
        private static bool active = false;

        private static float defaultPsychadeliaPhaseSeconds = 14.28f;
        private static float psychadeliaTimer = 0.0f;

        private static float userArenaBrightness = .5f;
        private static float userArenaReflection = 1f;

        private static float defaultArenaBrightness = .5f;
        private static float lastPsyTimer = 0f;

        
        public static class BuildInfo
        {
            public const string Name = "AutoLightshow";  // Name of the Mod.  (MUST BE SET)
            public const string Author = "Continuum"; // Author of the Mod.  (Set as null if none)
            public const string Company = null; // Company that made the Mod.  (Set as null if none)
            public const string Version = "1.0.0"; // Version of the Mod.  (MUST BE SET)
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

        public static void StartLightshow()
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            StopLightshow();
            active = true;
            lightshowToken = MelonCoroutines.Start(DoLightshow());
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
            MelonLogger.Log(userArenaReflection);
        }

        private static IEnumerator DoLightshow()
        {
            float oldExposure = RenderSettings.skybox.GetFloat("_Exposure");
            ArenaLoaderMod.CurrentSkyboxExposure = oldExposure;
            List<SongCues.Cue> cues = SongCues.I.mCues.cues.ToList();
            for (int i = cues.Count - 1; i >= 0; i--)
            {
                if (!Config.enableStroboChains)
                {
                    if (cues[i].behavior == Target.TargetBehavior.Chain) cues.RemoveAt(i);
                }

                if (cues[i].behavior == Target.TargetBehavior.Dodge) cues.RemoveAt(i);  //cues[i].tick < AudioDriver.I.mCachedTick

                if (cues[i].behavior != Target.TargetBehavior.Chain && cues[i].behavior != Target.TargetBehavior.ChainStart && cues[i].velocity == (int)Hitsound.ChainNode)
                {
                    cues.RemoveAt(0);
                    continue;
                }
                if (cues[i].nextCue.tick == cues[i].tick)
                {
                    if (cues[i].behavior == Target.TargetBehavior.Melee) cues.RemoveAt(1);
                    else
                    {
                        cues.RemoveAt(0);
                        continue;
                    }

                }
            }
               
            ExposureState state = ExposureState.Light;
            while (active)
            {
                SongCues.Cue cue = cues[0];
                if (cue.nextCue is null) yield break;
                if (cue.tick <= AudioDriver.I.mCachedTick)
                {
                    /*if (cue.behavior != Target.TargetBehavior.Chain && cue.behavior != Target.TargetBehavior.ChainStart && cue.velocity == 2)
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
                           
                    }*/
                   

                    float diff = cue.nextCue.tick - cue.tick;
                    float end = cue.nextCue.tick - (diff / 4f);
                    bool isLight = state == ExposureState.Light;
                    float target = isLight ? Config.minAllowedBrightness : defaultArenaBrightness * Config.maxAllowedBrightness;
                    float threshhold = SongDataHolder.I.songData.GetTempo(AudioDriver.I.mCachedTick) >= 150 ? 2f : 1f;
                    if (cue.behavior == Target.TargetBehavior.Standard || cue.behavior == Target.TargetBehavior.Vertical || cue.behavior == Target.TargetBehavior.Horizontal)
                    {
                        if(cue.velocity != (int)Hitsound.Percussion && cue.velocity != (int)Hitsound.Snare)
                        {
                            if (diff <= 120 * threshhold && cue.nextCue.behavior == cue.behavior)
                            {
                                cues.RemoveAt(1);
                            }
                        }                       
                    }
                    else if (cue.behavior == Target.TargetBehavior.Chain)
                    {
                        if(cue.velocity == 2)
                        {
                            
                            if (diff >= 240 * threshhold)
                            {
                                cues.RemoveAt(0);
                                continue;
                            }
                        }                       
                    }
                    /*  if (cue.behavior == Target.TargetBehavior.Hold)
                      {
                          if(cue.tickLength >= 1920f)
                          {
                              psyToken = MelonCoroutines.Start(DoPsychedelia(AudioDriver.I.mCachedTick + cue.tickLength - 480f));
                          }
                      }*/
                    if (cue.behavior == Target.TargetBehavior.Melee || cue.behavior == Target.TargetBehavior.ChainStart || cue.behavior == Target.TargetBehavior.Hold)
                    {

                        //if (cue.behavior == Target.TargetBehavior.Melee) LookForLastBehavior(cue, Target.TargetBehavior.Melee, tick);
                        //else LookForLastBehavior(cue, Target.TargetBehavior.ChainStart, tick);
                        List<float> ticks = new List<float>();
                        if (cue.behavior == Target.TargetBehavior.ChainStart) LookForEndOfChain(cue, ticks);
                        else LookForLastBehavior(cue, new Target.TargetBehavior[] { Target.TargetBehavior.Melee, Target.TargetBehavior.Hold }, ticks);

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

                    bool smallFade = false;
                    float exp = RenderSettings.skybox.GetFloat("_Exposure");
                    MelonCoroutines.Stop(faderToken);
                    if (diff >= 120f && cue.behavior != Target.TargetBehavior.Melee)
                    {
                        if(!Config.intenseLights && cue.velocity == (int)Hitsound.Kick && (cue.behavior == Target.TargetBehavior.Standard || cue.behavior == Target.TargetBehavior.Vertical || cue.behavior == Target.TargetBehavior.Horizontal))
                        {
                            
                            target = isLight ? exp - .1f : exp + .1f;
                            if (target > Config.maxAllowedBrightness)
                            {
                                target = Config.maxAllowedBrightness;
                            }                               
                            else if (target < Config.minAllowedBrightness)
                            {
                                target = Config.minAllowedBrightness;
                            }
                            else
                            {
                                smallFade = true;
                            }
                            faderToken = MelonCoroutines.Start(Fade(end, target));
                        }
                        else
                        {
                            if (cue.velocity == 60) end = cue.tick + 120;
                           
                            faderToken = MelonCoroutines.Start(Fade(end, target));
                        }
                       
                    }
                    else
                    {
                        faderToken = MelonCoroutines.Start(Fade(cue.tick + 60f, target));
                        //InstantLightChange(target);
                    }
                    if(!smallFade) state = isLight ? ExposureState.Dark : ExposureState.Light;
                    cues.RemoveAt(0);
                }
                yield return new WaitForSecondsRealtime(.01f);
            }
            yield return null;
        }

        private static void InstantLightChange(float target)
        {
            RenderSettings.skybox.SetFloat("_Exposure", target);
            RenderSettings.reflectionIntensity = target;
            ArenaLoaderMod.CurrentSkyboxReflection = 0f;
            ArenaLoaderMod.ChangeReflectionStrength(target);
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
            MelonCoroutines.Stop(psyToken);
            ResetArenaValues();
        }

        public static void Reset(bool restart = false)
        {
            if (!Integrations.arenaLoaderFound || !Config.enabled) return;
            StopLightshow();
            ResetArenaValues();
            lastPsyTimer = 0f;
            if (restart) StartLightshow();
        }

        private enum ExposureState
        {
            Dark,
            FadeToLight,
            FadeToDark,
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
    }
}

















































































