using QuantitiesNet;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public class QuantityPushProvider<D> : DataProvider<Quantity<D>>, IQuantityProvider
    where D : IDimension, new()
    {
        private readonly Dictionary<string, Quantity<D>> values = new Dictionary<string, Quantity<D>>();

        public QuantityPushProvider(string label, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
        }

        public Dimension Dimension => Dimension.ForType<D>();

        public void SetValue(TrainCar car, Quantity<D> value)
        {
            values[car.ID] = value;
        }

        public override bool TryGetFormatted(TrainCar car, out string s)
        {
            if (TryGetValue(car, out var v))
            {
                var settings = Main.settings.drivingInfoSettings.GetProviderSettings(this);
                if (settings.TryGetUnit(Dimension, out var unit))
                {
                    var format = $"F{settings.precision}";
                    var strValue = v.In(unit).ToString(format);
                    s = $"{strValue} {unit.Symbol}";
                    return true;
                }
            }
            s = "";
            return false;
        }

        public override bool TryGetValue(TrainCar car, out Quantity<D> v)
        {
            return values.TryGetValue(car.ID, out v);
        }
    }
}
