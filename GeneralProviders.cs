using UnitsNet;
using UnitsNet.Units;
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
                car => Speed.FromMetersPerSecond(Mathf.Abs(car.GetForwardSpeed())),
                QuantityType.Speed));

            Registry.Register(new QueryDataProvider(
                "Grade",
                car =>
                {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Ratio.FromDecimalFractions(Mathf.Tan(inclination * Mathf.PI / 180)).ToUnit(RatioUnit.Percent);
                },
                QuantityType.Ratio));

            Registry.Register(new QueryDataProvider(
                "Brake pipe",
                car => Pressure.FromBars(car.brakeSystem.brakePipePressure),
                QuantityType.Pressure));

            Registry.Register(new QueryDataProvider(
                "Consist mass",
                car => Mass.FromKilograms(car.trainset.TotalMass()),
                QuantityType.Mass));

            Registry.Register(new QueryDataProvider(
                "Consist length",
                car => Length.FromMeters(car.trainset.OverallLength()),
                QuantityType.Length));
        }
    }
}