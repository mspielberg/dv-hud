using DV.Logic.Job;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public static class GeneralProviders
    {
        // U+2007 FIGURE SPACE
        // U+002B PLUS SIGN
        // U+2212 MINUS SIGN
        private const string GradeFormat = "\u002b0.0' %';\u22120.0' %'";

        public static void Register()
        {
            Registry.Register(new QueryDataProvider(
                "Speed",
                car => Mathf.Abs(car.GetForwardSpeed()) * 3.6f,
                f => $"{f:F1} km/h"));

            Registry.Register(new QueryDataProvider(
                "Grade",
                car => {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
                },
                f => f.ToString(GradeFormat)));

            Registry.Register(new QueryDataProvider(
                "Brake pipe",
                car => car.brakeSystem?.brakePipePressure,
                f => $"{f:F2} bar"));

            Registry.Register(new QueryDataProvider(
                "Consist mass",
                car => car.trainset.cars.Sum(c => c.totalMass + CargoTypes.GetCargoMass(c.LoadedCargo, c.LoadedCargoAmount)),
                f => $"{f / 1000:F0} t"));

            Registry.Register(new QueryDataProvider(
                "Consist length",
                car => car.trainset.cars.Sum(c => c.logicCar.length),
                f => $"{f:F0} m"));
        }
    }
}