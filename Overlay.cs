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
                    GUILayout.Label(dp.label);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    GUILayout.Label(dp.formatter(dp.provider(PlayerManager.Car)));
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }
    }
}