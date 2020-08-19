using DV.Logic.Job;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static class GeneralProviders
    {
        public static void Register()
        {
            Registry.Register(TrainCarType.NotSet, new QueryDataProvider(
                "Speed",
                () => Main.settings.ShowSpeed,
                car => Mathf.Abs(car.GetForwardSpeed()) * 3.6f,
                f => $"{f.ToString("F1")} kph"));
            Registry.Register(TrainCarType.NotSet, new QueryDataProvider("Grade",
                () => Main.settings.ShowGrade,
                car => {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
                },
                f => $"{f.ToString("F1")} %"));
            Registry.Register(TrainCarType.NotSet, new QueryDataProvider(
                "Brake pipe",
                () => Main.settings.ShowBrakePipe,
                car => car.brakeSystem?.brakePipePressure ?? 0f,
                f => $"{f.ToString("F2")} bar"));
            Registry.Register(TrainCarType.NotSet, new QueryDataProvider(
                "Consist mass",
                () => Main.settings.ShowConsistMass,
                car => car.trainset.cars.Sum(c => c.totalMass + CargoTypes.GetCargoMass(c.LoadedCargo, c.LoadedCargoAmount)),
                f => $"{(f / 1000).ToString("F2")} t"));
        }
    }
}