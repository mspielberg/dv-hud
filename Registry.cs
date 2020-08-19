using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static public class Registry
    {
        static Dictionary<TrainCarType, OrderedDictionary> providers =
            new Dictionary<TrainCarType, OrderedDictionary>() {
                { TrainCarType.NotSet, new OrderedDictionary() }
            };

        public static void Register(TrainCarType carType, DataProvider dp)
        {
            if (!providers.ContainsKey(carType))
                providers.Add(carType, new OrderedDictionary());
            providers[carType][dp.Label] = dp;
            Main.DebugLog($"Registered data provider for {dp.Label}: {Environment.StackTrace}");
        }

        public static DataProvider GetProvider(TrainCarType carType, string label)
        {
            if (providers.ContainsKey(carType))
            {
                 var specificProviders = providers[carType];
                 if (specificProviders.Contains(label))
                    return (DataProvider)specificProviders[label];
            }
            return (DataProvider)providers[TrainCarType.NotSet][label];
        }

        public static List<List<DataProvider>> GetProviders(TrainCarType carType)
        {
            var providersForCarType = new List<List<DataProvider>>();
            providersForCarType.Add(new List<DataProvider>(providers[TrainCarType.NotSet].Values.Cast<DataProvider>()));
            if (providers.ContainsKey(carType))
                providersForCarType.Add(new List<DataProvider>(providers[carType].Values.Cast<DataProvider>()));
            return providersForCarType;
        }
    }
}