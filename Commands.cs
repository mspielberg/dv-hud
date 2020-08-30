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

            Register("hud.raycast", args => {
                var transform = PlayerManager.PlayerTransform;
                Terminal.Log($"casting from {transform.position} @ {transform.forward}");
                var hits = Physics.RaycastAll(
                    new Ray(transform.position, transform.forward),
                    1000f,
                    1 << TrackIndexer.SIGN_COLLIDER_LAYER);
                foreach (var hit in hits)
                {
                    Terminal.Log($"hit {hit.collider} at {hit.transform.position}: dp = {Vector3.Dot(transform.forward, hit.transform.forward)}, layer = {hit.collider.gameObject.layer}");
                }
            });

            Register("hud.followTrack", args => {
                var transform = PlayerManager.PlayerTransform;
                (RailTrack startTrack, EquiPointSet.Point? nullablePoint) = RailTrack.GetClosest(transform.position);
                if (startTrack == null)
                    return;
                var point = (EquiPointSet.Point)nullablePoint;
                var trackDirection = Vector3.Dot(transform.forward, point.forward) > 0f ? 1 : -1;

                var trackEvents = TrackFollower.FollowTrack(startTrack, point.span, trackDirection * 1000f);
                foreach (var trackEvent in trackEvents)
                    Terminal.Log(trackEvent.ToString());
            });
        }
    }

    static class TrackFollower
    {
        public static IEnumerable<TrackEvent> FollowTrack(RailTrack track, double startSpan, double distance)
        {
            const int MAX_ITERATIONS = 10;
            double distanceFromStart = 0f;
            for (int i = 0; i < MAX_ITERATIONS; i++)
            {
                yield return new TrackChangeEvent(distanceFromStart, track.logicTrack.ID);
                bool travelDirection = distance > 0;

                var trackEvents = TrackIndexer
                    .GetTrackEvents(track, travelDirection, startSpan)
                    .Offset(distanceFromStart);

                foreach (var trackEvent in trackEvents)
                    yield return trackEvent;

                double newSpan = startSpan + distance;

                Junction nextJunction;
                Junction.Branch nextBranch;
                if (newSpan < 0)
                {
                    nextBranch = track.GetInBranch();
                    if (nextBranch == null)
                        yield break;
                    distance += startSpan;
                    distanceFromStart += startSpan;
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
                        distanceFromStart += trackSpan - startSpan;
                        if (!nextBranch.first)
                            distance *= -1;
                        nextJunction = track.outJunction;
                    } else {
                        yield break;
                    }
                }

                if (nextBranch == null)
                    yield break;

                if (nextJunction != null && nextJunction.inBranch.track == track)
                {
                    Debug.Log(nextJunction.selectedBranch.ToString());
                    yield return new JunctionEvent(distanceFromStart, true, nextJunction.selectedBranch);
                }
                track = nextBranch.track;
                startSpan = nextBranch.first ? 0.0 : nextBranch.track.GetPointSet().span;
            }
        }
    }
}