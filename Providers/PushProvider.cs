using QuantityTypes;
using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    public class FloatPushProvider : DataProvider<float>
    {
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        private readonly Func<float, string> formatter;

        public FloatPushProvider(string label, Func<float, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
            this.formatter = formatter;
        }

        public void SetValue(TrainCar car, float value)
        {
            values[car.ID] = value;
        }

        public override bool TryGetFormatted(TrainCar car, out string s)
        {
            if (TryGetValue(car, out var v))
            {
                s = formatter(v);
                return true;
            }
            s = "";
            return false;
        }

        public override bool TryGetValue(TrainCar car, out float v)
        {
            return values.TryGetValue(car.ID, out v);
        }
    }

    public class QuantityPushProvider<Q> : DataProvider<Q>
    where Q : struct, IQuantity
    {
        private readonly Dictionary<string, Q> values = new Dictionary<string, Q>();

        public QuantityPushProvider(string label, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
        }

        public void SetValue(TrainCar car, Q value)
        {
            values[car.ID] = value;
        }

        public override bool TryGetFormatted(TrainCar car, out string s)
        {
            if (TryGetValue(car, out var v))
            {
                var unit = UnitProvider.Default.GetDisplayUnit(typeof(Q), out var symbol);
                s = $"{v.ConvertTo(unit):F1} {symbol}";
                return true;
            }
            s = "";
            return false;
        }

        public override bool TryGetValue(TrainCar car, out Q v)
        {
            return values.TryGetValue(car.ID, out v);
        }
    }
}