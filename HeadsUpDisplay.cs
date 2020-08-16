using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    [EnableReloading]
    public static class Main
    {
        public static bool enabled;
        public static Settings settings;
        public static UnityModManager.ModEntry mod;
        public static GameObject behaviourRoot;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;

            try { settings = Settings.Load<Settings>(modEntry); } catch {}
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            if (SaveLoadController.carsAndJobsLoadingFinished && WorldStreamingInit.IsLoaded)
                OnLoadingFinished();
            else
                WorldStreamingInit.LoadingFinished += OnLoadingFinished;

            return true;
        }

        static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        static void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value != enabled)
            {
                enabled = value;
            }
            return true;
        }

        static void OnLoadingFinished()
        {
            GenericProviders.Register();
            LocoProviders.Register();
            behaviourRoot = new GameObject();
            behaviourRoot.AddComponent<Overlay>();
        }

        static bool OnUnload(UnityModManager.ModEntry modEntry)
        {
            if (behaviourRoot != null)
                GameObject.Destroy(behaviourRoot);
            behaviourRoot = null;
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll(modEntry.Info.Id);
            return true;
        }

        public static void DebugLog(string message)
        {
            if (settings.enableLogging)
                mod.Logger.Log(message);
        }

        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            [Draw("Boiler steam generation rate")] public float steamGenerationRate = 0.5f;
            [Draw("Cutoff wheel gamma")] public float cutoffGamma = 1.9f;
            [Draw("Max boiler pressure")] public float safetyValveThreshold = 16f;

            [Draw("Enable detailed low-speed simulation")] public bool enableLowSpeedSimulation = true;
            [Draw("Low-speed simulation transition start", VisibleOn = "enableLowSpeedSimulation|true")]
            public float lowSpeedTransitionStart = 10f;
            [Draw("Low-speed simulation transition width", VisibleOn = "enableLowSpeedSimulation|true")]
            public float lowSpeedTransitionWidth = 5f;

            [Draw("Enable logging")] public bool enableLogging = false;
            [Draw("Show info overlay")] public bool showInfoOverlay = false;

            override public void Save(UnityModManager.ModEntry entry) {
                Save<Settings>(this, entry);
            }

            public void OnChange() {
                cutoffGamma = Mathf.Max(cutoffGamma, 0.1f);
                safetyValveThreshold = Mathf.Clamp(safetyValveThreshold, 0f, 20f);
            }
        }
    }
}
