using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public static class TrackFollower
    {
        public static GradeEvent? GetGrade(RailTrack track, double startSpan, bool direction)
        {
            var events = FollowTrack(track, startSpan, direction ? float.NegativeInfinity : float.PositiveInfinity);
            return events
                .OfType<GradeEvent>()
                .FirstOrDefault(ev => !ev.Direction);
        }

        public static float? GetSpeedLimit(RailTrack track, double startSpan, bool direction)
        {
            var events = FollowTrack(track, startSpan, direction ? float.NegativeInfinity : float.PositiveInfinity);
            return events
                .OfType<SpeedLimitEvent>()
                .FirstOrDefault(ev => !ev.Direction)
                ?.limit;
        }

        public static IEnumerable<TrackEvent> FollowTrack(RailTrack track, double startSpan, double distance)
        {
            const int MAX_ITERATIONS = 100;
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
                    }
                    else
                    {
                        yield break;
                    }
                }

                if (nextBranch == null)
                    yield break;

                if (nextJunction != null && nextJunction.inBranch.track == track)
                    yield return new JunctionEvent(distanceFromStart, true, nextJunction);

                track = nextBranch.track;
                startSpan = nextBranch.first ? 0.0 : nextBranch.track.GetPointSet().span;
            }
        }

        private static readonly Func<TrackID, string> SubYardSelector = (TrackID id) => id.FullDisplayID;
        private static readonly Func<TrackID, string> TrackSelector = (TrackID id) => id.TrackPartOnly;

        public static string DescribeJunctionBranches(Junction junction)
        {
            var left = DescribeBranch(junction.outBranches[0]);
            var right = DescribeBranch(junction.outBranches[1]);
            if (left == null || right == null)
                return junction.selectedBranch == 0 ? "<<<" : ">>>";

            var selector =
                (left.yardId != right.yardId || left.SignIDSubYardPart != right.SignIDSubYardPart) ? SubYardSelector :
                TrackSelector;

            return junction.selectedBranch == 0
                ? $"<color=lime>{selector(left)}</color> <<< {selector(right)}"
                : $"{selector(left)} >>> <color=lime>{selector(right)}</color>";
        }

        private static IEnumerable<Junction.Branch> GetNextBranches(Junction.Branch start) =>
            (start.first ? start.track.GetAllOutBranches() : start.track.GetAllInBranches()) ?? Enumerable.Empty<Junction.Branch>();

        private static readonly Dictionary<Junction.Branch, TrackID?> descriptions = new Dictionary<Junction.Branch, TrackID?>();

        private static TrackID? DescribeBranch(Junction.Branch startBranch)
        {
            if (descriptions.TryGetValue(startBranch, out var trackID))
                return trackID;

            // Main.DebugLog($"Starting BFS at {startBranch.track.logicTrack.ID}");
            var visited = new HashSet<RailTrack>();
            var queue = new Queue<Junction.Branch>(GetNextBranches(startBranch));
            while (queue.Count > 0)
            {
                var branch = queue.Dequeue();
                // Main.DebugLog($"Examining {branch}, track={branch.track}, logicTrack={branch.track.logicTrack}, ID={branch.track.logicTrack.ID}");
                trackID = branch.track.logicTrack.ID;
                if (!trackID.IsGeneric())
                {
                    descriptions[startBranch] = trackID;
                    return trackID;
                }
                // avoid cycles
                if (visited.Contains(branch.track))
                    continue;
                visited.Add(branch.track);
                foreach (var next in GetNextBranches(branch))
                    queue.Enqueue(next);
            }
            // Main.DebugLog($"BFS ended without result");
            descriptions[startBranch] = null;
            // Main.DebugLog($"returning null");
            return null;
        }
    }
}