using System;
using QuantitiesNet;

namespace DvMod.HeadsUpDisplay
{
    public interface IDataProvider
    {
        public string Label { get; }
        public IComparable Order { get; }
        public bool Hidden { get; }
        public abstract bool TryGetFormatted(TrainCar car, out string s);
    }

    public abstract class DataProvider<T> : IDataProvider
    {
        public string Label { get; }
        public IComparable Order { get; }
        public bool Hidden { get; }

        public abstract bool TryGetFormatted(TrainCar car, out string s);
        public abstract bool TryGetValue(TrainCar car, out T v);

        protected DataProvider(string label, IComparable? order = null, bool hidden = false)
        {
            this.Label = label;
            this.Order = order ?? label;
            this.Hidden = hidden;
        }
    }

    public interface IQuantityProvider : IDimension
    {
        public string QuantityName { get; }
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