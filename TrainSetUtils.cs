using DV.Logic.Job;
using System.Linq;

namespace DvMod.HeadsUpDisplay
{
    public static class TrainsetUtils
    {
        public static bool IsFacingFrontOfTrainset(TrainCar car) =>
            car.frontCoupler.IsCoupled()
                ? car.frontCoupler.coupledTo.train.indexInTrainset < car.indexInTrainset
                : car.indexInTrainset == 0;

        public static TrainCar CarAtEnd(TrainCar loco, bool atFront) => atFront ? FirstCar(loco) : LastCar(loco);

        public static TrainCar FirstCar(TrainCar loco) => IsFacingFrontOfTrainset(loco) ? loco.trainset.firstCar : loco.trainset.lastCar;
        public static TrainCar LastCar(TrainCar loco) => IsFacingFrontOfTrainset(loco) ? loco.trainset.lastCar : loco.trainset.firstCar;

        public static float OverallLength(this Trainset trainset) => trainset.cars.Sum(c => c.logicCar.length);
        public static float TotalMass(this Trainset trainset) => trainset.cars.Sum(c => c.totalMass + CargoTypes.GetCargoMass(c.LoadedCargo, c.LoadedCargoAmount));
    }
}