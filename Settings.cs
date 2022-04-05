using System.Collections.Generic;
using System.Linq;
using QuantitiesNet;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.HeadsUpDisplay
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public class DrivingInfoSettings
        {
            public bool enabled = true;
            public HashSet<string> disabledProviders = new HashSet<string>(Registry.providers.Keys);
            // <summary>Map from a provider labels to its preferred unit symbol.</summary>
            public List<(string label, string symbol)> preferredUnits = new List<(string, string)>();

            private void DrawUnitSettings(IQuantityProvider provider)
            {
                var dimension = provider.Dimension;
                var label = provider.Label;

                var symbols = GetDisplaySymbols(dimension).ToList();
                if (symbols.Count == 0)
                    return;

                var currentSymbol = GetPreferredSymbol(label);
                var selectedIndex = symbols.IndexOf(currentSymbol);
                if (selectedIndex < 0)
                {
                    SetPreferredSymbol(label, symbols[0]);
                    selectedIndex = 0;
                }

                var changed = UnityModManager.UI.ToggleGroup(
                    ref selectedIndex, symbols.ToArray(), style: null, GUILayout.MinWidth(50), GUILayout.ExpandWidth(false));
                if (changed)
                    SetPreferredSymbol(label, symbols[selectedIndex]);
            }

            private void DrawProviderSettings()
            {
                var providers = Registry.providers.Values
                    .Where(dp => !dp.Hidden)
                    .OrderBy(dp => dp.Order);

                foreach (var provider in providers)
                {
                    var label = provider.Label;

                    GUILayout.BeginHorizontal();

                    GUILayout.Label(label, GUILayout.MinWidth(150), GUILayout.ExpandWidth(false));
                    var result = GUILayout.Toggle(
                        !disabledProviders.Contains(label), "Enable", GUILayout.MinWidth(100), GUILayout.ExpandWidth(false));
                    if (result)
                        disabledProviders.Remove(label);
                    else
                        disabledProviders.Add(label);

                    if (provider is IQuantityProvider quantityProvider)
                        DrawUnitSettings(quantityProvider);

                    GUILayout.EndHorizontal();
                }
            }

            public void Draw()
            {
                GUILayout.Label("Driving info");
                GUILayout.BeginVertical("box");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Enable", GUILayout.ExpandWidth(false));
                enabled = GUILayout.Toggle(enabled, "");
                GUILayout.EndHorizontal();

                DrawProviderSettings();
                GUILayout.EndVertical();
            }

            public bool IsEnabled(IDataProvider dp) => !disabledProviders.Contains(dp.Label);

            private readonly static Dictionary<Dimension, List<string>> KnownUnits = new Dictionary<Dimension, List<string>>()
            {
                { Dimensions.Force.dimension, new List<string>() { "kN", "lbf" } },
                { Dimensions.Length.dimension, new List<string>() { "m", "ft" } },
                { Dimensions.Power.dimension, new List<string>() { "kW", "hp" } },
                { Dimensions.Pressure.dimension, new List<string>() { "bar", "psi" } },
                { Dimensions.Velocity.dimension, new List<string>() { $"km{Unit.DivisionOperator}h", "mph" } },
            };

            private static IEnumerable<string> GetDisplaySymbols(Dimension dimension)
            {
                if (KnownUnits.TryGetValue(dimension, out var symbols))
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

        public DrivingInfoSettings drivingInfoSettings = new DrivingInfoSettings();

        [Draw("Upcoming track info", Collapsible = true, Box = true)]
        public TrackInfoSettings trackInfoSettings = new TrackInfoSettings();

        [Draw("Train info", Collapsible = true, Box = true)]
        public TrainInfoSettings trainInfoSettings = new TrainInfoSettings();

        [Draw("Enable logging")] public bool enableLogging;
        [Draw("Lock position")] public bool lockPosition;

        public readonly string? version = Main.mod?.Info.Version;

        public Vector2 hudPosition;

        public bool IsEnabled(IDataProvider dp) => drivingInfoSettings.IsEnabled(dp);

        override public void Save(UnityModManager.ModEntry entry) => Save<Settings>(this, entry);

        public void Draw()
        {
            drivingInfoSettings.Draw();
            this.Draw(Main.mod);
        }

        public void OnChange()
        {
        }
    }
}
