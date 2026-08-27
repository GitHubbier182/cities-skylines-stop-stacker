using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using UnityEngine;

namespace StopStacker
{
    internal static class MultiBusStopService
    {
        private const float ServiceLaneLateralTolerance = 7.5f;
        private const float ServiceZoneContainmentTolerance = 1.5f;
        private const float StationaryPositionSqrTolerance = 0.000001f;
        private const float DefaultBusLength = 12f;
        private const byte QueueBlockedCounterThreshold = 4;
        private const int BusCandidateScanBudgetPerServiceTick = 96;
        private const int ServiceAttemptBudgetPerServiceTick = 6;
        private const int ServiceWarningLogLimit = 12;
        private const int BusCandidateDiscoveryBudgetPerServiceTick = 512;

        private static readonly Type[] VehicleOnlySignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType()
        };

        private static readonly Dictionary<ushort, BusServiceState> BusStates = new Dictionary<ushort, BusServiceState>(256);
        private static readonly List<ushort> BusCandidateVehicles = new List<ushort>(512);
        private static readonly HashSet<ushort> BusCandidateVehicleIds = new HashSet<ushort>();
        private static readonly HashSet<ushort> BusCandidatesSeenThisSweep = new HashSet<ushort>();

        private static MethodInfo _arriveAtTarget;
        private static bool _arrivalReflectionFailed;
        private static bool _arrivalReflectionLogged;
        private static bool _activeLogged;
        private static int _busCandidateCursor;
        private static int _busCandidateDiscoveryCursor;
        private static int _serviceWarningLogCount;
        private static bool _startupBacklogPrimePending;

        public static void Reset()
        {
            BusStates.Clear();
            BusCandidateVehicles.Clear();
            BusCandidateVehicleIds.Clear();
            BusCandidatesSeenThisSweep.Clear();
            _arriveAtTarget = null;
            _arrivalReflectionFailed = false;
            _arrivalReflectionLogged = false;
            _activeLogged = false;
            _busCandidateCursor = 0;
            _busCandidateDiscoveryCursor = 1;
            _serviceWarningLogCount = 0;
            _startupBacklogPrimePending = true;
        }

        public static void Update()
        {
            if (!StopStackerFeatures.MultiBusStopServiceEnabled)
                return;

            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            if (vehicleManager == null || vehicleManager.m_vehicles == null || vehicleManager.m_vehicles.m_buffer == null)
                return;

            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (transportManager == null || transportManager.m_lines == null || transportManager.m_lines.m_buffer == null)
                return;

            if (!_activeLogged)
            {
                _activeLogged = true;
                StopStackerDiagnostics.Advanced("[StopStacker] MULTI_BUS_SERVICE_ACTIVE:"
                          + " rule=all-eligible-buses-fully-inside-service-zone"
                          + " longBusException=true"
                          + " vehicleSource=bounded-vehicle-manager-sweep"
                          + " busCandidateDiscoveryBudget=" + BusCandidateDiscoveryBudgetPerServiceTick
                          + " busCandidateScanBudget=" + BusCandidateScanBudgetPerServiceTick
                          + " serviceAttemptBudget=" + ServiceAttemptBudgetPerServiceTick
                          + " lateralTolerance=" + ServiceLaneLateralTolerance.ToString("0.0")
                          + " containmentTolerance=" + ServiceZoneContainmentTolerance.ToString("0.0"));
            }

            Vehicle[] vehicles = vehicleManager.m_vehicles.m_buffer;
            AdvanceBusCandidateDiscovery(vehicleManager, vehicles);

            if (BusCandidateVehicles.Count == 0)
                return;

            if (_busCandidateCursor < 0 || _busCandidateCursor >= BusCandidateVehicles.Count)
                _busCandidateCursor = 0;

            int scanned = 0;
            int serviceAttempts = 0;
            bool completedCycle = false;
            while (_busCandidateCursor < BusCandidateVehicles.Count
                   && scanned < BusCandidateScanBudgetPerServiceTick
                   && serviceAttempts < ServiceAttemptBudgetPerServiceTick)
            {
                ushort vehicleId = BusCandidateVehicles[_busCandidateCursor];
                _busCandidateCursor++;
                scanned++;
                bool serviceAttempted;
                ProcessVehicle(vehicleId, vehicles, out serviceAttempted);
                if (serviceAttempted)
                {
                    serviceAttempts++;
                }
            }

            if (_busCandidateCursor >= BusCandidateVehicles.Count)
            {
                _busCandidateCursor = 0;
                completedCycle = true;
            }

            if (completedCycle)
                PruneBusStates(vehicleManager);
        }

