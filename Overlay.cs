using DV.Logic.Job;
using DV.Simulation.Brake;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public class Overlay : MonoBehaviour
    {
        private const int ColumnSpacing = 10;

        public static Overlay? instance;
        private static GUIStyle? noChrome;
        private static GUIStyle? noWrap;
        private static GUIStyle? rightAlign;

        private bool overlayEnabled = false;

        public void Start()
        {
            // Wait for a frame because for some reason RaycastAll doesn't detect colliders if called on the same frame.
            _ = StartCoroutine(DelayedEnable());
            instance = this;
        }

        private IEnumerator DelayedEnable()
        {
            yield return null;
            overlayEnabled = true;
            Main.DebugLog($"Overlay enabled on frame {Time.frameCount}. Fixed update {Time.fixedTime / Time.fixedDeltaTime}");
        }

        /// <summary>Can only be called during OnGui()</summary>
        private void InitializeStyles()
        {
            if (noChrome != null)
            {
                return;
            }

            noChrome = new GUIStyle(GUI.skin.window);
            noChrome.normal.background = null;
            noChrome.onNormal = noChrome.normal;

            noWrap = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false
            };

            rightAlign = new GUIStyle(noWrap)
            {
                alignment = TextAnchor.MiddleRight
            };
        }

        private static Rect prevRect = new Rect();

        public void OnGUI()
        {
            if (!overlayEnabled)
            {
                Main.DebugLog($"OnGUI called before Start on frame {Time.frameCount}. Fixed update {Time.fixedTime / Time.fixedDeltaTime}");
                return;
            }
            if (!Main.enabled)
                return;
            if (PlayerManager.Car == null)
                return;

            InitializeStyles();

            if (prevRect == new Rect())
                prevRect.position = Main.settings.hudPosition;
            prevRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                prevRect,
                DrawDrivingInfoWindow,
                "",
                noChrome);
            Main.settings.hudPosition = prevRect.position;
        }

        public void ResetPosition()
        {
            prevRect = new Rect();
        }

        private void DrawDrivingInfoWindow(int windowID)
        {
            foreach (var group in Registry.GetProviders(PlayerManager.Car.carType).Where(g => g.Count > 0))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.BeginVertical();
                foreach (var dp in group)
                {
                    if (dp.Enabled)
                        GUILayout.Label(dp.Label, noWrap);
                }
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                foreach (var dp in group)
                {
                    if (dp.Enabled)
                        GUILayout.Label(dp.GetValue(PlayerManager.Car), noWrap);
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (Main.settings.showTrackInfo)
                DrawUpcomingEvents();

            if (Main.settings.showCarList)
                DrawCarList();

            GUI.DragWindow();
        }

        private readonly struct CarGroup
        {
            public readonly int startIndex;
            public readonly int endIndex;
            public readonly List<TrainCar> cars;
            public readonly float maxStress;
            public readonly Job? job;
            public readonly Track? nextDestination;

            public readonly float minBrakePipePressure;
            public readonly float minBrakeReservoirPressure;
            public readonly float maxBrakeCylinderPressure;
            public readonly float maxBrakeFactor;

            public CarGroup(
                int startIndex,
                int endIndex,
                List<TrainCar> cars,
                float maxStress,
                Job? job,
                Track? nextDestination,
                float minBrakePipePressure,
                float minBrakeReservoirPressure,
                float maxBrakeCylinderPressure,
                float maxBrakeFactor)
            {
                this.startIndex = startIndex;
                this.endIndex = endIndex;
                this.cars = cars;
                this.maxStress = maxStress;
                this.job = job;
                this.nextDestination = nextDestination;
                this.minBrakePipePressure = minBrakePipePressure;
                this.minBrakeReservoirPressure = minBrakeReservoirPressure;
                this.maxBrakeCylinderPressure = maxBrakeCylinderPressure;
                this.maxBrakeFactor = maxBrakeFactor;
            }

            public override string ToString()
            {
                return $"{startIndex}-{endIndex} {string.Join(",", cars.Select(c => c.ID))} {maxStress} {job?.ID}";
            }
        }

        private float GetAuxReservoirPressure(TrainCar car) =>
            car.IsLoco ? car.brakeSystem.mainReservoirPressure
            : Registry.GetProvider(RegistryKeys.AllCars, "Aux reservoir pressure").Map(
                p => float.Parse(p.GetValue(car))) ?? default;

        private float GetBrakeCylinderPressure(TrainCar car) =>
            Registry.GetProvider(RegistryKeys.AllCars, "Brake cylinder pressure").Map(
                p => float.Parse(p.GetValue(car))) ?? default;

        private IEnumerable<CarGroup> GetCarGroups(IEnumerable<TrainCar> cars, bool individual)
        {
            Job? prevJob = null;
            Track? prevDestTrack = null;
            int startIndex = 0;
            float maxStress = 0f;

            var firstCar = cars.First();
            var groupCars = new List<TrainCar>(){ firstCar };
            float minBrakePipePressure = firstCar.brakeSystem.brakePipePressure;
            float minBrakeReservoirPressure = GetAuxReservoirPressure(firstCar);
            float maxBrakeCylinderPressure = GetBrakeCylinderPressure(firstCar);
            float maxBrakeFactor = firstCar.brakeSystem.brakingFactor;

            int i = 0;
            foreach (var car in cars)
            {
                float carStress = car.GetComponent<TrainStress>().derailBuildUp;
                Job? job = JobChainController.GetJobOfCar(car);
                Track? nextDestination = GetNextDestinationTrack(job, car.logicCar);
                BrakeSystem brakeSystem = car.brakeSystem;
                float pipePressure = brakeSystem.brakePipePressure;
                float auxReservoirPressure = GetAuxReservoirPressure(car);
                float brakeCylinderPressure = GetBrakeCylinderPressure(car);
                if (individual || nextDestination == null || nextDestination != prevDestTrack || job != prevJob)
                {
                    // complete previous group
                    if (i > 0)
                    {
                        yield return new CarGroup(
                            startIndex,
                            i,
                            groupCars,
                            maxStress,
                            prevJob,
                            prevDestTrack,
                            minBrakePipePressure,
                            minBrakeReservoirPressure,
                            maxBrakeCylinderPressure,
                            maxBrakeFactor);
                    }

                    // start new group
                    startIndex = i;
                    groupCars = new List<TrainCar>() { car };
                    prevJob = job;
                    prevDestTrack = nextDestination;
                    maxStress = carStress;
                    minBrakePipePressure = pipePressure;
                    minBrakeReservoirPressure = auxReservoirPressure;
                    maxBrakeCylinderPressure = brakeCylinderPressure;
                    maxBrakeFactor = brakeSystem.brakingFactor;
                }
                else
                {
                    groupCars.Add(car);
                    if (carStress > maxStress)
                        maxStress = carStress;
                    if (pipePressure < minBrakePipePressure)
                        minBrakeReservoirPressure = auxReservoirPressure;
                    if (auxReservoirPressure < minBrakeReservoirPressure)
                        minBrakeReservoirPressure = auxReservoirPressure;
                    if (brakeCylinderPressure > maxBrakeCylinderPressure)
                        maxBrakeCylinderPressure = brakeCylinderPressure;
                    if (brakeSystem.brakingFactor > maxBrakeFactor)
                        maxBrakeFactor = brakeSystem.brakingFactor;
                }
                i++;
            }

            // complete last group
            yield return new CarGroup(
                startIndex,
                i,
                groupCars,
                maxStress,
                prevJob,
                prevDestTrack,
                minBrakePipePressure,
                minBrakeReservoirPressure,
                maxBrakeCylinderPressure,
                maxBrakeFactor);
        }

        private const char EnDash = '\u2013';
        private void DrawCarList()
        {
            IEnumerable<TrainCar> cars = PlayerManager.Car.trainset.cars.AsReadOnly();
            if (!cars.First().IsLoco && cars.Last().IsLoco)
                cars = cars.Reverse();

            var groups = GetCarGroups(cars, !Main.settings.groupCarsByJob);

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label(" ", noWrap);
            foreach (CarGroup group in groups)
            {
                if (group.startIndex + 1 == group.endIndex)
                    GUILayout.Label(group.endIndex.ToString(), noWrap);
                else
                    GUILayout.Label($"{group.startIndex + 1}{EnDash}{group.endIndex}", noWrap);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("ID", noWrap);
            foreach (CarGroup group in groups)
                GUILayout.Label(group.cars[0].ID, noWrap);
            GUILayout.EndVertical();

            if (Main.settings.showCarStress)
                DrawCarStress(groups);
            if (Main.settings.showCarJobs)
                DrawCarJobs(groups);
            if (Main.settings.showCarDestinations)
                DrawCarDestinations(groups);

            if(Main.settings.showCarBrakeStatus)
                DrawCarBrakeStatus(groups);

            GUILayout.EndHorizontal();
        }

        private void DrawCarStress(IEnumerable<CarGroup> groups)
        {
            var derailThreshold = SimManager.instance.derailBuildUpThreshold;

            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Stress", noWrap);
            foreach (CarGroup group in groups)
            {
                var buildup = group.maxStress;
                var buildupPct = buildup / derailThreshold * 100;
                GUI.contentColor = Color.HSVToRGB(Mathf.Lerp(1f/3f, 0, (buildupPct - 30f) / 40f), 1f, 1f);
                GUILayout.Label(buildupPct.ToString("F0"), rightAlign);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        private Color JobColor(Job? job)
        {
            if (job == null)
                return Color.white;
            return job.State switch
            {
                JobState.Available => Color.yellow,
                JobState.InProgress => Color.white,
                JobState.Completed => Color.green,
                _ => Color.red,
            };
        }

        private void DrawCarJobs(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Job", noWrap);
            foreach (CarGroup group in groups)
            {
                GUI.contentColor = JobColor(group.job);
                GUILayout.Label(group.job?.ID ?? " ", noWrap);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        private void DrawCarDestinations(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Destination", noWrap);
            foreach (var group in groups)
            {
                var destination = group.nextDestination;
                var currentTracks = group.cars.Select(c => c.logicCar.CurrentTrack);
                GUI.contentColor = currentTracks.All(t => t == destination) ? Color.green
                    : currentTracks.Any(t => t == destination) ? Color.yellow
                    : Color.white;
                GUILayout.Label(
                    group.nextDestination?.ID?.FullDisplayID ?? " ",
                    noWrap);
                Main.DebugLog($"group={group}, dest={destination?.ID}, tracks={string.Join(",", group.cars.Select(c => c.logicCar?.CurrentTrack?.ID))}");
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        private Track? GetNextDestinationTrack(Task task, Car car)
        {
            var data = task.GetTaskData();
            if (data.type == TaskType.Transport || data.type == TaskType.Warehouse)
                return (data.state == TaskState.InProgress && data.cars.Contains(car)) ? data.destinationTrack : null;
            return data.nestedTasks.Select(t => GetNextDestinationTrack(t, car)).FirstOrDefault(track => track != null);
        }

        private Track? GetNextDestinationTrack(Job? job, Car car)
        {
            return job?.tasks.Select(task => GetNextDestinationTrack(task, car)).FirstOrDefault(track => track != null);
        }

        private void DrawCarBrakeStatus(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Pipe", noWrap);
            foreach (var group in groups)
                GUILayout.Label(group.minBrakePipePressure.ToString("F1"), noWrap);
            GUILayout.EndVertical();

            if (UnityModManager.FindMod("AirBrake") != null)
            {
                GUILayout.Space(ColumnSpacing);
                GUILayout.BeginVertical();
                GUILayout.Label("Res", noWrap);
                foreach (var group in groups)
                    GUILayout.Label(group.minBrakeReservoirPressure.ToString("F1"), noWrap);
                GUILayout.EndVertical();

                GUILayout.Space(ColumnSpacing);
                GUILayout.BeginVertical();
                GUILayout.Label("Cyl", noWrap);
                foreach (var group in groups)
                    GUILayout.Label(group.maxBrakeCylinderPressure.ToString("F1"), noWrap);
                GUILayout.EndVertical();
            }

            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Force", noWrap);
            foreach (var group in groups)
                GUILayout.Label($"{group.maxBrakeFactor * 100:F0} %", noWrap);
            GUILayout.EndVertical();
        }

        /*
        private string DumpTask(Task task, int indent = 0)
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

        private string DumpJob(Job job)
        {
            return string.Join("\n", job.tasks.Select(DumpTask));
        }
        */

        private void DrawUpcomingEvents()
        {
            var bogie = PlayerManager.Car.Bogies[0];
            var track = bogie.track;
            if (track == null)
                return;
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
                .TakeWhile(ev => ev.span < Main.settings.maxEventSpan)
                .Select(ev => ev switch
                    {
                        TrackChangeEvent e => (e.span, e.ID.ToString()),
                        JunctionEvent e => (e.span, e.selectedBranch == 0 ? "Left" : "Right"),
                        DualSpeedLimitEvent e => (e.span, $"{e.limit} / {e.rightLimit} km/h"),
                        SpeedLimitEvent e => (e.span, $"{e.limit} km/h"),
                        GradeEvent e => (e.span, $"{e.grade:F1} %"),
                        _ => (0.0, $"Unknown event: {ev}"),
                    });

            GUILayout.BeginHorizontal("box");

            GUILayout.BeginVertical(GUILayout.MaxWidth(50));
            foreach ((double span, string desc) in eventDescriptions)
                GUILayout.Label($"{Math.Round(span / 10) * 10:F0} m", rightAlign);
            GUILayout.EndVertical();

            GUILayout.Space(ColumnSpacing);

            GUILayout.BeginVertical();
            foreach ((double span, string desc) in eventDescriptions)
                GUILayout.Label(desc);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
    }
}