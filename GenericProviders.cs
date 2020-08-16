using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    static class GenericProviders
    {
        public static void Register()
        {
            Registry.Register(TrainCarType.NotSet, "Speed", car => car.GetForwardSpeed(), f => $"{f.ToString("F1")} kph");
            Registry.Register(TrainCarType.NotSet, "Grade", car => {
                var inclination = car.transform.localEulerAngles.x;
                inclination = inclination > 180 ? 360f - inclination : -inclination;
                return Mathf.Tan(inclination * Mathf.PI / 180) * 100;
            }, f => $"{f.ToString("F1")} %");
            Registry.Register(TrainCarType.NotSet, "Brake pipe", car => car.brakeSystem?.brakePipePressure ?? 0f, f => $"{f.ToString("F2")} bar");
        }
    }
}