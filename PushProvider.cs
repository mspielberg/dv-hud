using System;
using System.Collections.Generic;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    using Formatter = Func<float, string>;
    public class PushProvider : IDataProvider
    {
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        public string Label { get; }
        public IComparable Order { get; }

        private readonly Formatter formatter;

        public float alpha = 0.1f;

        public PushProvider(string label, Formatter formatter, IComparable? order = null)
        {
            this.Label = label;
            this.Order = order ?? label;
            this.formatter = formatter;
        }

        public override string ToString()
        {
            return $"PushProvider {Label}: {values.Aggregate("", (a,b) => a + b.ToString())}";
        }

        public float? GetValue(TrainCar car)
        {
            values.TryGetValue(car.ID, out var value);
            return value;
        }

        public string GetFormatted(TrainCar car)
        {
            return formatter(GetValue(car) ?? default);
        }

        public void SetValue(TrainCar car, float value)
        {
            // Main.DebugLog($"Setting value {value} for {car.ID} into {this}");
            values.Remove(car.ID);
            values.Add(car.ID, value);
        }

        public void MixSmoothedValue(TrainCar car, float value)
        {
            // Main.DebugLog($"Mixing value {value} for {car.ID} into {this}");
            values.TryGetValue(car.ID, out var oldValue);
            SetValue(car, (oldValue * alpha) + (value * (1f - alpha)));
            // Main.DebugLog($"After mix: {this}");
        }
    }
}