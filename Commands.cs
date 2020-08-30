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

}