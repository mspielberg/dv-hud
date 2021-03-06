using System;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float?>;
    using Formatter = Func<float, string>;

    public abstract class DataProvider
    {
        public string Label { get; }
        public IComparable Order { get; }
        private readonly Formatter formatter;
        public bool Hidden { get; }

        public abstract float? GetValue(TrainCar car);

        public string? GetFormatted(TrainCar car)
        {
            return GetValue(car) switch
            {
                float v => formatter(v),
                _ => null,
            };
        }

        protected DataProvider(string label, IComparable? order, Formatter formatter, bool hidden)
        {
            Label = label;
            Order = order ?? label;
            this.formatter = formatter;
            this.Hidden = hidden;
        }
    }

    public class QueryDataProvider : DataProvider
    {
        private readonly Provider provider;

        public QueryDataProvider(string label, Provider provider, Formatter formatter, IComparable? order = null, bool hidden = false)
        : base(label, order, formatter, hidden)
        {
            this.provider = provider;
        }

        public override float? GetValue(TrainCar car)
        {
            return provider(car);
        }

        public override string ToString()
        {
            return $"QueryProvider {Label}";
        }
    }

    public static class DataProviders
    {
        public static void Register()
        {
            GeneralProviders.Register();
            LocoProviders.Register();
        }
    }
}