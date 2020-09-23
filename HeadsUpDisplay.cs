using HarmonyLib;
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

        public static bool Load(UnityModManager.ModEntry modEntry)
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

        private static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
            if (GUILayout.Button("Reset position", GUILayout.ExpandWidth(false)))
            {
                settings.hudPosition = Settings.defaultPosition;
                Overlay.instance?.ResetPosition();
            }
        }

        private static void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value != enabled)
            {
                enabled = value;
            }
            return true;
        }

        private static void OnLoadingFinished()
        {
            behaviourRoot = new GameObject();
            behaviourRoot.AddComponent<Overlay>();
        }

        private static bool OnUnload(UnityModManager.ModEntry modEntry)
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

            [Draw("Show track info")] public bool showTrackInfo = true;
            [Draw("Max events", VisibleOn = "showTrackInfo|true")] public int maxEventCount = 10;
            [Draw("Max distance", VisibleOn = "showTrackInfo|true")] public double maxEventSpan = 5000;

            [Draw("Show car list")] public bool showCarList = true;
            [Draw("Group by job", VisibleOn = "showCarList|true")] public bool groupCarsByJob = true;
            [Draw("Cornering stress", VisibleOn = "showCarList|true")] public bool showCarStress = true;
            [Draw("Job ID", VisibleOn = "showCarList|true")] public bool showCarJobs = true;
            [Draw("Destination", VisibleOn = "showCarList|true")] public bool showCarDestinations = true;
            [Draw("Brake status", VisibleOn = "showCarList|true")] public bool showCarBrakeStatus = true;

            [Draw("Enable logging")] public bool enableLogging = false;

            public Vector2 hudPosition;

            public bool IsEnabled(DataProvider _)
            {
                return true;
            }

            override public void Save(UnityModManager.ModEntry entry) => Save<Settings>(this, entry);

            public void OnChange()
            {
            }
        }
    }
}
