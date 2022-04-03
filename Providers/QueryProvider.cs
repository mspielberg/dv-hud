using System;

namespace DvMod.HeadsUpDisplay
{
    public class QueryDataProvider<T> : DataProvider<T>
    where T : struct
    {
        private readonly Func<TrainCar, T?> provider;
        private readonly Func<T, string> formatter;

        public QueryDataProvider(string label, Func<TrainCar, T?> provider, Func<T, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, hidden)
        {
            this.provider = provider;
            this.formatter = formatter;
        }

        public override string? GetFormatted(TrainCar car)
        {
            return provider(car).Map(formatter);
        }

        public override T? GetValue(TrainCar car)
        {
            return provider(car);
        }

        public override string ToString()
        {
            return $"QueryProvider {Label}";
        }
    }

    public class FloatQueryDataProvider : QueryDataProvider<float>
    {
        public FloatQueryDataProvider(string label, Func<TrainCar, float?> provider, Func<float, string> formatter, IComparable? order = null, bool hidden = false)
        : base(label, provider, formatter, order, hidden)
        {
        }
    }
}
