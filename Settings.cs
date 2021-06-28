using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public struct DrivingInfoSettings
        {
            public HashSet<string> disabledProviders;

            static public DrivingInfoSettings Create()
            {
                return new DrivingInfoSettings() { disabledProviders = Registry.providers.Keys.ToHashSet() };
            }

            public void Draw()
            {
                GUILayout.BeginVertical("box");
                var providers = Registry.providers.Values
                    .Where(dp => !dp.Hidden)
                    .OrderBy(dp => dp.Order)
                    .Select(dp => dp.Label);
                foreach (string key in providers)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(key, GUILayout.ExpandWidth(false));
                    var result = GUILayout.Toggle(!disabledProviders.Contains(key), "");
                    if (result)
                        disabledProviders.Remove(key);
                    else
                        disabledProviders.Add(key);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }

            public bool IsEnabled(DataProvider dp) => !disabledProviders.Contains(dp.Label);
        }

        public class TrackInfoSettings
        {
            [Draw("Enable")] public bool enabled = true;
            [Draw("Max events", VisibleOn = "enabled|true")] public int maxEventCount = 10;
            [Draw("Max distance", VisibleOn = "enabled|true")] public double maxEventSpan = 5000;
        }

        public class TrainInfoSettings
        {
            [Draw("Enable")] public bool enabled = true;
            [Draw("Length & mass")] public bool showTrainInfo = true;
            [Draw("Car list")] public bool showCarList = true;
            [Draw("Group by job", VisibleOn = "showCarList|true")] public bool groupCarsByJob = true;
            [Draw("Stress", VisibleOn = "showCarList|true")] public bool showCarStress = true;
            [Draw("Job ID", VisibleOn = "showCarList|true")] public bool showCarJobs = true;
            [Draw("Destination", VisibleOn = "showCarList|true")] public bool showCarDestinations = true;
            [Draw("Brake status", VisibleOn = "showCarList|true")] public bool showCarBrakeStatus = true;
        }

        public static Vector2 defaultPosition = new Vector2(10, 10);

        public bool showDrivingInfo = true;
        public DrivingInfoSettings drivingInfoSettings = DrivingInfoSettings.Create();

        [Draw("Upcoming track info", Collapsible = true, Box = true)]
        public TrackInfoSettings trackInfoSettings = new TrackInfoSettings();

        [Draw("Train info", Collapsible = true, Box = true)]
        public TrainInfoSettings trainInfoSettings = new TrainInfoSettings();

        [Draw("Enable logging")] public bool enableLogging;
        [Draw("Lock position")] public bool lockPosition;

        public readonly string? version = Main.mod?.Info.Version;

        public Vector2 hudPosition;

        public bool IsEnabled(DataProvider dp) => drivingInfoSettings.IsEnabled(dp);

        override public void Save(UnityModManager.ModEntry entry) => Save<Settings>(this, entry);

        public void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Show driving info", GUILayout.ExpandWidth(false));
            showDrivingInfo = GUILayout.Toggle(showDrivingInfo, "");
            GUILayout.EndHorizontal();
            if (showDrivingInfo)
                drivingInfoSettings.Draw();
            this.Draw(Main.mod);
        }

        public void OnChange()
        {
        }
    }
}