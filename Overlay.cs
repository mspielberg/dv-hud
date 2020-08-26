using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    class Overlay : MonoBehaviour
    {
        public static Overlay instance;
        public Main.Settings settings;

        public Overlay()
        {
            this.settings = Main.settings;
            instance = this;
        }

        public void OnGUI()
        {
            if (!Main.enabled)
                return;
            if (PlayerManager.Car == null)
                return;
            
            foreach (var group in Registry.GetProviders(PlayerManager.Car.carType))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    if (dp.Enabled)
                        GUILayout.Label(dp.Label);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    if (dp.Enabled)
                        GUILayout.Label(dp.GetValue(PlayerManager.Car));
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (Main.settings.showCarList)
                DrawCarList();
        }

        void DrawCarList()
        {
            if (PlayerManager.Car == null)
                return;
            IEnumerable<TrainCar> cars = PlayerManager.Car.trainset.cars.AsReadOnly();
            if (cars.Last().IsLoco)
                cars = cars.Reverse();
            var stressThreshold = SimManager.instance.derailStressThreshold;
            var derailThreshold = SimManager.instance.derailBuildUpThreshold;

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label("Cars:");
            foreach (TrainCar car in cars)
                GUILayout.Label(car.ID);
            GUILayout.EndVertical();

            if (Main.settings.showTrainStress)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("Stress %");

                foreach (TrainCar car in cars)
                {
                    var buildup = car.GetComponent<TrainStress>().derailBuildUp;
                    var buildupPct = buildup / derailThreshold * 100;
                    GUI.contentColor = Color.HSVToRGB(Mathf.Lerp(1f/3f, 0, (buildupPct - 30f) / 40f), 1f, 1f);
                    GUILayout.Label(buildupPct.ToString("F0"));
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
        }
    }
}