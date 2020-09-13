using DV.Logic.Job;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static class GeneralProviders
    {
        // U+2007 FIGURE SPACE
        // U+002B PLUS SIGN
        // U+2212 MINUS SIGN
        private const string GradeFormat = "\u002b0.0' %';\u22120.0' %'";

        public static void Register()
        {
            Registry.Register(RegistryKeys.AllCars, new QueryDataProvider(
                "Speed",
                () => Main.settings.ShowSpeed,
                car => Mathf.Abs(car.GetForwardSpeed()) * 3.6f,
                f => $"{f.ToString("F1")} km/h"));
            Registry.Register(RegistryKeys.AllCars, new QueryDataProvider(
                "Grade",
                () => Main.settings.ShowGrade,
                car => {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
                },
                f => f.ToString(GradeFormat)));
            Registry.Register(RegistryKeys.AllCars, new QueryDataProvider(
                "Brake pipe",
                () => Main.settings.ShowBrakePipe,
                car => car.brakeSystem?.brakePipePressure ?? 0f,
                f => $"{f.ToString("F2")} bar"));
            Registry.Register(RegistryKeys.AllCars, new QueryDataProvider(
                "Consist mass",
                () => Main.settings.ShowConsistMass,
                car => car.trainset.cars.Sum(c => c.totalMass + CargoTypes.GetCargoMass(c.LoadedCargo, c.LoadedCargoAmount)),
                f => $"{(f / 1000).ToString("F0")} t"));

            Registry.Register(RegistryKeys.AllCars, new QueryDataProvider(
                "Consist length",
                () => Main.settings.ShowConsistMass,
                car => car.trainset.cars.Sum(c => c.logicCar.length),
                f => $"{f.ToString("F0")} m"));
        }
    }
}