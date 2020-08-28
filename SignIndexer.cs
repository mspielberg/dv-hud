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
        const int SIGN_COLLIDER_LAYER = 30;
        const float SIMPLIFIED_RESOLUTION = 10f;

        static Dictionary<RailTrack, List<SignData>> indexedTracks =
            new Dictionary<RailTrack, List<SignData>>();

        public static List<SignData> GetSignData(RailTrack track)
        {
            List<SignData> data;
            if (!indexedTracks.TryGetValue(track, out data))
                data = indexedTracks[track] = LookForSigns(track.GetPointSet()).ToList();
            return data;
        }

        private static IEnumerable<SignData> ParseSign(string colliderName, int direction, double span)
        {
            string[] parts = colliderName.Split('\n');
            switch (parts.Length)
            {
                case 1:
                    yield return new SignData(-1, int.Parse(parts[0]) * 10, direction, span);
                    break;
                case 2:
                    yield return new SignData(0, int.Parse(parts[0]) * 10, direction, span);
                    yield return new SignData(1, int.Parse(parts[1]) * 10, direction, span);
                    break;
            }
        }

        static IEnumerable<SignData> LookForSigns(EquiPointSet pointSet)
        {
            EquiPointSet simplified = EquiPointSet.ResampleEquidistant(
                pointSet,
                Mathf.Min(SIMPLIFIED_RESOLUTION, (float)pointSet.span / 2));

            foreach (var point in simplified.points)
            {
                var hits = Physics.RaycastAll(
                    new Ray((Vector3)point.position + WorldMover.currentMove, point.forward),
                    (float)point.spanToNextPoint,
                    1 << SIGN_COLLIDER_LAYER);

                foreach (var hit in hits)
                {
                    int direction = Vector3.Dot(hit.collider.transform.forward, point.forward) < 0f ? 1 : -1;
                    foreach (var data in ParseSign(hit.collider.name, direction, point.span + hit.distance))
                        yield return data;
                }
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
                    var collider = signDebug.gameObject.AddComponent<SphereCollider>();
                    collider.name = signDebug.text;
                    collider.center = new Vector3(2f, 0f, 0f);
                    collider.radius = 1f;
                }
            }
        }
    }
}