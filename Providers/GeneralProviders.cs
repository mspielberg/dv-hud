using QuantitiesNet;
using static QuantitiesNet.Units;
using UnityEngine;
using System.Linq;

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
            Registry.Register(new QuantityQueryDataProvider<Dimensions.Length>(
                "Altitude",
                car => new Quantities.Length(car.transform.position.y - 110f)));

            Registry.Register(new QuantityQueryDataProvider<Dimensions.Velocity>(
                "Speed",
                car => new Quantities.Velocity(Mathf.Abs(car.GetForwardSpeed()), Meter / Second)));

            Registry.Register(new FloatQueryDataProvider(
                "Grade",
                car =>
                {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
                },
                f => f.ToString(GradeFormat)));

            Registry.Register(new QuantityQueryDataProvider<Dimensions.Pressure>(
                "Brake pipe",
                car => new Quantities.Pressure(car.brakeSystem.brakePipePressure, Bar)));

        }
    }
}
