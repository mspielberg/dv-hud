using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float?>;
    using Formatter = Func<float, string>;
    using Pusher = Action<TrainCar, float>;

    public static class RegistryKeys
    {
        public static object AllCars = new object();
    }

    static public class Registry
    {
        private static readonly Dictionary<object, OrderedDictionary> providers =
            new Dictionary<object, OrderedDictionary>() { [RegistryKeys.AllCars] = new OrderedDictionary() };

        public static void Register(object key, IDataProvider dp)
        {
            if (!providers.ContainsKey(key))
                providers.Add(key, new OrderedDictionary());
            providers[key][dp.Label] = dp;
            Main.DebugLog($"Registered data provider for {key}: {dp.Label}");
        }

    public static IDataProvider? GetProvider(object key, string label)
        {
            if (providers.ContainsKey(key))
            {
                 var specificProviders = providers[key];
                 if (specificProviders.Contains(label))
                    return (IDataProvider)specificProviders[label];
            }
            return (IDataProvider)providers[RegistryKeys.AllCars][label];
        }

        public static List<List<IDataProvider>> GetProviders(object key)
        {
            var providersForCarType = new List<List<IDataProvider>>
            {
                new List<IDataProvider>(providers[RegistryKeys.AllCars].Values.Cast<IDataProvider>())
            };
            if (providers.ContainsKey(key))
                providersForCarType.Add(new List<IDataProvider>(providers[key].Values.Cast<IDataProvider>()));
            return providersForCarType;
        }

        public static void RegisterPull(object key, string label, Provider provider, Formatter formatter, IComparable order)
        {
            Register(key, new QueryDataProvider(label, provider, formatter, order));
        }

        public static Pusher RegisterPush(object key, string label, Formatter formatter, IComparable order)
        {
            var pp = new PushProvider(label, formatter, order);
            Register(key, pp);
            return pp.SetValue;
        }

        public static Pusher? GetPusher(object key, string label)
        {
            if (GetProvider(key, label) is PushProvider pp)
                return pp.SetValue;
            return null;
        }
    }
}