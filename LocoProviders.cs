using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static class LocoProviders
    {
        public static PushProvider tractiveEffortProvider = new PushProvider(
            "Tractive effort", () => Main.settings.ShowTractiveEffort, f => $"{(f / 1000).ToString("F0")} kN");

        public static PushProvider adhesionProvider = new PushProvider(
            "Adhesion", () => Main.settings.ShowAdhesion, f => $"{(f / 1000).ToString("F0")} kN");

        public static void Register(TrainCarType carType)
        {
            Registry.Register(carType, tractiveEffortProvider);
            Registry.Register(carType, adhesionProvider);
            Registry.Register(carType, new QueryDataProvider(
                "Slip",
                () => Main.settings.ShowSlip,
                car => car.GetComponent<DrivingForce>().wheelslip * 100,
                f => $"{f.ToString("F1")} %"));

            if (CarTypes.IsSteamLocomotive(carType))
                SteamLocoProviders.Register(carType);
        }

        [HarmonyPatch]
        static class GetTractionForcePatch
        {
            static void Postfix(LocoControllerBase __instance, float __result)
            {
                tractiveEffortProvider.SetValue(__instance.train, __result);
            }

            static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(LocoControllerDiesel), "GetTractionForce");
                yield return AccessTools.Method(typeof(LocoControllerShunter), "GetTractionForce");
                yield return AccessTools.Method(typeof(LocoControllerSteam), "GetTractionForce");
            }
        }

        [HarmonyPatch(typeof(DrivingForce), "UpdateWheelslip")]
        static class UpdateWheelslipPatch
        {
            static FieldInfo slipLimitField = AccessTools.DeclaredField(typeof(DrivingForce), "tractionForceWheelslipLimit");
            static void Postfix(DrivingForce __instance, Bogie bogie)
            {
                var car = bogie.Car;
                adhesionProvider.SetValue(car, (float)slipLimitField.GetValue(__instance) * car.Bogies.Length);
            }
        }
    }

    static class SteamLocoProviders
    {
        public static PushProvider cutoffProvider = new PushProvider(
            "Cutoff", () => true, f => $"{Mathf.RoundToInt(f * 100)} %");

        public static void Register(TrainCarType carType)
        {
            Registry.Register(carType, cutoffProvider);
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateTick")]
        static class SimulateTickPatch
        {
            static bool Prefix(SteamLocoSimulation __instance)
            {
                cutoffProvider.SetValue(__instance.GetComponent<TrainCar>(), __instance.cutoff.value);
                return true;
            }
        }
    }
}