        private static void AdvanceBusCandidateDiscovery(VehicleManager vehicleManager, Vehicle[] vehicles)
        {
            int vehicleLimit = Math.Min(
                Math.Min((int)vehicleManager.m_vehicles.m_size, vehicles.Length),
                ushort.MaxValue + 1);
            if (_busCandidateDiscoveryCursor < 1 || _busCandidateDiscoveryCursor >= vehicleLimit)
                _busCandidateDiscoveryCursor = 1;

            int inspected = 0;
            while (_busCandidateDiscoveryCursor < vehicleLimit
                   && inspected < BusCandidateDiscoveryBudgetPerServiceTick)
            {
                ushort vehicleId = (ushort)_busCandidateDiscoveryCursor;
                _busCandidateDiscoveryCursor++;
                inspected++;
                Vehicle vehicle = vehicles[vehicleId];
                if (!IsCandidateBus(ref vehicle))
                    continue;

                BusCandidatesSeenThisSweep.Add(vehicleId);
                if (BusCandidateVehicleIds.Add(vehicleId))
                {
                    BusCandidateVehicles.Add(vehicleId);
                    if (_startupBacklogPrimePending)
                        PrimeCurrentCandidateStop(vehicleId, ref vehicle);
                }
            }

            if (_busCandidateDiscoveryCursor < vehicleLimit)
                return;

            PruneCandidateVehiclesNotSeen();
            BusCandidatesSeenThisSweep.Clear();
            _busCandidateDiscoveryCursor = 1;
            _startupBacklogPrimePending = false;
        }

        private static void PrimeCurrentCandidateStop(ushort vehicleId, ref Vehicle vehicle)
        {
            if (!IsCandidateBus(ref vehicle))
                return;

            ushort lineId = vehicle.m_transportLine;
            ushort targetStop = vehicle.m_targetBuilding;
            if (lineId == 0 || targetStop == 0)
                return;

            BusStates[vehicleId] = new BusServiceState(lineId, targetStop, true);
        }

        private static void PruneCandidateVehiclesNotSeen()
        {
            for (int i = BusCandidateVehicles.Count - 1; i >= 0; i--)
            {
                ushort vehicleId = BusCandidateVehicles[i];
                if (BusCandidatesSeenThisSweep.Contains(vehicleId))
                    continue;

                BusCandidateVehicles.RemoveAt(i);
                BusCandidateVehicleIds.Remove(vehicleId);
                BusStates.Remove(vehicleId);
            }

            _busCandidateCursor = 0;
        }

        private static void ProcessVehicle(ushort vehicleId, Vehicle[] vehicles, out bool serviceAttempted)
        {
            serviceAttempted = false;

            Vehicle vehicle = vehicles[vehicleId];
            if (!IsCandidateBus(ref vehicle))
            {
                BusStates.Remove(vehicleId);
                return;
            }

            ushort targetStop = vehicle.m_targetBuilding;
            ushort lineId = vehicle.m_transportLine;
            BusServiceState state;
            BusStates.TryGetValue(vehicleId, out state);
            if (state.LineId != lineId || state.StopNode != targetStop)
            {
                state = new BusServiceState(lineId, targetStop, false);
            }

            if (state.InvokedForStop)
            {
                BusStates[vehicleId] = state;
                return;
            }

            StopStackerBerthOverlay.StopServiceZone zone;
            if (!StopStackerBerthOverlay.TryGetServiceZone(lineId, targetStop, out zone))
            {
                ResetStationaryObservation(ref state);
                BusStates[vehicleId] = state;
                return;
            }

            string reason;
            float progress;
            float lateralDistance;
            float busLength;
            bool longBusException;
            if (!IsReadyForServiceZoneExchange(
                    ref vehicle,
                    zone,
                    out progress,
                    out lateralDistance,
                    out busLength,
                    out longBusException,
                    out reason))
            {
                ResetStationaryObservation(ref state);
                BusStates[vehicleId] = state;
                return;
            }

            Vector3 stationaryPosition = vehicle.GetLastFramePosition();
            if (!state.HasStationaryObservation
                || SqrDistance(state.StationaryPosition, stationaryPosition) > StationaryPositionSqrTolerance)
            {
                state.HasStationaryObservation = true;
                state.StationaryPosition = stationaryPosition;
                BusStates[vehicleId] = state;
                return;
            }

            serviceAttempted = true;
            ushort beforePassengers = vehicle.m_transferSize;
            IptEssentialsPassengerStatsCompatibility.RegisterServiceWindow(vehicleId, lineId, targetStop, beforePassengers);
            if (TryInvokeVanillaStopService(vehicleId, ref vehicle))
            {
                IptEssentialsPassengerStatsCompatibility.ReconcileServiceWindow(vehicleId, lineId, targetStop, beforePassengers, vehicle.m_transferSize);
                vehicles[vehicleId] = vehicle;
                state.InvokedForStop = true;
                BusStates[vehicleId] = state;
            }
            else
            {
                BusStates[vehicleId] = state;
            }
        }

