using DV;
using HarmonyLib;
using QuantityTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    internal static class LocoProviders
    {
        // public static FloatPushProvider tractiveEffortProvider = new FloatPushProvider(
            // "Tractive effort", f => $"{f / 1000:F0} kN");
        public static QuantityPushProvider<Force> tractiveEffortProvider =
            new QuantityPushProvider<Force>("Tractive effort");

        public static FloatPushProvider adhesionProvider = new FloatPushProvider(
            "Adhesion", f => $"{f / 1000:F0} kN");

        // public static FloatPushProvider indicatedPowerProvider = new FloatPushProvider(
        //     "Power", f => $"{f / 1000:F0} kW");
        public static QuantityPushProvider<Power> indicatedPowerProvider =
            new QuantityPushProvider<Power>("Power");

        public static void Register()
        {
            Registry.Register(tractiveEffortProvider);
            Registry.Register(adhesionProvider);
            Registry.Register(indicatedPowerProvider);
            Registry.Register(new FloatQueryDataProvider(
                "Slip",
                car => car.GetComponent<DrivingForce>()?.wheelslip,
                f => $"{f:P1}"));

            SteamLocoProviders.Register();
        }

        [HarmonyPatch]
        public static class GetTractionForcePatch
        {
            public static void Postfix(LocoControllerBase __instance, float __result)
            {
                if (!AppUtil.IsPaused)
                {
                    tractiveEffortProvider.SetValue(__instance.train, __result * Force.Newton);
                    indicatedPowerProvider.SetValue(__instance.train, (__result * Force.Newton) * (__instance.GetSpeedKmH() * Velocity.KilometrePerHour));
                }
            }

            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(LocoControllerDiesel), nameof(LocoControllerBase.GetTractionForce));
                yield return AccessTools.Method(typeof(LocoControllerShunter), nameof(LocoControllerBase.GetTractionForce));
                yield return AccessTools.Method(typeof(LocoControllerSteam), nameof(LocoControllerBase.GetTractionForce));
                if (UnityModManager.FindMod("DVCustomCarLoader")?.Assembly is Assembly assembly && assembly != null)
                {
                    var typeNames = new string[]
                    {
                        "DieselElectric.CustomLocoControllerDiesel",
                        "Steam.CustomLocoControllerSteam",
                    };
                    var methods = typeNames
                        .Select(n => assembly.GetType($"DVCustomCarLoader.LocoComponents.{n}"))
                        .OfType<Type>()
                        .Where(typeof(LocoControllerBase).IsAssignableFrom)
                        .Select(t => t.GetMethod("GetTractionForce"))
                        .OfType<MethodBase>();
                    foreach (var method in methods)
                        yield return method;
                }
            }
        }

        [HarmonyPatch(typeof(DrivingForce), "UpdateWheelslip")]
        public static class UpdateWheelslipPatch
        {
            private static readonly FieldInfo slipLimitField =
                AccessTools.DeclaredField(typeof(DrivingForce), nameof(DrivingForce.tractionForceWheelslipLimit));

            public static void Postfix(DrivingForce __instance, Bogie bogie)
            {
                var car = bogie.Car;
                adhesionProvider.SetValue(car, (float)slipLimitField.GetValue(__instance) * car.Bogies.Length);
            }
        }
    }

    internal static class SteamLocoProviders
    {
        public static FloatPushProvider cutoffProvider = new FloatPushProvider("Cutoff", f => $"{f:P0}");

        public static void Register()
        {
            Registry.Register(cutoffProvider);
        }

        [HarmonyPatch(typeof(SteamLocoSimulation), "SimulateTick")]
        public static class SimulateTickPatch
        {
            public static bool Prefix(SteamLocoSimulation __instance)
            {
                cutoffProvider.SetValue(TrainCar.Resolve(__instance.gameObject), __instance.cutoff.value);
                return true;
            }
        }
    }
}