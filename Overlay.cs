using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    class Overlay : MonoBehaviour
    {
        public static Overlay? instance;
        static GUIStyle? noTitleBar;
        static GUIStyle? noWrap;
        static GUIStyle? rightAlign;

        public Overlay()
        {
            instance = this;
        }

        /// Can only be called during OnGui()
        private void InitializeStyles()
        {
            if (noTitleBar != null)
                return;

            noTitleBar = new GUIStyle(GUI.skin.window);
            noTitleBar.normal.background = null;
            noTitleBar.border.top = noTitleBar.border.bottom;
            noTitleBar.padding.top = noTitleBar.padding.bottom;
            noTitleBar.onNormal = noTitleBar.normal;

            rightAlign = new GUIStyle(noWrap);
            rightAlign.alignment = TextAnchor.MiddleRight;

            noWrap = new GUIStyle(GUI.skin.label);
            noWrap.wordWrap = false;
        }

        public void OnGUI()
        {
            if (!Main.enabled)
                return;
            InitializeStyles();

            Main.settings.hudPosition = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                Main.settings.hudPosition,
                DrawDrivingInfoWindow,
                "",
                noTitleBar);
        }

        void DrawDrivingInfoWindow(int windowID)
        {
            foreach (var group in Registry.GetProviders(PlayerManager.Car.carType))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    if (dp.Enabled)
                        GUILayout.Label(dp.Label, noWrap);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    if (dp.Enabled)
                        GUILayout.Label(dp.GetValue(PlayerManager.Car), noWrap);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (Main.settings.showCarList)
                DrawCarList();

            if (Main.settings.showTrackInfo)
                DrawUpcomingEvents();

            GUI.DragWindow();
        }

        void DrawCarList()
        {
            IEnumerable<TrainCar> cars = PlayerManager.Car.trainset.cars.AsReadOnly();
            if (!cars.First().IsLoco && cars.Last().IsLoco)
                cars = cars.Reverse();

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label("Cars:", noWrap);
            foreach (TrainCar car in cars)
                GUILayout.Label(car.ID, noWrap);
            GUILayout.EndVertical();

            if (Main.settings.showCarStress)
                DrawCarStress(cars);
            if (Main.settings.showCarJobs)
                DrawCarJobs(cars);
            if (Main.settings.showCarDestinations)
                DrawCarDestinations(cars);

            GUILayout.EndHorizontal();
        }

        void DrawCarStress(IEnumerable<TrainCar> cars)
        {
            var derailThreshold = SimManager.instance.derailBuildUpThreshold;

            GUILayout.BeginVertical();
            GUILayout.Label("Stress", noWrap);
            foreach (TrainCar car in cars)
            {
                var buildup = car.GetComponent<TrainStress>().derailBuildUp;
                var buildupPct = buildup / derailThreshold * 100;
                GUI.contentColor = Color.HSVToRGB(Mathf.Lerp(1f/3f, 0, (buildupPct - 30f) / 40f), 1f, 1f);
                GUILayout.Label($"{buildupPct.ToString("F0")} %", rightAlign);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        void DrawCarJobs(IEnumerable<TrainCar> cars)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Job", noWrap);
            foreach (TrainCar car in cars)
                GUILayout.Label(JobChainController.GetJobOfCar(car)?.ID ?? " ", noWrap);
            GUILayout.EndVertical();
        }

        void DrawCarDestinations(IEnumerable<TrainCar> cars)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Destination", noWrap);
            foreach (TrainCar car in cars)
                GUILayout.Label(
                    GetNextDestinationTrack(JobChainController.GetJobOfCar(car), car.logicCar)?.ID?.FullDisplayID ?? " ",
                    noWrap);
            GUILayout.EndVertical();
        }

        Track? GetNextDestinationTrack(Task task, Car car)
        {
            var data = task.GetTaskData();
            if (data.type == TaskType.Transport || data.type == TaskType.Warehouse)
                return (data.state == TaskState.InProgress && data.cars.Contains(car)) ? data.destinationTrack : null;
            return data.nestedTasks.Select(t => GetNextDestinationTrack(t, car)).FirstOrDefault(track => track != null);
        }

        Track? GetNextDestinationTrack(Job job, Car car)
        {
            return job?.tasks.Select(task => GetNextDestinationTrack(task, car)).FirstOrDefault(track => track != null);
        }

        string DumpTask(Task task, int indent = 0)
        {
            var data = task.GetTaskData();
            var indentStr = new string(' ', indent * 2);
            switch (data.type)
            {
                case TaskType.Transport:
                case TaskType.Warehouse:
                    var carsStr = string.Join(",", data.cars.Select(car => car.ID));
                    var destStr = data.destinationTrack.ID.FullDisplayID;
                    return $"{indentStr}{data.warehouseTaskType}:{carsStr}:{destStr}:{data.state}";
                case TaskType.Parallel:
                case TaskType.Sequential:
                    return $"{indentStr}{data.type}\n{string.Join("\n", data.nestedTasks.Select(t => DumpTask(t, indent+1)))}";
            }
            return "";
        }

        string DumpJob(Job job)
        {
            return string.Join("\n", job.tasks.Select(DumpTask));
        }

        void DrawUpcomingEvents()
        {
            var bogie = PlayerManager.Car.Bogies[0];
            var track = bogie.track;
            var startSpan = bogie.traveller.Span;
            var locoDirection = (PlayerManager.LastLoco?.GetComponent<LocoControllerBase>()?.reverser ?? 0f) >= 0f;
            var direction = !locoDirection ^ (bogie.trackDirection > 0);
            var currentGrade = TrackIndexer.Grade(bogie.point1) * (direction ? 1 : -1);

            var events = TrackFollower.FollowTrack(
                track,
                startSpan,
                direction ? Main.settings.maxEventSpan : -Main.settings.maxEventSpan);

            var eventDescriptions = events
                .ExceptUnnamedTracks()
                .ResolveJunctionSpeedLimits()
                .FilterRedundantSpeedLimits()
                .FilterGradeEvents(currentGrade)
                .Take(Main.settings.maxEventCount)
                .Select(ev => {
                    switch (ev)
                    {
                    case TrackChangeEvent e:
                        return (e.span, e.ID.ToString());
                    case JunctionEvent e:
                        return (e.span, e.selectedBranch == 0 ? "Left" : "Right");
                    case DualSpeedLimitEvent e:
                        return (e.span, $"{e.limit} / {e.rightLimit} km/h");
                    case SpeedLimitEvent e:
                        return (e.span, $"{e.limit} km/h");
                    case GradeEvent e:
                        return (e.span, $"{e.grade.ToString("F1")}%");
                    default:
                        return (0.0, $"Unknown event: {ev}");
                    }
                });

            GUILayout.BeginHorizontal("box");

            GUILayout.BeginVertical();
            foreach ((double span, string desc) in eventDescriptions)
                GUILayout.Label($"{(Math.Round(span / 10) * 10).ToString("F0")} m", rightAlign);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            foreach ((double span, string desc) in eventDescriptions)
                GUILayout.Label(desc);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
    }
}