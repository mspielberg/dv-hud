using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public static class Styles
    {
        public static readonly GUIStyle noChrome;
        public static readonly GUIStyle noWrap;
        public static readonly GUIStyle noWrapBold;
        public static readonly GUIStyle rightAlign;
        public static readonly GUIStyle richText;

        /// <summary>Can only be called during OnGui()</summary>
        static Styles()
        {
            noChrome = new GUIStyle(GUI.skin.window);
            noChrome.normal.background = null;
            noChrome.onNormal = noChrome.normal;

            noWrap = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false
            };

            noWrapBold = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                fontStyle = FontStyle.Bold,
            };

            rightAlign = new GUIStyle(noWrap)
            {
                alignment = TextAnchor.MiddleRight
            };

            richText = new GUIStyle(noWrap)
            {
                richText = true
            };
        }
    }

    public class Overlay : MonoBehaviour
    {
        public const int ColumnSpacing = 10;

        public static Overlay? instance;

        private bool overlayEnabled = false;

        public void Start()
        {
            // Wait for a frame because for some reason RaycastAll doesn't detect colliders if called on the same frame.
            _ = StartCoroutine(DelayedEnable());
            instance = this;
        }

        private IEnumerator DelayedEnable()
        {
            yield return null;
            overlayEnabled = true;
            Main.DebugLog($"Overlay enabled on frame {Time.frameCount}. Fixed update {Time.fixedTime / Time.fixedDeltaTime}");
        }

        private static Rect prevRect = new Rect();

        public void OnGUI()
        {
            if (!overlayEnabled)
            {
                Main.DebugLog($"OnGUI called before Start on frame {Time.frameCount}. Fixed update {Time.fixedTime / Time.fixedDeltaTime}");
                return;
            }
            if (!Main.enabled)
                return;

            if (prevRect == new Rect())
                prevRect.position = Main.settings.hudPosition;
            var newRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                prevRect,
                DrawDrivingInfoWindow,
                "",
                Styles.noChrome);
            if (newRect.min == prevRect.min)
                newRect.height = 0;
            prevRect = newRect;
            Main.settings.hudPosition = prevRect.position;
        }

        public void ResetPosition()
        {
            prevRect = new Rect();
        }

        public static void DrawColumn<T>(IEnumerable<T> cells, string? label = null, Func<T, string>? renderer = null, GUIStyle? style = null)
        {
            renderer ??= x => x!.ToString();
            style ??= Styles.noWrap;
            GUILayout.Space(Overlay.ColumnSpacing);
            GUILayout.BeginVertical();
            if (label != null)
                GUILayout.Label(label, style);
            foreach (var cell in cells)
                GUILayout.Label(renderer(cell), style);
            GUILayout.EndVertical();
        }

        private void DrawDrivingInfoWindow(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            if (Main.settings.showDrivingInfo)
                DrawCurrentCarInfo();
            if (Main.settings.trackInfoSettings.enabled)
                EventList.DrawUpcomingEvents();

            GUILayout.EndHorizontal();

            if (Main.settings.trainInfoSettings.enabled)
                DrawTrainInfo();

            GUILayout.EndVertical();

            if (!Main.settings.lockPosition)
                GUI.DragWindow();
        }

        private void DrawCurrentCarInfo()
        {
            if (!PlayerManager.Car)
                return;

            static (string, string)? GetFormattedFromProvider(IDataProvider dp, TrainCar car)
            {
                if (dp.TryGetFormatted(car, out var s))
                    return (dp.Label, s);
                return null;
            }

            var labelsAndValues = Registry.providers.Values
                .Where(dp => !dp.Hidden)
                .Where(Main.settings.IsEnabled)
                .OrderBy(dp => dp.Order)
                .Select(dp => GetFormattedFromProvider(dp, PlayerManager.Car))
                .OfType<(string, string)>();

            GUILayout.BeginHorizontal("box", GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical();
            foreach (var (label, value) in labelsAndValues)
            {
                if (value != null)
                    GUILayout.Label(label, Styles.noWrap);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            foreach (var (label, value) in labelsAndValues)
            {
                if (value != null)
                    GUILayout.Label(value, Styles.noWrap);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawTrainInfo()
        {
            var trainset = PlayerManager.Car?.trainset ?? PlayerManager.LastLoco?.trainset;
            if (trainset == null)
                return;

            GUILayout.BeginVertical("box");

            if (Main.settings.trainInfoSettings.showTrainInfo)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Train");
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label($"{trainset.cars.Count} car{(trainset.cars.Count != 1 ? "s" : "")}");
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label($"{trainset.OverallLength():F0} m");
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label($"{trainset.TotalMass() / 1000:F0} t");
                GUILayout.EndHorizontal();
            }

            if (Main.settings.trainInfoSettings.showCarList)
                CarList.DrawCarList(trainset);

            GUILayout.EndVertical();
        }
    }
}