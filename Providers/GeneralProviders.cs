using QuantitiesNet;
using QuantitiesNet.Dimensions;
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
            Registry.Register(new QuantityQueryDataProvider<Length>(
                "Altitude",
                car => new QuantitiesNet.Quantities.Length(car.transform.position.y - 110f)));

            Registry.Register(new QuantityQueryDataProvider<Velocity>(
                "SpeedQ",
                car => new QuantitiesNet.Quantities.Velocity(car.GetForwardSpeed(), Meter / Second)));

            Registry.Register(new FloatQueryDataProvider(
                "Grade",
                car =>
                {
                    var inclination = car.transform.localEulerAngles.x;
                    inclination = inclination > 180 ? 360f - inclination : -inclination;
                    return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
                },
                f => f.ToString(GradeFormat)));

            Registry.Register(new QuantityQueryDataProvider<Pressure>(
                "Brake pipe",
                car => new QuantitiesNet.Quantities.Pressure(car.brakeSystem.brakePipePressure, Bar)));

        }
    }
}