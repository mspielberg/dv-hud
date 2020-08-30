using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    enum TrackEventType
    {
        Junction,
        SpeedLimit,
        DualSpeedLimit,
        Grade,
    }

    abstract class TrackEvent
    {
        public readonly double span;

        protected TrackEvent(double span)
        {
            this.span = span;
        }

        abstract public TrackEvent WithSpan(double span);
    }

    class TrackChangeEvent : TrackEvent
    {
        public readonly TrackID ID;

        public TrackChangeEvent(double span, TrackID ID)
        : base(span)
        {
            this.ID = ID;
        }

        public override TrackEvent WithSpan(double span)
        {
            return new TrackChangeEvent(span, this.ID);
        }

        public override string ToString()
        {
            return $"{span}: New track {ID}";
        }
    }

    class JunctionEvent : TrackEvent
    {
        public readonly bool direction;
        public readonly int selectedBranch;

        public JunctionEvent(double span, bool direction, int selectedBranch)
        : base(span)
        {
            this.direction = direction;
            this.selectedBranch = selectedBranch;
        }

        public override TrackEvent WithSpan(double span)
        {
            return new JunctionEvent(span, true, this.selectedBranch);
        }

        public override string ToString()
        {
            var directionString = selectedBranch == 0 ? "left" : "right";
            return $"{span} {direction}: Junction setting {directionString}";
        }
    }

    class SpeedLimitEvent : TrackEvent
    {
        public readonly bool direction;
        public readonly int limit;

        public SpeedLimitEvent(double span, bool direction, int limit)
        : base(span)
        {
            this.direction = direction;
            this.limit = limit;
        }

        public override TrackEvent WithSpan(double span)
        {
            return new SpeedLimitEvent(span, true, this.limit);
        }

        public override string ToString()
        {
            return $"{span} {direction}: Speed limit {limit}";
        }
    }

    class DualSpeedLimitEvent : SpeedLimitEvent
    {
        public readonly int rightLimit;
        public DualSpeedLimitEvent(double span, bool direction, int limit, int rightLimit)
        : base(span, direction, limit)
        {
            this.rightLimit = rightLimit;
        }

        public override TrackEvent WithSpan(double span)
        {
            return new DualSpeedLimitEvent(span, true, this.limit, this.rightLimit);
        }

        public override string ToString()
        {
            return $"{span} {direction}: Junction speed limit {limit}, {rightLimit}";
        }
    }

    class GradeEvent : TrackEvent
    {
        public readonly float grade;
        public GradeEvent(double span, float grade)
        : base(span)
        {
            this.grade = grade;
        }

        public override TrackEvent WithSpan(double span)
        {
            return new GradeEvent(span, grade);
        }

        public GradeEvent Reversed()
        {
            return new GradeEvent(span, -grade);
        }

        public override string ToString()
        {
            return $"{span}: Grade {grade}%";
        }
    }

    static class TrackEventsExtension
    {
        public static IEnumerable<TrackEvent> RelativeFromSpan(this IEnumerable<TrackEvent> events, double startSpan, bool direction)
        {
            // Debug.Log($"Filtering events based on {startSpan}, {direction}");
            foreach (var ev in (direction ? events : events.Reverse()))
            {
                double relativeSpan = direction ? ev.span - startSpan : startSpan - ev.span;
                if (relativeSpan >= 0)
                {
                    switch (ev)
                    {
                        case GradeEvent gradeEvent:
                            yield return (direction ? gradeEvent : gradeEvent.Reversed()).WithSpan(relativeSpan);
                            break;
                        case JunctionEvent junctionEvent:
                            if (junctionEvent.direction == direction)
                                yield return junctionEvent.WithSpan(relativeSpan);
                            break;
                        case DualSpeedLimitEvent dualLimit:
                            if (dualLimit.direction == direction)
                                yield return dualLimit.WithSpan(relativeSpan);
                            break;
                        case SpeedLimitEvent speedLimit:
                            if (speedLimit.direction == direction)
                                yield return speedLimit.WithSpan(relativeSpan);
                            break;
                    }
                }
            }
        }

        public static IEnumerable<TrackEvent> Offset(this IEnumerable<TrackEvent> events, double offset)
        {
            foreach (var ev in events)
            {
                // Debug.Log($"setting span to {ev.span} + {offset} = {ev.span+offset}");
                yield return ev.WithSpan(ev.span + offset);
            }
        }
    }
}