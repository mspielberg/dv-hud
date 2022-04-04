using QuantitiesNet;
using System;
using System.Linq;

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

    public class QuantityQueryDataProvider<D> : DataProvider<Quantity<D>>, IQuantityProvider
    where D : IDimension, new()
    {
        private readonly Func<TrainCar, Quantity<D>?> provider;

        public QuantityQueryDataProvider(
            string label,
            Func<TrainCar, Quantity<D>?> provider,
            IComparable? order = null,
            bool hidden = false) : base(label, order, hidden)
        {
            this.provider = provider;
        }

        public Dimension Dimension => Dimension.ForType<D>();
        public string QuantityName => typeof(D).Name;

        public override bool TryGetFormatted(TrainCar car, out string s)
        {
            if (TryGetValue(car, out var v))
            {
                Main.DebugLog($"Got value {v}");
                if (UnitRegistry.Default.TryGetPreferredUnit(v.dimension, out var unit))
                {
                    Main.DebugLog($"Got unit {unit}");
                    s = $"{v.In(unit):F1} {unit.Symbol}";
                    return true;
                }
            }
            s = "";
            return false;
        }

        public override bool TryGetValue(TrainCar car, out Quantity<D> v)
        {
            var result = provider(car);
            if (result != null)
            {
                v = result;
                return true;
            }
            v = default;
            return false;
        }
    }
}
