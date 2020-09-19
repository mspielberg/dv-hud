using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public static class RegistryKeys
    {
        public static object AllCars = new object();
    }

    static public class Registry
    {
        private static readonly Dictionary<object, OrderedDictionary> providers =
            new Dictionary<object, OrderedDictionary>() { [RegistryKeys.AllCars] = new OrderedDictionary() };

        public static void Register(object key, DataProvider dp)
        {
            if (!providers.ContainsKey(key))
                providers.Add(key, new OrderedDictionary());
            providers[key][dp.Label] = dp;
            Main.DebugLog($"Registered data provider for {key}: {dp.Label}");
        }

        public static DataProvider? GetProvider(object key, string label)
        {
            if (providers.ContainsKey(key))
            {
                 var specificProviders = providers[key];
                 if (specificProviders.Contains(label))
                    return (DataProvider)specificProviders[label];
            }
            return (DataProvider)providers[RegistryKeys.AllCars][label];
        }

        public static List<List<DataProvider>> GetProviders(object key)
        {
            var providersForCarType = new List<List<DataProvider>>
            {
                new List<DataProvider>(providers[RegistryKeys.AllCars].Values.Cast<DataProvider>())
            };
            if (providers.ContainsKey(key))
                providersForCarType.Add(new List<DataProvider>(providers[key].Values.Cast<DataProvider>()));
            return providersForCarType;
        }
    }
}