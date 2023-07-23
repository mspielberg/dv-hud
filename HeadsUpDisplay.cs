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

            try
            {
                var loaded = Settings.Load<Settings>(modEntry);
                if (loaded.version == modEntry.Info.Version)
                    settings = loaded;
                else
                    settings = new Settings();
            }
            catch
            {
                settings = new Settings();
            }
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = OnUnload;

            DataProviders.Register();

            if (AStartGameData.carsAndJobsLoadingFinished && WorldStreamingInit.IsLoaded)
                OnLoadingFinished();
            else
                WorldStreamingInit.LoadingFinished += OnLoadingFinished;

            return true;
        }

        private static void OnGui(UnityModManager.ModEntry modEntry)
        {
            settings.Draw();
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
                Object.Destroy(behaviourRoot);
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
    }
}
