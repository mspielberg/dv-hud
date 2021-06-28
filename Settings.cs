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
                foreach (string key in from dp in Registry.providers.Values orderby dp.Order select dp.Label)
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

        public static Vector2 defaultPosition = new Vector2(10, 10);

        public bool showDrivingInfo = true;
        public DrivingInfoSettings drivingInfoSettings = DrivingInfoSettings.Create();

        [Draw("Show track info")] public bool showTrackInfo = true;
        [Draw("Max events", VisibleOn = "showTrackInfo|true")] public int maxEventCount = 10;
        [Draw("Max distance", VisibleOn = "showTrackInfo|true")] public double maxEventSpan = 5000;

        [Draw("Show car list")] public bool showCarList = true;
        [Draw("Group by job", VisibleOn = "showCarList|true")] public bool groupCarsByJob = true;
        [Draw("Cornering stress", VisibleOn = "showCarList|true")] public bool showCarStress = true;
        [Draw("Job ID", VisibleOn = "showCarList|true")] public bool showCarJobs = true;
        [Draw("Destination", VisibleOn = "showCarList|true")] public bool showCarDestinations = true;
        [Draw("Brake status", VisibleOn = "showCarList|true")] public bool showCarBrakeStatus = true;

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