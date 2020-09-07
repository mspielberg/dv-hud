using DV.Logic.Job;
using DV.Simulation.Brake;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    class Overlay : MonoBehaviour
    {
        const int ColumnSpacing = 10;

        public static Overlay? instance;
        static GUIStyle? noChrome;
        static GUIStyle? noWrap;
        static GUIStyle? rightAlign;

        bool overlayEnabled = false;

        public void Start()
        {
            // Wait for a frame because for some reason RaycastAll doesn't detect colliders if called on the same frame.
            StartCoroutine(DelayedEnable());
            instance = this;
        }

        IEnumerator DelayedEnable()
        {
            yield return null;
            overlayEnabled = true;
            Main.DebugLog($"Overlay enabled on frame {Time.frameCount}. Fixed update {Time.fixedTime / Time.fixedDeltaTime}");
        }

        /// Can only be called during OnGui()
        private void InitializeStyles()
        {
            if (noChrome != null)
                return;

            noChrome = new GUIStyle(GUI.skin.window);
            noChrome.normal.background = null;
            noChrome.onNormal = noChrome.normal;

            noWrap = new GUIStyle(GUI.skin.label);
            noWrap.wordWrap = false;

            rightAlign = new GUIStyle(noWrap);
            rightAlign.alignment = TextAnchor.MiddleRight;
        }

        static Rect prevRect = new Rect();
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

            if (Main.settings.showTrackInfo)
                DrawUpcomingEvents();

            if (Main.settings.showCarList)
                DrawCarList();

            GUI.DragWindow();
        }

        readonly struct CarGroup
        {
            public readonly int startIndex;
            public readonly int endIndex;
            public readonly TrainCar lastCar;
            public readonly float maxStress;
            public readonly Job? job;

            public readonly float minBrakeReservoirPressure;
            public readonly float minBrakeCylinderPressure;
            public readonly float minBrakeFactor;

            public CarGroup(int startIndex, int endIndex, TrainCar lastCar, float maxStress, Job? job, float minBrakeReservoirPressure, float minBrakeCylinderPressure, float minBrakeFactor)
            {
                this.startIndex = startIndex;
                this.endIndex = endIndex;
                this.lastCar = lastCar;
                this.maxStress = maxStress;
                this.job = job;
                this.minBrakeReservoirPressure = minBrakeReservoirPressure;
                this.minBrakeCylinderPressure = minBrakeCylinderPressure;
                this.minBrakeFactor = minBrakeFactor;
            }

            public override string ToString()
            {
                return $"{startIndex}-{endIndex} {lastCar?.ID} {maxStress} {job?.ID}";
            }
        }

        IEnumerable<CarGroup> GetCarGroups(IEnumerable<TrainCar> cars, bool individual)
        {
            Job? prevJob = null;
            Track? prevDestTrack = null;
            TrainCar? prevCar = null;
            int startIndex = 0;
            float maxStress = 0f;

            float minBrakeReservoirPressure = BrakeSystemConsts.MAX_BRAKE_PIPE_PRESSURE;
            float minBrakeCylinderPressure = BrakeSystemConsts.MAX_BRAKE_PIPE_PRESSURE;
            float minBrakeFactor = Single.PositiveInfinity;

            int i = 0;
            foreach (var car in cars)
            {
                var carStress = car.GetComponent<TrainStress>().derailBuildUp;
                Job? job = JobChainController.GetJobOfCar(car);
                var destTrack = GetNextDestinationTrack(job, car.logicCar);//?.ID?.ToString();
                var brakeSystem = car.brakeSystem;
                if (individual || destTrack == null || destTrack != prevDestTrack)
                {
                    // complete previous group
                    if (i > 0)
                        yield return new CarGroup(
                            startIndex,
                            i,
                            prevCar!,
                            maxStress,
                            prevJob,
                            minBrakeReservoirPressure,
                            minBrakeCylinderPressure,
                            minBrakeFactor);

                    // start new group
                    startIndex = i;
                    prevJob = job;
                    prevDestTrack = destTrack;
                    maxStress = carStress;
                    minBrakeReservoirPressure = brakeSystem.mainReservoirPressure;
                    minBrakeCylinderPressure = brakeSystem.independentPipePressure;
                    minBrakeFactor = brakeSystem.brakingFactor;
                }
                else
                {
                    if (carStress > maxStress)
                        maxStress = carStress;
                    if (brakeSystem.mainReservoirPressure < minBrakeReservoirPressure)
                        minBrakeReservoirPressure = brakeSystem.mainReservoirPressure;
                    if (brakeSystem.independentPipePressure < minBrakeCylinderPressure)
                        minBrakeCylinderPressure = brakeSystem.independentPipePressure;
                    if (brakeSystem.brakingFactor < minBrakeFactor)
                        minBrakeFactor = brakeSystem.brakingFactor;
                }

                prevCar = car;
                i++;
            }

            // complete last group
            yield return new CarGroup(
                startIndex,
                i,
                prevCar!,
                maxStress,
                prevJob,
                minBrakeReservoirPressure,
                minBrakeCylinderPressure,
                minBrakeFactor);
        }

        const char EnDash = '\u2013';
        void DrawCarList()
        {
            IEnumerable<TrainCar> cars = PlayerManager.Car.trainset.cars.AsReadOnly();
            if (!cars.First().IsLoco && cars.Last().IsLoco)
                cars = cars.Reverse();

            var groups = GetCarGroups(cars, !Main.settings.groupCarsByJob);

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label(" ", noWrap);
            foreach (CarGroup group in groups)
                if (group.startIndex + 1 == group.endIndex)
                    GUILayout.Label(group.endIndex.ToString(), noWrap);
                else
                    GUILayout.Label($"{group.startIndex + 1}{EnDash}{group.endIndex}", noWrap);
            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            GUILayout.Label("ID", noWrap);
            foreach (CarGroup group in groups)
                GUILayout.Label(group.lastCar.ID, noWrap);
            GUILayout.EndVertical();

            if (Main.settings.showCarStress)
                DrawCarStress(groups);
            if (Main.settings.showCarJobs)
                DrawCarJobs(groups);
            if (Main.settings.showCarDestinations)
                DrawCarDestinations(groups);

            if(true/*Main.settings.showCarBrakes*/)
                DrawCarBrakeStatus(groups);

            GUILayout.EndHorizontal();
        }

        void DrawCarStress(IEnumerable<CarGroup> groups)
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

        Color JobColor(Job? job)
        {
            if (job == null)
                return Color.white;
            switch (job.State)
            {
                case JobState.Available: return Color.yellow;
                case JobState.InProgress: return Color.white;
                case JobState.Completed: return Color.green;
                default: return Color.red;
            };
        }

        void DrawCarJobs(IEnumerable<CarGroup> groups)
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

        void DrawCarDestinations(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Destination", noWrap);
            foreach (var group in groups)
                GUILayout.Label(
                    GetNextDestinationTrack(group.job, group.lastCar.logicCar)?.ID?.FullDisplayID ?? " ",
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

        Track? GetNextDestinationTrack(Job? job, Car car)
        {
            return job?.tasks.Select(task => GetNextDestinationTrack(task, car)).FirstOrDefault(track => track != null);
        }

        void DrawCarBrakeStatus(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Reservoir", noWrap);
            foreach (var group in groups)
                GUILayout.Label(group.minBrakeReservoirPressure.ToString("F2"));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Cylinder", noWrap);
            foreach (var group in groups)
                GUILayout.Label(group.minBrakeCylinderPressure.ToString("F2"));
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            GUILayout.Label("Brake", noWrap);
            foreach (var group in groups)
                GUILayout.Label(group.minBrakeFactor.ToString("F2"));
            GUILayout.EndVertical();
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
                        return (e.span, $"{e.grade.ToString("F1")} %");
                    default:
                        return (0.0, $"Unknown event: {ev}");
                    }
                });

            GUILayout.BeginHorizontal("box");

            GUILayout.BeginVertical(GUILayout.MaxWidth(50));
            foreach ((double span, string desc) in eventDescriptions)
                GUILayout.Label($"{(Math.Round(span / 10) * 10).ToString("F0")} m", rightAlign);
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