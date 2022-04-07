using DV.Logic.Job;
using DV.MultipleUnit;
using DV.Simulation.Brake;
using QuantitiesNet;
using static QuantitiesNet.Quantities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public static class CarList
    {
        private readonly struct CarGroup
        {
            public readonly int startIndex;
            public readonly int endIndex;
            public readonly List<TrainCar> cars;
            public readonly float maxStress;
            public readonly float? maxCouplerStress;
            public readonly Job? job;
            public readonly Track? nextDestination;

            public readonly Pressure minBrakePipePressure;
            public readonly Pressure minBrakeReservoirPressure;
            public readonly Pressure maxBrakeCylinderPressure;
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
                Pressure minBrakePipePressure,
                Pressure minBrakeReservoirPressure,
                Pressure maxBrakeCylinderPressure,
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

        private static float? GetCouplerStress(IEnumerable<TrainCar> cars, int index)
        {
            if (index == 0)
                return null;
            if (!(Registry.GetProvider("Front coupler") is DataProvider<float> frontCouplerProvider))
                return null;
            if (!(Registry.GetProvider("Rear coupler") is DataProvider<float> rearCouplerProvider))
                return null;

            var frontCar = cars.ElementAt(index - 1);
            var rearCar = cars.ElementAt(index);
            static bool IsFrontCoupledTo(TrainCar car, TrainCar attached) =>
                car.frontCoupler.springyCJ && car.frontCoupler.coupledTo.train == attached;
            static bool IsRearCoupledTo(TrainCar car, TrainCar attached) =>
                car.rearCoupler.springyCJ && car.rearCoupler?.coupledTo?.train == attached;

            (TrainCar car, bool isFront) FindCoupler()
            {
                if (IsFrontCoupledTo(frontCar, rearCar))
                    return (frontCar, true);
                else if (IsRearCoupledTo(frontCar, rearCar))
                    return (frontCar, false);
                else if (IsFrontCoupledTo(rearCar, frontCar))
                    return (rearCar, true);
                else if (IsRearCoupledTo(rearCar, frontCar))
                    return (rearCar, false);
                throw new System.InvalidOperationException("Could not find coupler link between cars");
            }

            var (carWithCoupler, isFront) = FindCoupler();
            var provider = isFront ? frontCouplerProvider : rearCouplerProvider;
            if (provider.TryGetValue(carWithCoupler, out var stress))
                return stress;
            return null;
        }

        private static Pressure GetPressure(IDataProvider? provider, TrainCar car)
        {
            switch (provider)
            {
                case DataProvider<Quantity<Dimensions.Pressure>> quantityProvider:
                    if (quantityProvider.TryGetValue(car, out var quantity) && quantity is Pressure pressure)
                        return pressure;
                    break;
                case DataProvider<float> floatProvider:
                    if (floatProvider.TryGetValue(car, out var v))
                        return new Pressure(v, QuantitiesNet.Units.Bar);
                    break;
            }
            return new Pressure();
        }

        private static Pressure GetAuxReservoirPressure(TrainCar car)
        {
            if (car.IsLoco)
                return new Pressure(car.brakeSystem.mainReservoirPressure, QuantitiesNet.Units.Bar);
            return GetPressure(Registry.GetProvider("Aux reservoir"), car);
        }

        private static Pressure GetBrakeCylinderPressure(TrainCar car)
        {
            return GetPressure(Registry.GetProvider("Brake cylinder"), car);
        }

        private static char GetTripleValveState(TrainCar car)
        {
            if (Registry.GetProvider("Triple valve mode") is DataProvider<float> provider
                && provider.TryGetValue(car, out var v))
            {
                return (char)v;
            }
            return default;
        }

        private static IEnumerable<CarGroup> GetCarGroups(IEnumerable<TrainCar> cars, bool individual)
        {
            Job? prevJob = null;
            Track? prevDestTrack = null;
            int startIndex = 0;
            float maxStress = 0f;
            float? maxCouplerStress = null;

            var firstCar = cars.First();
            var groupCars = new List<TrainCar>() { firstCar };
            Pressure minBrakePipePressure = new Pressure(firstCar.brakeSystem.brakePipePressure, QuantitiesNet.Units.Bar);
            Pressure minBrakeReservoirPressure = GetAuxReservoirPressure(firstCar);
            Pressure maxBrakeCylinderPressure = GetBrakeCylinderPressure(firstCar);
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
                Pressure pipePressure = new Pressure(brakeSystem.brakePipePressure, QuantitiesNet.Units.Bar);
                Pressure auxReservoirPressure = GetAuxReservoirPressure(car);
                Pressure brakeCylinderPressure = GetBrakeCylinderPressure(car);

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

        private const char EnDash = '\u2013';
        private static float lastUpdateTime = 0f;
        private static CarGroup[]? groups;

        public static void DrawCarList(Trainset trainset)
        {
            var trainInfoSettings = Main.settings.trainInfoSettings;
            IEnumerable<TrainCar> cars = trainset.cars.AsReadOnly();
            if (cars.Last() == PlayerManager.LastLoco || (!cars.First().IsLoco && cars.Last().IsLoco))
                cars = cars.Reverse();

            if (groups == null || UnityEngine.Time.time - lastUpdateTime >= Main.settings.trainInfoSettings.updatePeriod)
            {
                groups = GetCarGroups(cars, !trainInfoSettings.groupCarsByJob).ToArray();
                lastUpdateTime = UnityEngine.Time.time;
            }

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
        private static Color GetCarColor(TrainCar car)
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

        private static void DrawCarStress(IEnumerable<CarGroup> groups)
        {
            var derailThreshold = SimManager.instance.derailBuildUpThreshold;

            GUILayout.Space(Overlay.ColumnSpacing);
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

        private static void DrawCouplerStress(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(Overlay.ColumnSpacing);
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

        private static Color JobColor(Job? job)
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

        private static void DrawCarJobs(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(Overlay.ColumnSpacing);
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

        private static void DrawCarDestinations(IEnumerable<CarGroup> groups)
        {
            GUILayout.Space(Overlay.ColumnSpacing);
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

        private static Track? GetNextDestinationTrack(Task task, Car car)
        {
            var data = task.GetTaskData();
            if (data.type == TaskType.Transport || data.type == TaskType.Warehouse)
                return (data.state == TaskState.InProgress && data.cars.Contains(car)) ? data.destinationTrack : null;
            return data.nestedTasks.Select(t => GetNextDestinationTrack(t, car)).FirstOrDefault(track => track != null);
        }

        private static Track? GetNextDestinationTrack(Job? job, Car car)
        {
            return job?.tasks.Select(task => GetNextDestinationTrack(task, car)).FirstOrDefault(track => track != null);
        }

        private static Func<Pressure, string> MakePressureFormatter(string providerLabel)
        {
            var provider = Registry.GetProvider(providerLabel);
            if (provider is IQuantityProvider quantityProvider)
            {
                Main.settings.drivingInfoSettings.TryGetUnit(quantityProvider, out var unit);
                var providerSettings = Main.settings.drivingInfoSettings.GetProviderSettings(quantityProvider);
                return pressure => pressure.In(unit).ToString($"F{providerSettings.precision}");
            }
            return pressure => $"{pressure.Scalar:F2}";
        }

        private static Func<Pressure, string> BrakePipeFormatter => MakePressureFormatter("Brake pipe");
        private static Func<Pressure, string> AuxReservoirFormatter => MakePressureFormatter("Aux reservoir");
        private static Func<Pressure, string> BrakeCylinderFormatter => MakePressureFormatter("Brake cylinder");

        private static void DrawCarBrakeStatus(IEnumerable<CarGroup> groups)
        {
            var airBrakeModEnabled = UnityModManager.FindMod("AirBrake")?.Enabled ?? false;
            Overlay.DrawColumn(groups, "Pipe", g => BrakePipeFormatter(g.minBrakePipePressure));
            if (airBrakeModEnabled)
            {
                Overlay.DrawColumn(groups, "Res", g => AuxReservoirFormatter(g.minBrakeReservoirPressure));
                Overlay.DrawColumn(groups, "Mode", g => string.Join("", g.brakeModes));
                Overlay.DrawColumn(groups, "Cyl", g => BrakeCylinderFormatter(g.maxBrakeCylinderPressure));
            }
            Overlay.DrawColumn(groups, "Force", g => g.maxBrakeFactor.ToString("P0"), Styles.rightAlign);
        }
    }
}
