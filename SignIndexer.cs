using DV.PointSet;
using DV.Signs;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    class SignData
    {
        public int branch;
        public int limit;
        public int direction;
        public double span;

        public SignData(int branch, int limit, int direction, double span)
        {
            this.branch = branch;
            this.limit = limit;
            this.direction = direction;
            this.span = span;
        }
    }

    static class TrackIndexer
    {
        internal const int SIGN_COLLIDER_LAYER = 30;
        const float SIMPLIFIED_RESOLUTION = 10f;

        static Dictionary<RailTrack, List<TrackEvent>> indexedTracks =
            new Dictionary<RailTrack, List<TrackEvent>>();

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track)
        {
            List<TrackEvent> data;
            if (!indexedTracks.TryGetValue(track, out data))
                data = indexedTracks[track] = GenerateTrackEvents(track).ToList();
            return data;
        }

        public static IEnumerable<TrackEvent> GetTrackEvents(RailTrack track, bool first, double start)
        {
            var allTrackEvents = GetTrackEvents(track);
            var filtered = allTrackEvents.RelativeFromSpan(start, first);
            // Debug.Log($"allTrackEvents:\n{string.Join("\n",allTrackEvents)}\nfiltered:\n{string.Join("\n",filtered)}");
            return filtered;
        }

        private static SpeedLimitEvent ParseSign(string colliderName, bool direction, double span)
        {
            string[] parts = colliderName.Split('\n');
            switch (parts.Length)
            {
                case 1:
                    return new SpeedLimitEvent(span, direction, int.Parse(parts[0]) * 10);
                case 2:
                    return new DualSpeedLimitEvent(span, direction, int.Parse(parts[0]) * 10, int.Parse(parts[1]) * 10);
            }
            return null;
        }

        public static float Grade(EquiPointSet.Point point)
        {
            return (float)Mathf.RoundToInt(point.forward.y * 200) / 2f;
        }

        static IEnumerable<SpeedLimitEvent> FindSigns(EquiPointSet.Point point)
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
                yield return ParseSign(hit.collider.name, direction, point.span + hit.distance);
            }
        }

        static IEnumerable<TrackEvent> GenerateTrackEvents(RailTrack track)
        {
            var pointSet = track.GetPointSet();
            EquiPointSet simplified = EquiPointSet.ResampleEquidistant(
                pointSet,
                Mathf.Min(SIMPLIFIED_RESOLUTION, (float)pointSet.span / 3));

            foreach (var point in simplified.points)
            {
                foreach (var sign in FindSigns(point))
                    yield return sign;
                yield return new GradeEvent(point.span, Grade(point));
            }
        }

        [HarmonyPatch(typeof(Streamer), nameof(Streamer.AddSceneGO))]
        static class AddSceneGOPatch
        {
            static void Postfix(GameObject sceneGO)
            {
                foreach (var signDebug in sceneGO.GetComponentsInChildren<SignDebug>())
                {
                    signDebug.gameObject.layer = SIGN_COLLIDER_LAYER;
                    // var collider = signDebug.gameObject.AddComponent<CapsuleCollider>();
                    var collider = signDebug.gameObject.AddComponent<SphereCollider>();
                    collider.name = signDebug.text;
                    collider.center = new Vector3(2f, 0f, 0f);
                    collider.radius = 1f;
                    // collider.height = 100f;
                    // collider.direction = 1; // along Y-axis
                }
                for (int l = 0; l < 32; l++)
                    Physics.IgnoreLayerCollision(l, SIGN_COLLIDER_LAYER);
            }
        }
    }
}