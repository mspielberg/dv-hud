using System;
using System.Collections.Generic;
using System.Linq;
using Formatter = System.Func<float, string>;

namespace DvMod.HeadsUpDisplay
{
    public class PushProvider : DataProvider
    {
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();

        private const float alpha = 0.1f;

        public PushProvider(string label, Formatter formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, formatter, hidden)
        {
        }

        public override string ToString()
        {
            return $"PushProvider {Label}: {values.Aggregate("", (a,b) => a + b.ToString())}";
        }

        public override float? GetValue(TrainCar car)
        {
            return values.TryGetValue(car.ID, out var value) ? value : (float?)null;
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