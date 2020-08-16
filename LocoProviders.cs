using System;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static class LocoProviders
    {
        public static void Register()
        {
            foreach (TrainCarType carType in Enum.GetValues(typeof(TrainCarType)))
            {
                if (!CarTypes.IsLocomotive(carType))
                    continue;
                Registry.Register(carType, "Slip", car => car.GetComponent<LocoControllerBase>().drivingForce.wheelslip * 100, f => $"{f.ToString("F1")} %");
            }
        }
    }
}