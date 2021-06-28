using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    internal class TabletWrapper
    {
        private readonly object tablet;

        private readonly MethodInfo getMasterLoco;
        private readonly MethodInfo setNextJunction;

        private readonly MethodInfo setCurrentSpeed;
        private readonly MethodInfo setSpeedLimitNext;
        private readonly MethodInfo setSpeedMax;
        private readonly MethodInfo setSpeedLimitConsist;

        private readonly MethodInfo setNextWyeDir;
        private readonly MethodInfo setCurrentGrade;
        private readonly MethodInfo setWheelSlip;
        private readonly MethodInfo setTrainLength;
        private readonly MethodInfo setTrainWeight;

        private readonly MethodInfo setTrackSpeedItems;
        private readonly MethodInfo setTrackGradeItems;
        private readonly MethodInfo setTrackJunctionItems;

        public TabletWrapper(object tablet)
        {
            this.tablet = tablet;
            var tabletType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Cybex.ITabletComputer")).OfType<Type>().First();

            getMasterLoco = AccessTools.PropertyGetter(tabletType, "MasterLoco");
            setNextJunction = AccessTools.PropertySetter(tabletType, "NextJunction");

            setCurrentSpeed = AccessTools.Method(tabletType, "SetCurrentSpeed");
            setSpeedLimitNext = AccessTools.Method(tabletType, "SetSpeedLimitNext");
            setSpeedMax = AccessTools.Method(tabletType, "SetSpeedMax");
            setSpeedLimitConsist = AccessTools.Method(tabletType, "SetSpeedLimitConsist");

            setNextWyeDir = AccessTools.Method(tabletType, "SetNextWyeDir");
            setCurrentGrade = AccessTools.Method(tabletType, "SetCurrentGrade");
            setWheelSlip = AccessTools.Method(tabletType, "SetWheelSlip");
            setTrainLength = AccessTools.Method(tabletType, "SetTrainLength");
            setTrainWeight = AccessTools.Method(tabletType, "SetTrainWeight");

            setTrackSpeedItems = AccessTools.Method(tabletType, "SetTrackSpeedItems");
            setTrackGradeItems = AccessTools.Method(tabletType, "SetTrackGradeItems");
            setTrackJunctionItems = AccessTools.Method(tabletType, "SetTrackJunctionItems");
        }

        public bool Valid => (UnityEngine.Object)tablet != null;

        public TrainCar? GetMasterLoco() => (TrainCar?)getMasterLoco.Invoke(tablet, null);
        public void SetNextJunction(Junction junction) => setNextJunction.Invoke(tablet, new object[] { junction });

        public void SetCurrentSpeed(float speed) => setCurrentSpeed.Invoke(tablet, new object[] { speed });
        public void SetSpeedLimitNext(float speed) => setSpeedLimitNext.Invoke(tablet, new object[] { speed });
        public void SetSpeedMax(float speed) => setSpeedMax.Invoke(tablet, new object[] { speed });
        public void SetSpeedLimitConsist(float speed) => setSpeedLimitConsist.Invoke(tablet, new object[] { speed });

        public void SetNextWyeDir(int dir) => setNextWyeDir.Invoke(tablet, new object[] { dir });
        public void SetCurrentGrade(float grade) => setCurrentGrade.Invoke(tablet, new object[] { grade });
        public void SetWheelSlip(float slip) => setWheelSlip.Invoke(tablet, new object[] { slip });
        public void SetTrainLength(float length) => setTrainLength.Invoke(tablet, new object[] { length });
        public void SetTrainWeight(float weight) => setTrainWeight.Invoke(tablet, new object[] { weight });

        public void SetTrackSpeedItems(IEnumerable<(float span, float speed)> items) => setTrackSpeedItems.Invoke(tablet, new object[] { items.ToArray() });
        public void SetTrackGradeItems(IEnumerable<(float span, float grade, float extent)> items) => setTrackGradeItems.Invoke(tablet, new object[] { items.ToArray() });
    }

    public class DERAILDigitalIntegration : MonoBehaviour
    {
        private TabletWrapper? tablet;

        public void Start()
        {
            if (UnityModManager.FindMod("DERAILDigital") == null)
                Destroy(this);
            else
                StartCoroutine(SearchForTablet());
        }

        private Type? FindTabletControllerType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var controllerType = assembly.GetType("Cybex.TabletController");
                if (controllerType != null)
                {
                    Main.DebugLog($"Found {controllerType.AssemblyQualifiedName}");
                    return controllerType;
                }
            }
            return null;
        }

        private IEnumerator SearchForTablet()
        {
            while (tablet == null)
            {
                yield return WaitFor.Seconds(1f);
                if (!UnityModManager.FindMod("DERAILDigital").Enabled)
                    continue;
                var tabletControllerType = FindTabletControllerType();
                if (tabletControllerType == null)
                {
                    Main.DebugLog("Could not find type Cybex.TabletController");
                    continue;
                }
                var getInstance = AccessTools.PropertyGetter(tabletControllerType, "Instance");
                if (getInstance == null)
                {
                    Main.DebugLog("Could not find property getter Instance");
                    continue;
                }
                var tabletController = getInstance.Invoke(null, null);
                if (tabletController == null)
                {
                    Main.DebugLog("null returned from Instance");
                    continue;
                }
                tablet = new TabletWrapper(tabletController);
            }
            StartCoroutine(Pusher());
        }

        private void PushProviderData(TrainCar car)
        {
            foreach (var provider in Registry.providers.Values)
            {
                switch (provider.Label)
                {
                    case "Speed": provider.GetValue(car).ForEach(tablet!.SetCurrentSpeed); break;
                    case "Grade": provider.GetValue(car).ForEach(v => tablet!.SetCurrentGrade(v * 10)); break;
                    case "Consist mass": provider.GetValue(car).ForEach(v => tablet!.SetTrainWeight(v / 1000)); break;
                    case "Consist length": provider.GetValue(car).ForEach(v => tablet!.SetTrainLength(v)); break;
                    case "Slip": provider.GetValue(car).ForEach(v => tablet!.SetWheelSlip(v * 100)); break;
                }
            }
        }

        private void PushConsistSpeedLimit(TrainCar loco)
        {
            var length = loco.trainset.OverallLength();
            var lastCar = TrainsetUtils.CarAtEnd(loco, loco.GetComponent<LocoControllerBase>().reverser < 0);
            var bogie = lastCar.Bogies[0];
            if (bogie.track == null)
                return;
            var startLimit = TrackFollower.GetSpeedLimit(bogie.track, bogie.traveller.Span, bogie.trackDirection >= 0) ?? 0f;
            var intraTrainLimits = TrackFollower.FollowTrack(bogie.track, bogie.traveller.Span, bogie.trackDirection * length)
                .ResolveJunctionSpeedLimits()
                .TakeWhile(ev => ev.span < length)
                .OfType<SpeedLimitEvent>()
                .Select(ev => ev.limit);
            var limits = (startLimit > 0f ? intraTrainLimits.Prepend((int)startLimit) : intraTrainLimits).ToArray();
            Main.DebugLog($"limits affecting train: {string.Join(",", limits)}");
            if (limits.Length > 0)
            {
                tablet!.SetSpeedMax(limits.Last());
                tablet!.SetSpeedLimitConsist(limits.Min());
            }
        }

        private void PushEvents(TrainCar loco)
        {
            var bogie = loco.Bogies[1];
            if (bogie.track == null)
                return;
            var direction = bogie.trackDirection;
            if (loco.GetComponent<LocoControllerBase>().reverser < 0)
                direction *= -1;

            var prevGradeEvent = TrackFollower.GetGrade(bogie.track, bogie.traveller.Span, direction >= 0);

            var events = TrackFollower.FollowTrack(bogie.track, bogie.traveller.Span, direction * double.PositiveInfinity)
                .ResolveJunctionSpeedLimits()
                .FilterRedundantSpeedLimits()
                .FilterGradeEvents(prevGradeEvent?.grade ?? 0f)
                .TakeWhile(ev => ev.span < Main.settings.trackInfoSettings.maxEventSpan)
                .ToArray();

            var speedEvents = events.OfType<SpeedLimitEvent>().Select(ev => ((float)ev.span, (float)ev.limit)).ToArray();
            tablet!.SetTrackSpeedItems(speedEvents);

            var gradeEvents = prevGradeEvent != null
                ? events.OfType<GradeEvent>().Prepend(new GradeEvent(-prevGradeEvent.span, true, prevGradeEvent.grade))
                : events.OfType<GradeEvent>();
            Main.DebugLog($"relevant grade events: {string.Join(",", gradeEvents)}");
            var gradeItems = gradeEvents
                .Zip(
                    gradeEvents.Skip(1),
                    (a, b) => ((float)a.span, a.grade * 10, (float)(b.span - a.span)))
                .Where(item => (int)item.Item2 != 0);
            Main.DebugLog($"setting grade items: {string.Join(",", gradeItems)}");
            tablet.SetTrackGradeItems(gradeItems);

            var nextJunction = events.OfType<JunctionEvent>().Select(ev => ev.junction).FirstOrDefault();
            if (nextJunction != default)
            {
                tablet.SetNextJunction(nextJunction);
                tablet.SetNextWyeDir(nextJunction.selectedBranch == 0 ? 1 : -1);
            }
            else
            {
                tablet.SetNextWyeDir(0);
            }
        }

        private IEnumerator Pusher()
        {
            while (tablet?.Valid ?? false)
            {
                yield return WaitFor.Seconds(1f);
                var loco = tablet.GetMasterLoco();
                if (loco == null)
                    continue;
                PushProviderData(loco);
                PushConsistSpeedLimit(loco);
                PushEvents(loco);
            }
            StartCoroutine(SearchForTablet());
        }
    }
}