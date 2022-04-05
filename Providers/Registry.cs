using QuantitiesNet;
using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float?>;
    using Formatter = Func<float, string>;
    using Pusher = Action<TrainCar, float>;

    static public class Registry
    {
        public static readonly Dictionary<string, IDataProvider> providers = new Dictionary<string, IDataProvider>();

        public static void Register(IDataProvider dp)
        {
            providers[dp.Label] = dp;
            Main.DebugLog($"Registered data provider for {dp.Label}");
        }

        public static IDataProvider? GetProvider(string label)
        {
            providers.TryGetValue(label, out var dp);
            return dp;
        }

        public static void RegisterPull(string label, Provider provider, Formatter formatter, IComparable order)
        {
            RegisterPull(label, provider, formatter, order, false);
        }

        public static void RegisterPull(string label, Provider provider, Formatter formatter, IComparable order, bool hidden)
        {
            Register(new FloatQueryDataProvider(label, provider, formatter, order, hidden));
        }

        public static Pusher RegisterPush(string label, Formatter formatter, IComparable order)
        {
            var pp = new FloatPushProvider(label, formatter, order);
            Register(pp);
            return pp.SetValue;
        }

        public static void RegisterPull<D>(string label, Func<TrainCar, Quantity<D>?> provider, IComparable order, bool hidden)
        where D : IDimension, new()
        {
            Register(new QuantityQueryDataProvider<D>(label, provider, order, hidden));
        }

        public static Action<TrainCar, Quantity<D>> RegisterPush<D>(string label, IComparable order, bool hidden)
        where D : IDimension, new()
        {
            var pp = new QuantityPushProvider<D>(label, order, hidden);
            Register(pp);
            return pp.SetValue;
        }
    }
}
