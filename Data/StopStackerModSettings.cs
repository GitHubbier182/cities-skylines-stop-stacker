using UnityEngine;

namespace StopStacker
{
    internal enum StopStackerPropStyle
    {
        Modern = 0,
        Futuristic = 1,
        OldWorld = 2,
        None = 3
    }

    internal static class StopStackerModSettings
    {
        private const string BusStopSignStyleKey = "StopStacker.BusStopSignStyle";
        private const string DispatchBoardStyleKey = "StopStacker.DispatchBoardStyle";
        private const string DisableMultiBusLoadingAtDisabledStopsKey = "StopStacker.DisableMultiBusLoadingAtDisabledStops";
        private const string AdvancedDiagnosticsKey = "StopStacker.AdvancedDiagnostics";
        private const int DefaultStyleIndex = (int)StopStackerPropStyle.Modern;
        private static volatile bool _disableMultiBusLoadingAtDisabledStops =
            PlayerPrefs.GetInt(DisableMultiBusLoadingAtDisabledStopsKey, 0) != 0;

        public static readonly string[] PropStyleOptions =
        {
            "Modern (current style)",
            "Futuristic",
            "Old world",
            "None"
        };

        public static int BusStopSignStyleIndex
        {
            get { return NormalizeStyleIndex(PlayerPrefs.GetInt(BusStopSignStyleKey, DefaultStyleIndex)); }
            set
            {
                PlayerPrefs.SetInt(BusStopSignStyleKey, NormalizeStyleIndex(value));
                PlayerPrefs.Save();
            }
        }

        public static int DispatchBoardStyleIndex
        {
            get { return NormalizeStyleIndex(PlayerPrefs.GetInt(DispatchBoardStyleKey, DefaultStyleIndex)); }
            set
            {
                PlayerPrefs.SetInt(DispatchBoardStyleKey, NormalizeStyleIndex(value));
                PlayerPrefs.Save();
            }
        }

        public static StopStackerPropStyle BusStopSignStyle
        {
            get { return StyleFromIndex(BusStopSignStyleIndex); }
        }

        public static StopStackerPropStyle DispatchBoardStyle
        {
            get { return StyleFromIndex(DispatchBoardStyleIndex); }
        }

        public static bool DisableMultiBusLoadingAtDisabledStops
        {
            get { return _disableMultiBusLoadingAtDisabledStops; }
            set
            {
                _disableMultiBusLoadingAtDisabledStops = value;
                PlayerPrefs.SetInt(DisableMultiBusLoadingAtDisabledStopsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool AdvancedDiagnostics
        {
            get { return PlayerPrefs.GetInt(AdvancedDiagnosticsKey, 0) != 0; }
            set
            {
                bool changed = AdvancedDiagnostics != value;
                PlayerPrefs.SetInt(AdvancedDiagnosticsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (changed)
                {
                    StopStackerDiagnostics.Info(
                        "Advanced diagnostics " +
                        (value ? "enabled." : "disabled."));
                }
            }
        }

        public static string GetStyleLogValue(StopStackerPropStyle style)
        {
            switch (style)
            {
                case StopStackerPropStyle.Futuristic:
                    return "futuristic";
                case StopStackerPropStyle.OldWorld:
                    return "old-world";
                case StopStackerPropStyle.None:
                    return "none";
                default:
                    return "modern";
            }
        }

        private static StopStackerPropStyle StyleFromIndex(int value)
        {
            return (StopStackerPropStyle)NormalizeStyleIndex(value);
        }

        private static int NormalizeStyleIndex(int value)
        {
            if (value < 0)
                return DefaultStyleIndex;

            if (value >= PropStyleOptions.Length)
                return DefaultStyleIndex;

            return value;
        }
    }
}
