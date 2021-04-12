using System;
using UnitsNet;

namespace DvMod.HeadsUpDisplay
{
    using Formatter = Func<float, string>;

    public abstract class DataProvider
    {
        public string Label { get; }
        public IComparable Order { get; }
        private readonly Formatter? formatter;
        public readonly QuantityType quantityType;

        public abstract IQuantity? GetQuantity(TrainCar car);

        public string? GetFormatted(TrainCar car)
        {
            var q = GetQuantity(car);
            if (q == null)
                return null;
            if (q.QuantityInfo.ValueType == typeof(Scalar))
                return formatter!((float)q.Value);
            else
                return q.ToUnit(Main.settings.GetUnit(Label)).ToString();
        }

        protected DataProvider(string label, QuantityType quantityType, Formatter? formatter = null, IComparable? order = null)
        {
            Label = label;
            Order = order ?? label;
            this.quantityType = quantityType;
            this.formatter = formatter;
        }
    }

    public class QueryDataProvider : DataProvider
    {
        private readonly Func<TrainCar, IQuantity?> provider;

        public QueryDataProvider(string label, Func<TrainCar, IQuantity?> provider, QuantityType quantityType, Formatter? formatter = null, IComparable? order = null)
        : base(label, quantityType, formatter, order)
        {
            this.provider = provider;
        }

        public override IQuantity? GetQuantity(TrainCar car) => provider(car);
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