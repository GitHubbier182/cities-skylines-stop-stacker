using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StopStacker
{
    internal static class PassengerWaitPositionHarmony
    {
        private const string HarmonyId = "ScratchyBald.StopStacker.PassengerWaitPosition";
        private const float PassengerStandingSpacing = 0.45f;
        private const float PassengerStandingRowSpacing = 0.35f;
        private const float SameAssignmentDistance = 0.35f;
        private const int PruneEveryRefreshes = 12;

        private static readonly Type[] GetTransportWaitPositionSignature =
        {
            typeof(ushort),
            typeof(CitizenInstance).MakeByRefType(),
            typeof(CitizenInstance.Frame).MakeByRefType(),
            typeof(float)
        };

        private static readonly Dictionary<ushort, WaitAssignment> Assignments = new Dictionary<ushort, WaitAssignment>(2048);
        private static readonly HashSet<ushort> RegisteredThisRefresh = new HashSet<ushort>();
        private static readonly object AssignmentLock = new object();
        private static Harmony _harmony;
        private static MethodBase _target;
        private static int _refreshId;
        private static bool _patched;

        public static void Apply()
        {
            if (_patched)
                return;

            try
            {
                _harmony = new Harmony(HarmonyId);
                _target = GetTransportWaitPositionMethod();
                if (_target == null)
                {
                    Debug.LogWarning("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_NOT_APPLIED: HumanAI.GetTransportWaitPosition target not found.");
                    return;
                }

                MethodInfo postfix = typeof(PassengerWaitPositionHarmony).GetMethod(
                    "GetTransportWaitPositionPostfix",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (postfix == null)
                {
                    Debug.LogWarning("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_NOT_APPLIED: postfix not found.");
                    return;
                }

                _harmony.Patch(_target, postfix: new HarmonyMethod(postfix));
                _patched = true;
                StopStackerDiagnostics.Advanced("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_APPLIED: rule=assigned-berth-wait-position");
            }
            catch (Exception e)
            {
                RollBackPartialPatch();
                _patched = false;
                _harmony = null;
                _target = null;
                Debug.LogError("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_FAILED: " + e.GetType().Name + ": " + e.Message);
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
                Debug.LogWarning("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_ROLLBACK_FAILED: "
                                 + rollbackError.GetType().Name + ": " + rollbackError.Message);
            }
        }

        public static void Unpatch()
        {
            ClearAssignments();
            if (_harmony == null || !_patched)
                return;

            try
            {
                _harmony.UnpatchAll(HarmonyId);
                StopStackerDiagnostics.Advanced("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_REMOVED.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StopStacker] PASSENGER_WAIT_POSITION_HARMONY_REMOVE_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                _patched = false;
                _harmony = null;
                _target = null;
            }
        }

        public static void BeginRefresh()
        {
            lock (AssignmentLock)
            {
                _refreshId++;
                RegisteredThisRefresh.Clear();

                if (_refreshId % PruneEveryRefreshes == 0)
                    PruneStaleAssignments();
            }
        }

        public static bool RegisterWaitingPassenger(
            ushort instanceId,
            uint citizenId,
            ushort lineId,
            ushort stopNode,
            int berthNumber,
            Vector3 berthWaitAnchor,
            int passengerIndex)
        {
            if (instanceId == 0 || citizenId == 0 || lineId == 0 || stopNode == 0 || berthNumber <= 0)
                return false;

            lock (AssignmentLock)
            {
                if (!RegisteredThisRefresh.Add(instanceId))
                    return false;

                WaitAssignment existing;
                if (Assignments.TryGetValue(instanceId, out existing)
                    && existing.LineId == lineId
                    && existing.StopNode == stopNode
                    && existing.CitizenId == citizenId
                    && existing.BerthNumber == berthNumber
                    && SqrDistanceXZ(existing.BerthWaitAnchor, berthWaitAnchor) <= SameAssignmentDistance * SameAssignmentDistance)
                {
                    existing.RefreshId = _refreshId;
                    Assignments[instanceId] = existing;
                    return true;
                }

                Assignments[instanceId] = new WaitAssignment(
                    citizenId,
                    lineId,
                    stopNode,
                    berthNumber,
                    berthWaitAnchor,
                    GetPassengerStandingPosition(berthWaitAnchor, passengerIndex),
                    _refreshId);
                return true;
            }
        }

        public static void ClearAssignments()
        {
            lock (AssignmentLock)
            {
                Assignments.Clear();
                RegisteredThisRefresh.Clear();
                _refreshId = 0;
            }
        }

        private static MethodBase GetTransportWaitPositionMethod()
        {
            return typeof(HumanAI).GetMethod(
                "GetTransportWaitPosition",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                GetTransportWaitPositionSignature,
                null);
        }

        private static void GetTransportWaitPositionPostfix(
            ushort instanceID,
            ref CitizenInstance citizenData,
            ref CitizenInstance.Frame frameData,
            float minSqrDistance,
            ref Vector4 __result)
        {
            if ((citizenData.m_flags & CitizenInstance.Flags.WaitingTransport) == 0)
                return;

            WaitAssignment assignment;
            lock (AssignmentLock)
            {
                if (!Assignments.TryGetValue(instanceID, out assignment))
                    return;

                if (assignment.CitizenId != citizenData.m_citizen)
                {
                    Assignments.Remove(instanceID);
                    return;
                }

                if (assignment.RefreshId < _refreshId - 1)
                    return;
            }

            Vector3 waitPosition = assignment.WaitPosition;
            __result = new Vector4(waitPosition.x, waitPosition.y, waitPosition.z, __result.w);
        }

        private static Vector3 GetPassengerStandingPosition(Vector3 berthWaitAnchor, int passengerIndex)
        {
            int safeIndex = Mathf.Max(0, passengerIndex - 1);
            int column = safeIndex % 3;
            int row = (safeIndex / 3) % 4;
            float xOffset = (column - 1) * PassengerStandingSpacing;
            float zOffset = row * PassengerStandingRowSpacing;
            return berthWaitAnchor + new Vector3(xOffset, 0f, zOffset);
        }

        private static void PruneStaleAssignments()
        {
            List<ushort> stale = null;
            foreach (KeyValuePair<ushort, WaitAssignment> item in Assignments)
            {
                if (item.Value.RefreshId >= _refreshId - 1)
                    continue;

                if (stale == null)
                    stale = new List<ushort>();

                stale.Add(item.Key);
            }

            if (stale == null)
                return;

            for (int i = 0; i < stale.Count; i++)
                Assignments.Remove(stale[i]);
        }

        private static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (dx * dx) + (dz * dz);
        }

        private struct WaitAssignment
        {
            public readonly uint CitizenId;
            public readonly ushort LineId;
            public readonly ushort StopNode;
            public readonly int BerthNumber;
            public readonly Vector3 BerthWaitAnchor;
            public readonly Vector3 WaitPosition;
            public int RefreshId;

            public WaitAssignment(
                uint citizenId,
                ushort lineId,
                ushort stopNode,
                int berthNumber,
                Vector3 berthWaitAnchor,
                Vector3 waitPosition,
                int refreshId)
            {
                CitizenId = citizenId;
                LineId = lineId;
                StopNode = stopNode;
                BerthNumber = berthNumber;
                BerthWaitAnchor = berthWaitAnchor;
                WaitPosition = waitPosition;
                RefreshId = refreshId;
            }
        }
    }
}
