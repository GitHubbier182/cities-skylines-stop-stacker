using System;
using System.Collections;
using ScratchyBald.CitiesSkylines.Shared;
using UnityEngine;

namespace StopStacker
{
    internal static class StopStackerScanCoordinator
    {
        public const string OwnerId = "StopStacker";
        private const string TopologyRequestId = "berth-topology";

        private static IEnumerator _topologyRoutine;
        private static string _topologyTicket;
        private static Action _topologyCompleted;
        private static Action<IEnumerator, Exception> _topologyFailed;
        private static bool _available;
        private static bool _failureLogged;
        private static int _topologyGeneration;

        public static void Initialize()
        {
            Shutdown();
            _failureLogged = false;
            try
            {
                ScratchysScanManager.Initialize(
                    OwnerId,
                    delegate
                    {
                        return StopStackerModSettings.AdvancedDiagnostics;
                    });
                _available = true;
                StopStackerDiagnostics.Advanced(
                    "[StopStacker] Scratchy's Scan Manager registered;"
                    + " berth-topology discovery will use cooperative"
                    + " main-thread requests.");
            }
            catch (Exception exception)
            {
                _available = false;
                LogFallback("initialization failed", exception);
            }
        }

        public static bool TryQueueTopology(
            IEnumerator routine,
            bool startup,
            bool playerRequested,
            Action completed,
            Action<IEnumerator, Exception> failed)
        {
            if (!_available || routine == null)
                return false;

            CancelTopology();
            _topologyRoutine = routine;
            _topologyCompleted = completed;
            _topologyFailed = failed;
            int requestGeneration = ++_topologyGeneration;
            try
            {
                _topologyTicket =
                    ScratchysScanManager.QueueMainThreadScan(
                        OwnerId,
                        TopologyRequestId,
                        playerRequested
                            ? ScratchysScanManager.PlayerRequestedPriority
                            : startup
                            ? ScratchysScanManager.StartupPriority
                            : ScratchysScanManager.MaintenancePriority,
                        () => StepTopology(requestGeneration),
                        () => CompleteTopology(requestGeneration),
                        exception => FailTopology(requestGeneration, exception));
                return true;
            }
            catch (Exception exception)
            {
                ClearTopology(false);
                _available = false;
                LogFallback(
                    "topology request submission failed",
                    exception);
                return false;
            }
        }

        public static void CancelTopology()
        {
            _topologyGeneration++;
            if (!string.IsNullOrEmpty(_topologyTicket))
            {
                try
                {
                    ScratchysScanManager.Cancel(_topologyTicket);
                }
                catch (Exception exception)
                {
                    LogFallback(
                        "topology cancellation failed",
                        exception);
                }
            }

            ClearTopology(true);
        }

        public static void Shutdown()
        {
            if (_available)
            {
                try
                {
                    ScratchysScanManager.CancelOwner(OwnerId);
                }
                catch (Exception exception)
                {
                    LogFallback(
                        "level-unload cancellation failed",
                        exception);
                }
            }

            ClearTopology(true);
            _available = false;
        }

        private static bool StepTopology(int requestGeneration)
        {
            if (requestGeneration != _topologyGeneration)
                return true;

            return _topologyRoutine == null
                   || !_topologyRoutine.MoveNext();
        }

        private static void CompleteTopology(int requestGeneration)
        {
            if (requestGeneration != _topologyGeneration)
                return;

            Action completed = _topologyCompleted;
            ClearTopology(true);
            if (completed != null)
                completed();
        }

        private static void FailTopology(int requestGeneration, Exception exception)
        {
            if (requestGeneration != _topologyGeneration)
                return;

            IEnumerator routine = _topologyRoutine;
            Action<IEnumerator, Exception> failed = _topologyFailed;
            ClearTopology(false);
            Debug.LogWarning(
                "[StopStacker] Scratchy's Scan Manager berth-topology"
                + " request failed; Stop Stacker will dispose the faulted"
                + " iterator and restart a complete pass through its local"
                + " coroutine. exception="
                + exception);
            if (failed != null)
                failed(routine, exception);
            else
                Dispose(routine);
        }

        private static void ClearTopology(bool dispose)
        {
            IEnumerator routine = _topologyRoutine;
            _topologyRoutine = null;
            _topologyTicket = null;
            _topologyCompleted = null;
            _topologyFailed = null;
            if (dispose)
                Dispose(routine);
        }

        private static void Dispose(IEnumerator routine)
        {
            IDisposable disposable = routine as IDisposable;
            if (disposable != null)
                disposable.Dispose();
        }

        private static void LogFallback(
            string operation,
            Exception exception)
        {
            if (_failureLogged)
                return;

            _failureLogged = true;
            Debug.LogWarning(
                "[StopStacker] Scratchy's Scan Manager "
                + operation
                + "; Stop Stacker will preserve its existing local"
                + " coroutine scheduler. exception="
                + exception);
        }
    }
}
