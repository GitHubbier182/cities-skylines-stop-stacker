using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace StopStacker
{
    internal static class BusStopPositionHarmony
    {
        private const string HarmonyId = "ScratchyBald.StopStacker.BusStopPosition";
        private const int ExternalOwnerLogLimit = 4;
        private const int ExternalOwnerCheckInterval = 256;

        private static readonly Type[] CalculateSegmentPositionSignature =
        {
            typeof(ushort),
            typeof(Vehicle).MakeByRefType(),
            typeof(PathUnit.Position),
            typeof(uint),
            typeof(byte),
            typeof(Vector3).MakeByRefType(),
            typeof(Vector3).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static Harmony _harmony;
        private static MethodBase _busTarget;
        private static MethodBase _trolleybusTarget;
        private static bool _patched;
        private static bool _externalStandDownLogged;
        private static int _externalOwnerLogCount;
        private static int _externalOwnerCheckCountdown;
        private static string _externalOwner;

        public static void Apply()
        {
            if (_patched)
                return;

            _externalStandDownLogged = false;
            _externalOwnerLogCount = 0;
            _externalOwnerCheckCountdown = 0;
            _externalOwner = string.Empty;

            try
            {
                _harmony = new Harmony(HarmonyId);
                int patchedTargets = 0;

                _busTarget = GetCalculateSegmentPositionMethod(typeof(BusAI));
                if (PatchTarget(_busTarget, "BusCalculateSegmentPositionPostfix"))
                    patchedTargets++;

                _trolleybusTarget = GetCalculateSegmentPositionMethod(typeof(TrolleybusAI));
                if (PatchTarget(_trolleybusTarget, "TrolleybusCalculateSegmentPositionPostfix"))
                    patchedTargets++;

                _patched = patchedTargets > 0;

                if (_patched)
                {
                    string owner;
                    bool externalActive = TryGetExternalStopPositionPatchOwner(out owner);
                    if (externalActive)
                        _externalOwner = owner;

                    StopStackerDiagnostics.Advanced("[StopStacker] STOP_POSITION_HARMONY_APPLIED:"
                              + " patchedTargets=" + patchedTargets
                              + " externalActive=" + externalActive
                              + (externalActive ? " externalOwner=" + owner : string.Empty)
                              + " rule=extend-arriving-bus-stop-position-to-forward-lane-end");
                }
                else
                {
                    Debug.LogWarning("[StopStacker] STOP_POSITION_HARMONY_NOT_APPLIED: no BusAI/TrolleybusAI CalculateSegmentPosition targets found.");
                }
            }
            catch (Exception e)
            {
                RollBackPartialPatch();
                _patched = false;
                _harmony = null;
                _busTarget = null;
                _trolleybusTarget = null;
                Debug.LogError("[StopStacker] STOP_POSITION_HARMONY_FAILED: " + e.GetType().Name + ": " + e.Message);
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
                Debug.LogWarning("[StopStacker] STOP_POSITION_HARMONY_ROLLBACK_FAILED: "
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
                StopStackerDiagnostics.Advanced("[StopStacker] STOP_POSITION_HARMONY_REMOVED.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StopStacker] STOP_POSITION_HARMONY_REMOVE_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                _patched = false;
                _harmony = null;
                _busTarget = null;
                _trolleybusTarget = null;
                _externalStandDownLogged = false;
                _externalOwnerCheckCountdown = 0;
                _externalOwner = string.Empty;
            }
        }

        private static bool PatchTarget(MethodBase target, string postfixName)
        {
            if (target == null || _harmony == null)
                return false;

            MethodInfo postfix = typeof(BusStopPositionHarmony).GetMethod(
                postfixName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (postfix == null)
                return false;

            HarmonyMethod postfixPatch = new HarmonyMethod(postfix);
            postfixPatch.priority = Priority.Last;
            _harmony.Patch(target, postfix: postfixPatch);
            return true;
        }

        private static MethodBase GetCalculateSegmentPositionMethod(Type aiType)
        {
            return aiType.GetMethod(
                "CalculateSegmentPosition",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                CalculateSegmentPositionSignature,
                null);
        }

        private static void BusCalculateSegmentPositionPostfix(
            ushort vehicleID,
            ref Vehicle vehicleData,
            PathUnit.Position position,
            uint laneID,
            byte offset,
            ref Vector3 pos,
            ref Vector3 dir)
        {
            ApplyExtendedStopPositionIfNeeded(_busTarget, vehicleID, ref vehicleData, position, laneID, offset, ref pos, ref dir);
        }

        private static void TrolleybusCalculateSegmentPositionPostfix(
            ushort vehicleID,
            ref Vehicle vehicleData,
            PathUnit.Position position,
            uint laneID,
            byte offset,
            ref Vector3 pos,
            ref Vector3 dir)
        {
            ApplyExtendedStopPositionIfNeeded(_trolleybusTarget, vehicleID, ref vehicleData, position, laneID, offset, ref pos, ref dir);
        }

        private static void ApplyExtendedStopPositionIfNeeded(
            MethodBase target,
            ushort vehicleID,
            ref Vehicle vehicleData,
            PathUnit.Position position,
            uint laneID,
            byte offset,
            ref Vector3 pos,
            ref Vector3 dir)
        {
            if (!StopStackerFeatures.BusStopPositionHarmonyEnabled)
                return;

            string owner;
            if (HasExternalStopPositionPatchOwner(target, out owner))
            {
                LogExternalStandDown(owner);
                return;
            }

            Vector3 adjustedPosition;
            Vector3 adjustedDirection;
            if (!TryCalculateExtendedStopPosition(vehicleID, ref vehicleData, position, laneID, offset, out adjustedPosition, out adjustedDirection))
                return;

            pos = adjustedPosition;
            dir = adjustedDirection;
        }

        private static bool TryCalculateExtendedStopPosition(
            ushort vehicleID,
            ref Vehicle vehicleData,
            PathUnit.Position position,
            uint laneID,
            byte offset,
            out Vector3 pos,
            out Vector3 dir)
        {
            pos = Vector3.zero;
            dir = Vector3.zero;

            if (vehicleID == 0 || vehicleData.m_transportLine == 0 || vehicleData.m_targetBuilding == 0)
                return false;

            if ((vehicleData.m_flags & Vehicle.Flags.Arriving) == 0
                || (vehicleData.m_flags & Vehicle.Flags.Leaving) != 0)
            {
                return false;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            if (netManager == null)
                return false;

            if (StopStackerBerthOverlay.HasUnsupportedNativeStopAnchor(netManager, vehicleData.m_targetBuilding))
                return false;

            if (position.m_segment == 0 || position.m_segment >= netManager.m_segments.m_buffer.Length)
                return false;

            if (laneID == 0 || laneID >= netManager.m_lanes.m_buffer.Length)
                return false;

            NetSegment segment = netManager.m_segments.m_buffer[position.m_segment];
            NetInfo info = segment.Info;
            if (info == null || info.m_lanes == null || position.m_lane >= info.m_lanes.Length)
                return false;

            NetInfo.Lane laneInfo = info.m_lanes[position.m_lane];
            if (laneInfo == null)
                return false;

            if ((laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == 0)
                return false;

            NetLane lane = netManager.m_lanes.m_buffer[(int)laneID];
            if (lane.m_length < 1f)
                return false;

            float adjustedLaneOffset;
            float laneOffset = offset * (1f / 255f);
            if (!TryGetForwardStopLaneOffset(lane.m_length, GetVehicleLength(ref vehicleData), laneInfo, segment.m_flags, laneOffset, out adjustedLaneOffset))
                return false;

            float stopOffset = laneInfo.m_stopOffset;
            if ((segment.m_flags & NetSegment.Flags.Invert) != 0)
                stopOffset = -stopOffset;

            lane.CalculateStopPositionAndDirection(adjustedLaneOffset, stopOffset, out pos, out dir);
            return true;
        }

        internal static bool TryGetForwardStopLaneOffset(
            float laneLength,
            float vehicleLength,
            NetInfo.Lane laneInfo,
            NetSegment.Flags segmentFlags,
            float laneOffset,
            out float adjustedLaneOffset)
        {
            adjustedLaneOffset = 0f;

            float maxForwardStopOffset = CalculateMaxForwardStopOffset(laneLength, vehicleLength);
            if (maxForwardStopOffset < 0.5f)
                return false;

            bool reverseLane = IsReverseLane(laneInfo, segmentFlags);
            float normalizedOffset = reverseLane ? 1f - Mathf.Clamp01(laneOffset) : Mathf.Clamp01(laneOffset);
            float adjustedNormalizedOffset = Mathf.Clamp01(normalizedOffset * (2f * maxForwardStopOffset));

            adjustedLaneOffset = reverseLane ? 1f - adjustedNormalizedOffset : adjustedNormalizedOffset;
            return true;
        }

        private static float CalculateMaxForwardStopOffset(float laneLength, float vehicleLength)
        {
            if (laneLength < 1f)
                return 0f;

            float laneEndReserve = laneLength / 6f;
            float halfVehicleLength = Mathf.Max(0f, vehicleLength) * 0.5f;
            return 1f - ((laneEndReserve + halfVehicleLength) / laneLength);
        }

        private static bool IsReverseLane(NetInfo.Lane laneInfo, NetSegment.Flags segmentFlags)
        {
            bool reverseLane = (laneInfo.m_finalDirection & NetInfo.Direction.Backward) != 0;
            if ((segmentFlags & NetSegment.Flags.Invert) != 0)
                reverseLane = !reverseLane;

            return reverseLane;
        }

        private static float GetVehicleLength(ref Vehicle vehicleData)
        {
            VehicleInfo vehicleInfo = vehicleData.Info;
            if (vehicleInfo == null || vehicleInfo.m_generatedInfo == null)
                return 0f;

            return vehicleInfo.m_generatedInfo.m_size.z;
        }

        private static bool TryGetExternalStopPositionPatchOwner(out string owner)
        {
            owner = string.Empty;

            string busOwner;
            if (TryGetExternalStopPositionPatchOwner(_busTarget ?? GetCalculateSegmentPositionMethod(typeof(BusAI)), out busOwner))
            {
                owner = busOwner;
                return true;
            }

            string trolleybusOwner;
            if (TryGetExternalStopPositionPatchOwner(_trolleybusTarget ?? GetCalculateSegmentPositionMethod(typeof(TrolleybusAI)), out trolleybusOwner))
            {
                owner = trolleybusOwner;
                return true;
            }

            return false;
        }

        private static bool TryGetExternalStopPositionPatchOwner(MethodBase target, out string owner)
        {
            owner = string.Empty;
            if (target == null)
                return false;

            try
            {
                Patches patches = Harmony.GetPatchInfo(target);
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
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] STOP_POSITION_OWNER_CHECK_FAILED: " + e.GetType().Name + ": " + e.Message);
                }
            }

            return false;
        }

        private static bool HasExternalStopPositionPatchOwner(MethodBase target, out string owner)
        {
            if (!string.IsNullOrEmpty(_externalOwner))
            {
                owner = _externalOwner;
                return true;
            }

            if (_externalOwnerCheckCountdown > 0)
            {
                _externalOwnerCheckCountdown--;
                owner = string.Empty;
                return false;
            }

            _externalOwnerCheckCountdown = ExternalOwnerCheckInterval;
            if (!TryGetExternalStopPositionPatchOwner(target, out owner))
                return false;

            _externalOwner = owner;
            return true;
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

                if (IsExternalStopPositionOwner(patch.owner))
                {
                    owner = patch.owner;
                    return true;
                }
            }

            return false;
        }

        private static bool IsExternalStopPositionOwner(string owner)
        {
            if (string.IsNullOrEmpty(owner) || owner == HarmonyId)
                return false;

            return owner.IndexOf("BetterBusStopPosition", StringComparison.OrdinalIgnoreCase) >= 0
                   || owner.IndexOf("ImprovedPublicTransport", StringComparison.OrdinalIgnoreCase) >= 0
                   || owner.IndexOf("IPTEssentials", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void LogExternalStandDown(string owner)
        {
            if (_externalStandDownLogged)
                return;

            _externalStandDownLogged = true;
            StopStackerDiagnostics.Advanced("[StopStacker] STOP_POSITION_HARMONY_STAND_DOWN:"
                      + " externalOwner=" + owner
                      + " reason=known-bbs-or-ipt-stop-position-patch-active");
        }

    }
}
