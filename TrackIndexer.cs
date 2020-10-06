using DV.PointSet;
using DV.Signs;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public static class TrackIndexer
    {
        internal const int SIGN_COLLIDER_LAYER = 30;
        private const float SIMPLIFIED_RESOLUTION = 10f;

        private static readonly Dictionary<RailTrack, List<TrackEvent>> indexedTracks =
            new Dictionary<RailTrack, List<TrackEvent>>();

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track)
        {
            if (!indexedTracks.TryGetValue(track, out var data))
            {
                float start = Time.realtimeSinceStartup;
                data = indexedTracks[track] = GenerateTrackEvents(track).ToList();
                float end = Time.realtimeSinceStartup;
                Main.DebugLog($"Indexed track {track.logicTrack.ID}."
                   + $" Found {data.Count} events ({data.OfType<SpeedLimitEvent>().Count()} speed signs) in {end-start} s.");
            }
            return data;
        }

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track, bool first, double start)
        {
            var allTrackEvents = GetTrackEvents(track);
            var filtered = allTrackEvents.RelativeFromSpan(start, first);
            // Debug.Log($"allTrackEvents:\n{string.Join("\n",allTrackEvents)}\nfiltered:\n{string.Join("\n",filtered)}");
            return filtered;
        }

        private static SpeedLimitEvent? ParseSign(string colliderName, bool direction, double span)
        {
            string[] parts = colliderName.Split('\n');
            return parts.Length switch
            {
                1 => new SpeedLimitEvent(span, direction, int.Parse(parts[0]) * 10),
                2 => new DualSpeedLimitEvent(span, direction, int.Parse(parts[0]) * 10, int.Parse(parts[1]) * 10),
                _ => null,
            };
        }

        public static float Grade(EquiPointSet.Point point)
        {
            return (float)Mathf.RoundToInt(point.forward.y * 200) / 2f;
        }

        private static IEnumerable<SpeedLimitEvent> FindSigns(EquiPointSet.Point point)
        {
            // Debug.Log($"Raycasting from {(Vector3)point.position + WorldMover.currentMove} / {point.forward}");
            var hits = Physics.RaycastAll(
                new Ray((Vector3)point.position + WorldMover.currentMove, point.forward),
                (float)point.spanToNextPoint,
                1 << SIGN_COLLIDER_LAYER);

            foreach (var hit in hits)
            {
                var dp = Vector3.Dot(hit.collider.transform.forward, point.forward);
                // Debug.Log($"Found sign {hit.collider.name} at {hit.point}, dp = {dp}");
                bool direction = dp < 0f;
                var signEvent = ParseSign(hit.collider.name, direction, point.span + hit.distance);
                if (signEvent == null)
                    Debug.Log($"Could not parse sign text {hit.collider.name}");
                else
                    yield return signEvent;
            }
        }

        private static IEnumerable<TrackEvent> GenerateTrackEvents(RailTrack track)
        {
            var pointSet = track.GetPointSet();
            EquiPointSet simplified = EquiPointSet.ResampleEquidistant(
                pointSet,
                Mathf.Min(SIMPLIFIED_RESOLUTION, (float)pointSet.span / 3));

            var lastGrade = float.NaN;
            foreach (var point in simplified.points)
            {
                foreach (var sign in FindSigns(point))
                    yield return sign;
                var grade = Grade(point);
                if (grade != lastGrade)
                {
                    yield return new GradeEvent(point.span, grade);
                    lastGrade = grade;
                }
            }
        }

        [HarmonyPatch(typeof(Streamer), nameof(Streamer.AddSceneGO))]
        public static class AddSceneGOPatch
        {
            public static void Postfix(GameObject sceneGO)
            {
                var signDebugs = sceneGO.GetComponentsInChildren<SignDebug>();
                bool foundSigns = false;
                foreach (var signDebug in signDebugs)
                {
                    signDebug.gameObject.layer = SIGN_COLLIDER_LAYER;
                    var collider = signDebug.gameObject.AddComponent<CapsuleCollider>();
                    collider.name = signDebug.text;
                    collider.center = new Vector3(2f, 0f, 0f);
                    collider.height = 10f;
                    collider.direction = 1; // along Y-axis
                    collider.isTrigger = true;

                    foundSigns = true;
                }
                // Main.DebugLog($"Loaded tile {sceneGO} on frame {Time.frameCount}. Fixed update {Time.fixedTime / Time.fixedDeltaTime}");
                if (foundSigns)
                    indexedTracks.Clear();
            }
        }
    }
}