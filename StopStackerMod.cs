using ColossalFramework.UI;
using ICities;
using ScratchyBald.CitiesSkylines.UI;
using UnityEngine;

namespace StopStacker
{
    internal static class StopStackerFeatures
    {
        public static readonly bool BusStopPositionHarmonyEnabled = true;
        public static readonly bool IptEssentialsPassengerStatsCompatibilityEnabled = true;
        public static readonly bool LauncherButtonEnabled = true;
        public static readonly bool PassengerWaitPositionHarmonyEnabled = true;
        public static readonly bool BusDwellReductionEnabled = true;
        public static readonly bool MultiBusStopServiceEnabled = true;
        public static bool LevelActive;
        public static readonly ReleaseNoticeContent ReleaseNotice = new ReleaseNoticeContent(
            "StopStacker.ShownReleaseNoticeId",
            "v2.2.5",
            "Stop Stacker 2.2.5",
            "Station platforms keep their own waiting areas",
            string.Empty,
            "SS",
            new[]
            {
                "Bus-station platforms with native building lanes stay under vanilla control instead of being matched to a nearby roadside berth.",
                "Supported roadside and bus-lane stops keep their existing berth, passenger and multi-bus service behavior."
            },
            true,
            string.Empty,
            null,
            new[]
            {
                new ReleaseNoticeVersion("v2.2.4", "9 August 2026, 23:26 BST", new[]
                {
                    "Detailed Stop Stacker and shared scan diagnostics stay off unless Advanced logs is enabled.",
                    "Stop signs and departure boards stay complete when route or road changes overlap a paced visual rebuild."
                }, true),
                new ReleaseNoticeVersion("v2.2.3", "30 July 2026, 12:21 BST", new[]
                {
                    "Concurrent buses must be stationary before passenger exchange begins.",
                    "Expanded vehicle pools and exceptionally long Bus routes are fully discovered.",
                    "Opening Stop Stacker during a rebuild prioritises stops in the camera view.",
                    "Adds optional UnifiedUI launcher support with the standalone fallback."
                }, true),
                new ReleaseNoticeVersion("v2.2.2", "29 July 2026, 02:08 BST", new[]
                {
                    "Maximum-capacity transport-line cities no longer freeze Stop Stacker.",
                    "Waiting positions stay with the correct cim and IPT passenger figures reset correctly.",
                    "Improves rebuilding after road changes, city switches and load recovery."
                }, true),
                new ReleaseNoticeVersion("v2.2.1", "15 July 2026, 18:22 BST", new[]
                {
                    "Waiting passengers, signs and boards stay on the outward pavement side of left-hand custom roads."
                }, false),
                new ReleaseNoticeVersion("v2.2.0", "10 July 2026, 22:44 BST", new[]
                {
                    "Paces topology, passenger and visual rebuilding to reduce large-city startup work.",
                    "Improves UI-scale overlay hiding and IPT-family passenger figures.",
                    "Preserves external bus-spacing mods and cleans old saved stop signs."
                }, true),
                new ReleaseNoticeVersion("v2.1.1", "4 July 2026, 21:09 BST", new[]
                {
                    "Adds an option for disabled stops to opt out of multi-bus loading.",
                    "Improves IPT passenger figures and old sign cleanup."
                }, true),
                new ReleaseNoticeVersion("v2.1.0", "2 July 2026, 07:17 BST", new[]
                {
                    "Adds saved per-stop disable controls and a reset option.",
                    "Keeps signs and dispatch boards runtime-only and improves live passenger counts."
                }, true),
                new ReleaseNoticeVersion("v2.0.2", "27 June 2026, 13:31 BST", new[]
                {
                    "Reduces large-save pauses and avoids unsafe native sign props.",
                    "Adds sign and dispatch-board styles plus safer overlay hiding.",
                    "Stands down from departure nudges when another mod controls bus spacing."
                }, true),
                new ReleaseNoticeVersion("v2.0.1", "26 June 2026, 17:51 BST", new[]
                {
                    "Paces startup work and selects safer native sign props.",
                    "Keeps Stop Stacker runtime-only with no saved berth state."
                }, true),
                new ReleaseNoticeVersion("v2.0.0", "25 June 2026, 12:07 BST", new[]
                {
                    "Rebuilt release restores shared-stop stacking and multi-bus passenger service."
                }, false)
            });
    }

    public class StopStackerMod : IUserMod
    {
        public string Name
        {
            get { return "Stop Stacker"; }
        }

