using System;

namespace DvMod.HeadsUpDisplay
{
    public interface IDataProvider
    {
        public string Label { get; }
        public IComparable Order { get; }
        public bool Hidden { get; }
        public abstract string? GetFormatted(TrainCar car);
    }

    public abstract class DataProvider<T> : IDataProvider
    where T : struct
    {
        public string Label { get; }
        public IComparable Order { get; }
        public bool Hidden { get; }

        public abstract string? GetFormatted(TrainCar car);
        public abstract T? GetValue(TrainCar car);

        protected DataProvider(string label, IComparable? order = null, bool hidden = false)
        {
            this.Label = label;
            this.Order = order ?? label;
            this.Hidden = hidden;
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