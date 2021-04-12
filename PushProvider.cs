using System;
using System.Collections.Generic;
using System.Linq;
using UnitsNet;
using UnitsNet.Units;
using Formatter = System.Func<float, string>;

namespace DvMod.HeadsUpDisplay
{
    public class PushProvider : DataProvider
    {
        private readonly Dictionary<string, IQuantity> values = new Dictionary<string, IQuantity>();

        public PushProvider(string label, Formatter formatter, IComparable? order = null, QuantityType quantityType = QuantityType.Undefined)
        : base(label, quantityType, formatter, order)
        {
        }

        public override string ToString()
        {
            return $"PushProvider {Label}: {values.Aggregate("", (a,b) => a + b.ToString())}";
        }

        public override IQuantity? GetQuantity(TrainCar car)
        {
            return values.TryGetValue(car.ID, out var value) ? value : null;
        }

        public void SetValue(TrainCar car, float value)
        {
            SetQuantity(car, Scalar.From(value, ScalarUnit.Undefined));
        }

        public void SetQuantity(TrainCar car, IQuantity value)
        {
            values.Remove(car.ID);
            values.Add(car.ID, value);
        }
    }
}