        public string Description
        {
            get { return "Extends bus stop approach points toward the usable end of supported bus stop lanes."; }
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            UIHelperBase diagnosticsGroup = helper.AddGroup("Diagnostics");
            diagnosticsGroup.AddCheckbox(
                "Enable advanced logs",
                StopStackerModSettings.AdvancedDiagnostics,
                value => StopStackerModSettings.AdvancedDiagnostics = value);

            UIHelperBase visualGroup = helper.AddGroup("Stop Stacker visuals");
            visualGroup.AddDropdown(
                "Bus stop signs",
                StopStackerModSettings.PropStyleOptions,
                StopStackerModSettings.BusStopSignStyleIndex,
                value =>
                {
                    StopStackerModSettings.BusStopSignStyleIndex = value;
                    StopStackerBerthOverlay.HandleVisualSettingsChanged("bus-stop-sign-style");
                });

            visualGroup.AddDropdown(
                "Dispatch board",
                StopStackerModSettings.PropStyleOptions,
                StopStackerModSettings.DispatchBoardStyleIndex,
                value =>
                {
                    StopStackerModSettings.DispatchBoardStyleIndex = value;
                    StopStackerBerthOverlay.HandleVisualSettingsChanged("dispatch-board-style");
                });

            visualGroup.AddCheckbox(
                "Disabled stops also switch off multi-bus loading",
                StopStackerModSettings.DisableMultiBusLoadingAtDisabledStops,
                value =>
                {
                    StopStackerModSettings.DisableMultiBusLoadingAtDisabledStops = value;
                    StopStackerBerthOverlay.HandleDisabledStopServiceSettingsChanged("settings-disabled-stop-service-mode");
                });

            visualGroup.AddButton(
                "Reset all disabled stops",
                () =>
                {
                    StopStackerBerthOverlay.ResetAllDisabledStopsFromSettings();
                });
        }
    }

    public class StopStackerLoading : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.LoadGame && mode != LoadMode.NewGame)
                return;

            StopStackerFeatures.LevelActive = false;
            StopStackerScanCoordinator.Initialize();

            if (StopStackerFeatures.BusStopPositionHarmonyEnabled)
                BusStopPositionHarmony.Apply();

            if (StopStackerFeatures.PassengerWaitPositionHarmonyEnabled)
                PassengerWaitPositionHarmony.Apply();

            if (StopStackerFeatures.IptEssentialsPassengerStatsCompatibilityEnabled)
                IptEssentialsPassengerStatsCompatibility.Apply();

            if (StopStackerFeatures.BusDwellReductionEnabled)
                BusDwellReduction.Apply();

            if (StopStackerFeatures.MultiBusStopServiceEnabled)
                MultiBusStopService.Reset();

            StopStackerBerthOverlay.ResetForLevelLoad();
            StopStackerFeatures.LevelActive = true;

            if (StopStackerFeatures.LauncherButtonEnabled)
            {
                StopStackerBerthOverlay.CreateIfNeeded();
                StopStackerLauncherButton.CreateIfNeeded(UIView.GetAView());
            }

            OneTimeUpdateNoticePanel.ShowIfNeeded(UIView.GetAView(), StopStackerFeatures.ReleaseNotice);

            StopStackerDiagnostics.Info("Enabled. busStopPositionHarmony="
                      + StopStackerFeatures.BusStopPositionHarmonyEnabled
                      + " launcherButton="
                      + StopStackerFeatures.LauncherButtonEnabled
                      + " passengerWaitPositionHarmony="
                      + StopStackerFeatures.PassengerWaitPositionHarmonyEnabled
                      + " iptePassengerStatsCompatibility="
                      + StopStackerFeatures.IptEssentialsPassengerStatsCompatibilityEnabled
                      + " busDwellReduction="
                      + StopStackerFeatures.BusDwellReductionEnabled
                      + " multiBusStopService="
                      + StopStackerFeatures.MultiBusStopServiceEnabled
                      + " disabledStopsDisableMultiBus="
                      + StopStackerModSettings.DisableMultiBusLoadingAtDisabledStops
                      + " advancedDiagnostics="
                      + StopStackerModSettings.AdvancedDiagnostics
                      + ".");
        }

        public override void OnLevelUnloading()
        {
            StopStackerFeatures.LevelActive = false;
            StopStackerScanCoordinator.Shutdown();
            base.OnLevelUnloading();

            if (StopStackerFeatures.BusStopPositionHarmonyEnabled)
                BusStopPositionHarmony.Unpatch();

            if (StopStackerFeatures.PassengerWaitPositionHarmonyEnabled)
                PassengerWaitPositionHarmony.Unpatch();

            if (StopStackerFeatures.IptEssentialsPassengerStatsCompatibilityEnabled)
                IptEssentialsPassengerStatsCompatibility.Unpatch();

            if (StopStackerFeatures.BusDwellReductionEnabled)
                BusDwellReduction.Unpatch();

            if (StopStackerFeatures.MultiBusStopServiceEnabled)
                MultiBusStopService.Reset();

            if (StopStackerFeatures.LauncherButtonEnabled)
            {
                StopStackerLauncherButton.DestroyInstance();
                StopStackerBerthOverlay.DestroyInstance();
            }

            OneTimeUpdateNoticePanel.DestroyInstance();
            StopStackerBerthOverlay.ResetForLevelUnload();

            StopStackerDiagnostics.Info("Disabled.");
        }
    }

    public class StopStackerThreading : ThreadingExtensionBase
    {
        private const uint MultiBusServiceIntervalFrames = 2;
        private uint _multiBusServiceFrame;

        public override void OnBeforeSimulationFrame()
        {
            base.OnBeforeSimulationFrame();

            if (!StopStackerFeatures.LevelActive || !StopStackerFeatures.MultiBusStopServiceEnabled)
            {
                _multiBusServiceFrame = 0;
                return;
            }

            _multiBusServiceFrame++;
            if (_multiBusServiceFrame < MultiBusServiceIntervalFrames)
                return;

            _multiBusServiceFrame = 0;
            MultiBusStopService.Update();
        }
    }
}
