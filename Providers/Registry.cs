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

        public static Pusher? GetPusher(string label)
        {
            return GetProvider(label) switch
            {
                FloatPushProvider pp => pp.SetValue,
                _ => null
            };
        }
    }
}