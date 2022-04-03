using QuantityTypes;
using System;

namespace DvMod.HeadsUpDisplay
{
    public class FloatQueryDataProvider : DataProvider<float>
    {
        private readonly Func<TrainCar, float?> provider;
        private readonly Func<float, string> formatter;

        public FloatQueryDataProvider(string label, Func<TrainCar, float?> provider, Func<float, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
            this.provider = provider;
            this.formatter = formatter;
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
            var result = provider(car);
            if (result == null)
            {
                v = default;
                return false;
            }
            v = (float)result;
            return true;
        }
    }

    public class QuantityQueryDataProvider<Q> : DataProvider<Q>
    where Q : struct, IQuantity
    {
        private readonly Func<TrainCar, Q?> provider;

        public QuantityQueryDataProvider(string label, Func<TrainCar, Q?> provider, IComparable? order = null, bool hidden = false) : base(label, order, hidden)
        {
            this.provider = provider;
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
            var result = provider(car);
            if (result != null)
            {
                v = (Q)result;
                return true;
            }
            v = default;
            return false;
        }
    }
}
