using System;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float>;
    using Formatter = Func<float, string>;

    public interface DataProvider
    {
        string Label { get; }
        bool Enabled { get; }
        string GetValue(TrainCar car);
    }

    public struct Quantity
    {
        public float multiple;
        public string unit;
    }

    public readonly struct QueryDataProvider : DataProvider
    {
        public string Label { get; }
        public bool Enabled { get => enable(); }
        readonly Provider provider;
        readonly Formatter formatter;
        private readonly Func<bool> enable;

        public QueryDataProvider(string label, Func<bool> enable, Provider provider, Formatter formatter)
        {
            this.Label = label;
            this.provider = provider;
            this.formatter = formatter;
            this.enable = enable;
        }

        public string GetValue(TrainCar car)
        {
            return formatter(provider(car));
        }
    }

    public static class DataProviders
    {
        public static void Register()
        {
            GeneralProviders.Register();
            foreach (TrainCarType carType in Enum.GetValues(typeof(TrainCarType)))
                if (CarTypes.IsLocomotive(carType))
                    LocoProviders.Register(carType);
        }
    }
}