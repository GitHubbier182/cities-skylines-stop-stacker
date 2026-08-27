using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace StopStacker
{
    internal static class IptEssentialsPassengerStatsCompatibility
    {
        private const string HarmonyId = "ScratchyBald.StopStacker.IptEssentialsPassengerStats";
        private const uint ServiceWindowFrames = 2048u;
        private const int LogLimit = 24;

        private static readonly Type[] StopPassengerSignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType(),
            typeof(ushort),
            typeof(ushort)
        };

        private static readonly Dictionary<ushort, PassengerStatsWindow> PassengerStatsWindows = new Dictionary<ushort, PassengerStatsWindow>(128);
        private static readonly Dictionary<ushort, PassengerTransferState> PassengerLoadStates = new Dictionary<ushort, PassengerTransferState>(128);
        private static readonly Dictionary<ushort, PassengerTransferState> PassengerUnloadStates = new Dictionary<ushort, PassengerTransferState>(128);
        private static readonly List<PassengerStatsBridge> PassengerStatsBridges = new List<PassengerStatsBridge>(4);

        private static Harmony _harmony;
        private static MethodBase _enterVehicleTarget;
        private static MethodBase _loadPassengersTarget;
        private static MethodBase _unloadPassengersTarget;
        private static bool _patched;
        private static bool _bridgesResolved;
        private static bool _bridgesAvailable;
        private static int _logCount;

        public static void Apply()
        {
            if (_patched)
                return;

            Reset();
            if (!TryResolvePassengerStatsBridges())
            {
                StopStackerDiagnostics.Advanced("[StopStacker] IPT_PASSENGER_STATS_COMPAT_STAND_DOWN: reason=no-supported-ipt-passenger-cache");
                return;
            }

            try
            {
                _harmony = new Harmony(HarmonyId);
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                _enterVehicleTarget = typeof(HumanAI).GetMethod(
                    "EnterVehicle",
                    flags);
                _loadPassengersTarget = typeof(BusAI).GetMethod(
                    "LoadPassengers",
                    flags,
                    null,
                    StopPassengerSignature,
                    null);
                _unloadPassengersTarget = typeof(BusAI).GetMethod(
                    "UnloadPassengers",
                    flags,
                    null,
                    StopPassengerSignature,
                    null);

                MethodInfo postfix = typeof(IptEssentialsPassengerStatsCompatibility).GetMethod(
                    "HumanEnterVehiclePostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo loadPrefix = typeof(IptEssentialsPassengerStatsCompatibility).GetMethod(
                    "BusLoadPassengersPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo loadPostfix = typeof(IptEssentialsPassengerStatsCompatibility).GetMethod(
                    "BusLoadPassengersPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo unloadPrefix = typeof(IptEssentialsPassengerStatsCompatibility).GetMethod(
                    "BusUnloadPassengersPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo unloadPostfix = typeof(IptEssentialsPassengerStatsCompatibility).GetMethod(
                    "BusUnloadPassengersPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (_enterVehicleTarget == null || postfix == null)
                {
                    Debug.LogWarning("[StopStacker] IPT_PASSENGER_STATS_COMPAT_NOT_APPLIED: HumanAI.EnterVehicle target not found.");
                    return;
                }

                HarmonyMethod postfixPatch = new HarmonyMethod(postfix);
                postfixPatch.priority = Priority.Last;
                _harmony.Patch(_enterVehicleTarget, postfix: postfixPatch);

                int patchedTargets = 1;
                if (_loadPassengersTarget != null && loadPrefix != null && loadPostfix != null)
                {
                    HarmonyMethod loadPrefixPatch = new HarmonyMethod(loadPrefix);
                    loadPrefixPatch.priority = Priority.First;
                    HarmonyMethod loadPostfixPatch = new HarmonyMethod(loadPostfix);
                    loadPostfixPatch.priority = Priority.Last;
                    _harmony.Patch(_loadPassengersTarget, prefix: loadPrefixPatch, postfix: loadPostfixPatch);
                    patchedTargets++;
                }
                else
                {
                    Debug.LogWarning("[StopStacker] IPT_PASSENGER_STATS_BOARDING_COMPAT_METHOD_NOT_FOUND: BusAI.LoadPassengers target not found.");
                }

                if (_unloadPassengersTarget != null && unloadPrefix != null && unloadPostfix != null)
                {
                    HarmonyMethod unloadPrefixPatch = new HarmonyMethod(unloadPrefix);
                    unloadPrefixPatch.priority = Priority.First;
                    HarmonyMethod unloadPostfixPatch = new HarmonyMethod(unloadPostfix);
                    unloadPostfixPatch.priority = Priority.Last;
                    _harmony.Patch(_unloadPassengersTarget, prefix: unloadPrefixPatch, postfix: unloadPostfixPatch);
                    patchedTargets++;
                }
                else
                {
                    Debug.LogWarning("[StopStacker] IPT_PASSENGER_STATS_ALIGHTING_COMPAT_METHOD_NOT_FOUND: BusAI.UnloadPassengers target not found.");
                }

                _patched = true;
                StopStackerDiagnostics.Advanced("[StopStacker] IPT_PASSENGER_STATS_COMPAT_APPLIED:"
                          + " patchedTargets=" + patchedTargets
                          + " rule=backfill-ipt-family-last-stop-passenger-exchange-for-stop-stacker-service-windows");
            }
            catch (Exception e)
            {
                RollBackPartialPatch();
                _patched = false;
                _harmony = null;
                _enterVehicleTarget = null;
                _loadPassengersTarget = null;
                _unloadPassengersTarget = null;
                Debug.LogWarning("[StopStacker] IPT_PASSENGER_STATS_COMPAT_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void RollBackPartialPatch()
        {
            if (_harmony == null)
                return;

            try
            {
                _harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception rollbackError)
            {
                Debug.LogWarning("[StopStacker] IPT_PASSENGER_STATS_COMPAT_ROLLBACK_FAILED: "
                                 + rollbackError.GetType().Name + ": " + rollbackError.Message);
            }
        }

        public static void Unpatch()
        {
            Reset();
            if (_harmony == null || !_patched)
                return;

            try
            {
                _harmony.UnpatchAll(HarmonyId);
                StopStackerDiagnostics.Advanced("[StopStacker] IPT_PASSENGER_STATS_COMPAT_REMOVED.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StopStacker] IPT_PASSENGER_STATS_COMPAT_REMOVE_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                _patched = false;
                _harmony = null;
                _enterVehicleTarget = null;
                _loadPassengersTarget = null;
                _unloadPassengersTarget = null;
            }
        }

        public static void Reset()
        {
            PassengerStatsWindows.Clear();
            PassengerLoadStates.Clear();
            PassengerUnloadStates.Clear();
            PassengerStatsBridges.Clear();
            _bridgesResolved = false;
            _bridgesAvailable = false;
            _logCount = 0;
        }

        public static void RegisterServiceWindow(ushort vehicleId, ushort lineId, ushort stopId)
        {
            RegisterServiceWindow(vehicleId, lineId, stopId, -1);
        }

        public static void RegisterServiceWindow(ushort vehicleId, ushort lineId, ushort stopId, int passengersBeforeService)
        {
            if (!_bridgesAvailable || vehicleId == 0 || lineId == 0 || stopId == 0)
                return;

            ushort leadVehicle = GetLeadVehicle(vehicleId);
            if (leadVehicle == 0)
                return;

            PassengerStatsWindow window;
            if (!PassengerStatsWindows.TryGetValue(leadVehicle, out window)
                || window.LineId != lineId
                || window.StopId != stopId)
            {
                window = new PassengerStatsWindow(lineId, stopId, 0, 0, passengersBeforeService, GetCurrentFrame() + ServiceWindowFrames);
            }
            else
            {
                if (passengersBeforeService >= 0)
                    window.PassengersBeforeService = passengersBeforeService;

                window.ExpiresFrame = GetCurrentFrame() + ServiceWindowFrames;
            }

            PassengerStatsWindows[leadVehicle] = window;
        }

        public static void ReconcileServiceWindow(ushort vehicleId, ushort lineId, ushort stopId, int passengersBeforeService, int passengersAfterService)
        {
            if (!_bridgesAvailable || vehicleId == 0 || lineId == 0 || stopId == 0)
                return;

            ushort leadVehicle = GetLeadVehicle(vehicleId);
            if (leadVehicle == 0)
                return;

            PassengerStatsWindow window;
            if (!TryGetExistingWindow(leadVehicle, lineId, out window) || window.StopId != stopId)
                return;

            if (passengersBeforeService >= 0)
                window.PassengersBeforeService = passengersBeforeService;

            if (passengersBeforeService >= 0 && passengersAfterService >= 0)
            {
                int derivedBoarded = Math.Max(0, passengersAfterService - passengersBeforeService + window.Alighted);
                window.Boarded = Math.Max(window.Boarded, derivedBoarded);
                window.BoardingAuthoritative = true;
            }

            window.ExpiresFrame = GetCurrentFrame() + ServiceWindowFrames;
            PassengerStatsWindows[leadVehicle] = window;
            BackfillPassengerStatsLastStopExchange(leadVehicle, window.StopId, window.Boarded, window.Alighted, window.BoardingAuthoritative, true);
        }

        private static void HumanEnterVehiclePostfix(ushort instanceID, ref CitizenInstance citizenData)
        {
            try
            {
                ObserveBoarding(ref citizenData);
            }
            catch (Exception e)
            {
                if (_logCount < LogLimit)
                {
                    _logCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] IPT_PASSENGER_STATS_BOARDING_OBSERVE_FAILED:"
                                     + " citizenInstance=" + instanceID
                                     + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private static void BusLoadPassengersPrefix(ushort vehicleID, ref Vehicle data, ushort currentStop, ushort nextStop)
        {
            try
            {
                CapturePassengerTransfer(PassengerLoadStates, vehicleID, ref data, currentStop);
            }
            catch (Exception e)
            {
                if (_logCount < LogLimit)
                {
                    _logCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] IPT_PASSENGER_STATS_LOAD_PREFIX_FAILED:"
                                     + " bus=" + vehicleID
                                     + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private static void BusLoadPassengersPostfix(ushort vehicleID, ref Vehicle data, ushort currentStop, ushort nextStop)
        {
            try
            {
                CompletePassengerTransfer(PassengerLoadStates, vehicleID, ref data, currentStop, true);
            }
            catch (Exception e)
            {
                if (_logCount < LogLimit)
                {
                    _logCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] IPT_PASSENGER_STATS_LOAD_POSTFIX_FAILED:"
                                     + " bus=" + vehicleID
                                     + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private static void BusUnloadPassengersPrefix(ushort vehicleID, ref Vehicle data, ushort currentStop, ushort nextStop)
        {
            try
            {
                CapturePassengerTransfer(PassengerUnloadStates, vehicleID, ref data, currentStop);
            }
            catch (Exception e)
            {
                if (_logCount < LogLimit)
                {
                    _logCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] IPT_PASSENGER_STATS_UNLOAD_PREFIX_FAILED:"
                                     + " bus=" + vehicleID
                                     + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private static void BusUnloadPassengersPostfix(ushort vehicleID, ref Vehicle data, ushort currentStop, ushort nextStop)
        {
            try
            {
                CompletePassengerTransfer(PassengerUnloadStates, vehicleID, ref data, currentStop, false);
            }
            catch (Exception e)
            {
                if (_logCount < LogLimit)
                {
                    _logCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] IPT_PASSENGER_STATS_UNLOAD_POSTFIX_FAILED:"
                                     + " bus=" + vehicleID
                                     + " error=" + e.GetType().Name + ": " + e.Message);
                }
            }
        }

        private static void ObserveBoarding(ref CitizenInstance citizenData)
        {
            uint citizenId = citizenData.m_citizen;
            if (citizenId == 0)
                return;

            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            if (citizenManager == null
                || vehicleManager == null
                || citizenManager.m_citizens == null
                || citizenManager.m_citizens.m_buffer == null
                || vehicleManager.m_vehicles == null
                || vehicleManager.m_vehicles.m_buffer == null)
                return;

            if (citizenId >= citizenManager.m_citizens.m_size
                || citizenId >= citizenManager.m_citizens.m_buffer.Length)
                return;

            ushort boardedVehicle = citizenManager.m_citizens.m_buffer[citizenId].m_vehicle;
            ushort leadVehicle = GetLeadVehicle(boardedVehicle);
            if (leadVehicle == 0
                || leadVehicle >= vehicleManager.m_vehicles.m_size
                || leadVehicle >= vehicleManager.m_vehicles.m_buffer.Length)
                return;

            Vehicle vehicle = vehicleManager.m_vehicles.m_buffer[leadVehicle];
            if (!IsBusLineVehicle(ref vehicle))
                return;

            PassengerStatsWindow window;
            if (!TryGetBoardingWindow(leadVehicle, ref vehicle, out window))
                return;

            window.Boarded++;
            window.BoardingAuthoritative = true;
            window.ExpiresFrame = GetCurrentFrame() + ServiceWindowFrames;
            PassengerStatsWindows[leadVehicle] = window;

            BackfillPassengerStatsLastStopExchange(leadVehicle, window.StopId, window.Boarded, window.Alighted, true, false);
        }

        private static void CapturePassengerTransfer(
            Dictionary<ushort, PassengerTransferState> states,
            ushort vehicleId,
            ref Vehicle vehicle,
            ushort currentStop)
        {
            if (states == null || currentStop == 0 || !IsBusLineVehicle(ref vehicle))
                return;

            ushort leadVehicle = GetLeadVehicle(vehicleId);
            if (leadVehicle == 0)
                return;

            PassengerStatsWindow window;
            if (!TryGetTransferWindow(leadVehicle, ref vehicle, currentStop, out window))
            {
                states.Remove(leadVehicle);
                return;
            }

            states[leadVehicle] = new PassengerTransferState(vehicle.m_transportLine, currentStop, vehicle.m_transferSize);
        }

        private static void CompletePassengerTransfer(
            Dictionary<ushort, PassengerTransferState> states,
            ushort vehicleId,
            ref Vehicle vehicle,
            ushort currentStop,
            bool loading)
        {
            if (states == null || currentStop == 0)
                return;

            ushort leadVehicle = GetLeadVehicle(vehicleId);
            if (leadVehicle == 0)
                return;

            PassengerTransferState transfer;
            if (!states.TryGetValue(leadVehicle, out transfer))
                return;

            states.Remove(leadVehicle);
            if (transfer.LineId != vehicle.m_transportLine || transfer.StopId != currentStop)
                return;

            PassengerStatsWindow window;
            if (!TryGetExistingWindow(leadVehicle, vehicle.m_transportLine, out window) || window.StopId != currentStop)
                return;

            int delta = vehicle.m_transferSize - transfer.PassengersBefore;
            if (loading)
            {
                int boarded = Math.Max(0, delta);
                int cumulativeBoarded = Math.Max(window.Boarded, boarded);
                if (window.BoardingAuthoritative && cumulativeBoarded == window.Boarded)
                    return;

                window.Boarded = cumulativeBoarded;
                window.BoardingAuthoritative = true;
            }
            else
            {
                int alighted = Math.Max(0, -delta);
                if (alighted <= window.Alighted)
                    return;

                window.Alighted = alighted;
                if (window.PassengersBeforeService < 0)
                    window.PassengersBeforeService = transfer.PassengersBefore;
            }

            window.ExpiresFrame = GetCurrentFrame() + ServiceWindowFrames;
            PassengerStatsWindows[leadVehicle] = window;
            BackfillPassengerStatsLastStopExchange(leadVehicle, window.StopId, window.Boarded, window.Alighted, true, true);
        }

        private static bool TryGetBoardingWindow(ushort leadVehicle, ref Vehicle vehicle, out PassengerStatsWindow window)
        {
            if (TryGetExistingWindow(leadVehicle, vehicle.m_transportLine, out window))
            {
                if (window.StopId == vehicle.m_targetBuilding)
                    return true;

                PassengerStatsWindows.Remove(leadVehicle);
            }

            StopStackerBerthOverlay.StopServiceZone zone;
            if (StopStackerBerthOverlay.TryGetServiceZone(vehicle.m_transportLine, vehicle.m_targetBuilding, out zone))
            {
                RegisterServiceWindow(leadVehicle, vehicle.m_transportLine, vehicle.m_targetBuilding, vehicle.m_transferSize);
                return PassengerStatsWindows.TryGetValue(leadVehicle, out window);
            }

            window = default(PassengerStatsWindow);
            return false;
        }

        private static bool TryGetTransferWindow(ushort leadVehicle, ref Vehicle vehicle, ushort currentStop, out PassengerStatsWindow window)
        {
            if (TryGetExistingWindow(leadVehicle, vehicle.m_transportLine, out window))
            {
                if (window.StopId == currentStop)
                    return true;

                PassengerStatsWindows.Remove(leadVehicle);
            }

            StopStackerBerthOverlay.StopServiceZone zone;
            if (StopStackerBerthOverlay.TryGetServiceZone(vehicle.m_transportLine, currentStop, out zone))
            {
                RegisterServiceWindow(leadVehicle, vehicle.m_transportLine, currentStop, vehicle.m_transferSize);
                return PassengerStatsWindows.TryGetValue(leadVehicle, out window);
            }

            window = default(PassengerStatsWindow);
            return false;
        }

        private static bool TryGetExistingWindow(ushort leadVehicle, ushort lineId, out PassengerStatsWindow window)
        {
            uint currentFrame = GetCurrentFrame();
            if (PassengerStatsWindows.TryGetValue(leadVehicle, out window))
            {
                if (window.LineId == lineId && IsFrameAtOrBefore(currentFrame, window.ExpiresFrame))
                    return true;

                PassengerStatsWindows.Remove(leadVehicle);
            }

            window = default(PassengerStatsWindow);
            return false;
        }

        private static bool IsFrameAtOrBefore(uint currentFrame, uint expiryFrame)
        {
            return unchecked((int)(expiryFrame - currentFrame)) >= 0;
        }

        private static void BackfillPassengerStatsLastStopExchange(ushort vehicleId, ushort stopId, int boarded, int alighted)
        {
            BackfillPassengerStatsLastStopExchange(vehicleId, stopId, boarded, alighted, false, false);
        }

        private static void BackfillPassengerStatsLastStopExchange(
            ushort vehicleId,
            ushort stopId,
            int boarded,
            int alighted,
            bool authoritativeBoarding,
            bool authoritativeAlighting)
        {
            if ((boarded <= 0 && alighted <= 0 && !authoritativeBoarding && !authoritativeAlighting)
                || !TryResolvePassengerStatsBridges())
            {
                return;
            }

            for (int i = 0; i < PassengerStatsBridges.Count; i++)
            {
                PassengerStatsBridge bridge = PassengerStatsBridges[i];
                try
                {
                    TryBackfillPassengerStatsBridge(bridge, vehicleId, stopId, boarded, alighted, authoritativeBoarding, authoritativeAlighting);
                }
                catch (Exception e)
                {
                    if (_logCount < LogLimit)
                    {
                        _logCount++;
                        StopStackerDiagnostics.AdvancedWarning("[StopStacker] IPT_PASSENGER_STATS_BACKFILL_FAILED:"
                                         + " target=" + bridge.Name
                                         + " bus=" + vehicleId
                                         + " stop=" + stopId
                                         + " boarded=" + boarded
                                         + " alighted=" + alighted
                                         + " authoritativeBoarding=" + authoritativeBoarding
                                         + " authoritativeAlighting=" + authoritativeAlighting
                                         + " error=" + e.GetType().Name + ": " + e.Message);
                    }
                }
            }
        }

        private static bool TryBackfillPassengerStatsBridge(
            PassengerStatsBridge bridge,
            ushort vehicleId,
            ushort stopId,
            int boarded,
            int alighted,
            bool authoritativeBoarding,
            bool authoritativeAlighting)
        {
            Array vehicleData = bridge.CachedVehicleDataField.GetValue(null) as Array;
            if (vehicleData == null || vehicleId >= vehicleData.Length)
                return false;

            object entry = vehicleData.GetValue(vehicleId);
            int existingBoarded = 0;
            int existingAlighted = 0;
            ushort existingStop = 0;

            object existingBoardedValue = bridge.LastStopNewPassengersProperty.GetValue(entry, null);
            if (existingBoardedValue is int)
                existingBoarded = (int)existingBoardedValue;

            object existingAlightedValue = bridge.LastStopGonePassengersProperty.GetValue(entry, null);
            if (existingAlightedValue is int)
                existingAlighted = (int)existingAlightedValue;

            object existingStopValue = bridge.CurrentStopProperty.GetValue(entry, null);
            if (existingStopValue is ushort)
                existingStop = (ushort)existingStopValue;

            int requestedBoarded = Math.Max(0, boarded);
            int requestedAlighted = Math.Max(0, alighted);
            int nextBoarded;
            if (existingStop != stopId)
            {
                nextBoarded = requestedBoarded;
            }
            else if (authoritativeBoarding && requestedBoarded > 0)
            {
                nextBoarded = requestedBoarded;
            }
            else
            {
                // IPT-family LoadPassengers postfixes run before this Priority.Last bridge and
                // calculate total consist boardings. Stop Stacker's lead-vehicle transfer delta
                // can still be zero, so that zero must never erase the same-call positive cache.
                nextBoarded = Math.Max(existingBoarded, requestedBoarded);
            }
            int nextAlighted = authoritativeAlighting || existingStop != stopId
                ? requestedAlighted
                : Math.Max(existingAlighted, requestedAlighted);

            if (existingStop == stopId && existingBoarded == nextBoarded && existingAlighted == nextAlighted)
                return false;

            bridge.LastStopNewPassengersProperty.SetValue(entry, nextBoarded, null);
            bridge.LastStopGonePassengersProperty.SetValue(entry, nextAlighted, null);
            bridge.CurrentStopProperty.SetValue(entry, stopId, null);
            vehicleData.SetValue(entry, vehicleId);

            if (_logCount < LogLimit)
            {
                _logCount++;
                StopStackerDiagnostics.Advanced("[StopStacker] IPT_PASSENGER_STATS_BACKFILLED:"
                          + " target=" + bridge.Name
                          + " bus=" + vehicleId
                          + " stop=" + stopId
                          + " boarded=" + nextBoarded
                          + " alighted=" + nextAlighted
                          + " requestedBoarded=" + requestedBoarded
                          + " requestedAlighted=" + requestedAlighted
                          + " authoritativeBoarding=" + authoritativeBoarding
                          + " authoritativeAlighting=" + authoritativeAlighting
                          + " previousStop=" + existingStop
                          + " previousBoarded=" + existingBoarded
                          + " previousAlighted=" + existingAlighted);
            }

            return true;
        }

        private static bool TryResolvePassengerStatsBridges()
        {
            if (_bridgesResolved)
                return _bridgesAvailable;

            _bridgesResolved = true;
            PassengerStatsBridges.Clear();

            TryAddPassengerStatsBridge(
                "IPTE",
                "ImprovedPublicTransport2.Data.CachedVehicleData",
                "ImprovedPublicTransport2.Data.VehicleData",
                "ImprovedPublicTransport2");

            TryAddPassengerStatsBridge(
                "IPT3",
                "ImprovedPublicTransport.Data.CachedVehicleData",
                "ImprovedPublicTransport.Data.VehicleData",
                "ImprovedPublicTransport3");

            _bridgesAvailable = PassengerStatsBridges.Count > 0;
            return _bridgesAvailable;
        }

        private static void TryAddPassengerStatsBridge(string name, string cachedVehicleDataTypeName, string vehicleDataTypeName, string preferredAssemblyName)
        {
            Type cachedVehicleDataType = ResolveLoadedType(cachedVehicleDataTypeName, preferredAssemblyName);
            Type vehicleDataType = cachedVehicleDataType == null ? null : cachedVehicleDataType.Assembly.GetType(vehicleDataTypeName, false);
            if (vehicleDataType == null)
                vehicleDataType = ResolveLoadedType(vehicleDataTypeName, preferredAssemblyName);

            if (cachedVehicleDataType == null || vehicleDataType == null)
                return;

            FieldInfo cachedVehicleDataField = cachedVehicleDataType.GetField("m_cachedVehicleData", BindingFlags.Static | BindingFlags.Public);
            PropertyInfo lastStopNewPassengersProperty = vehicleDataType.GetProperty("LastStopNewPassengers", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo lastStopGonePassengersProperty = vehicleDataType.GetProperty("LastStopGonePassengers", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo currentStopProperty = vehicleDataType.GetProperty("CurrentStop", BindingFlags.Instance | BindingFlags.Public);
            if (cachedVehicleDataField == null
                || lastStopNewPassengersProperty == null
                || lastStopGonePassengersProperty == null
                || currentStopProperty == null)
            {
                return;
            }

            for (int i = 0; i < PassengerStatsBridges.Count; i++)
            {
                if (PassengerStatsBridges[i].CachedVehicleDataField == cachedVehicleDataField)
                    return;
            }

            PassengerStatsBridges.Add(new PassengerStatsBridge(
                name,
                cachedVehicleDataField,
                lastStopNewPassengersProperty,
                lastStopGonePassengersProperty,
                currentStopProperty));

            if (_logCount < LogLimit)
            {
                _logCount++;
                StopStackerDiagnostics.Advanced("[StopStacker] IPT_PASSENGER_STATS_BRIDGE_RESOLVED:"
                          + " target=" + name
                          + " assembly=" + cachedVehicleDataType.Assembly.GetName().Name
                          + " fields=vehicle-cache,last-stop-boardings,last-stop-alightings,current-stop");
            }
        }

        private static Type ResolveLoadedType(string typeName, string preferredAssemblyName)
        {
            Type directType = Type.GetType(typeName + ", " + preferredAssemblyName, false);
            if (directType != null)
                return directType;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                    continue;

                try
                {
                    Type type = assembly.GetType(typeName, false);
                    if (type != null)
                        return type;
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private static bool IsBusLineVehicle(ref Vehicle vehicle)
        {
            if ((vehicle.m_flags & Vehicle.Flags.Created) == 0)
                return false;

            if (vehicle.m_transportLine == 0)
                return false;

            VehicleInfo info = vehicle.Info;
            return info != null && info.m_vehicleAI is BusAI;
        }

        private static ushort GetLeadVehicle(ushort vehicleId)
        {
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            if (vehicleManager == null
                || vehicleManager.m_vehicles == null
                || vehicleManager.m_vehicles.m_buffer == null
                || vehicleId == 0
                || vehicleId >= vehicleManager.m_vehicles.m_size
                || vehicleId >= vehicleManager.m_vehicles.m_buffer.Length)
                return 0;

            Vehicle vehicle = vehicleManager.m_vehicles.m_buffer[vehicleId];
            ushort lead = vehicle.GetFirstVehicle(vehicleId);
            return lead == 0 ? vehicleId : lead;
        }

        private static uint GetCurrentFrame()
        {
            SimulationManager simulationManager = Singleton<SimulationManager>.instance;
            return simulationManager == null ? 0u : simulationManager.m_currentFrameIndex;
        }

        private struct PassengerStatsWindow
        {
            public readonly ushort LineId;
            public readonly ushort StopId;
            public int Boarded;
            public int Alighted;
            public int PassengersBeforeService;
            public bool BoardingAuthoritative;
            public uint ExpiresFrame;

            public PassengerStatsWindow(ushort lineId, ushort stopId, int boarded, int alighted, int passengersBeforeService, uint expiresFrame)
            {
                LineId = lineId;
                StopId = stopId;
                Boarded = boarded;
                Alighted = alighted;
                PassengersBeforeService = passengersBeforeService;
                BoardingAuthoritative = false;
                ExpiresFrame = expiresFrame;
            }
        }

        private struct PassengerTransferState
        {
            public readonly ushort LineId;
            public readonly ushort StopId;
            public readonly int PassengersBefore;

            public PassengerTransferState(ushort lineId, ushort stopId, int passengersBefore)
            {
                LineId = lineId;
                StopId = stopId;
                PassengersBefore = passengersBefore;
            }
        }

        private sealed class PassengerStatsBridge
        {
            public readonly string Name;
            public readonly FieldInfo CachedVehicleDataField;
            public readonly PropertyInfo LastStopNewPassengersProperty;
            public readonly PropertyInfo LastStopGonePassengersProperty;
            public readonly PropertyInfo CurrentStopProperty;

            public PassengerStatsBridge(
                string name,
                FieldInfo cachedVehicleDataField,
                PropertyInfo lastStopNewPassengersProperty,
                PropertyInfo lastStopGonePassengersProperty,
                PropertyInfo currentStopProperty)
            {
                Name = name;
                CachedVehicleDataField = cachedVehicleDataField;
                LastStopNewPassengersProperty = lastStopNewPassengersProperty;
                LastStopGonePassengersProperty = lastStopGonePassengersProperty;
                CurrentStopProperty = currentStopProperty;
            }
        }
    }
}
