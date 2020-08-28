using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    class Overlay : MonoBehaviour
    {
        public static Overlay instance;
        static GUIStyle noTitleBar;
        static GUIStyle noWrap;

        public Overlay()
        {
            instance = this;
        }

        public void OnGUI()
        {
            if (!Main.enabled)
                return;
            if (PlayerManager.Car == null)
                return;

            if (noTitleBar == null)
            {
                noTitleBar = new GUIStyle(GUI.skin.window);
                noTitleBar.normal.background = null;
                noTitleBar.border.top = noTitleBar.border.bottom;
                noTitleBar.padding.top = noTitleBar.padding.bottom;
                noTitleBar.onNormal = noTitleBar.normal;
            }

            Main.settings.hudPosition = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                Main.settings.hudPosition,
                DrawWindow,
                "",
                noTitleBar);
        }

        void DrawWindow(int windowID)
        {
            if (noWrap == null)
            {
                noWrap = new GUIStyle(GUI.skin.label);
                noWrap.wordWrap = false;
            }

            foreach (var group in Registry.GetProviders(PlayerManager.Car.carType))
            {
                GUILayout.BeginHorizontal("box");
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    if (dp.Enabled)
                        GUILayout.Label(dp.Label, noWrap);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                foreach (var dp in group)
                    if (dp.Enabled)
                        GUILayout.Label(dp.GetValue(PlayerManager.Car), noWrap);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (Main.settings.showCarList)
                DrawCarList();

            GUI.DragWindow();
        }

        void DrawCarList()
        {
            IEnumerable<TrainCar> cars = PlayerManager.Car.trainset.cars.AsReadOnly();
            if (!cars.First().IsLoco && cars.Last().IsLoco)
                cars = cars.Reverse();

            GUILayout.BeginHorizontal("box");
            GUILayout.BeginVertical();
            GUILayout.Label("Cars:", noWrap);
            foreach (TrainCar car in cars)
                GUILayout.Label(car.ID, noWrap);
            GUILayout.EndVertical();

            if (Main.settings.showCarStress)
                DrawCarStress(cars);
            if (Main.settings.showCarJobs)
                DrawCarJobs(cars);
            if (Main.settings.showCarDestinations)
                DrawCarDestinations(cars);

            GUILayout.EndHorizontal();
        }

        GUIStyle rightAlign;
        void DrawCarStress(IEnumerable<TrainCar> cars)
        {
            if (rightAlign == null)
            {
                rightAlign = new GUIStyle(noWrap);
                rightAlign.alignment = TextAnchor.MiddleRight;
            }

            var derailThreshold = SimManager.instance.derailBuildUpThreshold;

            GUILayout.BeginVertical();
            GUILayout.Label("Stress", noWrap);
            foreach (TrainCar car in cars)
            {
                var buildup = car.GetComponent<TrainStress>().derailBuildUp;
                var buildupPct = buildup / derailThreshold * 100;
                GUI.contentColor = Color.HSVToRGB(Mathf.Lerp(1f/3f, 0, (buildupPct - 30f) / 40f), 1f, 1f);
                GUILayout.Label($"{buildupPct.ToString("F0")} %", rightAlign);
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();
        }

        void DrawCarJobs(IEnumerable<TrainCar> cars)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Job", noWrap);
            foreach (TrainCar car in cars)
                GUILayout.Label(JobChainController.GetJobOfCar(car)?.ID ?? " ", noWrap);
            GUILayout.EndVertical();
        }

        void DrawCarDestinations(IEnumerable<TrainCar> cars)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Destination", noWrap);
            foreach (TrainCar car in cars)
                GUILayout.Label(
                    GetNextDestinationTrack(JobChainController.GetJobOfCar(car), car.logicCar)?.ID?.FullDisplayID ?? " ",
                    noWrap);
            GUILayout.EndVertical();
        }

        Track GetNextDestinationTrack(Task task, Car car)
        {
            var data = task.GetTaskData();
            if (data.type == TaskType.Transport || data.type == TaskType.Warehouse)
                return (data.state == TaskState.InProgress && data.cars.Contains(car)) ? data.destinationTrack : null;
            return data.nestedTasks.Select(t => GetNextDestinationTrack(t, car)).FirstOrDefault(track => track != null);
        }

        Track GetNextDestinationTrack(Job job, Car car)
        {
            return job?.tasks.Select(task => GetNextDestinationTrack(task, car)).FirstOrDefault(track => track != null);
        }

        string DumpTask(Task task, int indent = 0)
        {
            var data = task.GetTaskData();
            var indentStr = new string(' ', indent * 2);
            switch (data.type)
            {
                case TaskType.Transport:
                case TaskType.Warehouse:
                    var carsStr = string.Join(",", data.cars.Select(car => car.ID));
                    var destStr = data.destinationTrack.ID.FullDisplayID;
                    return $"{indentStr}{data.warehouseTaskType}:{carsStr}:{destStr}:{data.state}";
                case TaskType.Parallel:
                case TaskType.Sequential:
                    return $"{indentStr}{data.type}\n{string.Join("\n", data.nestedTasks.Select(t => DumpTask(t, indent+1)))}";
            }
            return "";
        }

        string DumpJob(Job job)
        {
            return string.Join("\n", job.tasks.Select(DumpTask));
        }
    }
}