        private static bool IsCandidateBus(ref Vehicle vehicle)
        {
            if ((vehicle.m_flags & Vehicle.Flags.Created) == 0)
                return false;

            if (vehicle.m_transportLine == 0 || vehicle.m_targetBuilding == 0)
                return false;

            VehicleInfo vehicleInfo = vehicle.Info;
            if (vehicleInfo == null || !(vehicleInfo.m_vehicleAI is BusAI))
                return false;

            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (transportManager == null
                || transportManager.m_lines == null
                || transportManager.m_lines.m_buffer == null
                || vehicle.m_transportLine >= transportManager.m_lines.m_size
                || vehicle.m_transportLine >= transportManager.m_lines.m_buffer.Length)
            {
                return false;
            }

            TransportLine line = transportManager.m_lines.m_buffer[vehicle.m_transportLine];
            if ((line.m_flags & TransportLine.Flags.Created) == 0
                || (line.m_flags & (TransportLine.Flags.Temporary | TransportLine.Flags.Hidden)) != 0)
                return false;

            TransportInfo lineInfo = line.Info;
            return lineInfo != null && lineInfo.m_transportType == TransportInfo.TransportType.Bus;
        }

        private static bool IsReadyForServiceZoneExchange(
            ref Vehicle vehicle,
            StopStackerBerthOverlay.StopServiceZone zone,
            out float progress,
            out float lateralDistance,
            out float busLength,
            out bool longBusException,
            out string reason)
        {
            progress = 0f;
            lateralDistance = 0f;
            busLength = GetVehicleLength(ref vehicle);
            longBusException = busLength > zone.ServiceLength + ServiceZoneContainmentTolerance;

            if (vehicle.m_targetBuilding != zone.StopNode)
            {
                reason = "target-stop-mismatch";
                return false;
            }

            Vehicle.Flags flags = vehicle.m_flags;
            if ((flags & (Vehicle.Flags.Leaving | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingTarget | Vehicle.Flags.GoingBack)) != 0)
            {
                reason = "vanilla-transition";
                return false;
            }

            if (!IsStationaryForService(ref vehicle))
            {
                reason = "not-stopped";
                return false;
            }

            if (!StopStackerBerthOverlay.TryGetBusProgressInServiceZone(zone, vehicle.GetLastFramePosition(), out progress, out lateralDistance))
            {
                reason = "no-lane-progress";
                return false;
            }

            if (lateralDistance > ServiceLaneLateralTolerance)
            {
                reason = "outside-lateral-zone";
                return false;
            }

            if (progress < -ServiceZoneContainmentTolerance)
            {
                reason = "front-before-service-zone";
                return false;
            }

            if (longBusException)
            {
                if (progress <= zone.ServiceLength + ServiceZoneContainmentTolerance)
                {
                    reason = "eligible-long-bus";
                    return true;
                }

                reason = "front-past-service-zone";
                return false;
            }

            if (progress + busLength <= zone.ServiceLength + ServiceZoneContainmentTolerance)
            {
                reason = "eligible-contained";
                return true;
            }

            reason = "rear-outside-service-zone";
            return false;
        }

