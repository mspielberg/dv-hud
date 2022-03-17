using DV.Logic.Job;
using System;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public static class EventList
    {
        private static string GetJunctionEventDescription(JunctionEvent e)
        {
            var description = TrackFollower.DescribeJunctionBranches(e.junction);
            var car = GetCarOnJunction(e.junction);
            var carText = (car is Car && car != PlayerManager.Car.logicCar)
                ? $" <color=orange>({car.ID})</color>" : "";
            return description + carText;
        }

        private static string GetSpeedLimitEventDescription(SpeedLimitEvent e)
        {
            var currentSpeed = Mathf.Abs(PlayerManager.Car.GetForwardSpeed() * 3.6f);
            var color = "white";
            if (currentSpeed > e.limit + 5f)
                color = e.span < 500f ? "red" : e.span < 1000f ? "orange" : "yellow";
            else if (currentSpeed < e.limit - 10f)
                color = "lime";
            return $"<color={color}>{e.limit} km/h</color>";
        }

        public static void DrawUpcomingEvents()
        {
            if (!PlayerManager.Car)
                return;

            var trackInfoSettings = Main.settings.trackInfoSettings;

            var bogie = PlayerManager.Car.Bogies[1];
            var track = bogie.track;
            if (track == null)
                return;
            var startSpan = bogie.traveller.Span;
            var locoDirection = PlayerManager.LastLoco == null || PlayerManager.LastLoco.GetComponent<LocoControllerBase>()?.reverser >= 0f;
            var direction = !locoDirection ^ (bogie.trackDirection > 0);
            var currentGrade = TrackIndexer.Grade(bogie.point1) * (direction ? 1 : -1);

            var events = TrackFollower.FollowTrack(
                track,
                startSpan,
                direction ? trackInfoSettings.maxEventSpan : -trackInfoSettings.maxEventSpan);

            var eventDescriptions = events
                .ExceptUnnamedTracks()
                .ResolveJunctionSpeedLimits()
                .FilterRedundantSpeedLimits()
                .FilterGradeEvents(currentGrade)
                .Take(trackInfoSettings.maxEventCount)
                .TakeWhile(ev => ev.span < trackInfoSettings.maxEventSpan)
                .Select(ev => ev switch
                    {
                        TrackChangeEvent e => (e.span, e.ID.ToString()),
                        JunctionEvent e => (e.span, GetJunctionEventDescription(e)),
                        DualSpeedLimitEvent e => (e.span, $"{e.limit} / {e.rightLimit} km/h"),
                        SpeedLimitEvent e => (e.span, GetSpeedLimitEventDescription(e)),
                        GradeEvent e => (e.span, $"{e.grade:F1} %"),
                        _ => (0.0, $"Unknown event: {ev}"),
                    })
                .ToList();

            static string FormatSpan((double span, string description) ev) => $"{Math.Round(ev.span / 10) * 10:F0} m";
            var blanks = Enumerable.Repeat(" ", trackInfoSettings.maxEventCount - eventDescriptions.Count);

            GUILayout.BeginHorizontal("box", GUILayout.ExpandHeight(true));
            Overlay.DrawColumn(
                eventDescriptions.Select(FormatSpan).Concat(blanks),
                style: Styles.rightAlign);
            Overlay.DrawColumn(
                eventDescriptions.Select(p => p.Item2).Concat(blanks),
                style: Styles.richText);
            GUILayout.EndHorizontal();
        }

        public static Car? GetCarOnJunction(Junction junction)
        {
            static double DistanceToBranch(Junction.Branch branch, TrainCar car)
            {
                return car.Bogies
                    .Where(bogie => bogie.track == branch.track)
                    .Min(bogie => branch.first ? bogie.traveller.Span : branch.track.logicTrack.length - bogie.traveller.Span);
            }

            static (double, Car)? ClosestCar(Junction.Branch branch)
            {
                var logicTrack = branch.track.logicTrack;
                var logicCarToTrainCar = SingletonBehaviour<IdGenerator>.Instance.logicCarToTrainCar;
                var allCars = logicTrack.GetCarsFullyOnTrack().Concat(logicTrack.GetCarsPartiallyOnTrack());
                if (!allCars.Any())
                    return null;
                var byDistance = allCars.ToDictionary(car => DistanceToBranch(branch, logicCarToTrainCar[car]));
                var minDistance = byDistance.Keys.Min();
                return (minDistance, byDistance[minDistance]);
            }

            const double SpanTolerance = 7.0;
            var branches = junction.outBranches.Append(junction.inBranch);
            var closest = branches.Select(ClosestCar).OfType<(double, Car)>().OrderBy(p => p.Item1).FirstOrDefault();

            if (closest.Item1 < SpanTolerance && closest.Item2 != null)
                return closest.Item2;
            return null;
        }
    }
}