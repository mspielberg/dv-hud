using System;
using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public class PushProvider<T> : DataProvider<T>
    where T : struct
    {
        private readonly Func<T, string> formatter;

        private readonly Dictionary<string, T> values = new Dictionary<string, T>();

        public PushProvider(string label, Func<T, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
            this.formatter = formatter;
        }

        public override string ToString()
        {
            return $"PushProvider {Label}: {values.Aggregate("", (a,b) => a + b.ToString())}";
        }

        public override T? GetValue(TrainCar car)
        {
            return values.TryGetValue(car.ID, out var value) ? value : default;
        }

        public void SetValue(TrainCar car, T value)
        {
            // Main.DebugLog($"Setting value {value} for {car.ID} into {this}");
            values.Remove(car.ID);
            values.Add(car.ID, value);
        }

        public override string? GetFormatted(TrainCar car)
        {
            return GetValue(car).Map(formatter);
        }
    }

    public class FloatPushProvider : PushProvider<float>
    {
        public FloatPushProvider(string label, Func<float, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, formatter, order, hidden)
        {
        }
    }
}