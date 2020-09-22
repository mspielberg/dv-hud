using System;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float?>;
    using Formatter = Func<float, string>;

    public interface IDataProvider
    {
        string Label { get; }
        IComparable Order { get; }
        float? GetValue(TrainCar car);
        string GetFormatted(TrainCar car);
    }

    public readonly struct QueryDataProvider : IDataProvider
    {
        public string Label { get; }
        public IComparable Order { get; }
        private readonly Provider provider;
        private readonly Formatter formatter;

        public QueryDataProvider(string label, Provider provider, Formatter formatter, IComparable? order = null)
        {
            this.Label = label;
            this.Order = order ?? label;
            this.provider = provider;
            this.formatter = formatter;
        }

        public float? GetValue(TrainCar car)
        {
            return provider(car);
        }

        public string GetFormatted(TrainCar car)
        {
            return formatter(GetValue(car) ?? default);
        }
    }

    public static class DataProviders
    {
        public static void Register()
        {
            GeneralProviders.Register();
            foreach (TrainCarType carType in Enum.GetValues(typeof(TrainCarType)))
            {
                if (CarTypes.IsLocomotive(carType))
                    LocoProviders.Register(carType);
            }
        }
    }
}