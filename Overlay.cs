using DV.Logic.Job;
using DV.MultipleUnit;
using DV.Simulation.Brake;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public static class Styles
    {
        public static readonly GUIStyle noChrome;
        public static readonly GUIStyle noWrap;
        public static readonly GUIStyle noWrapBold;
        public static readonly GUIStyle rightAlign;
        public static readonly GUIStyle richText;

        /// <summary>Can only be called during OnGui()</summary>
        static Styles()
        {
            noChrome = new GUIStyle(GUI.skin.window);
            noChrome.normal.background = null;
            noChrome.onNormal = noChrome.normal;

            noWrap = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false
            };

            noWrapBold = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                fontStyle = FontStyle.Bold,
            };

            rightAlign = new GUIStyle(noWrap)
            {
                alignment = TextAnchor.MiddleRight
            };

            richText = new GUIStyle(noWrap)
            {
                richText = true
            };
        }
    }

    public class Overlay : MonoBehaviour
    {
        public const int ColumnSpacing = 10;

        public static Overlay? instance;

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

            if (prevRect == new Rect())
                prevRect.position = Main.settings.hudPosition;
            prevRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                prevRect,
                DrawDrivingInfoWindow,
                "",
                Styles.noChrome);
            Main.settings.hudPosition = prevRect.position;
        }

        public void ResetPosition()
        {
            prevRect = new Rect();
        }

        private void DrawDrivingInfoWindow(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            if (Main.settings.showDrivingInfo)
                DrawCurrentCarInfo();
            if (Main.settings.trackInfoSettings.enabled)
                EventList.DrawUpcomingEvents();

            GUILayout.EndHorizontal();

            if (Main.settings.trainInfoSettings.enabled)
                DrawTrainInfo();

            GUILayout.EndVertical();

            if (!Main.settings.lockPosition)
                GUI.DragWindow();
        }

        private void DrawCurrentCarInfo()
        {
            if (!PlayerManager.Car)
                return;

            var labelsAndValues = Registry.providers.Values
                .Where(p => !p.Hidden)
                .Where(Main.settings.IsEnabled)
                .Select(dp => (dp.Label, dp.GetFormatted(PlayerManager.Car)))
                .Where(p => p.Item2 != null);

            GUILayout.BeginHorizontal("box", GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical();
            foreach (var (label, value) in labelsAndValues)
            {
                if (value != null)
                    GUILayout.Label(label, Styles.noWrap);
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical();
            foreach (var (label, value) in labelsAndValues)
            {
                if (value != null)
                    GUILayout.Label(value, Styles.noWrap);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private readonly struct CarGroup
        {
            public readonly int startIndex;
            public readonly int endIndex;
            public readonly List<TrainCar> cars;
            public readonly float maxStress;
            public readonly float? maxCouplerStress;
            public readonly Job? job;
            public readonly Track? nextDestination;

            public readonly float minBrakePipePressure;
            public readonly float minBrakeReservoirPressure;
            public readonly float maxBrakeCylinderPressure;
            public readonly float maxBrakeFactor;

            public readonly List<char> brakeModes;

            public CarGroup(
                int startIndex,
                int endIndex,
                List<TrainCar> cars,
                float maxStress,
                float? maxCouplerStress,
                Job? job,
                Track? nextDestination,
                float minBrakePipePressure,
                float minBrakeReservoirPressure,
                float maxBrakeCylinderPressure,
                float maxBrakeFactor,
                IEnumerable<char> brakeModes)
            {
                this.startIndex = startIndex;
                this.endIndex = endIndex;
                this.cars = cars;
                this.maxStress = maxStress;
                this.maxCouplerStress = maxCouplerStress;
                this.job = job;
                this.nextDestination = nextDestination;
                this.minBrakePipePressure = minBrakePipePressure;
                this.minBrakeReservoirPressure = minBrakeReservoirPressure;
                this.maxBrakeCylinderPressure = maxBrakeCylinderPressure;
                this.maxBrakeFactor = maxBrakeFactor;
                this.brakeModes = brakeModes.ToList();
                if (this.brakeModes.Count == 0)
                    this.brakeModes.Add(' ');
            }

            public override string ToString()
            {
                return $"{startIndex}-{endIndex} {string.Join(",", cars.Select(c => c.ID))} {maxStress} {job?.ID}";
            }
        }

        private float? GetCouplerStress(IEnumerable<TrainCar> cars, int index)
        {
            if (index == 0)
                return null;
            var frontCouplerRegistry = Registry.GetProvider("Front coupler");
            if (frontCouplerRegistry == null)
                return null;
            var rearCouplerRegistry = Registry.GetProvider("Rear coupler");
            if (rearCouplerRegistry == null)
                return null;

            var frontCar = cars.ElementAt(index - 1);
            var rearCar = cars.ElementAt(index);
            static bool? IsFrontCoupler(TrainCar car, TrainCar attached) =>
                car.frontCoupler.springyCJ && car.frontCoupler.coupledTo.train == attached ? (bool?)true
                : car.rearCoupler.springyCJ && car.rearCoupler.coupledTo.train == attached ? (bool?)false
                : null;
            var frontCarCoupler = IsFrontCoupler(frontCar, rearCar);
            var rearCarCoupler = IsFrontCoupler(rearCar, frontCar);
            // Main.DebugLog($"car={rearCar.ID}: frontCarCoupler=({frontCarCoupler.HasValue},{frontCarCoupler}),rearCarCoupler=({rearCarCoupler.HasValue},{rearCarCoupler})");
            if (frontCarCoupler != null)
                return ((bool)frontCarCoupler ? frontCouplerRegistry : rearCouplerRegistry).GetValue(frontCar);
            if (rearCarCoupler != null)
                return ((bool)rearCarCoupler ? frontCouplerRegistry : rearCouplerRegistry).GetValue(rearCar);
            return null;
        }

        private float GetAuxReservoirPressure(TrainCar car) =>
            car.IsLoco
            ? car.brakeSystem.mainReservoirPressure
            : Registry.GetProvider("Aux reservoir")
                .FlatMap(p => p.GetValue(car))
                ?? default;

        private float GetBrakeCylinderPressure(TrainCar car) =>
            Registry.GetProvider("Brake cylinder")
                .FlatMap(p => p.GetValue(car))
                ?? default;

        private char GetTripleValveState(TrainCar car)
        {
            var provider = Registry.GetProvider("Triple valve mode");
            var value = provider.FlatMap(p => p.GetValue(car));
            var c = value.FlatMap(v => (char?)v);
            return c ?? default;
        }

        private IEnumerable<CarGroup> GetCarGroups(IEnumerable<TrainCar> cars, bool individual)
        {
            Job? prevJob = null;
            Track? prevDestTrack = null;
            int startIndex = 0;
            float maxStress = 0f;
            float? maxCouplerStress = null;

            var firstCar = cars.First();
            var groupCars = new List<TrainCar>() { firstCar };
            float minBrakePipePressure = firstCar.brakeSystem.brakePipePressure;
            float minBrakeReservoirPressure = GetAuxReservoirPressure(firstCar);
            float maxBrakeCylinderPressure = GetBrakeCylinderPressure(firstCar);
            float maxBrakeFactor = firstCar.brakeSystem.brakingFactor;

            var brakeModes = new SortedSet<char>();

            if (!CarTypes.IsAnyLocomotiveOrTender(firstCar.carType))
                brakeModes.Add(GetTripleValveState(firstCar));

            int i = 0;
            foreach (var (car, index) in cars.Select((x, i) => (x, i)))
            {
                float carStress = car.GetComponent<TrainStress>().derailBuildUp;
                float? couplerStress = GetCouplerStress(cars, index);
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
                            maxCouplerStress,
                            prevJob,
                            prevDestTrack,
                            minBrakePipePressure,
                            minBrakeReservoirPressure,
                            maxBrakeCylinderPressure,
                            maxBrakeFactor,
                            brakeModes);
                    }

                    // start new group
                    startIndex = i;
                    groupCars = new List<TrainCar>() { car };
                    prevJob = job;
                    prevDestTrack = nextDestination;
                    maxStress = carStress;
                    maxCouplerStress = couplerStress;
                    minBrakePipePressure = pipePressure;
                    minBrakeReservoirPressure = auxReservoirPressure;
                    maxBrakeCylinderPressure = brakeCylinderPressure;
                    maxBrakeFactor = brakeSystem.brakingFactor;
                    brakeModes.Clear();

                    if (!CarTypes.IsAnyLocomotiveOrTender(car.carType))
                        brakeModes.Add(GetTripleValveState(car));
                }
                else
                {
                    groupCars.Add(car);
                    if (carStress > maxStress)
                        maxStress = carStress;
                    if (couplerStress != null && (maxCouplerStress == null || couplerStress > maxCouplerStress))
                        maxCouplerStress = couplerStress;
                    if (pipePressure < minBrakePipePressure)
                        minBrakeReservoirPressure = auxReservoirPressure;
                    if (auxReservoirPressure < minBrakeReservoirPressure)
                        minBrakeReservoirPressure = auxReservoirPressure;
                    if (brakeCylinderPressure > maxBrakeCylinderPressure)
                        maxBrakeCylinderPressure = brakeCylinderPressure;
                    if (brakeSystem.brakingFactor > maxBrakeFactor)
                        maxBrakeFactor = brakeSystem.brakingFactor;

                    if (!CarTypes.IsAnyLocomotiveOrTender(car.carType))
                        brakeModes.Add(GetTripleValveState(car));
                }
                i++;
            }

            // complete last group
            yield return new CarGroup(
                startIndex,
                i,
                groupCars,
                maxStress,
                maxCouplerStress,
                prevJob,
                prevDestTrack,
                minBrakePipePressure,
                minBrakeReservoirPressure,
                maxBrakeCylinderPressure,
                maxBrakeFactor,
                brakeModes);
        }

        private void DrawTrainInfo()
        {
            var trainset = PlayerManager.Car?.trainset ?? PlayerManager.LastLoco?.trainset;
            if (trainset == null)
                return;

            GUILayout.BeginVertical("box");

            if (Main.settings.trainInfoSettings.showTrainInfo)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Train");
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label($"{trainset.cars.Count} car{(trainset.cars.Count > 1 ? "s" : "")}");
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label($"{trainset.OverallLength():F0} m");
                GUILayout.Space(ColumnSpacing);
                GUILayout.Label($"{trainset.TotalMass() / 1000:F0} t");
                GUILayout.EndHorizontal();
            }

            if (Main.settings.trainInfoSettings.showCarList)
                DrawCarList(trainset);

            GUILayout.EndVertical();
        }

        private const char EnDash = '\u2013';
        private void DrawCarList(Trainset trainset)
        {
            var trainInfoSettings = Main.settings.trainInfoSettings;
            IEnumerable<TrainCar> cars = trainset.cars.AsReadOnly();
            if (cars.Last() == PlayerManager.LastLoco || (!cars.First().IsLoco && cars.Last().IsLoco))
                cars = cars.Reverse();
            var groups = GetCarGroups(cars, !trainInfoSettings.groupCarsByJob);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label(" ", Styles.noWrap);
            foreach (CarGroup group in groups)
            {
                if (group.startIndex + 1 == group.endIndex)
                    GUILayout.Label(group.endIndex.ToString(), Styles.noWrap);
                else
                    GUILayout.Label($"{group.startIndex + 1}{EnDash}{group.endIndex}", Styles.noWrap);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("ID", Styles.noWrap);
            foreach (CarGroup group in groups)
            {
                GUI.contentColor = GetCarColor(group.cars[0]);
                GUILayout.Label(group.cars[0].ID, Styles.noWrap);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();

            if (trainInfoSettings.showCarStress)
                DrawCarStress(groups);
            if (trainInfoSettings.showCarStress && Registry.GetProvider("Front coupler") != null)
                DrawCouplerStress(groups);
            if (trainInfoSettings.showCarJobs)
                DrawCarJobs(groups);
            if (trainInfoSettings.showCarDestinations)
                DrawCarDestinations(groups);
            if (trainInfoSettings.showCarBrakeStatus)
                DrawCarBrakeStatus(groups);

            GUILayout.EndHorizontal();
        }

        private const float HueOrange = 30f/360f;
        private Color GetCarColor(TrainCar car)
        {
            if (car.derailed)
                return Color.red;
            if (!car.IsLoco)
                return Color.white;

            var isMultipleUnitCapable = car.TryGetComponent<MultipleUnitModule>(out var muModule);
            var frontMUDisconnected = isMultipleUnitCapable
                && car.frontCoupler.coupledTo?.train?.carType == car.carType
                && !muModule.frontCable.IsConnected;
            var rearMUDisconnected = isMultipleUnitCapable
                && car.rearCoupler.coupledTo?.train?.carType == car.carType
                && !muModule.rearCable.IsConnected;
            var hasDisconnectedMUCable = frontMUDisconnected || rearMUDisconnected;

            var isRunning = car.carType switch
            {
                TrainCarType.LocoShunter => car.GetComponent<LocoControllerShunter>().GetEngineRunning(),
                TrainCarType.LocoDiesel => car.GetComponent<LocoControllerDiesel>().GetEngineRunning(),
                _ => true,
            };

            return Color.HSVToRGB(HueOrange, hasDisconnectedMUCable ? 1 : 0, isRunning ? 1 : 0.8f);
        }

        private void DrawCarStress(IEnumerable<CarGroup> groups)
        {
            var derailThreshold = SimManager.instance.derailBuildUpThreshold;

            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Derail", Styles.noWrap);
            foreach (CarGroup group in groups)
            {
                var buildup = group.maxStress;
                var buildupPct = buildup / derailThreshold * 100;
                GUI.contentColor = Color.HSVToRGB(Mathf.Lerp(1f / 3f, 0, (buildupPct - 30f) / 40f), 1f, 1f);
                GUILayout.Label(buildupPct.ToString("F0"), Styles.rightAlign);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        private void DrawCouplerStress(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Coupler", Styles.noWrap);
            foreach (CarGroup group in groups)
            {
                var stress = group.maxCouplerStress;
                if (stress == null)
                {
                    GUILayout.Label(" ", Styles.rightAlign);
                }
                else
                {
                    float stressPct = (float)stress / 1e4f;
                    GUI.contentColor = Color.HSVToRGB(Mathf.Lerp(1f / 3f, 0, (stressPct - 30f) / 40f), 1f, 1f);
                    GUILayout.Label(stressPct.ToString("F0"), Styles.rightAlign);
                }
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
            GUILayout.Label("Job", Styles.noWrap);
            foreach (CarGroup group in groups)
            {
                GUI.contentColor = JobColor(group.job);
                GUILayout.Label(group.job?.ID ?? " ", Styles.noWrap);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        private void DrawCarDestinations(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            GUILayout.Label("Destination", Styles.noWrap);
            foreach (var group in groups)
            {
                var destination = group.nextDestination;
                var carsOnTrack = group.cars.Count(c => c.logicCar.CurrentTrack == destination);
                GUI.contentColor = carsOnTrack == group.cars.Count ? Color.green
                    : carsOnTrack > 0 ? Color.yellow
                    : Color.white;
                GUILayout.Label(
                    group.nextDestination?.ID?.FullDisplayID ?? " ",
                    Styles.noWrap);
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

        public static void DrawColumn<T>(IEnumerable<T> cells, string? label = null, Func<T, string>? renderer = null, GUIStyle? style = null)
        {
            renderer ??= x => x!.ToString();
            style ??= Styles.noWrap;
            GUILayout.Space(ColumnSpacing);
            GUILayout.BeginVertical();
            if (label != null)
                GUILayout.Label(label, style);
            foreach (var cell in cells)
                GUILayout.Label(renderer(cell), style);
            GUILayout.EndVertical();
        }

        private void DrawCarBrakeStatus(IEnumerable<CarGroup> groups)
        {
            var airBrakeModEnabled = UnityModManager.FindMod("AirBrake")?.Enabled ?? false;
            DrawColumn(groups, "Pipe", g => g.minBrakePipePressure.ToString("F2"));
            if (airBrakeModEnabled)
            {
                DrawColumn(groups, "Res", g => g.minBrakeReservoirPressure.ToString("F2"));
                DrawColumn(groups, "Mode", g => string.Join("", g.brakeModes));
                DrawColumn(groups, "Cyl", g => g.maxBrakeCylinderPressure.ToString("F2"));
            }
            DrawColumn(groups, "Force", g => g.maxBrakeFactor.ToString("P0"), Styles.rightAlign);
        }
    }
}