using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    using Provider = Func<TrainCar, float>;
    using Formatter = Func<float, string>;

    public readonly struct DataProvider
    {
        public readonly string label;
        public readonly Provider provider;
        public readonly Formatter formatter;

        public DataProvider(string label, Provider provider, Formatter formatter)
        {
            this.label = label;
            this.provider = provider;
            this.formatter = formatter;
        }
    }

    static public class Registry
    {
        static Dictionary<TrainCarType, List<DataProvider>> providers = new Dictionary<TrainCarType, List<DataProvider>>() {
            {TrainCarType.NotSet, new List<DataProvider>()}
        };
        public static void Register(TrainCarType carType, string name, Provider provider, Formatter formatter)
        {
            var dp = new DataProvider(name, provider, formatter);
            if (!providers.ContainsKey(carType))
                providers.Add(carType, new List<DataProvider>());
            providers[carType].Add(dp);
        }

        public static List<List<DataProvider>> GetProviders(TrainCarType carType)
        {
            var providersForCarType = new List<List<DataProvider>>();
            List<DataProvider> genericProviders;
            if (providers.TryGetValue(TrainCarType.NotSet, out genericProviders))
                providersForCarType.Add(genericProviders);
            List<DataProvider> specificProviders;
            if (providers.TryGetValue(carType, out specificProviders))
                providersForCarType.Add(specificProviders);
            return providersForCarType;
        }
    }
}