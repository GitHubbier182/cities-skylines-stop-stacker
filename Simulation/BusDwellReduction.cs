using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace StopStacker
{
    internal static class BusDwellReduction
    {
        private const string HarmonyId = "ScratchyBald.StopStacker.BusDwellReduction";
        private const int MaxCitizenGridIterations = 65536;
        private const int DwellWarningLogLimit = 12;
        private const int ExternalOwnerLogLimit = 12;
        private const int ExternalCanLeavePatchCheckInterval = 128;
        private const byte VanillaMinimumDwellWaitCounter = 12;
        private const byte VanillaLineSpacingReadyWaitCounter = 64;

        private static readonly Type[] VehicleOnlySignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType()
        };

        private static readonly Type[] CanLeaveSignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType()
        };

        private static readonly Type[] StopPassengerSignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType(),
            typeof(ushort),
            typeof(ushort)
        };

        private static readonly Type[] TransportArriveAtTargetSignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType(),
            typeof(int).MakeByRefType()
        };

        private static readonly Dictionary<ushort, StopExchangeState> ExchangeStates = new Dictionary<ushort, StopExchangeState>(256);

        private static Harmony _harmony;
        private static MethodBase _arriveAtTargetTarget;
        private static MethodBase _canLeaveTarget;
        private static MethodBase _transportArriveAtTargetTarget;
        private static MethodBase _loadPassengersTarget;
        private static bool _patched;
        private static bool _activeLogged;
        private static bool _externalCanLeavePatchFound;
        private static bool _externalCanLeaveStandDownLogged;
        private static int _externalCanLeavePatchCheckCountdown;
        private static int _externalOwnerLogCount;
        private static string _externalCanLeavePatchOwner;
        private static int _dwellWarningLogCount;

        public static void Apply()
        {
            if (_patched)
                return;

            Reset();

            try
            {
                _harmony = new Harmony(HarmonyId);
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _arriveAtTargetTarget = typeof(BusAI).GetMethod(
                    "ArriveAtTarget",
                    flags,
                    null,
                    VehicleOnlySignature,
                    null);

                _canLeaveTarget = typeof(BusAI).GetMethod(
                    "CanLeave",
                    flags,
                    null,
                    CanLeaveSignature,
                    null);

                _transportArriveAtTargetTarget = typeof(BusAI).GetMethod(
                    "TransportArriveAtTarget",
                    flags,
                    null,
                    TransportArriveAtTargetSignature,
                    null);

                _loadPassengersTarget = typeof(BusAI).GetMethod(
                    "LoadPassengers",
                    flags,
                    null,
                    StopPassengerSignature,
                    null);

                MethodInfo arrivePrefix = typeof(BusDwellReduction).GetMethod(
                    "BusArriveAtTargetPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo arrivePostfix = typeof(BusDwellReduction).GetMethod(
                    "BusArriveAtTargetPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo canLeavePrefix = typeof(BusDwellReduction).GetMethod(
                    "BusCanLeavePrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo transportArrivePrefix = typeof(BusDwellReduction).GetMethod(
                    "BusTransportArriveAtTargetPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo transportArrivePostfix = typeof(BusDwellReduction).GetMethod(
                    "BusTransportArriveAtTargetPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo loadPrefix = typeof(BusDwellReduction).GetMethod(
                    "BusLoadPassengersPrefix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo loadPostfix = typeof(BusDwellReduction).GetMethod(
                    "BusLoadPassengersPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (_arriveAtTargetTarget == null || _canLeaveTarget == null || arrivePrefix == null || arrivePostfix == null || canLeavePrefix == null)
                {
                    Debug.LogWarning("[StopStacker] DWELL_REDUCTION_HARMONY_NOT_APPLIED: required BusAI lifecycle target not found.");
                    return;
                }

                HarmonyMethod arrivePrefixPatch = new HarmonyMethod(arrivePrefix);
                arrivePrefixPatch.priority = Priority.First;
                HarmonyMethod arrivePostfixPatch = new HarmonyMethod(arrivePostfix);
                arrivePostfixPatch.priority = Priority.Last;
                _harmony.Patch(_arriveAtTargetTarget, prefix: arrivePrefixPatch, postfix: arrivePostfixPatch);

                HarmonyMethod canLeavePrefixPatch = new HarmonyMethod(canLeavePrefix);
                canLeavePrefixPatch.priority = Priority.First;
                _harmony.Patch(_canLeaveTarget, prefix: canLeavePrefixPatch);

                int patchedTargets = 2;
                bool exchangeCountersPatched = false;

                if (_transportArriveAtTargetTarget != null && transportArrivePrefix != null && transportArrivePostfix != null)
                {
                    HarmonyMethod transportArrivePrefixPatch = new HarmonyMethod(transportArrivePrefix);
                    transportArrivePrefixPatch.priority = Priority.First;
                    HarmonyMethod transportArrivePostfixPatch = new HarmonyMethod(transportArrivePostfix);
                    transportArrivePostfixPatch.priority = Priority.Last;
                    _harmony.Patch(_transportArriveAtTargetTarget, prefix: transportArrivePrefixPatch, postfix: transportArrivePostfixPatch);
                    patchedTargets++;
                    exchangeCountersPatched = true;
                }
                else
                {
                    Debug.LogWarning("[StopStacker] DWELL_REDUCTION_EXCHANGE_COUNTER_NOT_APPLIED: BusAI.TransportArriveAtTarget target not found.");
                }

                if (_loadPassengersTarget != null && loadPrefix != null && loadPostfix != null)
                {
                    HarmonyMethod loadPrefixPatch = new HarmonyMethod(loadPrefix);
                    loadPrefixPatch.priority = Priority.First;
                    HarmonyMethod loadPostfixPatch = new HarmonyMethod(loadPostfix);
                    loadPostfixPatch.priority = Priority.Last;
                    _harmony.Patch(_loadPassengersTarget, prefix: loadPrefixPatch, postfix: loadPostfixPatch);
                    patchedTargets++;
                    exchangeCountersPatched = true;
                }
                else
                {
                    Debug.LogWarning("[StopStacker] DWELL_REDUCTION_LOAD_COUNTER_NOT_APPLIED: BusAI.LoadPassengers target not found.");
                }

                _patched = true;

                string externalCanLeaveOwner;
                if (TryGetExternalCanLeavePatchOwner(out externalCanLeaveOwner))
                    RememberExternalCanLeavePatchOwner(externalCanLeaveOwner);

                StopStackerDiagnostics.Advanced("[StopStacker] DWELL_REDUCTION_HARMONY_APPLIED:"
                          + " patchedTargets=" + patchedTargets
                          + " exchangeCounters=" + exchangeCountersPatched
                          + " externalCanLeavePatch=" + _externalCanLeavePatchFound
                          + " externalCanLeaveOwner=" + (string.IsNullOrEmpty(_externalCanLeavePatchOwner) ? "none" : _externalCanLeavePatchOwner)
                          + " rule=arrival-zero-exchange-and-canleave-after-exchange-complete");
            }
            catch (Exception e)
            {
                RollBackPartialPatch();
                _patched = false;
                _harmony = null;
                _arriveAtTargetTarget = null;
                _canLeaveTarget = null;
                _transportArriveAtTargetTarget = null;
                _loadPassengersTarget = null;
                Debug.LogError("[StopStacker] DWELL_REDUCTION_HARMONY_FAILED: " + e.GetType().Name + ": " + e.Message);
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
                Debug.LogWarning("[StopStacker] DWELL_REDUCTION_HARMONY_ROLLBACK_FAILED: "
                                 + rollbackError.GetType().Name + ": " + rollbackError.Message);
            }
        }

        public static void Unpatch()
        {
            if (_harmony == null || !_patched)
                return;

            try
            {
                _harmony.UnpatchAll(HarmonyId);
                StopStackerDiagnostics.Advanced("[StopStacker] DWELL_REDUCTION_HARMONY_REMOVED.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StopStacker] DWELL_REDUCTION_HARMONY_REMOVE_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                _patched = false;
                _harmony = null;
                _arriveAtTargetTarget = null;
                _canLeaveTarget = null;
                _transportArriveAtTargetTarget = null;
                _loadPassengersTarget = null;
                Reset();
            }
        }

        public static void Reset()
        {
            ExchangeStates.Clear();
            _activeLogged = false;
            _externalCanLeavePatchFound = false;
            _externalCanLeaveStandDownLogged = false;
            _externalCanLeavePatchCheckCountdown = 0;
            _externalOwnerLogCount = 0;
            _externalCanLeavePatchOwner = string.Empty;
            _dwellWarningLogCount = 0;
        }

        private static void BusArriveAtTargetPrefix(ushort vehicleID, ref Vehicle data)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            ResetExchangeState(vehicleID, ref data);
        }

        private static void BusArriveAtTargetPostfix(ushort vehicleID, ref Vehicle data)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            try
            {
                StopExchangeState state;
                bool hasState = ExchangeStates.TryGetValue(vehicleID, out state);
                if (hasState && state.HasPassengerDelta)
                    return;

                if (hasState && !state.HasAnyPassengerObservation)
                    return;

                if (HasExternalCanLeavePatchOwner())
                {
                    LogExternalCanLeaveStandDown("arrival-release");
                    return;
                }

                PrimeDepartureIfExchangeComplete(vehicleID, ref data);
            }
            catch (Exception e)
            {
                LogDwellWarning("DWELL_REDUCTION_ARRIVE_POSTFIX_FAILED", vehicleID, e);
            }
        }

        private static void BusTransportArriveAtTargetPrefix(ushort vehicleID, ref Vehicle data)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            ResetExchangeState(vehicleID, ref data);
        }

        private static void BusTransportArriveAtTargetPostfix(ushort vehicleID, ref Vehicle data, ref int serviceCounter)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            if (!IsEligibleBusLineVehicle(data, false))
                return;

            StopExchangeState state = GetOrCreateExchangeState(vehicleID, ref data);
            state.UnloadObserved = true;
            state.Alighted = Math.Max(0, serviceCounter);
            ExchangeStates[vehicleID] = state;
        }

        private static void BusLoadPassengersPrefix(ushort vehicleID, ref Vehicle data)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            if (!IsEligibleBusLineVehicle(data, false))
                return;

            StopExchangeState state = GetOrCreateExchangeState(vehicleID, ref data);
            state.LoadStarted = true;
            state.PassengersBeforeLoad = data.m_transferSize;
            ExchangeStates[vehicleID] = state;
        }

        private static void BusLoadPassengersPostfix(ushort vehicleID, ref Vehicle data)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            if (!IsEligibleBusLineVehicle(data, false))
                return;

            StopExchangeState state = GetOrCreateExchangeState(vehicleID, ref data);
            state.LoadFinished = true;
            state.PassengersAfterLoad = data.m_transferSize;
            ExchangeStates[vehicleID] = state;
        }

        private static void BusCanLeavePrefix(ushort vehicleID, ref Vehicle vehicleData)
        {
            if (!StopStackerFeatures.BusDwellReductionEnabled)
                return;

            try
            {
                if (HasExternalCanLeavePatchOwner())
                {
                    LogExternalCanLeaveStandDown("canleave");
                    return;
                }

                PrimeDepartureIfExchangeComplete(vehicleID, ref vehicleData);
            }
            catch (Exception e)
            {
                LogDwellWarning("DWELL_REDUCTION_CANLEAVE_PREFIX_FAILED", vehicleID, e);
            }
        }

        private static bool PrimeDepartureIfExchangeComplete(ushort vehicleId, ref Vehicle vehicle)
        {
            if (!_activeLogged)
            {
                _activeLogged = true;
                StopStackerDiagnostics.Advanced("[StopStacker] DWELL_REDUCTION_ACTIVE:"
                          + " scope=arrival-and-canleave"
                          + " rule=allow-departure-after-passenger-exchange-complete"
                          + " minimumDwellOwner=vanilla-wait-counter"
                          + " compatibility=stand-down-when-external-canleave-patch-active");
            }

            if (!IsEligibleBusLineVehicle(vehicle, true))
                return false;

            StopExchangeState state;
            if (!TryGetCurrentExchangeState(vehicleId, ref vehicle, out state))
                return false;

            if (!IsPassengerExchangeComplete(vehicleId, ref vehicle))
                return false;

            if (vehicle.m_waitCounter < VanillaMinimumDwellWaitCounter)
                return false;

            if (vehicle.m_waitCounter < VanillaLineSpacingReadyWaitCounter)
                vehicle.m_waitCounter = VanillaLineSpacingReadyWaitCounter;

            return true;
        }

        private static bool HasExternalCanLeavePatchOwner()
        {
            if (_externalCanLeavePatchFound)
                return true;

            if (_externalCanLeavePatchCheckCountdown > 0)
            {
                _externalCanLeavePatchCheckCountdown--;
                return false;
            }

            _externalCanLeavePatchCheckCountdown = ExternalCanLeavePatchCheckInterval;
            string owner;
            if (!TryGetExternalCanLeavePatchOwner(out owner))
                return false;

            RememberExternalCanLeavePatchOwner(owner);
            return true;
        }

        private static void RememberExternalCanLeavePatchOwner(string owner)
        {
            _externalCanLeavePatchFound = true;
            _externalCanLeavePatchOwner = string.IsNullOrEmpty(owner) ? "unknown" : owner;
        }

        private static bool TryGetExternalCanLeavePatchOwner(out string owner)
        {
            owner = string.Empty;
            if (_canLeaveTarget == null)
                return false;

            try
            {
                Patches patches = Harmony.GetPatchInfo(_canLeaveTarget);
                if (patches == null)
                    return false;

                return TryGetExternalOwner(patches.Prefixes, out owner)
                       || TryGetExternalOwner(patches.Postfixes, out owner)
                       || TryGetExternalOwner(patches.Transpilers, out owner)
                       || TryGetExternalOwner(patches.Finalizers, out owner);
            }
            catch (Exception e)
            {
                if (_externalOwnerLogCount < ExternalOwnerLogLimit)
                {
                    _externalOwnerLogCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] DWELL_REDUCTION_CANLEAVE_OWNER_CHECK_FAILED: " + e.GetType().Name + ": " + e.Message);
                }
            }

            return false;
        }

        private static bool TryGetExternalOwner(IEnumerable<Patch> patches, out string owner)
        {
            owner = string.Empty;
            if (patches == null)
                return false;

            foreach (Patch patch in patches)
            {
                if (patch == null || string.IsNullOrEmpty(patch.owner))
                    continue;

                if (patch.owner == HarmonyId)
                    continue;

                owner = patch.owner;
                return true;
            }

            return false;
        }

        private static void LogExternalCanLeaveStandDown(string phase)
        {
            if (_externalCanLeaveStandDownLogged)
                return;

            _externalCanLeaveStandDownLogged = true;
            StopStackerDiagnostics.Advanced("[StopStacker] DWELL_REDUCTION_CANLEAVE_STAND_DOWN:"
                      + " externalOwner=" + (string.IsNullOrEmpty(_externalCanLeavePatchOwner) ? "unknown" : _externalCanLeavePatchOwner)
                      + " phase=" + (string.IsNullOrEmpty(phase) ? "unknown" : phase)
                      + " reason=external-bus-departure-or-unbunching-patch-active");
        }

        private static void ResetExchangeState(ushort vehicleId, ref Vehicle vehicle)
        {
            if (!IsEligibleBusLineVehicle(vehicle, false))
            {
                ExchangeStates.Remove(vehicleId);
                return;
            }

            StopExchangeState state = new StopExchangeState();
            state.Line = vehicle.m_transportLine;
            state.ArrivalStop = vehicle.m_targetBuilding;
            state.PassengersBeforeLoad = vehicle.m_transferSize;
            state.PassengersAfterLoad = vehicle.m_transferSize;
            ExchangeStates[vehicleId] = state;
        }

        private static StopExchangeState GetOrCreateExchangeState(ushort vehicleId, ref Vehicle vehicle)
        {
            StopExchangeState state;
            if (!ExchangeStates.TryGetValue(vehicleId, out state)
                || state.Line != vehicle.m_transportLine
                || state.ArrivalStop != vehicle.m_targetBuilding)
            {
                state = new StopExchangeState();
                state.Line = vehicle.m_transportLine;
                state.ArrivalStop = vehicle.m_targetBuilding;
                state.PassengersBeforeLoad = vehicle.m_transferSize;
                state.PassengersAfterLoad = vehicle.m_transferSize;
            }

            return state;
        }

        private static bool IsEligibleBusLineVehicle(Vehicle vehicle, bool requireStopped)
        {
            if ((vehicle.m_flags & Vehicle.Flags.Created) == 0)
                return false;

            if (requireStopped && (vehicle.m_flags & Vehicle.Flags.Stopped) == 0)
                return false;

            if ((vehicle.m_flags & (Vehicle.Flags.Leaving | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingTarget | Vehicle.Flags.GoingBack)) != 0)
                return false;

            if (vehicle.m_transportLine == 0 || vehicle.m_targetBuilding == 0)
                return false;

            VehicleInfo vehicleInfo = vehicle.Info;
            if (vehicleInfo == null || !(vehicleInfo.m_vehicleAI is BusAI))
                return false;

            TransportManager transportManager = TransportManager.instance;
            if (transportManager == null
                || transportManager.m_lines == null
                || transportManager.m_lines.m_buffer == null
                || vehicle.m_transportLine >= transportManager.m_lines.m_size
                || vehicle.m_transportLine >= transportManager.m_lines.m_buffer.Length)
                return false;

            TransportLine line = transportManager.m_lines.m_buffer[vehicle.m_transportLine];
            if ((line.m_flags & TransportLine.Flags.Created) == 0)
                return false;

            TransportInfo lineInfo = line.Info;
            return lineInfo != null && lineInfo.m_transportType == TransportInfo.TransportType.Bus;
        }

        private static bool IsPassengerExchangeComplete(ushort vehicleId, ref Vehicle vehicle)
        {
            StopExchangeState state;
            return TryGetCurrentExchangeState(vehicleId, ref vehicle, out state)
                   && state.UnloadObserved
                   && state.LoadStarted
                   && state.LoadFinished
                   && !HasEnteringPassengerOnVehicle(ref vehicle);
        }

        private static bool TryGetCurrentExchangeState(
            ushort vehicleId,
            ref Vehicle vehicle,
            out StopExchangeState state)
        {
            return ExchangeStates.TryGetValue(vehicleId, out state)
                   && state.Line == vehicle.m_transportLine
                   && state.ArrivalStop == vehicle.m_targetBuilding;
        }

        private static bool HasEnteringPassengerOnVehicle(ref Vehicle vehicle)
        {
            CitizenManager citizenManager = CitizenManager.instance;
            if (citizenManager == null
                || citizenManager.m_units == null
                || citizenManager.m_units.m_buffer == null
                || citizenManager.m_citizens == null
                || citizenManager.m_citizens.m_buffer == null
                || citizenManager.m_instances == null
                || citizenManager.m_instances.m_buffer == null)
                return false;

            uint unitId = vehicle.m_citizenUnits;
            int unitGuard = 0;
            while (unitId != 0)
            {
                if (unitId >= citizenManager.m_units.m_size
                    || unitId >= citizenManager.m_units.m_buffer.Length)
                    break;

                CitizenUnit unit = citizenManager.m_units.m_buffer[unitId];
                uint nextUnit = unit.m_nextUnit;
                for (int i = 0; i < 5; i++)
                {
                    uint citizenId = unit.GetCitizen(i);
                    if (citizenId == 0
                        || citizenId >= citizenManager.m_citizens.m_size
                        || citizenId >= citizenManager.m_citizens.m_buffer.Length)
                        continue;

                    ushort instanceId = citizenManager.m_citizens.m_buffer[citizenId].m_instance;
                    if (instanceId == 0
                        || instanceId >= citizenManager.m_instances.m_size
                        || instanceId >= citizenManager.m_instances.m_buffer.Length)
                        continue;

                    CitizenInstance instance = citizenManager.m_instances.m_buffer[instanceId];
                    if ((instance.m_flags & CitizenInstance.Flags.EnteringVehicle) != 0)
                        return true;
                }

                unitId = nextUnit;
                unitGuard++;
                if (unitGuard > MaxCitizenGridIterations)
                    break;
            }

            return false;
        }


        private static void LogDwellWarning(string eventName, ushort vehicleId, Exception e)
        {
            if (_dwellWarningLogCount >= DwellWarningLogLimit)
                return;

            _dwellWarningLogCount++;
            StopStackerDiagnostics.AdvancedWarning("[StopStacker] " + eventName + ": bus=" + vehicleId + " error=" + e.GetType().Name + ": " + e.Message);
        }

        private struct StopExchangeState
        {
            public ushort Line;
            public ushort ArrivalStop;
            public bool UnloadObserved;
            public int Alighted;
            public bool LoadStarted;
            public bool LoadFinished;
            public int PassengersBeforeLoad;
            public int PassengersAfterLoad;

            public bool HasAnyPassengerObservation
            {
                get { return UnloadObserved || LoadStarted || LoadFinished; }
            }

            public int Boarded
            {
                get
                {
                    if (!LoadFinished)
                        return 0;

                    return Math.Max(0, PassengersAfterLoad - PassengersBeforeLoad);
                }
            }

            public bool HasPassengerDelta
            {
                get { return Alighted > 0 || Boarded > 0; }
            }
        }
    }
}
