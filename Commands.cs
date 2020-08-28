using CommandTerminal;
using DV.PointSet;
using DV.Signs;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static class Commands
    {
        [HarmonyPatch(typeof(CommandShell), nameof(CommandShell.RegisterCommands))]
        static class RegisterCommandsPatch
        {
            static void Postfix()
            {
                Register();
            }
        }

        static void Register(string name, Action<CommandArg[]> proc)
        {
            if (Terminal.Shell == null)
                return;
            if (Terminal.Shell.Commands.Remove(name.ToUpper()))
                Main.DebugLog($"replacing existing command {name}");
            Terminal.Shell.AddCommand(name, proc);
            Terminal.Autocomplete.Register(name);
        }

        public static void Register()
        {
            Register("hud.dumpTrack", args => {
                var bogie = PlayerManager.Car?.Bogies[0];
                var track = bogie?.track;
                if (track == null)
                    return;
                var direction = bogie.trackDirection;
                var span = bogie.traveller.Span;
                var segments = SignPlacer.GetSegmentInfos(track.curve, 0.4f, 200f, false);
                var output = $"direction = {direction}, span = {span}\n";
                output += string.Join("\n", segments.Select(seg => $"{seg.bezierStartT} -> {seg.bezierEndT}, {seg.segmentLength}, {seg.GetSpeed()}"));
                Terminal.Log(output);
                Main.DebugLog(output);
            });

            // Register("hud.raycast", args => {
            //     var transform = PlayerManager.PlayerTransform;
            //     Terminal.Log($"casting from {transform.position} @ {transform.forward}");
            //     var hits = Physics.RaycastAll(
            //         new Ray(transform.position, transform.forward),
            //         1000f,
            //         1 << TrackIndexer.SIGN_COLLIDER_LAYER);
            //     foreach (var hit in hits)
            //     {
            //         Terminal.Log($"hit {hit.collider} at {hit.transform.position}: dp = {Vector3.Dot(transform.forward, hit.transform.forward)}, layer = {hit.collider.gameObject.layer}");
            //     }
            // });

            Register("hud.followTrack", args => {
                var bogie = PlayerManager.Car?.Bogies[0];
                var startTrack = bogie?.track;
                if (startTrack == null)
                    return;

                var tracks = TrackFollower.FollowTrack(startTrack, bogie.traveller.Span, bogie.trackDirection * 1000f);
                foreach ((RailTrack track, int selectedBranch) in tracks)
                    Terminal.Log($"{track.logicTrack.ID} {selectedBranch}");
            });
        }
    }

    static class TrackFollower
    {
        private static bool SpanIsAhead(int travelDirection, double span, double startSpan)
        {
            return travelDirection > 0 ? span > startSpan : span < startSpan;
        }

        public static IEnumerable<(RailTrack, int)> FollowTrack(RailTrack track, double startSpan, double distance)
        {
            int selectedBranch = -1;
            const int MAX_ITERATIONS = 10;
            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                yield return (track, selectedBranch);
                Junction nextJunction;
                Junction.Branch nextBranch;
                int travelDirection = distance > 0 ? 1 : -1;
                double newSpan = startSpan + distance;

                foreach (var data in TrackIndexer.GetSignData(track)
                    .Where(data => data.direction == travelDirection && SpanIsAhead(travelDirection, data.span, startSpan)))
                    yield return (track, data.limit);

                if (newSpan < 0)
                {
                    nextBranch = track.GetInBranch();
                    if (nextBranch == null)
                        yield break;
                    distance += startSpan;
                    if (nextBranch.first)
                        distance *= -1;
                    nextJunction = track.inJunction;
                }
                else
                {
                    double trackSpan = track.GetPointSet().span;
                    if (newSpan >= trackSpan)
                    {
                        nextBranch = track.GetOutBranch();
                        if (nextBranch == null)
                            yield break;
                        distance -= trackSpan - startSpan;
                        if (!nextBranch.first)
                            distance *= -1;
                        nextJunction = track.outJunction;
                    } else {
                        yield break;
                    }
                }

                if (nextBranch == null)
                    yield break;

                if (nextJunction == null)
                    selectedBranch = -1;
                else if (nextJunction.inBranch.track == track)
                    selectedBranch = nextJunction.selectedBranch;
                track = nextBranch.track;
                startSpan = nextBranch.first ? 0.0 : track.GetPointSet().span;
            }
        }
    }
}