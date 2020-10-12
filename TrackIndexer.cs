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
                data = indexedTracks[track] = GenerateTrackEvents(track).ToList();
            }
            return data;
        }

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track, bool first, double start)
        {
            var allTrackEvents = GetTrackEvents(track);
            return allTrackEvents.RelativeFromSpan(start, first);
        }

        private static IEnumerable<TrackEvent> ParseSign(string colliderName, bool direction, double span)
        {
            string[] parts = colliderName.Split('\n');
            switch (parts.Length)
            {
                case 1:
                    if (int.TryParse(parts[0], out var limit))
                        yield return new SpeedLimitEvent(span, direction, limit * 10);
                    break;

                case 2:
                    if (int.TryParse(parts[0], out var top))
                    {
                        if (parts[1][0] == '+' || parts[1][0] == '-')
                        {
                            yield return new SpeedLimitEvent(span, direction, top * 10);
                            yield return new GradeEvent(span, direction, float.Parse(parts[1]));
                        }
                        else if (int.TryParse(parts[1], out var bottom))
                        {
                            yield return new DualSpeedLimitEvent(span, direction, top * 10, bottom * 10);
                        }
                    }
                    else if (int.TryParse(parts[1], out var bottom))
                    {
                        yield return new SpeedLimitEvent(span, direction, bottom * 10);
                    }
                    else
                    {
                        Main.DebugLog($"Unable to parse sign: \"{colliderName.Replace("\n", "\\n")}\"");
                    }
                    break;
            }
        }

        public static float Grade(EquiPointSet.Point point)
        {
            return Mathf.RoundToInt(point.forward.y * 200) / 2f;
        }

        private static IEnumerable<TrackEvent> FindSigns(EquiPointSet.Point point)
        {
            var hits = Physics.RaycastAll(
                new Ray((Vector3)point.position + WorldMover.currentMove, point.forward),
                (float)point.spanToNextPoint,
                1 << SIGN_COLLIDER_LAYER);

            foreach (var hit in hits)
            {
                var dp = Vector3.Dot(hit.collider.transform.forward, point.forward);
                bool direction = dp < 0f;
                foreach (var trackEvent in ParseSign(hit.collider.name, direction, point.span + hit.distance))
                    yield return trackEvent;
            }
        }

        private static IEnumerable<TrackEvent> GenerateTrackEvents(RailTrack track)
        {
            var pointSet = track.GetPointSet();
            EquiPointSet simplified = EquiPointSet.ResampleEquidistant(
                pointSet,
                Mathf.Min(SIMPLIFIED_RESOLUTION, (float)pointSet.span / 3));

            foreach (var point in simplified.points)
            {
                foreach (var trackEvent in FindSigns(point))
                    yield return trackEvent;
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
                if (foundSigns)
                    indexedTracks.Clear();
            }
        }
    }
}