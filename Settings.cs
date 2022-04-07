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
            public List<ProviderSettings> providerSettings = new List<ProviderSettings>();

            static DrivingInfoSettings()
            {
                UnitRegistry.Default.Add(Unit.Of<Dimensions.MassFlow>("kg/h", 1, QuantitiesNet.Units.Kilogram / QuantitiesNet.Units.Hour));
                UnitRegistry.Default.Add(Unit.Of<Dimensions.MassFlow>("lb/h", 1, QuantitiesNet.Units.Pound / QuantitiesNet.Units.Hour));
                UnitRegistry.Default.Add(Unit.Of<Dimensions.Power>("btu/m", 1, QuantitiesNet.Units.Btu / QuantitiesNet.Units.Minute));
            }

            public class ProviderSettings
            {
                public string providerLabel = "";
                public string unitSymbol = "";
                public int precision;

                public override string ToString() => $"{providerLabel}: {unitSymbol}, {precision}";
            }

            private void DrawPrecisionSettings(IDataProvider provider)
            {
                var settings = GetProviderSettings(provider);
                UnityModManager.UI.DrawIntField(
                    ref settings.precision, "Precision", style: null, GUILayout.Width(20), GUILayout.ExpandWidth(false));
                if (settings.precision < 0)
                    settings.precision = 0;
            }

            private void DrawUnitSettings(IQuantityProvider provider)
            {
                var dimension = provider.Dimension;
                var settings = GetProviderSettings(provider);

                var symbols = GetDisplaySymbols(dimension).ToList();
                if (symbols.Count == 0)
                    return;

                var currentSymbol = settings.unitSymbol;
                var selectedIndex = symbols.IndexOf(currentSymbol);
                if (selectedIndex < 0)
                {
                    settings.unitSymbol = symbols[0];
                    selectedIndex = 0;
                }

                var changed = UnityModManager.UI.ToggleGroup(
                    ref selectedIndex, symbols.ToArray(), style: null, GUILayout.MinWidth(50), GUILayout.ExpandWidth(false));
                if (changed)
                    settings.unitSymbol = symbols[selectedIndex];
            }

            private void DrawProviderSettings(IDataProvider provider)
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
                {
                    DrawPrecisionSettings(quantityProvider);
                    DrawUnitSettings(quantityProvider);
                }

                GUILayout.EndHorizontal();
            }

            private void DrawProviderSettings()
            {
                var providers = Registry.providers.Values
                    .Where(dp => !dp.Hidden)
                    .OrderBy(dp => dp.Order);

                foreach (var provider in providers)
                    DrawProviderSettings(provider);
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
                { Dimensions.Mass.dimension, new List<string>() { "kg", "lb" } },
                { Dimensions.Power.dimension, new List<string>() { "kW", "hp", "btu/m" } },
                { Dimensions.Pressure.dimension, new List<string>() { "bar", "psi" } },
                { Dimensions.Velocity.dimension, new List<string>() { "km/h", "mph" } },
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

            public ProviderSettings GetProviderSettings(IDataProvider provider)
            {
                var label = provider.Label;
                var settings = providerSettings.Find(p => p.providerLabel == label);
                if (settings == default)
                {
                    settings = new ProviderSettings() { providerLabel = label };
                    providerSettings.Add(settings);
                }
                return settings;
            }

            public bool TryGetUnit(IQuantityProvider provider, out Unit unit)
            {
                var settings = GetProviderSettings(provider);
                var dimension = provider.Dimension;
                var symbol = settings.unitSymbol;
                if (symbol.Length == 0)
                    symbol = GetDisplaySymbols(dimension).FirstOrDefault() ?? "";
                var maybeUnit = UnitForSymbol(dimension, symbol);
                if (maybeUnit == null)
                {
#pragma warning disable CS8625
                    unit = default;
#pragma warning restore CS8625
                    return false;
                }

                unit = (Unit)maybeUnit;
                return true;
            }

            private static Unit? UnitForSymbol(Dimension dimension, string symbol)
            {
                if (UnitRegistry.Default.TryGetUnits(dimension, out var units))
                {
                    return units.Find(unit => unit.Symbol == symbol);
                }
                return default;
            }
        }

        public class TrackInfoSettings
        {
            public enum EventDistanceUnits
            {
                m,
                mi,
                ft,
            }
            [Draw("Enable")] public bool enabled = true;
            [Draw("Max events", VisibleOn = "enabled|true")] public int maxEventCount = 10;
            [Draw("Max distance", VisibleOn = "enabled|true")] public double maxEventSpan = 5000;
            [Draw("Distance units", Type = DrawType.ToggleGroup)] public EventDistanceUnits distanceUnits = EventDistanceUnits.m;
        }

        public class TrainInfoSettings
        {
            public enum LengthUnits
            {
                m,
                ft,
            }
            [Draw("Enable")] public bool enabled = true;
            [Draw("Length & mass")] public bool showTrainInfo = true;
            [Draw("Length units", Type = DrawType.ToggleGroup)] public LengthUnits lengthUnits = LengthUnits.m;
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
