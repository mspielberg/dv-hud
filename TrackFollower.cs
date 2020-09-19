using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public static class TrackFollower
    {
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
                    } else {
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
    }
}