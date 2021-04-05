using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float?>;
    using Formatter = Func<float, string>;
    using Pusher = Action<TrainCar, float>;

    static public class Registry
    {
        public static readonly Dictionary<string, DataProvider> providers = new Dictionary<string, DataProvider>();

        public static void Register(DataProvider dp)
        {
            providers[dp.Label] = dp;
            Main.DebugLog($"Registered data provider for {dp.Label}");
        }

        public static DataProvider? GetProvider(string label)
        {
            providers.TryGetValue(label, out var dp);
            return dp;
        }

        public static void RegisterPull(string label, Provider provider, Formatter formatter, IComparable order)
        {
            Register(new QueryDataProvider(label, provider, formatter, order));
        }

        public static Pusher RegisterPush(string label, Formatter formatter, IComparable order)
        {
            var pp = new PushProvider(label, formatter, order);
            Register(pp);
            return pp.SetValue;
        }

        public static Pusher? GetPusher(string label)
        {
            return GetProvider(label) switch
            {
                PushProvider pp => pp.SetValue,
                _ => null
            };
        }
    }
}