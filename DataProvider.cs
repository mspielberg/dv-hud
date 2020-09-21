using System;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float>;
    using Formatter = Func<float, string>;

    public interface DataProvider
    {
        string Label { get; }
        bool Enabled { get; }
        float GetValue(TrainCar car);
        string GetFormatted(TrainCar car);
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

        public float GetValue(TrainCar car)
        {
            return provider(car);
        }

        public string GetFormatted(TrainCar car)
        {
            return formatter(GetValue(car));
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