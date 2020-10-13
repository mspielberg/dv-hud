using CommandTerminal;
using DV.PointSet;
using DV.Signs;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public static class Commands
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
        public static class RegisterCommandsPatch
        {
            public static void Postfix()
            {
                Register();
            }
        }

        private static void Register(string name, Action<CommandArg[]> proc)
        {
            if (Terminal.Shell == null)
                return;
            if (Terminal.Shell.Commands.Remove(name.ToUpper()))
                Main.DebugLog($"replacing existing command {name}");
            else
                Terminal.Autocomplete.Register(name);
            Terminal.Shell.AddCommand(name, proc);
        }

        private static string GetPath(Component c)
        {
            return string.Join("/", c.GetComponentsInParent<Transform>(true).Reverse().Select(c => c.name));
        }

        private static string DumpHierarchy(GameObject gameObject)
        {
            return string.Join("\n", gameObject.GetComponentsInChildren<Component>().Select(c => $"{GetPath(c)} {c.GetType()}"));
        }

        public static void Register()
        {
            Register("hud.dumpTrack", _ =>
            {
                if (PlayerManager.Car == null)
                    return;
                var bogie = PlayerManager.Car.Bogies[0];
                var track = bogie.track;
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

            Register("hud.raycast", _ =>
            {
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

            Register("hud.trackevents", _ =>
            {
                var transform = PlayerManager.PlayerTransform;
                (RailTrack startTrack, EquiPointSet.Point? point) = RailTrack.GetClosest(transform.position);
                if (startTrack == null)
                    return;
                var events = TrackIndexer.GetTrackEvents(startTrack);

                Terminal.Log($"All on track {startTrack.logicTrack.ID}");
                foreach (var trackEvent in events) {
                    Terminal.Log(trackEvent.ToString());
                    Main.DebugLog(trackEvent.ToString());
                }

                var pointForward = point?.forward ?? Vector3.zero;
                var pointSpan = point?.span ?? 0;
                var trackDirection = Vector3.Dot(transform.forward, pointForward) > 0f;
                Terminal.Log($"From {pointSpan} {trackDirection}:");
                foreach (var trackEvent in TrackIndexer.GetTrackEvents(startTrack, trackDirection, pointSpan)) {
                    Terminal.Log(trackEvent.ToString());
                    Main.DebugLog(trackEvent.ToString());
                }
            });

            Register("hud.followTrack", _ =>
            {
                var transform = PlayerManager.PlayerTransform;
                (RailTrack startTrack, EquiPointSet.Point? point) = RailTrack.GetClosest(transform.position);
                if (startTrack == null)
                    return;
                var pointForward = point?.forward ?? Vector3.zero;
                var pointSpan = point?.span ?? 0;
                var trackDirection = Vector3.Dot(transform.forward, pointForward) > 0f ? 1 : -1;

                var trackEvents = TrackFollower.FollowTrack(startTrack, pointSpan, trackDirection * 1000f);
                foreach (var trackEvent in trackEvents)
                {
                    Terminal.Log(trackEvent.ToString());
                    Main.DebugLog(trackEvent.ToString());
                }
            });

            Register("hud.findCarOnJunction", _ =>
            {
                var transform = PlayerManager.PlayerTransform;
                (RailTrack startTrack, EquiPointSet.Point? point) = RailTrack.GetClosest(transform.position);
                if (startTrack == null)
                    return;
                var pointForward = point?.forward ?? Vector3.zero;
                var pointSpan = point?.span ?? 0;
                var trackDirection = Vector3.Dot(transform.forward, pointForward) > 0f ? 1 : -1;

                var trackEvents = TrackFollower.FollowTrack(startTrack, pointSpan, trackDirection * 1000f);
                var junction = trackEvents.OfType<JunctionEvent>().FirstOrDefault();
                if (junction == null)
                {
                    Terminal.Log("no junction");
                    return;
                }

                Terminal.Log(Overlay.GetCarOnJunction(junction.junction)?.ID ?? "no car on junction");
            });

            Register("hud.dumpInterior", _ =>
            {
                if (PlayerManager.Car?.loadedInterior != null)
                {
                    var dump = DumpHierarchy(PlayerManager.Car.loadedInterior);
                    Terminal.Log(dump);
                    Debug.Log(dump);
                }
            });

            Register("hud.getSignedGrade", _ =>
            {
                var transform = PlayerManager.PlayerTransform;
                (RailTrack startTrack, EquiPointSet.Point? point) = RailTrack.GetClosest(transform.position);
                if (startTrack == null)
                    return;
                var pointForward = point?.forward ?? Vector3.zero;
                var pointSpan = point?.span ?? 0;
                var trackDirection = Vector3.Dot(transform.forward, pointForward) > 0f;
                Terminal.Log($"{TrackFollower.GetGrade(startTrack, pointSpan, trackDirection)}");
            });

            Register("hud.getSpeedLimit", _ =>
            {
                var transform = PlayerManager.PlayerTransform;
                (RailTrack startTrack, EquiPointSet.Point? point) = RailTrack.GetClosest(transform.position);
                if (startTrack == null)
                    return;
                var pointForward = point?.forward ?? Vector3.zero;
                var pointSpan = point?.span ?? 0;
                var trackDirection = Vector3.Dot(transform.forward, pointForward) > 0f;
                Terminal.Log($"{TrackFollower.GetSpeedLimit(startTrack, pointSpan, trackDirection)}");
            });
        }
    }
}