        private static bool IsStationaryForService(ref Vehicle vehicle)
        {
            Vehicle.Frame frame = vehicle.GetLastFrameData();
            float velocitySqr = frame.m_velocity.sqrMagnitude;
            if (float.IsNaN(velocitySqr) || velocitySqr > 0f)
                return false;

            Vehicle.Flags flags = vehicle.m_flags;
            if ((flags & (Vehicle.Flags.Stopped | Vehicle.Flags.WaitingLoading)) != 0)
                return true;

            return vehicle.m_blockCounter >= QueueBlockedCounterThreshold;
        }

        private static float SqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        private static void ResetStationaryObservation(ref BusServiceState state)
        {
            state.HasStationaryObservation = false;
            state.StationaryPosition = Vector3.zero;
        }

        private static bool TryInvokeVanillaStopService(ushort vehicleId, ref Vehicle vehicle)
        {
            BusAI busAI = vehicle.Info != null ? vehicle.Info.m_vehicleAI as BusAI : null;
            if (busAI == null || vehicle.m_targetBuilding == 0)
                return false;

            if (!EnsureArrivalReflectionReady())
                return false;

            try
            {
                object[] args = { vehicleId, vehicle };
                _arriveAtTarget.Invoke(busAI, args);
                vehicle = (Vehicle)args[1];
                return true;
            }
            catch (Exception e)
            {
                if (_serviceWarningLogCount < ServiceWarningLogLimit)
                {
                    _serviceWarningLogCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] MULTI_BUS_SERVICE_INVOKE_FAILED:"
                                     + " bus=" + vehicleId
                                     + " error=" + e.GetType().Name
                                     + " message=" + e.Message);
                }

                return false;
            }
        }

        private static bool EnsureArrivalReflectionReady()
        {
            if (_arrivalReflectionFailed)
                return false;

            if (_arriveAtTarget != null)
                return true;

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _arriveAtTarget = typeof(BusAI).GetMethod("ArriveAtTarget", flags, null, VehicleOnlySignature, null);
            if (_arriveAtTarget != null)
            {
                if (!_arrivalReflectionLogged)
                {
                    _arrivalReflectionLogged = true;
                    StopStackerDiagnostics.Advanced("[StopStacker] MULTI_BUS_SERVICE_HELPER_RESOLVED: method=BusAI.ArriveAtTarget");
                }

                return true;
            }

            _arrivalReflectionFailed = true;
            Debug.LogWarning("[StopStacker] MULTI_BUS_SERVICE_HELPER_NOT_FOUND: method=BusAI.ArriveAtTarget");
            return false;
        }

        private static float GetVehicleLength(ref Vehicle vehicle)
        {
            VehicleInfo info = vehicle.Info;
            if (info != null && info.m_generatedInfo != null && info.m_generatedInfo.m_size.z > 1f)
                return info.m_generatedInfo.m_size.z;

            return DefaultBusLength;
        }


        private static void PruneBusStates(VehicleManager vehicleManager)
        {
            if (BusStates.Count == 0
                || vehicleManager == null
                || vehicleManager.m_vehicles == null
                || vehicleManager.m_vehicles.m_buffer == null)
                return;

            ushort[] keys = new ushort[BusStates.Count];
            BusStates.Keys.CopyTo(keys, 0);
            for (int i = 0; i < keys.Length; i++)
            {
                ushort vehicleId = keys[i];
                if (vehicleId == 0
                    || vehicleId >= vehicleManager.m_vehicles.m_size
                    || vehicleId >= vehicleManager.m_vehicles.m_buffer.Length)
                {
                    BusStates.Remove(vehicleId);
                    continue;
                }

                Vehicle vehicle = vehicleManager.m_vehicles.m_buffer[vehicleId];
                if ((vehicle.m_flags & Vehicle.Flags.Created) == 0
                    || vehicle.m_transportLine == 0
                    || vehicle.m_targetBuilding == 0)
                {
                    BusStates.Remove(vehicleId);
                }
            }
        }

        private struct BusServiceState
        {
            public readonly ushort LineId;
            public readonly ushort StopNode;
            public bool InvokedForStop;
            public bool HasStationaryObservation;
            public Vector3 StationaryPosition;

            public BusServiceState(ushort lineId, ushort stopNode, bool invokedForStop)
            {
                LineId = lineId;
                StopNode = stopNode;
                InvokedForStop = invokedForStop;
                HasStationaryObservation = false;
                StationaryPosition = Vector3.zero;
            }
        }
    }
}
