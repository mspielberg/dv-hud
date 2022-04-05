using System.Collections.Generic;
using System.Linq;
using QuantitiesNet;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public struct DrivingInfoSettings
        {
            public HashSet<string> disabledProviders;

            static public DrivingInfoSettings Create()
            {
                return new DrivingInfoSettings() { disabledProviders = new HashSet<string>(Registry.providers.Keys) };
            }

            public void Draw()
            {
                GUILayout.BeginVertical("box");
                var providers = Registry.providers.Values
                    .Where(dp => !dp.Hidden)
                    .OrderBy(dp => dp.Order)
                    .Select(dp => dp.Label);
                foreach (string key in providers)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(key, GUILayout.ExpandWidth(false));
                    var result = GUILayout.Toggle(!disabledProviders.Contains(key), "");
                    if (result)
                        disabledProviders.Remove(key);
                    else
                        disabledProviders.Add(key);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }

            public bool IsEnabled(IDataProvider dp) => !disabledProviders.Contains(dp.Label);
        }

        public class TrackInfoSettings
        {
            [Draw("Enable")] public bool enabled = true;
            [Draw("Max events", VisibleOn = "enabled|true")] public int maxEventCount = 10;
            [Draw("Max distance", VisibleOn = "enabled|true")] public double maxEventSpan = 5000;
        }

        public class TrainInfoSettings
        {
            [Draw("Enable")] public bool enabled = true;
            [Draw("Length & mass")] public bool showTrainInfo = true;
            [Draw("Car list")] public bool showCarList = true;
            [Draw("Update period", Min = 0f, VisibleOn = "showCarList|true")] public float updatePeriod = 0.25f;
            [Draw("Group by job", VisibleOn = "showCarList|true")] public bool groupCarsByJob = true;
            [Draw("Stress", VisibleOn = "showCarList|true")] public bool showCarStress = true;
            [Draw("Job ID", VisibleOn = "showCarList|true")] public bool showCarJobs = true;
            [Draw("Destination", VisibleOn = "showCarList|true")] public bool showCarDestinations = true;
            [Draw("Brake status", VisibleOn = "showCarList|true")] public bool showCarBrakeStatus = true;
        }

        public static Vector2 defaultPosition = new Vector2(10, 10);

        public bool showDrivingInfo = true;
        public DrivingInfoSettings drivingInfoSettings = DrivingInfoSettings.Create();

        [Draw("Upcoming track info", Collapsible = true, Box = true)]
        public TrackInfoSettings trackInfoSettings = new TrackInfoSettings();

        [Draw("Train info", Collapsible = true, Box = true)]
        public TrainInfoSettings trainInfoSettings = new TrainInfoSettings();

        [Draw("Unit settings", Collapsible = true, Box = true)]
        public UnitSettings unitSettings = new UnitSettings();

        [Draw("Enable logging")] public bool enableLogging;
        [Draw("Lock position")] public bool lockPosition;

        public readonly string? version = Main.mod?.Info.Version;

        public Vector2 hudPosition;

        public bool IsEnabled(IDataProvider dp) => drivingInfoSettings.IsEnabled(dp);

        override public void Save(UnityModManager.ModEntry entry) => Save<Settings>(this, entry);

        public void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Show driving info", GUILayout.ExpandWidth(false));
            showDrivingInfo = GUILayout.Toggle(showDrivingInfo, "");
            GUILayout.EndHorizontal();
            if (showDrivingInfo)
                drivingInfoSettings.Draw();
            this.Draw(Main.mod);
            unitSettings.Draw();
        }

        public void OnChange()
        {
        }
    }

    public class UnitSettings : UnityModManager.ModSettings
    {
        // <summary>Map from a provider labels to its preferred unit symbol.</summary>
        public readonly List<(string label, string symbol)> preferredUnits = new List<(string, string)>();

        private readonly static Dictionary<string, List<string>> KnownUnits = new Dictionary<string, List<string>>()
        {
            { "Force", new List<string>() { "kN", "lbf" } },
            { "Length", new List<string>() { "m", "ft" } },
            { "Power", new List<string>() { "kW", "hp" } },
            { "Pressure", new List<string>() { "bar", "psi" } },
            { "Velocity", new List<string>() { $"km{Unit.DivisionOperator}h", "mph" } },
        };

        private static IEnumerable<string> GetDisplaySymbols(Dimension dimension)
        {
            var dimensionName = Quantities.GetName(dimension);
            if (dimensionName != null && KnownUnits.TryGetValue(dimensionName, out var symbols))
                return symbols;

            // fallback to all registered units if not known
            if (UnitRegistry.Default.TryGetUnits(dimension, out var units))
                return units.Select(unit => unit.Symbol);

            return Enumerable.Empty<string>();
        }

        private static IEnumerable<Unit> GetDisplayUnits(Dimension dimension)
        {
            return GetDisplaySymbols(dimension)
                .Select(symbol => UnitForSymbol(dimension, symbol))
                .OfType<Unit>();
        }

        private string GetPreferredSymbol(string label)
        {
            return preferredUnits.Where(p => p.label == label).Select(p => p.symbol).FirstOrDefault() ?? "";
        }

        private void SetPreferredSymbol(string label, string symbol)
        {
            var index = preferredUnits.FindIndex(p => p.label == label);
            if (index < 0)
                preferredUnits.Add((label, symbol));
            else
                preferredUnits[index] = (label, symbol);
        }

        public bool TryGetUnit(string label, Dimension dimension, out Unit unit)
        {
            var symbol = GetPreferredSymbol(label);
            if (symbol == "")
                symbol = GetDisplaySymbols(dimension).FirstOrDefault() ?? "";
            var maybeUnit = UnitForSymbol(dimension, symbol);
            if (maybeUnit == null)
            {
                unit = default;
                return false;
            }

            unit = (Unit)maybeUnit;
            return true;
        }

        private static Unit? UnitForSymbol(Dimension dimension, string symbol)
        {
            if (UnitRegistry.Default.TryGetUnits(dimension, out var units))
            {
                return units.FirstOrDefault(unit => unit.Symbol == symbol);
            }
            return default;
        }

        public void Draw()
        {
            var labelsAndDimensions = Registry.providers.Values
                .OfType<IQuantityProvider>()
                .OrderBy(p => p.Label)
                .Select(p => (p.Label, p.Dimension));

            foreach (var (label, dimension) in labelsAndDimensions)
            {
                var symbols = GetDisplaySymbols(dimension).ToList();
                if (symbols.Count == 0)
                    continue;

                var currentSymbol = GetPreferredSymbol(label);
                var selectedIndex = symbols.IndexOf(currentSymbol);
                if (selectedIndex < 0)
                {
                    SetPreferredSymbol(label, symbols[0]);
                    selectedIndex = 0;
                }

                GUILayout.BeginHorizontal(); // GUILayout.ExpandWidth(false));
                GUILayout.Label(label, GUILayout.MinWidth(100), GUILayout.ExpandWidth(false));
                var changed = UnityModManager.UI.ToggleGroup(
                    ref selectedIndex, symbols.ToArray(), style: null, GUILayout.MinWidth(50), GUILayout.ExpandWidth(false));
                if (changed)
                    SetPreferredSymbol(label, symbols[selectedIndex]);
                GUILayout.EndHorizontal();
            }
        }
    }
}
