using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DvMod.HeadsUpDisplay
{
    using Formatter = Func<float, string>;
    public class PushProvider : DataProvider
    {
        static Dictionary<string, PushProvider> providers = new Dictionary<string, PushProvider>();

        public static void SetValue(string label, TrainCar car, float value)
        {
            providers[label].SetValue(car, value);
        }

        //ConditionalWeakTable<TrainCar, object> values = new ConditionalWeakTable<TrainCar, object>();
        Dictionary<string, float> values = new Dictionary<string, float>();

        public string Label { get; }
        public bool Enabled { get { return enabled(); } }

        private Func<bool> enabled;
        private Formatter formatter;

        public float alpha = 0.1f;

        static int nextID = 0;

        int ID;

        public PushProvider(string label, Func<bool> enabled, Formatter formatter)
        {
            this.ID = nextID++;
            this.Label = label;
            this.enabled = enabled;
            this.formatter = formatter;
            providers[label] = this;
        }

        public override string ToString()
        {
            return $"PushProvider {ID}: {Label}: {values.Aggregate("", (a,b) => a + b.ToString())}";
        }

        public string GetValue(TrainCar car)
        {
            float value = 0f / 0f;
            values.TryGetValue(car.ID, out value);
            return formatter(value);
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
            float oldValue = 0f;
            values.TryGetValue(car.ID, out oldValue);
            SetValue(car, oldValue * alpha + value * (1f - alpha));
            // Main.DebugLog($"After mix: {this}");
        }
    }
}