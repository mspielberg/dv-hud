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
        public static bool enabled = true;
        public static Settings settings = new Settings();
        public static UnityModManager.ModEntry? mod;
        public static GameObject? behaviourRoot;

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

            Commands.Register();
            DataProviders.Register();

            if (SaveLoadController.carsAndJobsLoadingFinished && WorldStreamingInit.IsLoaded)
                OnLoadingFinished();
            else
                WorldStreamingInit.LoadingFinished += OnLoadingFinished;

            return true;
        }

        static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
            if (GUILayout.Button("Reset position", GUILayout.ExpandWidth(false)))
            {
                settings.hudPosition = Settings.defaultPosition;
                Overlay.instance?.ResetPosition();
            }
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
            if (settings.enableLogging && mod != null)
                mod.Logger.Log(message);
        }

        public class Settings : UnityModManager.ModSettings, IDrawable
        {
            public static Vector2 defaultPosition = new Vector2(10, 10);

            [Draw("Show general info")] private bool showGeneral = true;
            [Draw("Speed", VisibleOn = "showGeneral|true")] private bool showSpeed = true;
            public bool ShowSpeed { get => showGeneral && showSpeed; }
            [Draw("Grade", VisibleOn = "showGeneral|true")] private bool showGrade = true;
            public bool ShowGrade { get => showGeneral && showGrade; }
            [Draw("Brake pipe", VisibleOn = "showGeneral|true")] private bool showBrakePipe = true;
            public bool ShowBrakePipe { get => showGeneral && showBrakePipe; }
            [Draw("Consist mass", VisibleOn = "showGeneral|true")] private bool showConsistMass = true;
            public bool ShowConsistMass { get => showGeneral && showConsistMass; }


            [Draw("Show locomotive info")] private bool showLoco = true;
            [Draw("Tractive effort")] private bool showTractiveEffort = true;
            public bool ShowTractiveEffort { get => showLoco && showTractiveEffort; }
            [Draw("Adhesion")] private bool showAdhesion = true;
            public bool ShowAdhesion { get => showLoco && showAdhesion; }
            [Draw("Slip")] private bool showSlip = true;
            public bool ShowSlip { get => showLoco && showSlip; }

            [Draw("Show track info")] public bool showTrackInfo = true;
            [Draw("Max events", VisibleOn = "showTrackInfo|true")] public int maxEventCount = 20;
            [Draw("Max distance", VisibleOn = "showTrackInfo|true")] public double maxEventSpan = 2000;

            [Draw("Show car list")] public bool showCarList = true;
            [Draw("Wheel strain", VisibleOn = "showCarList|true")] public bool showCarStress = true;
            [Draw("Job ID", VisibleOn = "showCarList|true")] public bool showCarJobs = true;
            [Draw("Destination", VisibleOn = "showCarList|true")] public bool showCarDestinations = true;

            [Draw("Enable logging")] public bool enableLogging = false;

            public Vector2 hudPosition;

            override public void Save(UnityModManager.ModEntry entry) {
                Save<Settings>(this, entry);
            }

            public void OnChange()
            {
            }
        }
    }
}
