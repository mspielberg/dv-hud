using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public abstract class TrackEvent
    {
        public readonly double span;

        protected TrackEvent(double span)
        {
            this.span = span;
        }

        abstract public TrackEvent WithSpan(double span, bool direction);

        abstract public bool Direction { get; }
    }

    public class TrackChangeEvent : TrackEvent
    {
        public readonly TrackID ID;

        public TrackChangeEvent(double span, TrackID ID)
        : base(span)
        {
            this.ID = ID;
        }

        public override TrackEvent WithSpan(double span, bool direction)
        {
            return new TrackChangeEvent(span, this.ID);
        }

        public override string ToString()
        {
            return $"{span}: New track {ID}";
        }

        public override bool Direction => true;
    }

    public class JunctionEvent : TrackEvent
    {
        public readonly bool direction;
        public readonly Junction junction;

        public JunctionEvent(double span, bool direction, Junction junction)
        : base(span)
        {
            this.direction = direction;
            this.junction = junction;
        }

        public override TrackEvent WithSpan(double span, bool direction)
        {
            return new JunctionEvent(span, direction, junction);
        }

        public override string ToString()
        {
            var directionString = junction.selectedBranch == 0 ? "left" : "right";
            return $"{span} {direction}: Junction setting {directionString}";
        }

        public override bool Direction => direction;
    }

    public class SpeedLimitEvent : TrackEvent
    {
        private readonly bool direction;
        public readonly int limit;

        public SpeedLimitEvent(double span, bool direction, int limit)
        : base(span)
        {
            this.direction = direction;
            this.limit = limit;
        }

        public override TrackEvent WithSpan(double span, bool direction)
        {
            return new SpeedLimitEvent(span, direction, this.limit);
        }

        public override string ToString()
        {
            return $"{span} {direction}: Speed limit {limit}";
        }

        public override bool Direction => direction;
    }

    public class DualSpeedLimitEvent : SpeedLimitEvent
    {
        public readonly int rightLimit;
        public DualSpeedLimitEvent(double span, bool direction, int limit, int rightLimit)
        : base(span, direction, limit)
        {
            this.rightLimit = rightLimit;
        }

        public override TrackEvent WithSpan(double span, bool direction)
        {
            return new DualSpeedLimitEvent(span, direction, this.limit, this.rightLimit);
        }

        public override string ToString()
        {
            return $"{span} {Direction}: Junction speed limit {limit}, {rightLimit}";
        }
    }

    public class GradeEvent : TrackEvent
    {
        public override bool Direction { get; }
        public readonly float grade;
        public GradeEvent(double span, bool direction, float grade)
        : base(span)
        {
            Direction = direction;
            this.grade = grade;
        }

        public override TrackEvent WithSpan(double span, bool direction)
        {
            return new GradeEvent(span, direction, grade);
        }

        public override string ToString()
        {
            return $"{span} {Direction}: Grade {grade}%";
        }
    }

    public static class TrackEventsExtension
    {
        public static IEnumerable<TrackEvent> RelativeFromSpan(this IEnumerable<TrackEvent> events, double startSpan, bool direction)
        {
            foreach (var ev in direction ? events : events.Reverse())
            {
                double relativeSpan = direction ? ev.span - startSpan : startSpan - ev.span;
                if (relativeSpan >= 0)
                    yield return ev.WithSpan(relativeSpan, ev.Direction == direction);
            }
        }

        public static IEnumerable<TrackEvent> Offset(this IEnumerable<TrackEvent> events, double offset)
        {
            foreach (var ev in events)
            {
                yield return ev.WithSpan(ev.span + offset, ev.Direction);
            }
        }

        public static IEnumerable<TrackEvent> ResolveJunctionSpeedLimits(this IEnumerable<TrackEvent> events)
        {
            int i = 1;
            foreach (var ev in events)
            {
                if (ev is DualSpeedLimitEvent e)
                {
                    if (!e.Direction)
                        continue;
                    var nextJunctionEvent = events.Skip(i).OfType<JunctionEvent>().FirstOrDefault(ev => ev.Direction);
                    if (nextJunctionEvent == null)
                    {
                        yield return ev;
                    }
                    else
                    {
                        if (nextJunctionEvent.junction.selectedBranch == 0)
                            yield return new SpeedLimitEvent(e.span, true, e.limit);
                        else
                            yield return new SpeedLimitEvent(e.span, true, e.rightLimit);
                    }
                }
                else
                {
                    yield return ev;
                }
                i++;
            }
        }

        /// <summary>Filters out reversed and duplicate speed limit events.</summary>
        public static IEnumerable<TrackEvent> FilterRedundantSpeedLimits(this IEnumerable<TrackEvent> events)
        {
            int prevSpeedLimit = -1;
            foreach (TrackEvent ev in events)
            {
                if (ev is SpeedLimitEvent sle)
                {
                    if (sle.Direction && sle.limit != prevSpeedLimit)
                    {
                        prevSpeedLimit = sle.limit;
                        yield return sle;
                    }
                }
                else
                {
                    yield return ev;
                }
            }
        }

        public const double GradeChangeInterval = 100;

        public static IEnumerable<TrackEvent> FilterGradeEvents(this IEnumerable<TrackEvent> events, float prevGrade)
        {
            foreach (TrackEvent ev in events)
            {
                if (ev is GradeEvent gradeEvent)
                {
                    if (ev.Direction && prevGrade != gradeEvent.grade)
                    {
                        prevGrade = gradeEvent.grade;
                        yield return gradeEvent;
                    }
                }
                else
                {
                    yield return ev;
                }
            }
        }

        public static IEnumerable<TrackEvent> ExceptUnnamedTracks(this IEnumerable<TrackEvent> events)
        {
            return events.Where(ev =>
                !(ev is TrackChangeEvent tce) || !tce.ID.IsGeneric()
            );
        }
    }
}