using System.Collections;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using UnityEngine;

namespace StopStacker
{
    public class StopStackerBerthOverlay : MonoBehaviour
    {
        private const float BerthSpacing = 15f;
        private const float MinimumEndClearance = 14.9f;
        private const float RefreshSeconds = 3f;
        private const float InitialTopologyStabilitySeconds = 2.5f;
        private const float StatusBubbleRefreshSeconds = 2.5f;
        private const float MarkerWorldLift = 0.35f;
        private const float LabelMaxCameraHeight = 750f;
        private const float BusStopPropSideOffset = 2.8f;
        private const float LegacyNativePropCleanupDistance = 1.5f;
        private const float LegacyNativePropAngleTolerance = 0.18f;
        private const float StopLaneSearchRadius = 64f;
        private const float StatusBubbleBuildingSearchRadius = 128f;
        private const float StopOffsetEpsilon = 0.01f;
        private const float DuplicateBerthMergeDistance = 4f;
        private const float WaitingPassengerScanRadius = 32f;
        private const float PassengerAssignmentScanRadius = 128f;
        private const float LabelWidth = 41f;
        private const float LabelHeight = 22f;
        private const float LabelYOffset = 11f;
        private const int LabelFontSize = 15;
        private const float StatusBubbleWorldLift = 1.65f;
        private const float StatusBubbleWidth = 380f;
        private const float DisabledStatusBubbleWidth = 210f;
        private const float DisabledStatusBubbleHeight = 34f;
        private const float StatusBubbleLineHeight = 20f;
        private const float StatusBubblePaddingX = 10f;
        private const float StatusBubblePaddingY = 6f;
        private const float StatusBubbleScreenOffsetX = 26f;
        private const float StatusBubbleScreenOffsetY = 78f;
        private const float StatusBubbleScreenPadding = 8f;
        private const float StatusBubbleToggleSize = 16f;
        private const float StatusBubbleToggleInset = 5f;
        private const float UiOcclusionPadding = 2f;
        private const float UiOcclusionMinWidth = 24f;
        private const float UiOcclusionMinHeight = 18f;
        private const float StatusBubbleBerthWidth = 44f;
        private const float StatusBubbleStopWidth = 48f;
        private const float StatusBubbleWaitingWidth = 72f;
        private const float StatusBubbleRefreshViewportPadding = 0.08f;
        private const int StatusBubbleTitleFontSize = 13;
        private const int StatusBubbleFontSize = 12;
        private const int CitizenGridResolution = 2160;
        private const float CitizenGridCellSize = 8f;
        private const float CitizenGridHalfResolution = 1080f;
        private const int MaxCitizenGridChainIterations = 65536;
        private const int NetSegmentGridResolution = 270;
        private const float NetSegmentGridCellSize = 64f;
        private const float NetSegmentGridHalfResolution = 135f;
        private const int MaxSegmentGridChainIterations = ushort.MaxValue;
        private const int BuildingGridResolution = 270;
        private const float BuildingGridCellSize = 64f;
        private const float BuildingGridHalfResolution = 135f;
        private const int MaxBuildingGridChainIterations = 65536;
        private const int MaxTransportChainIterations = ushort.MaxValue;
        private const int WaitingRouteErrorLogLimit = 3;
        private const int StatusBubbleRefreshLogLimit = 24;
        private const float DefaultVisualBusLength = 12f;
        private const int FullCityTopologyStopsPerFrame = 1;
        private const int CameraPriorityNodeInspectionsPerFrame = 96;
        private const float CameraPriorityViewportPadding = 0.08f;
        private const float CameraPriorityWorldPadding = 128f;
        private const int FullStatusRowsPerFrame = 6;
        private const float FullStatusPassengerRatePerSecond = 1000f;
        private const float FullStatusPassengerBurst = 64f;
        private const int FullStatusGridCellsPerFrame = 256;
        private const int FullStatusCitizenInspectionsPerFrame = 256;
        private const int WorldSignsPerFrame = 18;
        private const int DepartureBoardsPerFrame = 3;
        private const int SkippedRoadSegmentObservation = -1;

        private enum StopAnchorResolution
        {
            Missing,
            SupportedRoad,
            UnsupportedNative
        }
        private const float WorldSignPoleHeight = 2.5f;
        private const float WorldSignPoleWidth = 0.12f;
        private const float WorldSignPlateLift = 2.2f;
        private const float WorldSignPlateWidth = 0.8f;
        private const float WorldSignPlateHeight = 0.55f;
        private const float WorldSignPlateDepth = 0.08f;
        private const float DepartureBoardSideOffset = 3.2f;
        private const float DepartureBoardDuplicateDistance = 4f;
        private const float DepartureBoardPoleHeight = 1.45f;
        private const float DepartureBoardPoleWidth = 0.07f;
        private const float DepartureBoardBaseWidth = 0.7f;
        private const float DepartureBoardBaseHeight = 0.08f;
        private const float DepartureBoardBaseDepth = 0.34f;
        private const float DepartureBoardFrameLift = 1.55f;
        private const float DepartureBoardFrameWidth = 1.35f;
        private const float DepartureBoardFrameHeight = 1.05f;
        private const float DepartureBoardFrameDepth = 0.1f;
        private const float DepartureBoardScreenWidth = 1.16f;
        private const float DepartureBoardScreenHeight = 0.8f;
        private const float DepartureBoardScreenDepth = 0.035f;
        private const float DepartureBoardFaceOffset = -0.07f;
        private const int InitialBerthCapacity = 512;

        private static readonly List<VisualBerth> Berths = new List<VisualBerth>(InitialBerthCapacity);
        private static readonly List<VisualDepartureBoard> DepartureBoards = new List<VisualDepartureBoard>(InitialBerthCapacity);
        private static readonly List<StopServiceZone> ServiceZones = new List<StopServiceZone>(InitialBerthCapacity);
        private static readonly object ServiceZonesLock = new object();
        private static readonly List<PitStatusBubble> StatusBubbles = new List<PitStatusBubble>(InitialBerthCapacity);
        private static readonly List<LegacyNativePropAnchor> LegacyNativePropAnchors = new List<LegacyNativePropAnchor>(InitialBerthCapacity);
        private static readonly List<Rect> DrawnStatusBubbleRects = new List<Rect>(InitialBerthCapacity);
        private static readonly List<Rect> NormalUiOcclusionRects = new List<Rect>(128);
        private static readonly Color32 LabelBackgroundColor = new Color32(0, 0, 0, 230);
        private static readonly Color32 LabelBorderColor = new Color32(116, 255, 144, 255);

        public static StopStackerBerthOverlay Instance;
        public static bool Visible;

        private readonly List<GameObject> _managedWorldBusStopSigns = new List<GameObject>(InitialBerthCapacity);
        private readonly List<GameObject> _managedDepartureBoards = new List<GameObject>(InitialBerthCapacity);
        private readonly List<BerthSlot> _scratchBerthSlots = new List<BerthSlot>(16);
        private readonly List<ushort> _scratchCandidateSegments = new List<ushort>(32);
        private readonly Dictionary<int, Material> _worldVisualMaterials = new Dictionary<int, Material>();
        private GUIStyle _labelStyle;
        private GUIStyle _statusTitleStyle;
        private GUIStyle _statusLineStyle;
        private GUIStyle _statusMutedStyle;
        private GUIStyle _statusRightStyle;
        private GUIStyle _statusToggleStyle;
        private GameObject _worldSignRoot;
        private Material _worldSignPoleMaterial;
        private Material _worldSignPlateMaterial;
        private Material _departureBoardFrameMaterial;
        private Material _departureBoardScreenMaterial;
        private Material _departureBoardGlassMaterial;
        private Material _departureBoardHeaderMaterial;
        private Material _departureBoardRowMaterial;
        private Material _departureBoardDueMaterial;
        private float _lastRefreshTime = -100f;
        private float _lastStatusBubbleRefreshTime = -100f;
        private int _lastPropLayoutHash;
        private bool _loggedFirstRefresh;
        private bool _loggedFirstWorldSignSync;
        private int _waitingRouteErrorLogCount;
        private int _statusBubbleRefreshLogCount;
        private bool _hasObservedNetworkState;
        private bool _hasCompletedCityScan;
        private float _lastObservedNetworkChangeTime = -100f;
        private int _lastObservedStopMembershipHash;
        private int _lastObservedStopCount;
        private bool _waitingForInitialTopologyStability;
        private string _pendingFullCityScanReason = "level-load";
        private Coroutine _fullCityScanCoroutine;
        private bool _fullCityScanManaged;
        private bool _managedScanPrioritizedCamera;
        private bool _cameraPriorityRequested;
        private Coroutine _statusRefreshCoroutine;
        private Coroutine _propSyncCoroutine;
        private int _pendingPropLayoutHash;

        public static void CreateIfNeeded()
        {
            if (Instance != null)
                return;

            UIView view = UIView.GetAView();
            if (view == null)
                return;

            Instance = view.gameObject.AddComponent<StopStackerBerthOverlay>();
            Instance.ResetCityScanState("level-load");
        }

        public static void ResetForLevelLoad()
        {
            ClearRuntimeState();
            if (Instance != null)
                Instance.ResetCityScanState("level-load");
        }

        public static void ResetForLevelUnload()
        {
            ClearRuntimeState();
        }

        public static void DestroyInstance()
        {
            Visible = false;
            Berths.Clear();
            LegacyNativePropAnchors.Clear();
            DepartureBoards.Clear();
            StatusBubbles.Clear();

            if (Instance == null)
                return;

            Instance.CancelPendingFullCityScan();
            Instance.ReleaseManagedBusStopProps();
            Object.Destroy(Instance);
            Instance = null;
        }

        public static void SetVisible(bool visible)
        {
            Visible = visible;
            CreateIfNeeded();
            if (visible && Instance != null)
            {
                Instance._lastStatusBubbleRefreshTime = -100f;
                Instance.RequestCameraPriority();
            }
        }

        private void RequestCameraPriority()
        {
            if (_hasCompletedCityScan)
                return;

            _cameraPriorityRequested = true;

            if (!_fullCityScanManaged && _fullCityScanCoroutine == null)
                return;

            NetManager netManager = Singleton<NetManager>.instance;
            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (netManager == null || transportManager == null)
                return;

            StartFullCityScan(
                netManager,
                transportManager,
                "ui-camera-priority",
                0f,
                _lastObservedStopCount,
                SkippedRoadSegmentObservation);
        }

        public static void HandleVisualSettingsChanged(string reason)
        {
            if (Instance == null)
                return;

            Instance.RefreshVisualSettings(reason);
        }

        public static void HandleDisabledStopServiceSettingsChanged(string reason)
        {
            StopStackerDiagnostics.Advanced("[StopStacker] DISABLED_STOP_SERVICE_SETTINGS_CHANGED:"
                      + " reason=" + (string.IsNullOrEmpty(reason) ? "unknown" : reason)
                      + " disabledStopsDisableMultiBus="
                      + StopStackerModSettings.DisableMultiBusLoadingAtDisabledStops);

            if (Instance == null)
                return;

            Instance.RebuildAfterDisabledStopsChanged(reason);
        }

        public static void ResetAllDisabledStopsFromSettings()
        {
            int before = StopStackerDisabledStops.Count;
            bool changed = StopStackerDisabledStops.ResetAll();
            StopStackerDiagnostics.Advanced("[StopStacker] DISABLED_STOPS_RESET_REQUESTED:"
                      + " source=settings"
                      + " before=" + before
                      + " changed=" + changed);

            if (Instance != null && changed)
                Instance.RebuildAfterDisabledStopsChanged("settings-reset");
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now - _lastRefreshTime >= RefreshSeconds)
            {
                _lastRefreshTime = now;
                ObserveStopTopologyAndRefresh();
            }

            if (Visible && now - _lastStatusBubbleRefreshTime >= StatusBubbleRefreshSeconds)
            {
                _lastStatusBubbleRefreshTime = now;
                RefreshStatusBubbleCounts(Camera.main);
            }
        }

        private void OnDestroy()
        {
            CancelPendingFullCityScan();
            CancelPendingStatusBubbleRefresh();
            ReleaseManagedBusStopProps();
            ReleaseWorldVisualMaterials();

            if (Instance == this)
                Instance = null;
        }

        private void OnGUI()
        {
            if (!Visible || (Berths.Count == 0 && StatusBubbles.Count == 0))
                return;

            Event e = Event.current;
            if (e == null)
                return;

            bool repaint = e.type == EventType.Repaint;
            bool mouseDown = e.type == EventType.MouseDown && e.button == 0;
            if (!repaint && !mouseDown)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            EnsureGuiResources();

            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.depth = -2600;
            GUI.matrix = Matrix4x4.identity;

            DrawnStatusBubbleRects.Clear();
            CollectNormalUiOcclusionRects();
            AddVisibleBerthMarkerRects(camera);
            if (camera.transform.position.y <= LabelMaxCameraHeight)
            {
                for (int i = 0; i < StatusBubbles.Count; i++)
                {
                    if (DrawOrHandleStatusBubble(camera, StatusBubbles[i], repaint, mouseDown, e.mousePosition))
                    {
                        e.Use();
                        GUI.matrix = oldMatrix;
                        GUI.color = oldColor;
                        GUI.depth = oldDepth;
                        return;
                    }
                }
            }

            if (repaint)
            {
                for (int i = 0; i < Berths.Count; i++)
                    DrawBerth(camera, Berths[i]);
            }

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        private void RefreshVisualSettings(string reason)
        {
            StopStackerDiagnostics.Advanced("[StopStacker] VISUAL_STYLE_SETTINGS_CHANGED:"
                      + " reason=" + (string.IsNullOrEmpty(reason) ? "unknown" : reason)
                      + " busStopSigns=" + StopStackerModSettings.GetStyleLogValue(StopStackerModSettings.BusStopSignStyle)
                      + " dispatchBoard=" + StopStackerModSettings.GetStyleLogValue(StopStackerModSettings.DispatchBoardStyle));
            SyncBusStopProps();
        }

        private void RebuildAfterDisabledStopsChanged(string reason)
        {
            NetManager netManager = Singleton<NetManager>.instance;
            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (netManager == null || transportManager == null)
            {
                ResetCityScanState(reason);
                return;
            }

            int stopCount;
            int stopMembershipHash = CalculateStopMembershipHash(transportManager, netManager, out stopCount);

            if (stopCount == 0)
            {
                CompleteNoStopCityScan(
                    string.IsNullOrEmpty(reason) ? "disabled-stops-change" : reason,
                    Time.unscaledTime,
                    stopMembershipHash);
                _lastStatusBubbleRefreshTime = -100f;
                return;
            }

            _hasObservedNetworkState = true;
            _lastObservedStopMembershipHash = stopMembershipHash;
            _lastObservedStopCount = stopCount;
            _lastObservedNetworkChangeTime = Time.unscaledTime;
            _pendingFullCityScanReason = string.IsNullOrEmpty(reason) ? "disabled-stops-change" : reason;

            PassengerWaitPositionHarmony.ClearAssignments();
            StartFullCityScan(
                netManager,
                transportManager,
                _pendingFullCityScanReason,
                0f,
                stopCount,
                SkippedRoadSegmentObservation);
            _lastStatusBubbleRefreshTime = -100f;
        }

        private void AddVisibleBerthMarkerRects(Camera camera)
        {
            if (camera == null || camera.transform.position.y > LabelMaxCameraHeight)
                return;

            for (int i = 0; i < Berths.Count; i++)
            {
                Vector2 markerPoint;
                if (!WorldToGuiPoint(camera, Berths[i].MarkerPosition + Vector3.up * MarkerWorldLift, out markerPoint))
                    continue;

                Rect markerRect = new Rect(
                    markerPoint.x - (LabelWidth * 0.5f),
                    markerPoint.y - LabelYOffset,
                    LabelWidth,
                    LabelHeight);
                if (IsCoveredByNormalUi(markerRect))
                    continue;

                DrawnStatusBubbleRects.Add(ExpandRect(markerRect, StatusBubbleScreenPadding));
            }
        }

        private static void ClearRuntimeState()
        {
            Berths.Clear();
            LegacyNativePropAnchors.Clear();
            DepartureBoards.Clear();
            ClearServiceZones();
            StatusBubbles.Clear();
            PassengerWaitPositionHarmony.ClearAssignments();
            if (Instance != null)
            {
                Instance.ReleaseManagedBusStopProps();
                Instance.ResetCityScanState("level-load");
            }
        }

        private void ResetCityScanState(string reason)
        {
            CancelPendingFullCityScan();
            CancelPendingStatusBubbleRefresh();
            _hasObservedNetworkState = false;
            _hasCompletedCityScan = false;
            _lastObservedNetworkChangeTime = Time.unscaledTime;
            _lastObservedStopMembershipHash = 0;
            _lastObservedStopCount = 0;
            _waitingForInitialTopologyStability = false;
            _cameraPriorityRequested = false;
            _pendingFullCityScanReason = string.IsNullOrEmpty(reason) ? "level-load" : reason;
            _lastRefreshTime = -100f;
            _lastStatusBubbleRefreshTime = -100f;
            _loggedFirstRefresh = false;
            _statusBubbleRefreshLogCount = 0;
        }

        private void ObserveStopTopologyAndRefresh()
        {
            NetManager netManager = Singleton<NetManager>.instance;
            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (netManager == null || transportManager == null)
                return;

            if (netManager.m_segments == null || netManager.m_lanes == null || netManager.m_nodes == null)
                return;

            if (transportManager.m_lines == null || transportManager.m_lines.m_buffer == null)
                return;

            int stopCount;
            int stopMembershipHash = CalculateStopMembershipHash(transportManager, netManager, out stopCount);
            float now = Time.unscaledTime;

            if (stopCount == 0)
            {
                string reason = !_hasObservedNetworkState
                    ? "level-load"
                    : _lastObservedStopCount == 0
                        ? "no-visible-bus-stops"
                        : "line-stop-change";
                CompleteNoStopCityScan(reason, now, stopMembershipHash);
                return;
            }

            string scanReason = null;
            if (!_hasObservedNetworkState)
            {
                _hasObservedNetworkState = true;
                _lastObservedStopMembershipHash = stopMembershipHash;
                _lastObservedStopCount = stopCount;
                _lastObservedNetworkChangeTime = now;
                _pendingFullCityScanReason = "level-load";
                _waitingForInitialTopologyStability = true;
                if (_cameraPriorityRequested)
                {
                    _waitingForInitialTopologyStability = false;
                    scanReason = "ui-camera-priority";
                }
                else
                {
                    StopStackerDiagnostics.Advanced("[StopStacker] BERTH_INITIAL_TOPOLOGY_STABILITY_WAIT:"
                              + " observedStops=" + stopCount
                              + " stabilitySeconds=" + InitialTopologyStabilitySeconds.ToString("0.0")
                              + " source=coalesce-post-load-lane-anchor-settlement");
                    return;
                }
            }
            else if (stopMembershipHash != _lastObservedStopMembershipHash)
            {
                if (!_hasCompletedCityScan)
                {
                    CancelPendingFullCityScan();
                    _lastObservedStopMembershipHash = stopMembershipHash;
                    _lastObservedStopCount = stopCount;
                    _lastObservedNetworkChangeTime = now;
                    _waitingForInitialTopologyStability = true;
                    return;
                }

                scanReason = stopCount != _lastObservedStopCount ? "visible-stop-count-change" : "visible-stop-topology-change";
            }
            else if (_waitingForInitialTopologyStability
                     && now - _lastObservedNetworkChangeTime >= InitialTopologyStabilitySeconds)
            {
                _waitingForInitialTopologyStability = false;
                scanReason = "level-load-stable";
            }

            if (string.IsNullOrEmpty(scanReason))
                return;

            _hasObservedNetworkState = true;
            _lastObservedStopMembershipHash = stopMembershipHash;
            _lastObservedStopCount = stopCount;
            _lastObservedNetworkChangeTime = now;
            _pendingFullCityScanReason = scanReason;

            StartFullCityScan(
                netManager,
                transportManager,
                scanReason,
                0f,
                stopCount,
                SkippedRoadSegmentObservation);
        }

        private void CompleteNoStopCityScan(string reason, float now, int stopMembershipHash)
        {
            bool shouldLog = !_hasObservedNetworkState
                             || !_hasCompletedCityScan
                             || _lastObservedStopCount != 0
                             || _lastObservedStopMembershipHash != stopMembershipHash;
            if (!shouldLog)
                return;

            bool hadCompletedScan = _hasCompletedCityScan;
            CancelPendingFullCityScan();
            Berths.Clear();
            LegacyNativePropAnchors.Clear();
            DepartureBoards.Clear();
            ClearServiceZones();
            StatusBubbles.Clear();
            CancelPendingStatusBubbleRefresh();
            PassengerWaitPositionHarmony.ClearAssignments();

            _hasObservedNetworkState = true;
            _hasCompletedCityScan = true;
            _lastObservedNetworkChangeTime = now;
            _lastObservedStopMembershipHash = stopMembershipHash;
            _lastObservedStopCount = 0;
            _pendingFullCityScanReason = string.IsNullOrEmpty(reason) ? "no-visible-bus-stops" : reason;

            SyncBusStopProps();

            if (shouldLog)
            {
                StopStackerDiagnostics.Advanced("[StopStacker] BERTH_FULL_CITY_SCAN_SKIPPED:"
                          + " reason=" + _pendingFullCityScanReason
                          + " observedStops=0"
                          + " roadSegments=skipped"
                          + " settleSeconds=0.0"
                          + " hadCompletedScan=" + hadCompletedScan
                          + " processing=skip-no-visible-bus-stops"
                          + " source=stop-membership");
            }
        }

        private void StartFullCityScan(
            NetManager netManager,
            TransportManager transportManager,
            string reason,
            float settledFor,
            int observedStopCount,
            int observedRoadSegmentCount)
        {
            CancelPendingFullCityScan();
            PassengerWaitPositionHarmony.ClearAssignments();
            _hasCompletedCityScan = false;
            bool prioritizeCamera = _cameraPriorityRequested;
            _cameraPriorityRequested = false;
            IEnumerator routine = RunFullCityScanPaced(
                netManager,
                transportManager,
                reason,
                settledFor,
                observedStopCount,
                observedRoadSegmentCount,
                prioritizeCamera);
            bool startup = !string.IsNullOrEmpty(reason)
                           && reason.StartsWith("level-load");
            if (StopStackerScanCoordinator.TryQueueTopology(
                    routine,
                    startup,
                    prioritizeCamera,
                    OnManagedFullCityScanCompleted,
                    OnManagedFullCityScanFailed))
            {
                _fullCityScanManaged = true;
                _managedScanPrioritizedCamera = prioritizeCamera;
                return;
            }

            _managedScanPrioritizedCamera = false;
            _fullCityScanCoroutine = StartCoroutine(routine);
        }

        private void CancelPendingFullCityScan()
        {
            if (_fullCityScanManaged)
            {
                StopStackerScanCoordinator.CancelTopology();
                _fullCityScanManaged = false;
            }

            _managedScanPrioritizedCamera = false;
            if (_fullCityScanCoroutine != null)
            {
                StopCoroutine(_fullCityScanCoroutine);
                _fullCityScanCoroutine = null;
            }
        }

        private void OnManagedFullCityScanCompleted()
        {
            _fullCityScanManaged = false;
            _managedScanPrioritizedCamera = false;
        }

        private void OnManagedFullCityScanFailed(
            IEnumerator remaining,
            System.Exception exception)
        {
            _fullCityScanManaged = false;
            bool prioritizeCamera = _managedScanPrioritizedCamera;
            _managedScanPrioritizedCamera = false;

            System.IDisposable disposable = remaining as System.IDisposable;
            if (disposable != null)
                disposable.Dispose();

            NetManager netManager = Singleton<NetManager>.instance;
            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (netManager == null || transportManager == null)
            {
                ResetCityScanState("manager-execution-fallback");
                return;
            }

            int stopCount;
            int stopMembershipHash = CalculateStopMembershipHash(
                transportManager,
                netManager,
                out stopCount);
            if (stopCount == 0)
            {
                CompleteNoStopCityScan(
                    "manager-execution-fallback",
                    Time.unscaledTime,
                    stopMembershipHash);
                return;
            }

            _hasObservedNetworkState = true;
            _hasCompletedCityScan = false;
            _lastObservedStopMembershipHash = stopMembershipHash;
            _lastObservedStopCount = stopCount;
            _lastObservedNetworkChangeTime = Time.unscaledTime;
            _pendingFullCityScanReason = "manager-execution-fallback";
            PassengerWaitPositionHarmony.ClearAssignments();
            _fullCityScanCoroutine = StartCoroutine(
                RunFullCityScanPaced(
                    netManager,
                    transportManager,
                    _pendingFullCityScanReason,
                    0f,
                    stopCount,
                    SkippedRoadSegmentObservation,
                    prioritizeCamera));
        }

        private IEnumerator RunFullCityScanPaced(
            NetManager netManager,
            TransportManager transportManager,
            string reason,
            float settledFor,
            int observedStopCount,
            int observedRoadSegmentCount,
            bool prioritizeCamera)
        {
            Berths.Clear();
            LegacyNativePropAnchors.Clear();
            DepartureBoards.Clear();
            ClearServiceZones();
            StatusBubbles.Clear();
            CancelPendingStatusBubbleRefresh();

            int visualStopGroups = 0;
            int pitBackedStops = 0;
            int laneBackedStops = 0;
            int uniqueStops = 0;
            int duplicateBerthsSkipped = 0;
            int departureBoards = 0;
            int duplicateDepartureBoardsSkipped = 0;
            int statusRows = 0;
            int disabledStops = 0;
            int fullyDisabledStops = 0;
            int processedStops = 0;
            int pacingYields = 0;
            int stopsThisFrame = 0;
            float startedAt = Time.realtimeSinceStartup;
            HashSet<ushort> visitedStops = new HashSet<ushort>();
            HashSet<ushort> observedStopNodes = new HashSet<ushort>();
            TransportLine[] lines = transportManager.m_lines.m_buffer;
            int lineLimit = Mathf.Min(
                Mathf.Min(lines.Length, (int)transportManager.m_lines.m_size),
                ushort.MaxValue + 1);
            List<ushort> lineOrder = new List<ushort>(lineLimit);
            Dictionary<ushort, ushort> cameraPriorityStops = new Dictionary<ushort, ushort>();
            if (prioritizeCamera)
            {
                IEnumerator cameraDiscovery = DiscoverCameraPriorityLinesPaced(
                    netManager,
                    transportManager,
                    cameraPriorityStops);
                while (cameraDiscovery.MoveNext())
                    yield return null;
            }

            for (int lineIndex = 1; lineIndex < lineLimit; lineIndex++)
            {
                ushort candidateLineId = (ushort)lineIndex;
                if (cameraPriorityStops.ContainsKey(candidateLineId)
                    && IsVisibleBusLine(ref lines[candidateLineId]))
                {
                    lineOrder.Add(candidateLineId);
                }
            }

            for (int lineIndex = 1; lineIndex < lineLimit; lineIndex++)
            {
                ushort candidateLineId = (ushort)lineIndex;
                if (!cameraPriorityStops.ContainsKey(candidateLineId)
                    && IsVisibleBusLine(ref lines[candidateLineId]))
                {
                    lineOrder.Add(candidateLineId);
                }
            }

            for (int lineOrderIndex = 0; lineOrderIndex < lineOrder.Count; lineOrderIndex++)
            {
                ushort lineId = lineOrder[lineOrderIndex];
                TransportLine line = lines[lineId];

                ushort canonicalFirstStop = line.m_stops;
                if (canonicalFirstStop == 0)
                    continue;

                float visualBusLength = GetRepresentativeBusLength(lineId);
                int totalLineStops = CountLineStops(canonicalFirstStop);
                ushort traversalFirstStop = canonicalFirstStop;
                int routeStopNumber = 1;
                ushort cameraStop;
                if (cameraPriorityStops.TryGetValue(lineId, out cameraStop))
                {
                    int cameraRouteStopNumber = FindRouteStopNumber(canonicalFirstStop, cameraStop);
                    if (cameraRouteStopNumber > 0)
                    {
                        traversalFirstStop = cameraStop;
                        routeStopNumber = cameraRouteStopNumber;
                    }
                }

                ushort currentStop = traversalFirstStop;
                int safety = 0;
                do
                {
                    if (currentStop == 0)
                        break;

                    observedStopNodes.Add(currentStop);
                    ushort nextStop = TransportLine.GetNextStop(currentStop);
                    processedStops++;
                    stopsThisFrame++;
                    if (stopsThisFrame >= FullCityTopologyStopsPerFrame)
                    {
                        stopsThisFrame = 0;
                        pacingYields++;
                        yield return null;
                    }

                    StopGeometry stopGeometry;
                    bool stopDisabled = StopStackerDisabledStops.IsDisabled(currentStop);
                    bool stopServiceDisabled = IsStopServiceDisabledBySettings(currentStop);
                    bool hasStopGeometry = TryGetStopGeometryForStopNode(netManager, currentStop, visualBusLength, out stopGeometry);
                    if (hasStopGeometry)
                    {
                        CollectBerthSlotsForStopGeometry(stopGeometry, _scratchBerthSlots);
                        AddLegacyNativePropAnchors(_scratchBerthSlots, stopDisabled);
                        if (!stopServiceDisabled)
                            RegisterServiceZone(lineId, currentStop, nextStop, stopGeometry, _scratchBerthSlots);

                        if (stopDisabled)
                        {
                            GetOrCreateDisabledStatusBubble(currentStop, stopGeometry.FirstBerthPosition);
                        }
                        else
                        {
                            AddStatusRow(transportManager, lineId, ref line, currentStop, nextStop, routeStopNumber, totalLineStops, stopGeometry.FirstBerthPosition, _scratchBerthSlots);
                            statusRows++;
                        }
                    }

                    if (visitedStops.Add(currentStop))
                    {
                        if (hasStopGeometry)
                        {
                            uniqueStops++;
                            if (stopGeometry.HasPitOffset)
                                pitBackedStops++;
                            else
                                laneBackedStops++;

                            if (stopDisabled)
                            {
                                disabledStops++;
                                if (stopServiceDisabled)
                                    fullyDisabledStops++;

                                currentStop = nextStop;
                                routeStopNumber++;
                                if (routeStopNumber > totalLineStops)
                                    routeStopNumber = 1;
                                safety++;
                                continue;
                            }

                            if (TryAddDepartureBoardForStopGeometry(stopGeometry))
                                departureBoards++;
                            else
                                duplicateDepartureBoardsSkipped++;

                            if (TryAddBerthsForStopGeometry(_scratchBerthSlots, ref duplicateBerthsSkipped))
                                visualStopGroups++;
                        }
                    }

                    currentStop = nextStop;
                    routeStopNumber++;
                    if (routeStopNumber > totalLineStops)
                        routeStopNumber = 1;
                    safety++;
                }
                while (currentStop != 0 && currentStop != traversalFirstStop && safety < MaxTransportChainIterations);
            }

            int prunedDisabledStops = StopStackerDisabledStops.PruneToKnownStops(observedStopNodes);
            int disabledBubbles = CountDisabledStatusBubbles();
            if (prunedDisabledStops > 0)
            {
                StopStackerDiagnostics.Advanced("[StopStacker] DISABLED_STOPS_PRUNED:"
                          + " removed=" + prunedDisabledStops
                          + " remaining=" + StopStackerDisabledStops.Count);
            }

            if (!_loggedFirstRefresh || !string.IsNullOrEmpty(reason))
            {
                _loggedFirstRefresh = true;
                StopStackerDiagnostics.Advanced("[StopStacker] BERTH_FULL_CITY_SCAN_COMPLETED:"
                          + " reason=" + reason
                          + " settledFor=" + settledFor.ToString("0.0")
                          + " observedStops=" + observedStopCount
                          + " observedRoadSegments=" + FormatObservedRoadSegments(observedRoadSegmentCount)
                          + " pits=" + pitBackedStops
                          + " laneBackedStops=" + laneBackedStops
                          + " stopGroups=" + visualStopGroups
                          + " serviceZones=" + GetServiceZoneCount()
                          + " berths=" + Berths.Count
                          + " departureBoards=" + departureBoards
                          + " uniqueStops=" + uniqueStops
                          + " duplicateBerthsSkipped=" + duplicateBerthsSkipped
                          + " duplicateDepartureBoardsSkipped=" + duplicateDepartureBoardsSkipped
                          + " statusBubbles=" + StatusBubbles.Count
                          + " disabledStops=" + disabledStops
                          + " fullyDisabledStops=" + fullyDisabledStops
                          + " disabledBubbles=" + disabledBubbles
                          + " prunedDisabledStops=" + prunedDisabledStops
                          + " statusRows=" + statusRows
                          + " processedStops=" + processedStops
                          + " stopsPerFrame=" + FullCityTopologyStopsPerFrame
                          + " pacingYields=" + pacingYields
                          + " elapsedSeconds=" + (Time.realtimeSinceStartup - startedAt).ToString("0.0")
                          + " statusWaiting=deferred"
                          + " waitAssignments=deferred"
                          + " spacing=" + BerthSpacing.ToString("0.0")
                          + " endClearance=" + MinimumEndClearance.ToString("0.0")
                          + " stopLaneSearchRadius=" + StopLaneSearchRadius.ToString("0.0")
                          + " processing=visible-stop-topology"
                          + " source=visible-bus-stop-scan"
                          + " roadNetworkHash=skipped"
                          + " passengerAssignments=paced-refresh-pending");
            }

            _hasCompletedCityScan = true;
            _fullCityScanCoroutine = null;
            ScheduleFullStatusBubbleCountRefresh(reason);
            SyncBusStopProps();
        }

        private static IEnumerator DiscoverCameraPriorityLinesPaced(
            NetManager netManager,
            TransportManager transportManager,
            Dictionary<ushort, ushort> priorityStops)
        {
            Camera camera = Camera.main;
            if (camera == null
                || netManager == null
                || netManager.m_nodeGrid == null
                || netManager.m_nodes == null
                || netManager.m_nodes.m_buffer == null
                || transportManager == null
                || transportManager.m_lines == null
                || transportManager.m_lines.m_buffer == null)
            {
                yield break;
            }

            float minWorldX;
            float minWorldZ;
            float maxWorldX;
            float maxWorldZ;
            GetCameraPriorityGroundBounds(
                camera,
                out minWorldX,
                out minWorldZ,
                out maxWorldX,
                out maxWorldZ);

            int minGridX = Mathf.Clamp(
                (int)(minWorldX / NetSegmentGridCellSize + NetSegmentGridHalfResolution),
                0,
                NetSegmentGridResolution - 1);
            int minGridZ = Mathf.Clamp(
                (int)(minWorldZ / NetSegmentGridCellSize + NetSegmentGridHalfResolution),
                0,
                NetSegmentGridResolution - 1);
            int maxGridX = Mathf.Clamp(
                (int)(maxWorldX / NetSegmentGridCellSize + NetSegmentGridHalfResolution),
                0,
                NetSegmentGridResolution - 1);
            int maxGridZ = Mathf.Clamp(
                (int)(maxWorldZ / NetSegmentGridCellSize + NetSegmentGridHalfResolution),
                0,
                NetSegmentGridResolution - 1);

            NetNode[] nodes = netManager.m_nodes.m_buffer;
            TransportLine[] lines = transportManager.m_lines.m_buffer;
            int lineLimit = Mathf.Min(
                Mathf.Min(lines.Length, (int)transportManager.m_lines.m_size),
                ushort.MaxValue + 1);
            int inspectedThisFrame = 0;
            for (int gridZ = minGridZ; gridZ <= maxGridZ; gridZ++)
            {
                for (int gridX = minGridX; gridX <= maxGridX; gridX++)
                {
                    int gridIndex = (gridZ * NetSegmentGridResolution) + gridX;
                    ushort nodeId = netManager.m_nodeGrid[gridIndex];
                    int safety = 0;
                    while (nodeId != 0
                           && nodeId < nodes.Length
                           && safety < MaxTransportChainIterations)
                    {
                        NetNode node = nodes[nodeId];
                        ushort nextNode = node.m_nextGridNode;
                        ushort lineId = node.m_transportLine;
                        if ((node.m_flags & NetNode.Flags.Created) != 0
                            && lineId != 0
                            && lineId < lineLimit
                            && IsVisibleBusLine(ref lines[lineId]))
                        {
                            Vector3 viewport = camera.WorldToViewportPoint(node.m_position);
                            if (viewport.z > 0f
                                && viewport.x >= -CameraPriorityViewportPadding
                                && viewport.x <= 1f + CameraPriorityViewportPadding
                                && viewport.y >= -CameraPriorityViewportPadding
                                && viewport.y <= 1f + CameraPriorityViewportPadding)
                            {
                                if (!priorityStops.ContainsKey(lineId))
                                    priorityStops.Add(lineId, nodeId);
                            }
                        }

                        nodeId = nextNode;
                        safety++;
                        inspectedThisFrame++;
                        if (inspectedThisFrame >= CameraPriorityNodeInspectionsPerFrame)
                        {
                            inspectedThisFrame = 0;
                            yield return null;
                        }
                    }
                }
            }

            StopStackerDiagnostics.Advanced("[StopStacker] BERTH_CAMERA_PRIORITY_DISCOVERY_COMPLETED:"
                      + " priorityLines=" + priorityStops.Count
                      + " nodeInspectionsPerFrame=" + CameraPriorityNodeInspectionsPerFrame
                      + " gridBounds=" + minGridX + "," + minGridZ + "-"
                      + maxGridX + "," + maxGridZ);
        }

        private static void GetCameraPriorityGroundBounds(
            Camera camera,
            out float minX,
            out float minZ,
            out float maxX,
            out float maxZ)
        {
            minX = float.MaxValue;
            minZ = float.MaxValue;
            maxX = float.MinValue;
            maxZ = float.MinValue;
            int hits = 0;
            Vector3[] viewportCorners =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            for (int i = 0; i < viewportCorners.Length; i++)
            {
                Ray ray = camera.ViewportPointToRay(viewportCorners[i]);
                float distance;
                if (!ground.Raycast(ray, out distance) || distance < 0f)
                    continue;

                Vector3 point = ray.GetPoint(distance);
                minX = Mathf.Min(minX, point.x);
                minZ = Mathf.Min(minZ, point.z);
                maxX = Mathf.Max(maxX, point.x);
                maxZ = Mathf.Max(maxZ, point.z);
                hits++;
            }

            if (hits == 0)
            {
                Vector3 position = camera.transform.position;
                float radius = Mathf.Max(512f, Mathf.Abs(position.y) * 2.5f);
                minX = position.x - radius;
                minZ = position.z - radius;
                maxX = position.x + radius;
                maxZ = position.z + radius;
            }

            minX -= CameraPriorityWorldPadding;
            minZ -= CameraPriorityWorldPadding;
            maxX += CameraPriorityWorldPadding;
            maxZ += CameraPriorityWorldPadding;
        }

        private static string FormatObservedRoadSegments(int observedRoadSegmentCount)
        {
            return observedRoadSegmentCount < 0 ? "skipped" : observedRoadSegmentCount.ToString();
        }

        private void ScheduleFullStatusBubbleCountRefresh(string reason)
        {
            CancelPendingStatusBubbleRefresh();
            if (StatusBubbles.Count == 0)
                return;

            if (string.IsNullOrEmpty(reason) || reason.StartsWith("level-load"))
            {
                _lastStatusBubbleRefreshTime = -100f;
                if (_statusBubbleRefreshLogCount < StatusBubbleRefreshLogLimit)
                {
                    _statusBubbleRefreshLogCount++;
                    StopStackerDiagnostics.Advanced("[StopStacker] STATUS_BUBBLE_COUNTS_REFRESH_DEFERRED:"
                              + " bubbles=" + StatusBubbles.Count
                              + " rows=" + CountStatusBubbleRows()
                              + " reason=" + (string.IsNullOrEmpty(reason) ? "unknown" : reason)
                              + " source=load-stand-down"
                              + " passengerAssignments=visible-only");
                }

                return;
            }

            _statusRefreshCoroutine = StartCoroutine(RefreshAllStatusBubbleCountsPaced(string.IsNullOrEmpty(reason) ? "unknown" : reason));
        }

        private static int CountStatusBubbleRows()
        {
            int rows = 0;
            for (int i = 0; i < StatusBubbles.Count; i++)
            {
                PitStatusBubble bubble = StatusBubbles[i];
                if (bubble != null)
                    rows += bubble.Lines.Count;
            }

            return rows;
        }

        private void CancelPendingStatusBubbleRefresh()
        {
            if (_statusRefreshCoroutine == null)
                return;

            StopCoroutine(_statusRefreshCoroutine);
            _statusRefreshCoroutine = null;
        }

        private IEnumerator RefreshAllStatusBubbleCountsPaced(string reason)
        {
            int rowCount = 0;
            int changedRows = 0;
            int totalWaiting = 0;
            int assignedPassengers = 0;
            int passengerPaceYields = 0;
            float startedAt = Time.realtimeSinceStartup;
            PassengerRefreshRateLimiter passengerRateLimiter = new PassengerRefreshRateLimiter(
                FullStatusPassengerRatePerSecond,
                FullStatusPassengerBurst);
            PassengerRefreshWorkLimiter workLimiter = new PassengerRefreshWorkLimiter(
                FullStatusGridCellsPerFrame,
                FullStatusCitizenInspectionsPerFrame);

            PassengerWaitPositionHarmony.BeginRefresh();
            for (int bubbleIndex = 0; bubbleIndex < StatusBubbles.Count; bubbleIndex++)
            {
                PitStatusBubble bubble = StatusBubbles[bubbleIndex];
                if (bubble == null)
                    continue;

                bubble.TotalWaiting = 0;
                for (int lineIndex = 0; lineIndex < bubble.Lines.Count; lineIndex++)
                    bubble.Lines[lineIndex].SetWaiting(0);
            }

            int rowsThisFrame = 0;
            for (int bubbleIndex = 0; bubbleIndex < StatusBubbles.Count; bubbleIndex++)
            {
                PitStatusBubble bubble = StatusBubbles[bubbleIndex];
                if (bubble == null)
                    continue;

                for (int lineIndex = 0; lineIndex < bubble.Lines.Count; lineIndex++)
                {
                    PitStatusLine line = bubble.Lines[lineIndex];
                    PacedWaitingScanResult scanResult = new PacedWaitingScanResult();
                    IEnumerator scan = CountWaitingPassengersForServicePaced(
                        line.LineId,
                        line.StopNode,
                        line.NextStop,
                        line.AssignedBerthNumber,
                        line.AssignedWaitingPosition,
                        passengerRateLimiter,
                        workLimiter,
                        scanResult);
                    while (scan.MoveNext())
                    {
                        passengerPaceYields++;
                        yield return scan.Current;
                    }

                    assignedPassengers += scanResult.AssignedCount;
                    if (line.SetWaiting(scanResult.WaitingCount))
                        changedRows++;

                    bubble.TotalWaiting += line.WaitingPassengers;
                    totalWaiting += line.WaitingPassengers;
                    rowCount++;
                    rowsThisFrame++;
                    if (rowsThisFrame >= FullStatusRowsPerFrame)
                    {
                        rowsThisFrame = 0;
                        workLimiter.ResetFrame();
                        yield return null;
                    }
                }
            }

            if (_statusBubbleRefreshLogCount < StatusBubbleRefreshLogLimit)
            {
                _statusBubbleRefreshLogCount++;
                StopStackerDiagnostics.Advanced("[StopStacker] STATUS_BUBBLE_COUNTS_REFRESHED_PACED:"
                          + " bubbles=" + StatusBubbles.Count
                          + " visibleBubbles=all"
                          + " skippedBubbles=0"
                          + " rows=" + rowCount
                          + " changedRows=" + changedRows
                          + " totalWaiting=" + totalWaiting
                          + " waitAssignments=" + assignedPassengers
                          + " rowsPerFrame=" + FullStatusRowsPerFrame
                          + " cimsPerSecondTarget=" + FullStatusPassengerRatePerSecond.ToString("0")
                          + " initialBurst=" + FullStatusPassengerBurst.ToString("0")
                          + " paceYields=" + passengerPaceYields
                          + " gridCells=" + workLimiter.TotalGridCells
                          + " gridCellYields=" + workLimiter.GridCellYields
                          + " citizenInspections=" + workLimiter.TotalCitizenInspections
                          + " citizenInspectionYields=" + workLimiter.CitizenInspectionYields
                          + " gridCellsPerFrame=" + FullStatusGridCellsPerFrame
                          + " citizenInspectionsPerFrame=" + FullStatusCitizenInspectionsPerFrame
                          + " elapsedSeconds=" + (Time.realtimeSinceStartup - startedAt).ToString("0.0")
                          + " source=full-city-paced-refresh"
                          + " reason=" + reason);
            }

            _statusRefreshCoroutine = null;
        }

        private void RefreshStatusBubbleCounts(Camera camera)
        {
            if (!_hasCompletedCityScan || StatusBubbles.Count == 0 || _statusRefreshCoroutine != null)
                return;

            if (camera == null || camera.transform.position.y > LabelMaxCameraHeight)
                return;

            int rowCount = 0;
            int changedRows = 0;
            int totalWaiting = 0;
            int assignedPassengers = 0;
            int visibleBubbles = 0;
            int skippedBubbles = 0;
            bool beganAssignmentRefresh = false;
            for (int bubbleIndex = 0; bubbleIndex < StatusBubbles.Count; bubbleIndex++)
            {
                PitStatusBubble bubble = StatusBubbles[bubbleIndex];
                if (bubble == null)
                    continue;

                if (!IsStatusBubbleVisibleForRefresh(camera, bubble))
                {
                    skippedBubbles++;
                    continue;
                }

                visibleBubbles++;
                if (!beganAssignmentRefresh)
                {
                    PassengerWaitPositionHarmony.BeginRefresh();
                    beganAssignmentRefresh = true;
                }

                int bubbleWaiting = 0;
                for (int lineIndex = 0; lineIndex < bubble.Lines.Count; lineIndex++)
                {
                    PitStatusLine line = bubble.Lines[lineIndex];
                    int assigned;
                    int waiting = CountWaitingPassengersForService(
                        line.LineId,
                        line.StopNode,
                        line.NextStop,
                        line.AssignedBerthNumber,
                        line.AssignedWaitingPosition,
                        true,
                        out assigned);

                    assignedPassengers += assigned;
                    if (line.SetWaiting(waiting))
                        changedRows++;

                    bubbleWaiting += line.WaitingPassengers;
                    rowCount++;
                }

                bubble.TotalWaiting = bubbleWaiting;
                totalWaiting += bubbleWaiting;
            }

            if (visibleBubbles == 0)
                return;

            if (_statusBubbleRefreshLogCount < StatusBubbleRefreshLogLimit && (changedRows > 0 || _statusBubbleRefreshLogCount < 3))
            {
                _statusBubbleRefreshLogCount++;
                StopStackerDiagnostics.Advanced("[StopStacker] STATUS_BUBBLE_COUNTS_REFRESHED:"
                          + " bubbles=" + StatusBubbles.Count
                          + " visibleBubbles=" + visibleBubbles
                          + " skippedBubbles=" + skippedBubbles
                          + " rows=" + rowCount
                          + " changedRows=" + changedRows
                          + " totalWaiting=" + totalWaiting
                          + " waitAssignments=" + assignedPassengers
                          + " interval=" + StatusBubbleRefreshSeconds.ToString("0.0")
                          + " source=dynamic-visual-refresh"
                          + " passengerAssignments=visible-only");
            }
        }

        private static bool IsStatusBubbleVisibleForRefresh(Camera camera, PitStatusBubble bubble)
        {
            if (camera == null || bubble == null)
                return false;

            Vector3 viewportPoint = camera.WorldToViewportPoint(bubble.AnchorPosition + Vector3.up * StatusBubbleWorldLift);
            if (viewportPoint.z <= 0f)
                return false;

            return viewportPoint.x >= -StatusBubbleRefreshViewportPadding
                   && viewportPoint.x <= 1f + StatusBubbleRefreshViewportPadding
                   && viewportPoint.y >= -StatusBubbleRefreshViewportPadding
                   && viewportPoint.y <= 1f + StatusBubbleRefreshViewportPadding;
        }

        internal static bool TryGetServiceZone(ushort lineId, ushort stopNode, out StopServiceZone zone)
        {
            lock (ServiceZonesLock)
            {
                for (int i = 0; i < ServiceZones.Count; i++)
                {
                    StopServiceZone candidate = ServiceZones[i];
                    if (candidate.LineId == lineId
                        && candidate.StopNode == stopNode
                        && !IsStopServiceDisabledBySettings(candidate.StopNode))
                    {
                        zone = candidate;
                        return true;
                    }
                }

                for (int i = 0; i < ServiceZones.Count; i++)
                {
                    StopServiceZone candidate = ServiceZones[i];
                    if (candidate.StopNode == stopNode
                        && !IsStopServiceDisabledBySettings(candidate.StopNode))
                    {
                        zone = candidate;
                        return true;
                    }
                }
            }

            zone = default(StopServiceZone);
            return false;
        }

        private static bool IsStopServiceDisabledBySettings(ushort stopNode)
        {
            return stopNode != 0
                   && StopStackerModSettings.DisableMultiBusLoadingAtDisabledStops
                   && StopStackerDisabledStops.IsDisabled(stopNode);
        }

        internal static bool TryGetBusProgressInServiceZone(
            StopServiceZone zone,
            Vector3 busPosition,
            out float progress,
            out float lateralDistance)
        {
            progress = 0f;
            lateralDistance = 0f;

            NetManager netManager = Singleton<NetManager>.instance;
            if (netManager == null
                || netManager.m_lanes == null
                || netManager.m_lanes.m_buffer == null
                || zone.LaneId == 0
                || zone.LaneId >= netManager.m_lanes.m_buffer.Length)
            {
                return false;
            }

            NetLane lane = netManager.m_lanes.m_buffer[(int)zone.LaneId];
            if (lane.m_length < 1f)
                return false;

            Vector3 lanePosition;
            float laneOffset;
            lane.GetClosestPosition(busPosition, out lanePosition, out laneOffset);

            progress = GetDistanceBehindFirstBerth(zone.ReverseLane, lane.m_length, zone.FirstBerthLaneOffset, laneOffset);
            lateralDistance = Mathf.Sqrt(SqrDistanceXZ(busPosition, lanePosition));
            return true;
        }

        private static bool IsVisibleBusLine(ref TransportLine line)
        {
            if ((line.m_flags & TransportLine.Flags.Created) == 0)
                return false;

            if ((line.m_flags & (TransportLine.Flags.Temporary | TransportLine.Flags.Hidden)) != 0)
                return false;

            TransportInfo info = line.Info;
            return info != null && info.m_transportType == TransportInfo.TransportType.Bus;
        }

        private static int CountLineStops(ushort firstStop)
        {
            if (firstStop == 0)
                return 0;

            int count = 0;
            ushort currentStop = firstStop;
            int safety = 0;
            do
            {
                count++;
                currentStop = TransportLine.GetNextStop(currentStop);
                safety++;
            }
            while (currentStop != 0 && currentStop != firstStop && safety < MaxTransportChainIterations);

            return count;
        }

        private static int FindRouteStopNumber(ushort firstStop, ushort targetStop)
        {
            if (firstStop == 0 || targetStop == 0)
                return 0;

            int routeStopNumber = 1;
            ushort currentStop = firstStop;
            int safety = 0;
            do
            {
                if (currentStop == targetStop)
                    return routeStopNumber;

                currentStop = TransportLine.GetNextStop(currentStop);
                routeStopNumber++;
                safety++;
            }
            while (currentStop != 0 && currentStop != firstStop && safety < MaxTransportChainIterations);

            return 0;
        }

        private static int CalculateStopMembershipHash(TransportManager transportManager, NetManager netManager, out int stopCount)
        {
            stopCount = 0;
            if (transportManager == null || transportManager.m_lines == null || transportManager.m_lines.m_buffer == null)
                return 0;

            unchecked
            {
                int hash = 17;
                TransportLine[] lines = transportManager.m_lines.m_buffer;
                int lineLimit = Mathf.Min(
                    Mathf.Min(lines.Length, (int)transportManager.m_lines.m_size),
                    ushort.MaxValue + 1);
                for (int lineIndex = 1; lineIndex < lineLimit; lineIndex++)
                {
                    ushort lineId = (ushort)lineIndex;
                    TransportLine line = lines[lineId];
                    if (!IsVisibleBusLine(ref line))
                        continue;

                    ushort firstStop = line.m_stops;
                    if (firstStop == 0)
                        continue;

                    hash = (hash * 31) + lineId;
                    hash = (hash * 31) + line.m_lineNumber;
                    ushort currentStop = firstStop;
                    int safety = 0;
                    do
                    {
                        if (currentStop == 0)
                            break;

                        stopCount++;
                        hash = (hash * 31) + currentStop;
                        hash = AddStopNodeTopologyToHash(hash, netManager, currentStop);
                        currentStop = TransportLine.GetNextStop(currentStop);
                        safety++;
                    }
                    while (currentStop != 0 && currentStop != firstStop && safety < MaxTransportChainIterations);
                }

                hash = (hash * 31) + stopCount;
                return hash;
            }
        }

        private static int AddStopNodeTopologyToHash(int hash, NetManager netManager, ushort nodeId)
        {
            if (netManager == null
                || netManager.m_nodes == null
                || netManager.m_nodes.m_buffer == null
                || nodeId == 0
                || nodeId >= netManager.m_nodes.m_buffer.Length)
            {
                return (hash * 31) + nodeId;
            }

            unchecked
            {
                NetNode node = netManager.m_nodes.m_buffer[nodeId];
                hash = (hash * 31) + (int)(node.m_flags & NetNode.Flags.Created);
                hash = (hash * 31) + Mathf.RoundToInt(node.m_position.x * 10f);
                hash = (hash * 31) + Mathf.RoundToInt(node.m_position.y * 10f);
                hash = (hash * 31) + Mathf.RoundToInt(node.m_position.z * 10f);
                hash = (hash * 31) + unchecked((int)node.m_lane);
                hash = (hash * 31) + node.m_laneOffset;
                return hash;
            }
        }

        private bool TryGetStopGeometryForStopNode(NetManager netManager, ushort stopNodeId, float visualBusLength, out StopGeometry stopGeometry)
        {
            stopGeometry = default(StopGeometry);

            if (stopNodeId == 0 || stopNodeId >= netManager.m_nodes.m_buffer.Length)
                return false;

            NetNode node = netManager.m_nodes.m_buffer[stopNodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return false;

            ushort segmentId;
            NetSegment segment;
            uint laneId;
            NetLane lane;
            NetInfo.Lane laneInfo;
            float pitStartLaneOffset;
            if (!TryFindStopLane(
                    netManager,
                    stopNodeId,
                    node.m_position,
                    out segmentId,
                    out segment,
                    out laneId,
                    out lane,
                    out laneInfo,
                    out pitStartLaneOffset))
            {
                return false;
            }

            if (laneInfo == null || lane.m_length < 1f || !IsCandidateStopLane(laneInfo))
                return false;

            float firstBerthLaneOffset;
            if (!BusStopPositionHarmony.TryGetForwardStopLaneOffset(
                    lane.m_length,
                    visualBusLength,
                    laneInfo,
                    segment.m_flags,
                    pitStartLaneOffset,
                    out firstBerthLaneOffset))
            {
                return false;
            }

            bool reverseLane = IsReverseLane(laneInfo, segment.m_flags);
            bool hasPitOffset = HasPitStopOffset(laneInfo);
            float stopOffset = laneInfo.m_stopOffset;
            if ((segment.m_flags & NetSegment.Flags.Invert) != 0)
                stopOffset = -stopOffset;
            float propSideOffset = GetPropSideOffset(laneInfo, stopOffset);

            Vector3 firstBerthPosition;
            Vector3 firstBerthDirection;
            lane.CalculateStopPositionAndDirection(Mathf.Clamp01(firstBerthLaneOffset), stopOffset, out firstBerthPosition, out firstBerthDirection);
            Vector3 vanillaStopPosition;
            Vector3 vanillaStopDirection;
            lane.CalculateStopPositionAndDirection(Mathf.Clamp01(pitStartLaneOffset), stopOffset, out vanillaStopPosition, out vanillaStopDirection);
            propSideOffset = ResolvePavementSideReference(
                netManager,
                ref segment,
                Mathf.Clamp01(pitStartLaneOffset),
                vanillaStopPosition,
                vanillaStopDirection,
                propSideOffset);
            Vector3 departureBoardSide = GetPavementSide(vanillaStopDirection, propSideOffset);
            Vector3 departureBoardPosition = vanillaStopPosition + (departureBoardSide * DepartureBoardSideOffset);

            stopGeometry = new StopGeometry(
                segmentId,
                laneId,
                lane,
                laneInfo,
                reverseLane,
                stopOffset,
                propSideOffset,
                hasPitOffset,
                Mathf.Clamp01(firstBerthLaneOffset),
                firstBerthPosition,
                departureBoardPosition,
                GetPropAngle(vanillaStopDirection));
            return true;
        }

        private bool TryFindStopLane(
            NetManager netManager,
            ushort stopNodeId,
            Vector3 stopPosition,
            out ushort bestSegmentId,
            out NetSegment bestSegment,
            out uint bestLaneId,
            out NetLane bestLane,
            out NetInfo.Lane bestLaneInfo,
            out float bestLaneOffset)
        {
            StopAnchorResolution anchorResolution = ResolveStopAnchorLane(
                netManager,
                stopNodeId,
                out bestSegmentId,
                out bestSegment,
                out bestLaneId,
                out bestLane,
                out bestLaneInfo,
                out bestLaneOffset);
            if (anchorResolution == StopAnchorResolution.SupportedRoad)
                return true;

            if (anchorResolution == StopAnchorResolution.UnsupportedNative)
                return false;

            return TryFindNearestStopLane(
                netManager,
                stopPosition,
                out bestSegmentId,
                out bestSegment,
                out bestLaneId,
                out bestLane,
                out bestLaneInfo,
                out bestLaneOffset);
        }

        internal static bool HasUnsupportedNativeStopAnchor(NetManager netManager, ushort stopNodeId)
        {
            ushort segmentId;
            NetSegment segment;
            uint laneId;
            NetLane lane;
            NetInfo.Lane laneInfo;
            float laneOffset;
            return ResolveStopAnchorLane(
                       netManager,
                       stopNodeId,
                       out segmentId,
                       out segment,
                       out laneId,
                       out lane,
                       out laneInfo,
                       out laneOffset)
                   == StopAnchorResolution.UnsupportedNative;
        }

        private static StopAnchorResolution ResolveStopAnchorLane(
            NetManager netManager,
            ushort stopNodeId,
            out ushort segmentId,
            out NetSegment segment,
            out uint laneId,
            out NetLane lane,
            out NetInfo.Lane laneInfo,
            out float laneOffset)
        {
            segmentId = 0;
            segment = default(NetSegment);
            laneId = 0;
            lane = default(NetLane);
            laneInfo = null;
            laneOffset = 0f;

            if (netManager == null
                || stopNodeId == 0
                || netManager.m_nodes == null
                || netManager.m_nodes.m_buffer == null
                || netManager.m_lanes == null
                || netManager.m_lanes.m_buffer == null
                || netManager.m_segments == null
                || netManager.m_segments.m_buffer == null
                || stopNodeId >= netManager.m_nodes.m_buffer.Length)
            {
                return StopAnchorResolution.Missing;
            }

            NetNode node = netManager.m_nodes.m_buffer[stopNodeId];
            if ((node.m_flags & NetNode.Flags.Created) == 0 || node.m_lane == 0)
                return StopAnchorResolution.Missing;

            if (node.m_lane >= netManager.m_lanes.m_buffer.Length)
                return StopAnchorResolution.Missing;

            NetLane anchoredLane = netManager.m_lanes.m_buffer[(int)node.m_lane];
            ushort anchoredSegmentId = anchoredLane.m_segment;
            if (anchoredSegmentId == 0 || anchoredSegmentId >= netManager.m_segments.m_buffer.Length)
                return StopAnchorResolution.Missing;

            NetSegment anchoredSegment = netManager.m_segments.m_buffer[anchoredSegmentId];
            if ((anchoredSegment.m_flags & NetSegment.Flags.Created) == 0
                || (anchoredSegment.m_flags & NetSegment.Flags.Collapsed) != 0)
            {
                return StopAnchorResolution.Missing;
            }

            // A live native anchor is authoritative. In particular, building paths are
            // untouchable segments and must never fall through to an unrelated nearby road.
            if ((anchoredSegment.m_flags & NetSegment.Flags.Untouchable) != 0)
                return StopAnchorResolution.UnsupportedNative;

            NetInfo info = anchoredSegment.Info;
            if (info == null || info.m_lanes == null || !(info.m_netAI is RoadBaseAI))
                return StopAnchorResolution.UnsupportedNative;

            NetLane[] lanes = netManager.m_lanes.m_buffer;
            uint currentLaneId = anchoredSegment.m_lanes;
            for (int laneIndex = 0; laneIndex < info.m_lanes.Length && currentLaneId != 0; laneIndex++)
            {
                if (currentLaneId >= lanes.Length)
                    break;

                NetInfo.Lane currentLaneInfo = info.m_lanes[laneIndex];
                NetLane currentLane = lanes[(int)currentLaneId];
                uint nextLaneId = currentLane.m_nextLane;
                if (currentLaneId == node.m_lane
                    && IsCandidateStopLane(currentLaneInfo)
                    && currentLane.m_length >= 1f)
                {
                    segmentId = anchoredSegmentId;
                    segment = anchoredSegment;
                    laneId = currentLaneId;
                    lane = currentLane;
                    laneInfo = currentLaneInfo;
                    laneOffset = NormalizeLaneOffset(node.m_laneOffset);
                    return StopAnchorResolution.SupportedRoad;
                }

                currentLaneId = nextLaneId;
            }

            return StopAnchorResolution.UnsupportedNative;
        }

        private bool TryFindNearestStopLane(
            NetManager netManager,
            Vector3 stopPosition,
            out ushort bestSegmentId,
            out NetSegment bestSegment,
            out uint bestLaneId,
            out NetLane bestLane,
            out NetInfo.Lane bestLaneInfo,
            out float bestLaneOffset)
        {
            bestSegmentId = 0;
            bestSegment = default(NetSegment);
            bestLaneId = 0;
            bestLane = default(NetLane);
            bestLaneInfo = null;
            bestLaneOffset = 0f;

            float bestSqr = StopLaneSearchRadius * StopLaneSearchRadius;
            NetSegment[] segments = netManager.m_segments.m_buffer;
            NetLane[] lanes = netManager.m_lanes.m_buffer;
            _scratchCandidateSegments.Clear();
            AddSegmentGridCandidates(netManager, stopPosition, StopLaneSearchRadius, _scratchCandidateSegments);

            for (int candidateIndex = 0; candidateIndex < _scratchCandidateSegments.Count; candidateIndex++)
            {
                ushort segmentId = _scratchCandidateSegments[candidateIndex];
                if (segmentId == 0 || segmentId >= segments.Length)
                    continue;

                NetSegment segment = segments[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0)
                    continue;

                if ((segment.m_flags & NetSegment.Flags.Collapsed) != 0)
                    continue;

                if (!IsSegmentNearPosition(ref segment, stopPosition, StopLaneSearchRadius))
                    continue;

                NetInfo info = segment.Info;
                if (info == null || info.m_lanes == null || !(info.m_netAI is RoadBaseAI))
                    continue;

                uint laneId = segment.m_lanes;
                for (int laneIndex = 0; laneIndex < info.m_lanes.Length && laneId != 0; laneIndex++)
                {
                    if (laneId >= lanes.Length)
                        break;

                    NetInfo.Lane laneInfo = info.m_lanes[laneIndex];
                    NetLane lane = lanes[(int)laneId];
                    uint nextLane = lane.m_nextLane;

                    if (!IsCandidateStopLane(laneInfo) || lane.m_length < 1f)
                    {
                        laneId = nextLane;
                        continue;
                    }

                    Vector3 lanePosition;
                    float laneOffset;
                    lane.GetClosestPosition(stopPosition, out lanePosition, out laneOffset);

                    float stopOffset = laneInfo.m_stopOffset;
                    if ((segment.m_flags & NetSegment.Flags.Invert) != 0)
                        stopOffset = -stopOffset;

                    Vector3 stopLanePosition;
                    Vector3 stopLaneDirection;
                    lane.CalculateStopPositionAndDirection(laneOffset, stopOffset, out stopLanePosition, out stopLaneDirection);

                    float candidateSqr = SqrDistanceXZ(stopLanePosition, stopPosition);
                    if (candidateSqr < bestSqr)
                    {
                        bestSqr = candidateSqr;
                        bestSegmentId = segmentId;
                        bestSegment = segment;
                        bestLaneId = laneId;
                        bestLane = lane;
                        bestLaneInfo = laneInfo;
                        bestLaneOffset = laneOffset;
                    }

                    laneId = nextLane;
                }
            }

            return bestSegmentId != 0;
        }

        private static void CollectBerthSlotsForStopGeometry(StopGeometry stopGeometry, List<BerthSlot> berthSlots)
        {
            berthSlots.Clear();
            if (stopGeometry.Lane.m_length < 1f)
                return;

            int berthNumber = 1;
            for (float distanceFromStart = 0f; distanceFromStart <= stopGeometry.Lane.m_length; distanceFromStart += BerthSpacing)
            {
                float laneOffset = GetLaneOffsetBehindPitStart(
                    stopGeometry.ReverseLane,
                    stopGeometry.Lane.m_length,
                    stopGeometry.FirstBerthLaneOffset,
                    distanceFromStart);
                if (laneOffset < 0f || laneOffset > 1f)
                    break;

                float distanceToPitEnd = GetDistanceToPitEnd(stopGeometry.ReverseLane, stopGeometry.Lane.m_length, laneOffset);
                if (distanceFromStart > 0f && distanceToPitEnd <= MinimumEndClearance)
                    break;

                Vector3 markerPosition;
                Vector3 laneDirection;
                stopGeometry.Lane.CalculateStopPositionAndDirection(laneOffset, stopGeometry.StopOffset, out markerPosition, out laneDirection);

                Vector3 side = GetPavementSide(laneDirection, stopGeometry.PropSideOffset);
                Vector3 propPosition = markerPosition + (side * BusStopPropSideOffset);
                berthSlots.Add(new BerthSlot(
                    stopGeometry.SegmentId,
                    stopGeometry.LaneId,
                    berthNumber,
                    markerPosition,
                    propPosition,
                    GetPropAngle(laneDirection)));

                berthNumber++;
            }
        }

        private static void RegisterServiceZone(
            ushort lineId,
            ushort stopNode,
            ushort nextStop,
            StopGeometry stopGeometry,
            List<BerthSlot> berthSlots)
        {
            if (lineId == 0 || stopNode == 0 || berthSlots == null || berthSlots.Count == 0)
                return;

            float serviceLength = stopGeometry.HasPitOffset
                ? Mathf.Max(BerthSpacing, GetDistanceToPitEnd(stopGeometry.ReverseLane, stopGeometry.Lane.m_length, stopGeometry.FirstBerthLaneOffset))
                : Mathf.Max(BerthSpacing, berthSlots.Count * BerthSpacing);

            StopServiceZone zone = new StopServiceZone(
                lineId,
                stopNode,
                nextStop,
                stopGeometry.SegmentId,
                stopGeometry.LaneId,
                stopGeometry.ReverseLane,
                stopGeometry.HasPitOffset,
                stopGeometry.StopOffset,
                stopGeometry.FirstBerthLaneOffset,
                stopGeometry.FirstBerthPosition,
                serviceLength,
                berthSlots.Count);

            lock (ServiceZonesLock)
            {
                for (int i = 0; i < ServiceZones.Count; i++)
                {
                    StopServiceZone existing = ServiceZones[i];
                    if (existing.LineId == lineId && existing.StopNode == stopNode)
                    {
                        ServiceZones[i] = zone;
                        return;
                    }
                }

                ServiceZones.Add(zone);
            }
        }

        private static void ClearServiceZones()
        {
            lock (ServiceZonesLock)
                ServiceZones.Clear();
        }

        private static int GetServiceZoneCount()
        {
            lock (ServiceZonesLock)
                return ServiceZones.Count;
        }

        private bool TryAddBerthsForStopGeometry(List<BerthSlot> berthSlots, ref int duplicateBerthsSkipped)
        {
            bool added = false;
            for (int i = 0; i < berthSlots.Count; i++)
            {
                BerthSlot slot = berthSlots[i];
                if (IsDuplicateBerthMarker(slot.MarkerPosition))
                {
                    duplicateBerthsSkipped++;
                    continue;
                }

                Berths.Add(new VisualBerth(
                    slot.SegmentId,
                    slot.LaneId,
                    slot.BerthNumber,
                    slot.MarkerPosition,
                    slot.WaitingPosition,
                    slot.PropAngle));

                added = true;
            }

            return added;
        }

        private static void AddLegacyNativePropAnchors(List<BerthSlot> berthSlots, bool disabledStop)
        {
            if (berthSlots == null)
                return;

            const float duplicateDistanceSqr = 0.25f * 0.25f;
            for (int i = 0; i < berthSlots.Count; i++)
            {
                BerthSlot slot = berthSlots[i];
                bool duplicate = false;
                for (int anchorIndex = 0; anchorIndex < LegacyNativePropAnchors.Count; anchorIndex++)
                {
                    LegacyNativePropAnchor existing = LegacyNativePropAnchors[anchorIndex];
                    if (SqrDistanceXZ(existing.Position, slot.WaitingPosition) > duplicateDistanceSqr)
                        continue;

                    if (disabledStop && !existing.DisabledStop)
                    {
                        LegacyNativePropAnchors[anchorIndex] = new LegacyNativePropAnchor(
                            existing.Position,
                            existing.Angle,
                            true);
                    }

                    duplicate = true;
                    break;
                }

                if (!duplicate)
                    LegacyNativePropAnchors.Add(new LegacyNativePropAnchor(slot.WaitingPosition, slot.PropAngle, disabledStop));
            }
        }

        private static bool TryAddDepartureBoardForStopGeometry(StopGeometry stopGeometry)
        {
            if (IsDuplicateDepartureBoard(stopGeometry.DepartureBoardPosition))
                return false;

            DepartureBoards.Add(new VisualDepartureBoard(
                stopGeometry.SegmentId,
                stopGeometry.DepartureBoardPosition,
                stopGeometry.DepartureBoardAngle));
            return true;
        }

        private static bool IsDuplicateBerthMarker(Vector3 markerPosition)
        {
            float maxSqr = DuplicateBerthMergeDistance * DuplicateBerthMergeDistance;
            for (int i = 0; i < Berths.Count; i++)
            {
                if (SqrDistanceXZ(Berths[i].MarkerPosition, markerPosition) <= maxSqr)
                    return true;
            }

            return false;
        }

        private static bool IsDuplicateDepartureBoard(Vector3 boardPosition)
        {
            float maxSqr = DepartureBoardDuplicateDistance * DepartureBoardDuplicateDistance;
            for (int i = 0; i < DepartureBoards.Count; i++)
            {
                if (SqrDistanceXZ(DepartureBoards[i].Position, boardPosition) <= maxSqr)
                    return true;
            }

            return false;
        }

        private static bool IsCandidateStopLane(NetInfo.Lane laneInfo)
        {
            if (laneInfo == null)
                return false;

            if ((laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) == 0)
                return false;

            return (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != 0
                   || (laneInfo.m_stopType & VehicleInfo.VehicleType.Car) != 0;
        }

        private static bool HasPitStopOffset(NetInfo.Lane laneInfo)
        {
            return laneInfo != null && Mathf.Abs(laneInfo.m_stopOffset) > StopOffsetEpsilon;
        }

        private static float GetPropSideOffset(NetInfo.Lane laneInfo, float stopOffset)
        {
            if (Mathf.Abs(stopOffset) > StopOffsetEpsilon)
                return stopOffset;

            if (laneInfo != null && Mathf.Abs(laneInfo.m_position) > StopOffsetEpsilon)
                return laneInfo.m_position;

            return 1f;
        }

        private static float ResolvePavementSideReference(
            NetManager netManager,
            ref NetSegment segment,
            float laneOffset,
            Vector3 stopPosition,
            Vector3 laneDirection,
            float fallbackSideReference)
        {
            if (netManager == null
                || segment.m_startNode == 0
                || segment.m_endNode == 0
                || segment.m_startNode >= netManager.m_nodes.m_buffer.Length
                || segment.m_endNode >= netManager.m_nodes.m_buffer.Length)
            {
                return fallbackSideReference;
            }

            Vector3 start = netManager.m_nodes.m_buffer[segment.m_startNode].m_position;
            Vector3 end = netManager.m_nodes.m_buffer[segment.m_endNode].m_position;
            Vector3 middleA;
            Vector3 middleB;
            NetSegment.CalculateMiddlePoints(
                start,
                segment.m_startDirection,
                end,
                segment.m_endDirection,
                false,
                false,
                out middleA,
                out middleB);

            Bezier3 centerLine = new Bezier3
            {
                a = start,
                b = middleA,
                c = middleB,
                d = end
            };

            Vector3 outward = stopPosition - centerLine.Position(Mathf.Clamp01(laneOffset));
            outward.y = 0f;
            laneDirection.y = 0f;
            if (outward.sqrMagnitude <= 0.01f || laneDirection.sqrMagnitude <= 0.001f)
                return fallbackSideReference;

            laneDirection.Normalize();
            Vector3 right = new Vector3(laneDirection.z, 0f, -laneDirection.x);
            float sideReference = Vector3.Dot(outward, right);
            return Mathf.Abs(sideReference) > StopOffsetEpsilon
                ? sideReference
                : fallbackSideReference;
        }

        private static float NormalizeLaneOffset(float rawLaneOffset)
        {
            if (rawLaneOffset > 1f)
                return Mathf.Clamp01(rawLaneOffset / 255f);

            return Mathf.Clamp01(rawLaneOffset);
        }

        private static float GetRepresentativeBusLength(ushort lineId)
        {
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
            TransportManager transportManager = Singleton<TransportManager>.instance;
            if (vehicleManager == null
                || vehicleManager.m_vehicles == null
                || vehicleManager.m_vehicles.m_buffer == null
                || transportManager == null
                || transportManager.m_lines == null
                || transportManager.m_lines.m_buffer == null
                || lineId == 0
                || lineId >= transportManager.m_lines.m_size
                || lineId >= transportManager.m_lines.m_buffer.Length)
                return DefaultVisualBusLength;

            Vehicle[] vehicles = vehicleManager.m_vehicles.m_buffer;
            ushort vehicleId = transportManager.m_lines.m_buffer[lineId].m_vehicles;
            int guard = 0;
            while (vehicleId != 0 && guard < MaxTransportChainIterations)
            {
                if (vehicleId >= vehicleManager.m_vehicles.m_size || vehicleId >= vehicles.Length)
                    break;

                Vehicle vehicle = vehicles[vehicleId];
                ushort nextVehicleId = vehicle.m_nextLineVehicle;
                if ((vehicle.m_flags & Vehicle.Flags.Created) != 0
                    && vehicle.m_transportLine == lineId)
                {
                    VehicleInfo info = vehicle.Info;
                    if (info != null && info.m_generatedInfo != null && info.m_generatedInfo.m_size.z > 1f)
                        return info.m_generatedInfo.m_size.z;
                }

                vehicleId = nextVehicleId;
                guard++;
            }

            return DefaultVisualBusLength;
        }

        private static bool IsSegmentNearPosition(ref NetSegment segment, Vector3 position, float radius)
        {
            Bounds bounds = segment.m_bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            return Mathf.Abs(position.x - center.x) <= extents.x + radius
                   && Mathf.Abs(position.y - center.y) <= extents.y + radius
                   && Mathf.Abs(position.z - center.z) <= extents.z + radius;
        }

        private static void AddSegmentGridCandidates(
            NetManager netManager,
            Vector3 stopPosition,
            float radius,
            List<ushort> candidates)
        {
            if (netManager == null
                || candidates == null
                || netManager.m_segmentGrid == null
                || netManager.m_segments == null
                || netManager.m_segments.m_buffer == null)
            {
                return;
            }

            int minGridX = Mathf.Max((int)((stopPosition.x - radius) / NetSegmentGridCellSize + NetSegmentGridHalfResolution), 0);
            int minGridZ = Mathf.Max((int)((stopPosition.z - radius) / NetSegmentGridCellSize + NetSegmentGridHalfResolution), 0);
            int maxGridX = Mathf.Min((int)((stopPosition.x + radius) / NetSegmentGridCellSize + NetSegmentGridHalfResolution), NetSegmentGridResolution - 1);
            int maxGridZ = Mathf.Min((int)((stopPosition.z + radius) / NetSegmentGridCellSize + NetSegmentGridHalfResolution), NetSegmentGridResolution - 1);
            NetSegment[] segments = netManager.m_segments.m_buffer;

            for (int z = minGridZ; z <= maxGridZ; z++)
            {
                int rowOffset = z * NetSegmentGridResolution;
                for (int x = minGridX; x <= maxGridX; x++)
                {
                    ushort segmentId = netManager.m_segmentGrid[rowOffset + x];
                    int guard = 0;
                    while (segmentId != 0)
                    {
                        AddCandidateSegment(candidates, segmentId);
                        if (segmentId >= segments.Length)
                            break;

                        segmentId = segments[segmentId].m_nextGridSegment;
                        guard++;
                        if (guard > MaxSegmentGridChainIterations)
                            break;
                    }
                }
            }
        }

        private static void AddCandidateSegment(List<ushort> candidates, ushort segmentId)
        {
            if (segmentId == 0 || candidates == null)
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == segmentId)
                    return;
            }

            candidates.Add(segmentId);
        }

        private static int StableStringHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (value == null)
                    return hash;

                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];

                return hash;
            }
        }

        private static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (dx * dx) + (dz * dz);
        }

        private static bool IsReverseLane(NetInfo.Lane laneInfo, NetSegment.Flags segmentFlags)
        {
            bool reverseLane = (laneInfo.m_finalDirection & NetInfo.Direction.Backward) != 0;
            if ((segmentFlags & NetSegment.Flags.Invert) != 0)
                reverseLane = !reverseLane;

            return reverseLane;
        }

        private static float GetLaneOffsetBehindPitStart(
            bool reverseLane,
            float laneLength,
            float pitStartLaneOffset,
            float distanceFromStart)
        {
            float normalizedDistance = laneLength <= 1f ? 0f : Mathf.Clamp01(distanceFromStart / laneLength);
            return reverseLane ? pitStartLaneOffset + normalizedDistance : pitStartLaneOffset - normalizedDistance;
        }

        private static float GetDistanceBehindFirstBerth(
            bool reverseLane,
            float laneLength,
            float firstBerthLaneOffset,
            float laneOffset)
        {
            float clampedFirst = Mathf.Clamp01(firstBerthLaneOffset);
            float clampedOffset = Mathf.Clamp01(laneOffset);
            return (reverseLane ? clampedOffset - clampedFirst : clampedFirst - clampedOffset) * Mathf.Max(0f, laneLength);
        }

        private static float GetDistanceToPitEnd(bool reverseLane, float laneLength, float laneOffset)
        {
            float remainingNormalizedDistance = reverseLane ? 1f - laneOffset : laneOffset;
            return remainingNormalizedDistance * laneLength;
        }

        private static Vector3 GetPavementSide(Vector3 laneDirection, float stopOffset)
        {
            laneDirection.y = 0f;
            if (laneDirection.sqrMagnitude < 0.001f)
                laneDirection = Vector3.forward;

            laneDirection.Normalize();
            Vector3 right = new Vector3(laneDirection.z, 0f, -laneDirection.x);
            if (stopOffset < 0f)
                right = -right;

            return right;
        }

        private static float GetPropAngle(Vector3 laneDirection)
        {
            laneDirection.y = 0f;
            if (laneDirection.sqrMagnitude < 0.001f)
                laneDirection = Vector3.forward;

            laneDirection.Normalize();
            return Mathf.Atan2(laneDirection.x, laneDirection.z);
        }

        private void DrawBerth(Camera camera, VisualBerth berth)
        {
            Vector2 markerPoint;
            if (!WorldToGuiPoint(camera, berth.MarkerPosition + Vector3.up * MarkerWorldLift, out markerPoint))
                return;

            if (camera.transform.position.y <= LabelMaxCameraHeight)
            {
                Rect labelRect = GetLabelRect(markerPoint);
                if (IsCoveredByNormalUi(labelRect))
                    return;

                DrawLabel(markerPoint, "B" + berth.BerthNumber.ToString());
            }
        }

        private void DrawLabel(Vector2 point, string label)
        {
            Rect borderRect = GetLabelRect(point);
            GUI.color = LabelBorderColor;
            GUI.DrawTexture(borderRect, Texture2D.whiteTexture);
            GUI.color = LabelBackgroundColor;
            GUI.DrawTexture(new Rect(borderRect.x + 1f, borderRect.y + 1f, borderRect.width - 2f, borderRect.height - 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(borderRect, label, _labelStyle);
        }

        private static Rect GetLabelRect(Vector2 point)
        {
            return new Rect(point.x - (LabelWidth * 0.5f), point.y - LabelYOffset, LabelWidth, LabelHeight);
        }

        private PitStatusLine AddStatusRow(
            TransportManager transportManager,
            ushort lineId,
            ref TransportLine line,
            ushort stopNode,
            ushort nextStop,
            int routeStopNumber,
            int totalLineStops,
            Vector3 pitAnchor,
            List<BerthSlot> berthSlots)
        {
            PitStatusBubble bubble = GetOrCreateStatusBubble(stopNode, pitAnchor);
            bubble.SetBerths(berthSlots);
            return bubble.GetOrCreateLine(lineId, stopNode, nextStop, GetVisibleLineName(transportManager, lineId, ref line), routeStopNumber, totalLineStops);
        }

        private PitStatusBubble GetOrCreateDisabledStatusBubble(ushort stopNode, Vector3 pitAnchor)
        {
            float maxSqr = DuplicateBerthMergeDistance * DuplicateBerthMergeDistance;
            for (int i = 0; i < StatusBubbles.Count; i++)
            {
                PitStatusBubble bubble = StatusBubbles[i];
                if (bubble != null && bubble.Disabled && SqrDistanceXZ(bubble.AnchorPosition, pitAnchor) <= maxSqr)
                {
                    bubble.IncludeStop(stopNode, pitAnchor);
                    return bubble;
                }
            }

            PitStatusBubble created = new PitStatusBubble(stopNode, pitAnchor, "Stop Stacker Disabled", true);
            StatusBubbles.Add(created);
            return created;
        }

        private PitStatusBubble GetOrCreateStatusBubble(ushort stopNode, Vector3 pitAnchor)
        {
            float maxSqr = DuplicateBerthMergeDistance * DuplicateBerthMergeDistance;
            for (int i = 0; i < StatusBubbles.Count; i++)
            {
                PitStatusBubble bubble = StatusBubbles[i];
                if (bubble != null && !bubble.Disabled && SqrDistanceXZ(bubble.AnchorPosition, pitAnchor) <= maxSqr)
                {
                    bubble.IncludeStop(stopNode, pitAnchor);
                    return bubble;
                }
            }

            PitStatusBubble created = new PitStatusBubble(stopNode, pitAnchor, GetStatusBubbleTitle(pitAnchor), false);
            StatusBubbles.Add(created);
            return created;
        }

        private static int CountDisabledStatusBubbles()
        {
            int count = 0;
            for (int i = 0; i < StatusBubbles.Count; i++)
            {
                PitStatusBubble bubble = StatusBubbles[i];
                if (bubble != null && bubble.Disabled)
                    count++;
            }

            return count;
        }

        private string GetStatusBubbleTitle(Vector3 anchorPosition)
        {
            ushort buildingId;
            string buildingName;
            float distance;
            if (TryGetNearestBuildingName(anchorPosition, out buildingId, out buildingName, out distance))
            {
                return buildingName + " Stop";
            }

            return "Bus Stop";
        }

        private static bool TryGetNearestBuildingName(
            Vector3 position,
            out ushort nearestBuilding,
            out string nearestName,
            out float nearestDistance)
        {
            nearestBuilding = 0;
            nearestName = string.Empty;
            nearestDistance = 0f;

            BuildingManager buildingManager = Singleton<BuildingManager>.instance;
            if (buildingManager == null
                || buildingManager.m_buildings == null
                || buildingManager.m_buildings.m_buffer == null
                || buildingManager.m_buildingGrid == null)
            {
                return false;
            }

            Building[] buildings = buildingManager.m_buildings.m_buffer;
            int buildingLimit = buildings.Length;
            if (buildingManager.m_buildings.m_size < buildingLimit)
                buildingLimit = (int)buildingManager.m_buildings.m_size;

            float bestSqr = StatusBubbleBuildingSearchRadius * StatusBubbleBuildingSearchRadius;
            int minGridX = Mathf.Max(
                (int)((position.x - StatusBubbleBuildingSearchRadius) / BuildingGridCellSize + BuildingGridHalfResolution),
                0);
            int minGridZ = Mathf.Max(
                (int)((position.z - StatusBubbleBuildingSearchRadius) / BuildingGridCellSize + BuildingGridHalfResolution),
                0);
            int maxGridX = Mathf.Min(
                (int)((position.x + StatusBubbleBuildingSearchRadius) / BuildingGridCellSize + BuildingGridHalfResolution),
                BuildingGridResolution - 1);
            int maxGridZ = Mathf.Min(
                (int)((position.z + StatusBubbleBuildingSearchRadius) / BuildingGridCellSize + BuildingGridHalfResolution),
                BuildingGridResolution - 1);

            for (int z = minGridZ; z <= maxGridZ; z++)
            {
                int rowOffset = z * BuildingGridResolution;
                for (int x = minGridX; x <= maxGridX; x++)
                {
                    int gridIndex = rowOffset + x;
                    if (gridIndex < 0 || gridIndex >= buildingManager.m_buildingGrid.Length)
                        continue;

                    ushort buildingId = buildingManager.m_buildingGrid[gridIndex];
                    int guard = 0;
                    while (buildingId != 0 && guard < MaxBuildingGridChainIterations)
                    {
                        if (buildingId >= buildingLimit)
                            break;

                        Building building = buildings[buildingId];
                        ushort nextBuildingId = building.m_nextGridBuilding;
                        if ((building.m_flags & Building.Flags.Created) != 0
                            && building.Info != null
                            && IsStatusBubbleNameSource(building.Info))
                        {
                            float distanceSqr = SqrDistanceXZ(position, building.m_position);
                            if (distanceSqr < bestSqr)
                            {
                                string name = GetBuildingDisplayName(building.m_flags, buildingId);
                                if (!string.IsNullOrEmpty(name))
                                {
                                    bestSqr = distanceSqr;
                                    nearestBuilding = buildingId;
                                    nearestName = name;
                                }
                            }
                        }

                        buildingId = nextBuildingId;
                        guard++;
                    }
                }
            }

            if (nearestBuilding == 0)
                return false;

            nearestDistance = Mathf.Sqrt(bestSqr);
            return true;
        }

        private static string GetBuildingDisplayName(Building.Flags flags, ushort buildingId)
        {
            if ((flags & Building.Flags.CustomName) == 0)
                return string.Empty;

            InstanceManager instanceManager = Singleton<InstanceManager>.instance;
            if (instanceManager == null)
                return string.Empty;

            string name = string.Empty;
            try
            {
                InstanceID instance = default(InstanceID);
                instance.Building = buildingId;
                name = instanceManager.GetName(instance);
            }
            catch
            {
                name = string.Empty;
            }

            if (string.IsNullOrEmpty(name))
                return string.Empty;

            name = name.Trim();
            return IsUsableBuildingDisplayName(name) ? name : string.Empty;
        }

        private static bool IsStatusBubbleNameSource(BuildingInfo info)
        {
            if (info == null)
                return false;

            string descriptor = GetPrefabName(info);
            return !IsUnsafeStatusBubbleNameDescriptor(descriptor);
        }

        private static bool IsUsableBuildingDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (name.StartsWith("BUILDING_TITLE[", System.StringComparison.Ordinal)
                || name.StartsWith("BUILDING_NAME[", System.StringComparison.Ordinal)
                || name.IndexOf("]:", System.StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return !IsUtilityBuildingDescriptor(name);
        }

        private static bool IsUnsafeStatusBubbleNameDescriptor(string descriptor)
        {
            if (string.IsNullOrEmpty(descriptor))
                return false;

            return IsUtilityBuildingDescriptor(descriptor)
                || IsCustomAssetDescriptor(descriptor)
                || IsDecorationBuildingDescriptor(descriptor);
        }

        private static bool IsUtilityBuildingDescriptor(string descriptor)
        {
            if (string.IsNullOrEmpty(descriptor))
                return false;

            string lower = descriptor.ToLowerInvariant();
            return lower.Contains("water pipe")
                || lower.Contains("sewage pipe")
                || lower.Contains("road electricity")
                || lower.Contains("electricity conduit")
                || lower.Contains("hidden road power")
                || lower.Contains("hidden road electricity");
        }

        private static bool IsCustomAssetDescriptor(string descriptor)
        {
            string lower = descriptor.ToLowerInvariant();
            return lower.Contains("_data")
                || lower.IndexOf('.') >= 0;
        }

        private static bool IsDecorationBuildingDescriptor(string descriptor)
        {
            string lower = descriptor.ToLowerInvariant();
            return lower.Contains("pillar")
                || lower.Contains("car port")
                || lower.Contains("parking lot")
                || lower.Contains("p_lot")
                || lower.Contains("tree sapling")
                || lower.Contains("sapling");
        }

        private static string GetVisibleLineName(TransportManager transportManager, ushort lineId, ref TransportLine line)
        {
            string name = transportManager == null ? string.Empty : transportManager.GetLineName(lineId);
            if (!string.IsNullOrEmpty(name))
            {
                string trimmed = name.Trim();
                if (trimmed.StartsWith("#"))
                    trimmed = "Line " + trimmed.TrimStart('#').Trim();

                if (!string.IsNullOrEmpty(trimmed))
                    return trimmed;
            }

            if (line.m_lineNumber != 0)
                return "Line " + line.m_lineNumber.ToString();

            return "Line " + lineId.ToString();
        }

        private int CountWaitingPassengersForService(
            ushort lineId,
            ushort currentStop,
            ushort nextStop,
            int assignedBerthNumber,
            Vector3 assignedWaitingPosition,
            bool registerAssignments,
            out int assignedCount)
        {
            assignedCount = 0;
            if (currentStop == 0 || nextStop == 0)
                return 0;

            NetManager netManager = Singleton<NetManager>.instance;
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            if (netManager == null || citizenManager == null)
                return 0;

            if (!IsCreatedNode(currentStop, netManager) || !IsCreatedNode(nextStop, netManager))
                return 0;

            if (citizenManager.m_citizenGrid == null || citizenManager.m_instances == null || citizenManager.m_instances.m_buffer == null)
                return 0;

            Vector3 currentPosition = netManager.m_nodes.m_buffer[currentStop].m_position;
            Vector3 nextPosition = netManager.m_nodes.m_buffer[nextStop].m_position;
            float minX = Mathf.Min(currentPosition.x, assignedWaitingPosition.x) - PassengerAssignmentScanRadius;
            float minZ = Mathf.Min(currentPosition.z, assignedWaitingPosition.z) - PassengerAssignmentScanRadius;
            float maxX = Mathf.Max(currentPosition.x, assignedWaitingPosition.x) + PassengerAssignmentScanRadius;
            float maxZ = Mathf.Max(currentPosition.z, assignedWaitingPosition.z) + PassengerAssignmentScanRadius;
            int minGridX = Mathf.Max((int)(minX / CitizenGridCellSize + CitizenGridHalfResolution), 0);
            int minGridZ = Mathf.Max((int)(minZ / CitizenGridCellSize + CitizenGridHalfResolution), 0);
            int maxGridX = Mathf.Min((int)(maxX / CitizenGridCellSize + CitizenGridHalfResolution), CitizenGridResolution - 1);
            int maxGridZ = Mathf.Min((int)(maxZ / CitizenGridCellSize + CitizenGridHalfResolution), CitizenGridResolution - 1);
            int waitingCount = 0;

            for (int z = minGridZ; z <= maxGridZ; z++)
            {
                int rowOffset = z * CitizenGridResolution;
                for (int x = minGridX; x <= maxGridX; x++)
                {
                    int gridIndex = rowOffset + x;
                    if (gridIndex < 0 || gridIndex >= citizenManager.m_citizenGrid.Length)
                        continue;

                    ushort instanceId = citizenManager.m_citizenGrid[gridIndex];
                    int guard = 0;
                    while (instanceId != 0)
                    {
                        if (instanceId >= citizenManager.m_instances.m_size
                            || instanceId >= citizenManager.m_instances.m_buffer.Length)
                            break;

                        CitizenInstance citizen = citizenManager.m_instances.m_buffer[instanceId];
                        ushort nextInstance = citizen.m_nextGridInstance;
                        if (IsWaitingForServiceAtStop(instanceId, citizen, currentPosition, nextPosition, assignedWaitingPosition))
                        {
                            waitingCount++;
                            if (registerAssignments && PassengerWaitPositionHarmony.RegisterWaitingPassenger(
                                    instanceId,
                                    citizen.m_citizen,
                                    lineId,
                                    currentStop,
                                    assignedBerthNumber,
                                    assignedWaitingPosition,
                                    waitingCount))
                            {
                                assignedCount++;
                            }
                        }

                        instanceId = nextInstance;
                        guard++;
                        if (guard > MaxCitizenGridChainIterations)
                            break;
                    }
                }
            }

            return waitingCount;
        }

        private IEnumerator CountWaitingPassengersForServicePaced(
            ushort lineId,
            ushort currentStop,
            ushort nextStop,
            int assignedBerthNumber,
            Vector3 assignedWaitingPosition,
            PassengerRefreshRateLimiter rateLimiter,
            PassengerRefreshWorkLimiter workLimiter,
            PacedWaitingScanResult result)
        {
            if (result == null || currentStop == 0 || nextStop == 0)
                yield break;

            NetManager netManager = Singleton<NetManager>.instance;
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            if (netManager == null || citizenManager == null)
                yield break;

            if (!IsCreatedNode(currentStop, netManager) || !IsCreatedNode(nextStop, netManager))
                yield break;

            if (citizenManager.m_citizenGrid == null || citizenManager.m_instances == null || citizenManager.m_instances.m_buffer == null)
                yield break;

            Vector3 currentPosition = netManager.m_nodes.m_buffer[currentStop].m_position;
            Vector3 nextPosition = netManager.m_nodes.m_buffer[nextStop].m_position;
            float minX = Mathf.Min(currentPosition.x, assignedWaitingPosition.x) - PassengerAssignmentScanRadius;
            float minZ = Mathf.Min(currentPosition.z, assignedWaitingPosition.z) - PassengerAssignmentScanRadius;
            float maxX = Mathf.Max(currentPosition.x, assignedWaitingPosition.x) + PassengerAssignmentScanRadius;
            float maxZ = Mathf.Max(currentPosition.z, assignedWaitingPosition.z) + PassengerAssignmentScanRadius;
            int minGridX = Mathf.Max((int)(minX / CitizenGridCellSize + CitizenGridHalfResolution), 0);
            int minGridZ = Mathf.Max((int)(minZ / CitizenGridCellSize + CitizenGridHalfResolution), 0);
            int maxGridX = Mathf.Min((int)(maxX / CitizenGridCellSize + CitizenGridHalfResolution), CitizenGridResolution - 1);
            int maxGridZ = Mathf.Min((int)(maxZ / CitizenGridCellSize + CitizenGridHalfResolution), CitizenGridResolution - 1);

            for (int z = minGridZ; z <= maxGridZ; z++)
            {
                int rowOffset = z * CitizenGridResolution;
                for (int x = minGridX; x <= maxGridX; x++)
                {
                    int gridIndex = rowOffset + x;
                    if (gridIndex < 0 || gridIndex >= citizenManager.m_citizenGrid.Length)
                        continue;

                    ushort instanceId = citizenManager.m_citizenGrid[gridIndex];
                    int guard = 0;
                    while (instanceId != 0)
                    {
                        if (instanceId >= citizenManager.m_instances.m_size
                            || instanceId >= citizenManager.m_instances.m_buffer.Length)
                            break;

                        CitizenInstance citizen = citizenManager.m_instances.m_buffer[instanceId];
                        ushort nextInstance = citizen.m_nextGridInstance;
                        if (IsWaitingForServiceAtStop(instanceId, citizen, currentPosition, nextPosition, assignedWaitingPosition))
                        {
                            while (rateLimiter != null && !rateLimiter.TryConsume())
                            {
                                if (workLimiter != null)
                                    workLimiter.ResetFrame();
                                yield return null;
                            }

                            result.WaitingCount++;
                            if (PassengerWaitPositionHarmony.RegisterWaitingPassenger(
                                    instanceId,
                                    citizen.m_citizen,
                                    lineId,
                                    currentStop,
                                    assignedBerthNumber,
                                    assignedWaitingPosition,
                                    result.WaitingCount))
                            {
                                result.AssignedCount++;
                            }
                        }

                        instanceId = nextInstance;
                        guard++;
                        if (workLimiter != null && workLimiter.ShouldYieldAfterCitizenInspection())
                            yield return null;
                        if (guard > MaxCitizenGridChainIterations)
                            break;
                    }

                    if (workLimiter != null && workLimiter.ShouldYieldAfterGridCell())
                        yield return null;
                }
            }
        }

        private bool IsWaitingForServiceAtStop(
            ushort instanceId,
            CitizenInstance citizen,
            Vector3 currentPosition,
            Vector3 nextPosition,
            Vector3 assignedWaitingPosition)
        {
            if ((citizen.m_flags & CitizenInstance.Flags.WaitingTransport) == 0)
                return false;

            Vector3 targetPosition = citizen.m_targetPos;
            Vector3 observedPosition = citizen.GetLastFramePosition();
            if (!IsWithinPassengerCountArea(targetPosition, currentPosition, assignedWaitingPosition)
                && !IsWithinPassengerCountArea(observedPosition, currentPosition, assignedWaitingPosition))
            {
                return false;
            }

            CitizenInfo info = citizen.Info;
            if (info == null || info.m_citizenAI == null)
                return false;

            try
            {
                CitizenInstance copy = citizen;
                return info.m_citizenAI.TransportArriveAtSource(instanceId, ref copy, currentPosition, nextPosition);
            }
            catch (System.Exception e)
            {
                if (_waitingRouteErrorLogCount < WaitingRouteErrorLogLimit)
                {
                    _waitingRouteErrorLogCount++;
                    StopStackerDiagnostics.AdvancedWarning("[StopStacker] STATUS_WAITING_ROUTE_CHECK_FAILED: " + e.GetType().Name + ": " + e.Message);
                }

                return false;
            }
        }

        private static bool IsWithinPassengerCountArea(Vector3 position, Vector3 currentPosition, Vector3 assignedWaitingPosition)
        {
            float currentStopMaxSqr = PassengerAssignmentScanRadius * PassengerAssignmentScanRadius;
            float assignedBerthMaxSqr = WaitingPassengerScanRadius * WaitingPassengerScanRadius;
            return SqrDistanceXZ(position, currentPosition) <= currentStopMaxSqr
                   || SqrDistanceXZ(position, assignedWaitingPosition) <= assignedBerthMaxSqr;
        }

        private static bool IsCreatedNode(ushort nodeId, NetManager netManager)
        {
            return netManager != null
                   && netManager.m_nodes != null
                   && netManager.m_nodes.m_buffer != null
                   && nodeId != 0
                   && nodeId < netManager.m_nodes.m_size
                   && nodeId < netManager.m_nodes.m_buffer.Length
                   && (netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) != 0;
        }

        private bool DrawOrHandleStatusBubble(Camera camera, PitStatusBubble bubble, bool repaint, bool mouseDown, Vector2 mousePosition)
        {
            if (bubble == null || (!bubble.Disabled && bubble.Lines.Count == 0))
                return false;

            Vector2 anchorPoint;
            if (!WorldToGuiPoint(camera, bubble.AnchorPosition + Vector3.up * StatusBubbleWorldLift, out anchorPoint))
                return false;

            float width = GetStatusBubbleWidth(bubble);
            float height = CalculateStatusBubbleHeight(bubble);
            Rect rect = GetStatusBubbleRect(anchorPoint, width, height);
            if (IsCoveredByNormalUi(rect))
                return false;

            DrawnStatusBubbleRects.Add(ExpandRect(rect, StatusBubbleScreenPadding));
            Rect toggleRect = GetStatusBubbleToggleRect(rect);
            if (mouseDown && toggleRect.Contains(mousePosition))
                return ToggleStatusBubbleDisabled(bubble);

            if (!repaint)
                return false;

            Color oldColor = GUI.color;
            GUI.color = LabelBorderColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = LabelBackgroundColor;
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), Texture2D.whiteTexture);
            DrawStatusBubbleToggle(toggleRect, bubble.Disabled);
            if (bubble.Disabled)
                DrawDisabledStatusBubbleText(rect);
            else
                DrawStatusBubbleText(rect, bubble);
            GUI.color = oldColor;
            return false;
        }

        private void DrawStatusBubbleText(Rect rect, PitStatusBubble bubble)
        {
            float x = rect.x + StatusBubblePaddingX;
            float y = rect.y + StatusBubblePaddingY;
            float width = rect.width - (StatusBubblePaddingX * 2f);
            float titleWidth = width - StatusBubbleToggleSize - StatusBubbleToggleInset;
            float lineWidth = width - StatusBubbleBerthWidth - StatusBubbleStopWidth - StatusBubbleWaitingWidth;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, titleWidth, StatusBubbleLineHeight), TruncateToWidth(bubble.Title, _statusTitleStyle, titleWidth), _statusTitleStyle);
            y += StatusBubbleLineHeight;

            GUI.color = _statusMutedStyle.normal.textColor;
            GUI.Label(new Rect(x, y, width, StatusBubbleLineHeight), FormatStatusBubbleSummary(bubble), _statusMutedStyle);
            y += StatusBubbleLineHeight;

            GUI.Label(new Rect(x, y, StatusBubbleBerthWidth, StatusBubbleLineHeight), "Berth", _statusMutedStyle);
            GUI.Label(new Rect(x + StatusBubbleBerthWidth, y, lineWidth, StatusBubbleLineHeight), "Line", _statusMutedStyle);
            GUI.Label(new Rect(x + StatusBubbleBerthWidth + lineWidth, y, StatusBubbleStopWidth, StatusBubbleLineHeight), "Stop", _statusMutedStyle);
            GUI.Label(new Rect(x + StatusBubbleBerthWidth + lineWidth + StatusBubbleStopWidth, y, StatusBubbleWaitingWidth, StatusBubbleLineHeight), "Waiting", _statusRightStyle);
            y += StatusBubbleLineHeight;

            for (int i = 0; i < bubble.Lines.Count; i++)
            {
                PitStatusLine line = bubble.Lines[i];
                GUI.color = Color.white;
                GUI.Label(
                    new Rect(x, y, StatusBubbleBerthWidth, StatusBubbleLineHeight),
                    line.AssignedBerthNumber > 0 ? "B" + line.AssignedBerthNumber.ToString() : "-",
                    _statusLineStyle);
                GUI.Label(
                    new Rect(x + StatusBubbleBerthWidth, y, lineWidth, StatusBubbleLineHeight),
                    TruncateToWidth(line.LineName, _statusLineStyle, lineWidth - 6f),
                    _statusLineStyle);
                GUI.Label(
                    new Rect(x + StatusBubbleBerthWidth + lineWidth, y, StatusBubbleStopWidth, StatusBubbleLineHeight),
                    line.RouteStopLabel,
                    _statusLineStyle);
                GUI.color = _statusMutedStyle.normal.textColor;
                GUI.Label(
                    new Rect(x + StatusBubbleBerthWidth + lineWidth + StatusBubbleStopWidth, y, StatusBubbleWaitingWidth, StatusBubbleLineHeight),
                    Mathf.Max(0, line.WaitingPassengers).ToString(),
                    _statusRightStyle);
                y += StatusBubbleLineHeight;
            }
        }

        private void DrawDisabledStatusBubbleText(Rect rect)
        {
            float x = rect.x + StatusBubblePaddingX;
            float y = rect.y + StatusBubblePaddingY;
            float width = rect.width - (StatusBubblePaddingX * 2f) - StatusBubbleToggleSize - StatusBubbleToggleInset;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, width, StatusBubbleLineHeight), "Stop Stacker Disabled", _statusTitleStyle);
        }

        private void DrawStatusBubbleToggle(Rect rect, bool disabled)
        {
            GUI.color = LabelBorderColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = LabelBackgroundColor;
            GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, disabled ? "x" : string.Empty, _statusToggleStyle);
        }

        private bool ToggleStatusBubbleDisabled(PitStatusBubble bubble)
        {
            if (bubble == null)
                return false;

            bool disable = !bubble.Disabled;
            int changed = 0;
            if (bubble.StopNodes.Count == 0 && bubble.AnchorStop != 0)
            {
                if (StopStackerDisabledStops.SetDisabled(bubble.AnchorStop, disable))
                    changed++;
            }
            else
            {
                for (int i = 0; i < bubble.StopNodes.Count; i++)
                {
                    if (StopStackerDisabledStops.SetDisabled(bubble.StopNodes[i], disable))
                        changed++;
                }
            }

            StopStackerDiagnostics.Advanced("[StopStacker] DISABLED_STOP_TOGGLED:"
                      + " disabled=" + disable
                      + " stops=" + Mathf.Max(1, bubble.StopNodes.Count)
                      + " changed=" + changed
                      + " totalDisabled=" + StopStackerDisabledStops.Count);

            if (changed <= 0)
                return false;

            RebuildAfterDisabledStopsChanged(disable ? "bubble-disable" : "bubble-enable");
            return true;
        }

        private static string FormatStatusBubbleSummary(PitStatusBubble bubble)
        {
            return "Lines served: " + bubble.Lines.Count.ToString()
                   + " | Waiting: " + bubble.TotalWaiting.ToString();
        }

        private static float GetStatusBubbleWidth(PitStatusBubble bubble)
        {
            return bubble != null && bubble.Disabled ? DisabledStatusBubbleWidth : StatusBubbleWidth;
        }

        private static float CalculateStatusBubbleHeight(PitStatusBubble bubble)
        {
            if (bubble != null && bubble.Disabled)
                return DisabledStatusBubbleHeight;

            return (StatusBubblePaddingY * 2f) + ((3 + Mathf.Max(1, bubble.Lines.Count)) * StatusBubbleLineHeight);
        }

        private static Rect GetStatusBubbleToggleRect(Rect bubbleRect)
        {
            return new Rect(
                bubbleRect.xMax - StatusBubbleToggleInset - StatusBubbleToggleSize,
                bubbleRect.y + StatusBubbleToggleInset,
                StatusBubbleToggleSize,
                StatusBubbleToggleSize);
        }

        private static Rect GetStatusBubbleRect(Vector2 anchorPoint, float width, float height)
        {
            Rect rightRect = new Rect(anchorPoint.x + StatusBubbleScreenOffsetX, anchorPoint.y - height - StatusBubbleScreenOffsetY, width, height);
            if (IsStatusBubbleRectUsable(rightRect))
                return rightRect;

            Rect leftRect = new Rect(anchorPoint.x - StatusBubbleScreenOffsetX - width, anchorPoint.y - height - StatusBubbleScreenOffsetY, width, height);
            if (IsStatusBubbleRectUsable(leftRect))
                return leftRect;

            Rect belowRightRect = new Rect(anchorPoint.x + StatusBubbleScreenOffsetX, anchorPoint.y + StatusBubbleScreenOffsetY, width, height);
            if (IsStatusBubbleRectUsable(belowRightRect))
                return belowRightRect;

            Rect belowLeftRect = new Rect(anchorPoint.x - StatusBubbleScreenOffsetX - width, anchorPoint.y + StatusBubbleScreenOffsetY, width, height);
            if (IsStatusBubbleRectUsable(belowLeftRect))
                return belowLeftRect;

            return ClampStatusBubbleRect(rightRect);
        }

        private static bool IsStatusBubbleRectUsable(Rect rect)
        {
            if (rect.xMin < StatusBubbleScreenPadding
                || rect.yMin < StatusBubbleScreenPadding
                || rect.xMax > Screen.width - StatusBubbleScreenPadding
                || rect.yMax > Screen.height - StatusBubbleScreenPadding)
            {
                return false;
            }

            Rect paddedRect = ExpandRect(rect, StatusBubbleScreenPadding);
            if (IsCoveredByNormalUi(paddedRect))
                return false;

            for (int i = 0; i < DrawnStatusBubbleRects.Count; i++)
            {
                if (paddedRect.Overlaps(DrawnStatusBubbleRects[i]))
                    return false;
            }

            return true;
        }

        private static Rect ClampStatusBubbleRect(Rect rect)
        {
            float maxX = Mathf.Max(StatusBubbleScreenPadding, Screen.width - StatusBubbleScreenPadding - rect.width);
            float maxY = Mathf.Max(StatusBubbleScreenPadding, Screen.height - StatusBubbleScreenPadding - rect.height);
            return new Rect(
                Mathf.Clamp(rect.x, StatusBubbleScreenPadding, maxX),
                Mathf.Clamp(rect.y, StatusBubbleScreenPadding, maxY),
                rect.width,
                rect.height);
        }

        private static Rect ExpandRect(Rect rect, float padding)
        {
            return new Rect(rect.x - padding, rect.y - padding, rect.width + (padding * 2f), rect.height + (padding * 2f));
        }

        private static void CollectNormalUiOcclusionRects()
        {
            NormalUiOcclusionRects.Clear();

            UIView view = UIView.GetAView();
            if (view == null)
                return;

            UIComponent[] components = view.GetComponentsInChildren<UIComponent>();
            if (components == null)
                return;

            for (int i = 0; i < components.Length; i++)
            {
                UIComponent component = components[i];
                if (!IsNormalUiOccluder(view, component))
                    continue;

                Rect rect = GetUiComponentRect(view, component);
                if (rect.width <= 0f || rect.height <= 0f)
                    continue;

                NormalUiOcclusionRects.Add(ExpandRect(rect, UiOcclusionPadding));
            }
        }

        private static bool IsNormalUiOccluder(UIView view, UIComponent component)
        {
            if (component == null || component == view || !component.enabled || !component.isVisible)
                return false;

            if (component.opacity <= 0.05f)
                return false;

            if (component.width < UiOcclusionMinWidth || component.height < UiOcclusionMinHeight)
                return false;

            if (IsStopStackerUiComponent(component))
                return false;

            if (!HasVisiblePanelSurface(component))
                return false;

            Rect rect = GetUiComponentRect(view, component);
            if (rect.xMax <= 0f || rect.yMax <= 0f || rect.xMin >= Screen.width || rect.yMin >= Screen.height)
                return false;

            if (rect.width >= Screen.width - UiOcclusionPadding && rect.height >= Screen.height - UiOcclusionPadding)
                return false;

            return true;
        }

        private static bool HasVisiblePanelSurface(UIComponent component)
        {
            UIPanel panel = component as UIPanel;
            return panel != null && !string.IsNullOrEmpty(panel.backgroundSprite);
        }

        private static bool IsStopStackerUiComponent(UIComponent component)
        {
            UIComponent current = component;
            while (current != null)
            {
                string name = current.name;
                if (!string.IsNullOrEmpty(name)
                    && (name.IndexOf("StopStacker", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("UnifiedTransitToolkitLauncherToolbar", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Rect GetUiComponentRect(UIView view, UIComponent component)
        {
            if (view == null || component == null)
                return new Rect();

            Vector2 uiResolution = view.GetScreenResolution();
            float scaleX = uiResolution.x > 0f ? Screen.width / uiResolution.x : 1f;
            float scaleY = uiResolution.y > 0f ? Screen.height / uiResolution.y : 1f;
            Vector3 position = component.absolutePosition;
            return new Rect(
                position.x * scaleX,
                position.y * scaleY,
                component.width * scaleX,
                component.height * scaleY);
        }

        private static bool IsCoveredByNormalUi(Rect rect)
        {
            if (NormalUiOcclusionRects.Count == 0)
                return false;

            for (int i = 0; i < NormalUiOcclusionRects.Count; i++)
            {
                if (rect.Overlaps(NormalUiOcclusionRects[i]))
                    return true;
            }

            return false;
        }

        private static string TruncateToWidth(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || style == null)
                return string.Empty;

            if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
                return text;

            const string suffix = "...";
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low <= 0 ? suffix : text.Substring(0, low) + suffix;
        }

        private void SyncBusStopProps()
        {
            int layoutHash = GetPropLayoutHash();
            StopStackerPropStyle busStopSignStyle = StopStackerModSettings.BusStopSignStyle;
            StopStackerPropStyle dispatchBoardStyle = StopStackerModSettings.DispatchBoardStyle;
            VisualBerth[] berthSnapshot = busStopSignStyle == StopStackerPropStyle.None
                ? new VisualBerth[0]
                : Berths.ToArray();
            VisualDepartureBoard[] departureBoardSnapshot = dispatchBoardStyle == StopStackerPropStyle.None
                ? new VisualDepartureBoard[0]
                : DepartureBoards.ToArray();
            int desiredSigns = berthSnapshot.Length;
            int desiredDepartureBoards = departureBoardSnapshot.Length;

            if (_propSyncCoroutine != null && layoutHash == _pendingPropLayoutHash)
                return;

            if (_propSyncCoroutine == null && layoutHash == _lastPropLayoutHash)
                return;

            ReleaseManagedBusStopProps();
            int legacyNativePropsReleased = ReleaseLegacyNativeBusStopProps();
            _pendingPropLayoutHash = layoutHash;
            if (desiredSigns == 0 && desiredDepartureBoards == 0)
            {
                _lastPropLayoutHash = layoutHash;
                _pendingPropLayoutHash = 0;
                return;
            }

            StopStackerDiagnostics.Advanced("[StopStacker] BUS_STOP_VISUAL_SYNC_SCHEDULED:"
                      + " desiredSigns=" + desiredSigns
                      + " desiredDepartureBoards=" + desiredDepartureBoards
                      + " signStyle=" + StopStackerModSettings.GetStyleLogValue(busStopSignStyle)
                      + " dispatchBoardStyle=" + StopStackerModSettings.GetStyleLogValue(dispatchBoardStyle)
                      + " signsPerFrame=" + WorldSignsPerFrame
                      + " boardsPerFrame=" + DepartureBoardsPerFrame
                      + " legacyNativePropsReleased=" + legacyNativePropsReleased
                      + " nativeProps=disabled-runtime-visuals-only");
            _propSyncCoroutine = StartCoroutine(RebuildManagedBusStopPropsPaced(
                layoutHash,
                berthSnapshot,
                departureBoardSnapshot));
        }

        private int GetPropLayoutHash()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)StopStackerModSettings.BusStopSignStyle;
                hash = (hash * 31) + (int)StopStackerModSettings.DispatchBoardStyle;
                hash = (hash * 31) + Berths.Count;
                hash = (hash * 31) + DepartureBoards.Count;
                for (int i = 0; i < Berths.Count; i++)
                {
                    Vector3 position = Berths[i].PropPosition;
                    hash = (hash * 31) + Mathf.RoundToInt(position.x * 10f);
                    hash = (hash * 31) + Mathf.RoundToInt(position.y * 10f);
                    hash = (hash * 31) + Mathf.RoundToInt(position.z * 10f);
                    hash = (hash * 31) + Mathf.RoundToInt(Berths[i].PropAngle * 100f);
                }

                for (int i = 0; i < DepartureBoards.Count; i++)
                {
                    Vector3 position = DepartureBoards[i].Position;
                    hash = (hash * 31) + Mathf.RoundToInt(position.x * 10f);
                    hash = (hash * 31) + Mathf.RoundToInt(position.y * 10f);
                    hash = (hash * 31) + Mathf.RoundToInt(position.z * 10f);
                    hash = (hash * 31) + Mathf.RoundToInt(DepartureBoards[i].Angle * 100f);
                }

                return hash;
            }
        }

        private IEnumerator RebuildManagedBusStopPropsPaced(
            int layoutHash,
            VisualBerth[] berthSnapshot,
            VisualDepartureBoard[] departureBoardSnapshot)
        {
            StopStackerPropStyle busStopSignStyle = StopStackerModSettings.BusStopSignStyle;
            StopStackerPropStyle dispatchBoardStyle = StopStackerModSettings.DispatchBoardStyle;
            int desiredSigns = berthSnapshot == null ? 0 : berthSnapshot.Length;
            int desiredDepartureBoards = departureBoardSnapshot == null ? 0 : departureBoardSnapshot.Length;

            if (desiredSigns > 0 || desiredDepartureBoards > 0)
                EnsureWorldSignResources();

            int signsCreated = 0;
            int boardsCreated = 0;
            int itemsThisFrame = 0;
            for (int i = 0; i < desiredSigns; i++)
            {
                GameObject sign = CreateWorldBusStopSign(berthSnapshot[i]);
                if (sign != null)
                {
                    _managedWorldBusStopSigns.Add(sign);
                    signsCreated++;
                }

                itemsThisFrame++;
                if (itemsThisFrame >= WorldSignsPerFrame)
                {
                    itemsThisFrame = 0;
                    yield return null;
                }
            }

            itemsThisFrame = 0;
            for (int i = 0; i < desiredDepartureBoards; i++)
            {
                GameObject board = CreateWorldDepartureBoard(departureBoardSnapshot[i]);
                if (board != null)
                {
                    _managedDepartureBoards.Add(board);
                    boardsCreated++;
                }

                itemsThisFrame++;
                if (itemsThisFrame >= DepartureBoardsPerFrame)
                {
                    itemsThisFrame = 0;
                    yield return null;
                }
            }

            if (!_loggedFirstWorldSignSync)
            {
                _loggedFirstWorldSignSync = true;
                StopStackerDiagnostics.Advanced("[StopStacker] BUS_STOP_WORLD_VISUALS_SYNCED:"
                          + " desiredSigns=" + desiredSigns
                          + " createdSigns=" + signsCreated
                          + " desiredDepartureBoards=" + desiredDepartureBoards
                          + " createdDepartureBoards=" + boardsCreated
                          + " signStyle=" + StopStackerModSettings.GetStyleLogValue(busStopSignStyle)
                          + " dispatchBoardStyle=" + StopStackerModSettings.GetStyleLogValue(dispatchBoardStyle)
                          + " processing=paced"
                          + " nativeProps=disabled-runtime-visuals-only");
            }

            CompletePendingBusStopPropSync(layoutHash);
        }

        private void CompletePendingBusStopPropSync(int layoutHash)
        {
            _lastPropLayoutHash = layoutHash;
            _pendingPropLayoutHash = 0;
            _propSyncCoroutine = null;
        }

        private void ReleaseManagedBusStopProps()
        {
            CancelPendingBusStopPropSync();
            ReleaseManagedWorldBusStopSigns();
            _lastPropLayoutHash = 0;
        }

        private int ReleaseLegacyNativeBusStopProps()
        {
            if (LegacyNativePropAnchors.Count == 0)
                return 0;

            PropManager propManager = Singleton<PropManager>.instance;
            if (propManager == null || propManager.m_props == null || propManager.m_props.m_buffer == null)
                return 0;

            PropInstance[] props = propManager.m_props.m_buffer;
            int maxPropId = Mathf.Min(props.Length - 1, ushort.MaxValue);
            int released = 0;
            for (int propId = 1; propId <= maxPropId; propId++)
            {
                PropInstance prop = props[propId];
                PropInstance.Flags flags = (PropInstance.Flags)prop.m_flags;
                if ((flags & PropInstance.Flags.Created) == 0 || (flags & PropInstance.Flags.Deleted) != 0)
                    continue;

                if (!IsLegacyStopStackerBusStopPropCandidate(prop.Info))
                    continue;

                bool disabledStopMatch;
                bool angleMatches;
                if (!TryMatchLegacyStopStackerPropPosition(prop.Position, prop.m_angle, out disabledStopMatch, out angleMatches))
                    continue;

                propManager.ReleaseProp((ushort)propId);
                released++;
            }

            return released;
        }

        private static bool IsLegacyStopStackerBusStopPropCandidate(PropInfo propInfo)
        {
            if (propInfo == null || propInfo.gameObject == null)
                return false;

            string descriptor = GetPrefabName(propInfo).ToLowerInvariant();
            return descriptor.Contains("bus") && descriptor.Contains("stop");
        }

        private static bool TryMatchLegacyStopStackerPropPosition(
            Vector3 position,
            float angle,
            out bool disabledStopMatch,
            out bool angleMatches)
        {
            disabledStopMatch = false;
            angleMatches = false;
            float maxDistanceSqr = LegacyNativePropCleanupDistance * LegacyNativePropCleanupDistance;
            float bestDistanceSqr = float.MaxValue;
            LegacyNativePropAnchor bestAnchor = default(LegacyNativePropAnchor);
            bool found = false;
            for (int i = 0; i < LegacyNativePropAnchors.Count; i++)
            {
                LegacyNativePropAnchor anchor = LegacyNativePropAnchors[i];
                float dx = position.x - anchor.Position.x;
                float dz = position.z - anchor.Position.z;
                float distanceSqr = (dx * dx) + (dz * dz);
                if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                bestAnchor = anchor;
                found = true;
            }

            if (!found)
                return false;

            disabledStopMatch = bestAnchor.DisabledStop;
            angleMatches = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, bestAnchor.Angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad
                           <= LegacyNativePropAngleTolerance;
            return true;
        }

        private void CancelPendingBusStopPropSync()
        {
            if (_propSyncCoroutine == null)
                return;

            StopCoroutine(_propSyncCoroutine);
            _propSyncCoroutine = null;
            _pendingPropLayoutHash = 0;
        }

        private int RebuildManagedWorldBusStopSigns()
        {
            ReleaseManagedWorldBusStopSigns();
            StopStackerPropStyle busStopSignStyle = StopStackerModSettings.BusStopSignStyle;
            StopStackerPropStyle dispatchBoardStyle = StopStackerModSettings.DispatchBoardStyle;
            int desiredSigns = busStopSignStyle == StopStackerPropStyle.None ? 0 : Berths.Count;
            int desiredDepartureBoards = dispatchBoardStyle == StopStackerPropStyle.None ? 0 : DepartureBoards.Count;
            if (desiredSigns == 0 && desiredDepartureBoards == 0)
                return 0;

            EnsureWorldSignResources();
            int signsCreated = 0;
            int boardsCreated = 0;
            for (int i = 0; i < desiredSigns; i++)
            {
                GameObject sign = CreateWorldBusStopSign(Berths[i]);
                if (sign == null)
                    continue;

                _managedWorldBusStopSigns.Add(sign);
                signsCreated++;
            }

            for (int i = 0; i < desiredDepartureBoards; i++)
            {
                GameObject board = CreateWorldDepartureBoard(DepartureBoards[i]);
                if (board == null)
                    continue;

                _managedDepartureBoards.Add(board);
                boardsCreated++;
            }

            if (!_loggedFirstWorldSignSync)
            {
                _loggedFirstWorldSignSync = true;
                StopStackerDiagnostics.Advanced("[StopStacker] BUS_STOP_WORLD_VISUALS_SYNCED:"
                          + " desiredSigns=" + desiredSigns
                          + " createdSigns=" + signsCreated
                          + " desiredDepartureBoards=" + desiredDepartureBoards
                          + " createdDepartureBoards=" + boardsCreated
                          + " signStyle=" + StopStackerModSettings.GetStyleLogValue(busStopSignStyle)
                          + " dispatchBoardStyle=" + StopStackerModSettings.GetStyleLogValue(dispatchBoardStyle));
            }

            return signsCreated + boardsCreated;
        }

        private void ReleaseManagedWorldBusStopSigns()
        {
            for (int i = 0; i < _managedWorldBusStopSigns.Count; i++)
            {
                if (_managedWorldBusStopSigns[i] != null)
                    UnityEngine.Object.Destroy(_managedWorldBusStopSigns[i]);
            }

            _managedWorldBusStopSigns.Clear();
            for (int i = 0; i < _managedDepartureBoards.Count; i++)
            {
                if (_managedDepartureBoards[i] != null)
                    UnityEngine.Object.Destroy(_managedDepartureBoards[i]);
            }

            _managedDepartureBoards.Clear();
            if (_worldSignRoot != null)
            {
                UnityEngine.Object.Destroy(_worldSignRoot);
                _worldSignRoot = null;
            }
        }

        private void ReleaseWorldVisualMaterials()
        {
            foreach (KeyValuePair<int, Material> item in _worldVisualMaterials)
            {
                if (item.Value != null)
                    UnityEngine.Object.Destroy(item.Value);
            }

            _worldVisualMaterials.Clear();
            _worldSignPoleMaterial = null;
            _worldSignPlateMaterial = null;
            _departureBoardFrameMaterial = null;
            _departureBoardScreenMaterial = null;
            _departureBoardGlassMaterial = null;
            _departureBoardHeaderMaterial = null;
            _departureBoardRowMaterial = null;
            _departureBoardDueMaterial = null;
        }

        private void EnsureWorldSignResources()
        {
            if (_worldSignRoot == null)
            {
                _worldSignRoot = new GameObject("Stop Stacker Bus Stop Signs");
                _worldSignRoot.transform.position = Vector3.zero;
            }

            if (_worldSignPoleMaterial == null)
                _worldSignPoleMaterial = GetWorldVisualMaterial(new Color32(230, 230, 230, 255));

            if (_worldSignPlateMaterial == null)
                _worldSignPlateMaterial = GetWorldVisualMaterial(new Color32(72, 235, 104, 255));

            if (_departureBoardFrameMaterial == null)
                _departureBoardFrameMaterial = GetWorldVisualMaterial(new Color32(18, 28, 24, 255));

            if (_departureBoardScreenMaterial == null)
                _departureBoardScreenMaterial = GetWorldVisualMaterial(new Color32(6, 16, 14, 255));

            if (_departureBoardGlassMaterial == null)
                _departureBoardGlassMaterial = GetWorldVisualMaterial(new Color32(24, 54, 48, 255));

            if (_departureBoardHeaderMaterial == null)
                _departureBoardHeaderMaterial = GetWorldVisualMaterial(new Color32(70, 230, 108, 255));

            if (_departureBoardRowMaterial == null)
                _departureBoardRowMaterial = GetWorldVisualMaterial(new Color32(170, 220, 190, 255));

            if (_departureBoardDueMaterial == null)
                _departureBoardDueMaterial = GetWorldVisualMaterial(new Color32(255, 210, 72, 255));
        }

        private GameObject CreateWorldBusStopSign(VisualBerth berth)
        {
            switch (StopStackerModSettings.BusStopSignStyle)
            {
                case StopStackerPropStyle.Futuristic:
                    return CreateFuturisticBusStopSign(berth);
                case StopStackerPropStyle.OldWorld:
                    return CreateOldWorldBusStopSign(berth);
                case StopStackerPropStyle.None:
                    return null;
                default:
                    return CreateModernBusStopSign(berth);
            }
        }

        private GameObject CreateModernBusStopSign(VisualBerth berth)
        {
            GameObject signRoot = CreateWorldVisualRoot("Stop Stacker Bus Stop Sign", berth.PropPosition, berth.PropAngle);

            GameObject pole = CreateWorldSignPart("Pole", _worldSignPoleMaterial);
            if (pole == null)
            {
                UnityEngine.Object.Destroy(signRoot);
                return null;
            }

            pole.transform.parent = signRoot.transform;
            pole.transform.localPosition = new Vector3(0f, WorldSignPoleHeight * 0.5f, 0f);
            pole.transform.localRotation = Quaternion.identity;
            pole.transform.localScale = new Vector3(WorldSignPoleWidth, WorldSignPoleHeight, WorldSignPoleWidth);

            GameObject plate = CreateWorldSignPart("Plate", _worldSignPlateMaterial);
            if (plate == null)
            {
                UnityEngine.Object.Destroy(signRoot);
                return null;
            }

            plate.transform.parent = signRoot.transform;
            plate.transform.localPosition = new Vector3(0f, WorldSignPlateLift, 0f);
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = new Vector3(WorldSignPlateWidth, WorldSignPlateHeight, WorldSignPlateDepth);

            return signRoot;
        }

        private GameObject CreateFuturisticBusStopSign(VisualBerth berth)
        {
            GameObject signRoot = CreateWorldVisualRoot("Stop Stacker Futuristic Bus Stop Sign", berth.PropPosition, berth.PropAngle);
            Material darkMetal = GetWorldVisualMaterial(new Color32(28, 38, 48, 255));
            Material glass = GetWorldVisualMaterial(new Color32(32, 86, 104, 255));
            Material glow = GetWorldVisualMaterial(new Color32(76, 238, 255, 255));
            Material softGlow = GetWorldVisualMaterial(new Color32(126, 245, 226, 255));

            if (!AddWorldVisualPart(signRoot, "Futuristic Sign Base", darkMetal, new Vector3(0f, 0.05f, 0f), new Vector3(0.48f, 0.1f, 0.28f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Futuristic Sign Mast", darkMetal, new Vector3(0f, 1.25f, 0f), new Vector3(0.08f, 2.5f, 0.08f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Futuristic Sign Panel", glass, new Vector3(0f, 2.15f, 0f), new Vector3(0.62f, 0.82f, 0.055f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Futuristic Sign Core", darkMetal, new Vector3(0f, 2.13f, -0.04f), new Vector3(0.42f, 0.48f, 0.02f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Futuristic Sign Header", glow, new Vector3(0f, 2.48f, -0.05f), new Vector3(0.52f, 0.08f, 0.025f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Futuristic Sign Route Bar", softGlow, new Vector3(0f, 2.18f, -0.055f), new Vector3(0.34f, 0.045f, 0.025f)))
                return DestroyAndReturnNull(signRoot);

            return signRoot;
        }

        private GameObject CreateOldWorldBusStopSign(VisualBerth berth)
        {
            GameObject signRoot = CreateWorldVisualRoot("Stop Stacker Old World Bus Stop Sign", berth.PropPosition, berth.PropAngle);
            Material wood = GetWorldVisualMaterial(new Color32(104, 66, 36, 255));
            Material darkWood = GetWorldVisualMaterial(new Color32(68, 42, 24, 255));
            Material paper = GetWorldVisualMaterial(new Color32(238, 218, 174, 255));
            Material ink = GetWorldVisualMaterial(new Color32(56, 45, 35, 255));

            if (!AddWorldVisualPart(signRoot, "Old World Sign Post", darkWood, new Vector3(0f, 1.1f, 0f), new Vector3(0.16f, 2.2f, 0.16f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Old World Sign Board", wood, new Vector3(0f, 1.92f, 0f), new Vector3(0.98f, 0.68f, 0.12f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Old World Sign Poster", paper, new Vector3(0f, 1.92f, -0.075f), new Vector3(0.74f, 0.46f, 0.02f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Old World Sign Title", ink, new Vector3(0f, 2.07f, -0.09f), new Vector3(0.52f, 0.04f, 0.018f)))
                return DestroyAndReturnNull(signRoot);

            if (!AddWorldVisualPart(signRoot, "Old World Sign Route", ink, new Vector3(0f, 1.86f, -0.092f), new Vector3(0.42f, 0.035f, 0.018f)))
                return DestroyAndReturnNull(signRoot);

            return signRoot;
        }

        private GameObject CreateWorldDepartureBoard(VisualDepartureBoard board)
        {
            switch (StopStackerModSettings.DispatchBoardStyle)
            {
                case StopStackerPropStyle.Futuristic:
                    return CreateFuturisticDepartureBoard(board);
                case StopStackerPropStyle.OldWorld:
                    return CreateOldWorldDepartureBoard(board);
                case StopStackerPropStyle.None:
                    return null;
                default:
                    return CreateModernDepartureBoard(board);
            }
        }

        private GameObject CreateModernDepartureBoard(VisualDepartureBoard board)
        {
            GameObject boardRoot = new GameObject("Stop Stacker Departure Board");
            boardRoot.transform.parent = _worldSignRoot.transform;
            boardRoot.transform.position = board.Position;
            boardRoot.transform.rotation = Quaternion.Euler(0f, board.Angle * Mathf.Rad2Deg, 0f);

            if (!AddDepartureBoardPart(boardRoot, "Base", _worldSignPoleMaterial, new Vector3(0f, DepartureBoardBaseHeight * 0.5f, 0f), new Vector3(DepartureBoardBaseWidth, DepartureBoardBaseHeight, DepartureBoardBaseDepth)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Left Post", _worldSignPoleMaterial, new Vector3(-0.48f, DepartureBoardPoleHeight * 0.5f, 0f), new Vector3(DepartureBoardPoleWidth, DepartureBoardPoleHeight, DepartureBoardPoleWidth)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Right Post", _worldSignPoleMaterial, new Vector3(0.48f, DepartureBoardPoleHeight * 0.5f, 0f), new Vector3(DepartureBoardPoleWidth, DepartureBoardPoleHeight, DepartureBoardPoleWidth)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Frame", _departureBoardFrameMaterial, new Vector3(0f, DepartureBoardFrameLift, 0f), new Vector3(DepartureBoardFrameWidth, DepartureBoardFrameHeight, DepartureBoardFrameDepth)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Screen", _departureBoardScreenMaterial, new Vector3(0f, DepartureBoardFrameLift, DepartureBoardFaceOffset), new Vector3(DepartureBoardScreenWidth, DepartureBoardScreenHeight, DepartureBoardScreenDepth)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Glass Sheen", _departureBoardGlassMaterial, new Vector3(-0.28f, DepartureBoardFrameLift + 0.08f, DepartureBoardFaceOffset - 0.018f), new Vector3(0.32f, DepartureBoardScreenHeight * 0.92f, 0.012f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Header", _departureBoardHeaderMaterial, new Vector3(0f, DepartureBoardFrameLift + 0.35f, DepartureBoardFaceOffset - 0.035f), new Vector3(1.04f, 0.12f, 0.018f)))
                return DestroyAndReturnNull(boardRoot);

            for (int i = 0; i < 4; i++)
            {
                float rowY = DepartureBoardFrameLift + 0.17f - (i * 0.15f);
                float rowWidth = i == 0 ? 0.82f : 0.72f;
                if (!AddDepartureBoardPart(boardRoot, "Route Row " + (i + 1).ToString(), _departureBoardRowMaterial, new Vector3(-0.09f, rowY, DepartureBoardFaceOffset - 0.04f), new Vector3(rowWidth, 0.035f, 0.016f)))
                    return DestroyAndReturnNull(boardRoot);

                if (!AddDepartureBoardPart(boardRoot, "Due Block " + (i + 1).ToString(), _departureBoardDueMaterial, new Vector3(0.47f, rowY, DepartureBoardFaceOffset - 0.045f), new Vector3(0.16f, 0.04f, 0.018f)))
                    return DestroyAndReturnNull(boardRoot);
            }

            if (!AddDepartureBoardPart(boardRoot, "Clock Dot", _departureBoardDueMaterial, new Vector3(0.47f, DepartureBoardFrameLift + 0.35f, DepartureBoardFaceOffset - 0.05f), new Vector3(0.08f, 0.08f, 0.02f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddDepartureBoardPart(boardRoot, "Top Cap", _departureBoardFrameMaterial, new Vector3(0f, DepartureBoardFrameLift + 0.58f, -0.01f), new Vector3(1.46f, 0.1f, 0.18f)))
                return DestroyAndReturnNull(boardRoot);

            return boardRoot;
        }

        private GameObject CreateFuturisticDepartureBoard(VisualDepartureBoard board)
        {
            GameObject boardRoot = CreateWorldVisualRoot("Stop Stacker Futuristic Departure Board", board.Position, board.Angle);
            Material darkMetal = GetWorldVisualMaterial(new Color32(20, 28, 38, 255));
            Material screen = GetWorldVisualMaterial(new Color32(7, 24, 36, 255));
            Material glass = GetWorldVisualMaterial(new Color32(28, 88, 112, 255));
            Material glow = GetWorldVisualMaterial(new Color32(68, 232, 255, 255));
            Material amber = GetWorldVisualMaterial(new Color32(255, 196, 70, 255));

            if (!AddWorldVisualPart(boardRoot, "Futuristic Board Base", darkMetal, new Vector3(0f, 0.04f, 0f), new Vector3(0.8f, 0.08f, 0.34f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Futuristic Board Spine", darkMetal, new Vector3(0f, 0.78f, 0.02f), new Vector3(0.1f, 1.5f, 0.08f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Futuristic Board Screen", screen, new Vector3(0f, 1.35f, -0.045f), new Vector3(1.18f, 1.02f, 0.055f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Futuristic Board Glass", glass, new Vector3(-0.28f, 1.35f, -0.082f), new Vector3(0.22f, 0.92f, 0.012f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Futuristic Board Header", glow, new Vector3(0f, 1.75f, -0.092f), new Vector3(0.92f, 0.08f, 0.018f)))
                return DestroyAndReturnNull(boardRoot);

            for (int i = 0; i < 4; i++)
            {
                float rowY = 1.52f - (i * 0.16f);
                if (!AddWorldVisualPart(boardRoot, "Futuristic Board Route Row " + (i + 1).ToString(), glow, new Vector3(-0.12f, rowY, -0.098f), new Vector3(0.62f, 0.032f, 0.016f)))
                    return DestroyAndReturnNull(boardRoot);

                if (!AddWorldVisualPart(boardRoot, "Futuristic Board Due Block " + (i + 1).ToString(), amber, new Vector3(0.42f, rowY, -0.102f), new Vector3(0.14f, 0.04f, 0.018f)))
                    return DestroyAndReturnNull(boardRoot);
            }

            if (!AddWorldVisualPart(boardRoot, "Futuristic Board Top Rail", glow, new Vector3(0f, 1.92f, -0.02f), new Vector3(1.28f, 0.06f, 0.1f)))
                return DestroyAndReturnNull(boardRoot);

            return boardRoot;
        }

        private GameObject CreateOldWorldDepartureBoard(VisualDepartureBoard board)
        {
            GameObject boardRoot = CreateWorldVisualRoot("Stop Stacker Old World Departure Board", board.Position, board.Angle);
            Material wood = GetWorldVisualMaterial(new Color32(112, 70, 36, 255));
            Material darkWood = GetWorldVisualMaterial(new Color32(66, 42, 24, 255));
            Material paper = GetWorldVisualMaterial(new Color32(236, 217, 174, 255));
            Material ink = GetWorldVisualMaterial(new Color32(54, 44, 34, 255));
            Material redInk = GetWorldVisualMaterial(new Color32(160, 54, 44, 255));

            if (!AddWorldVisualPart(boardRoot, "Old World Board Left Post", darkWood, new Vector3(-0.52f, 0.75f, 0f), new Vector3(0.12f, 1.5f, 0.12f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Old World Board Right Post", darkWood, new Vector3(0.52f, 0.75f, 0f), new Vector3(0.12f, 1.5f, 0.12f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Old World Board Frame", wood, new Vector3(0f, 1.28f, 0f), new Vector3(1.34f, 1f, 0.12f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Old World Board Poster", paper, new Vector3(0f, 1.28f, -0.075f), new Vector3(1.08f, 0.78f, 0.02f)))
                return DestroyAndReturnNull(boardRoot);

            if (!AddWorldVisualPart(boardRoot, "Old World Board Header", redInk, new Vector3(0f, 1.59f, -0.095f), new Vector3(0.72f, 0.06f, 0.018f)))
                return DestroyAndReturnNull(boardRoot);

            for (int i = 0; i < 4; i++)
            {
                float rowY = 1.41f - (i * 0.14f);
                float rowWidth = i == 0 ? 0.74f : 0.62f;
                if (!AddWorldVisualPart(boardRoot, "Old World Board Route Row " + (i + 1).ToString(), ink, new Vector3(-0.08f, rowY, -0.098f), new Vector3(rowWidth, 0.035f, 0.016f)))
                    return DestroyAndReturnNull(boardRoot);

                if (!AddWorldVisualPart(boardRoot, "Old World Board Due Mark " + (i + 1).ToString(), redInk, new Vector3(0.44f, rowY, -0.102f), new Vector3(0.14f, 0.04f, 0.018f)))
                    return DestroyAndReturnNull(boardRoot);
            }

            if (!AddWorldVisualPart(boardRoot, "Old World Board Top Cap", darkWood, new Vector3(0f, 1.82f, 0f), new Vector3(1.44f, 0.08f, 0.16f)))
                return DestroyAndReturnNull(boardRoot);

            return boardRoot;
        }

        private GameObject CreateWorldVisualRoot(string name, Vector3 position, float angle)
        {
            GameObject root = new GameObject(name);
            if (_worldSignRoot != null)
                root.transform.parent = _worldSignRoot.transform;

            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            return root;
        }

        private bool AddWorldVisualPart(GameObject parent, string name, Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject part = CreateWorldSignPart(name, material);
            if (part == null)
                return false;

            part.transform.parent = parent.transform;
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            return true;
        }

        private bool AddDepartureBoardPart(GameObject parent, string name, Material material, Vector3 localPosition, Vector3 localScale)
        {
            return AddWorldVisualPart(parent, "Departure Board " + name, material, localPosition, localScale);
        }

        private static GameObject DestroyAndReturnNull(GameObject gameObject)
        {
            if (gameObject != null)
                UnityEngine.Object.Destroy(gameObject);

            return null;
        }

        private static GameObject CreateWorldSignPart(string name, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Stop Stacker " + name;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;

            return part;
        }

        private Material GetWorldVisualMaterial(Color32 color)
        {
            int key = GetColorKey(color);
            Material material;
            if (_worldVisualMaterials.TryGetValue(key, out material) && material != null)
                return material;

            material = CreateWorldSignMaterial(color);
            _worldVisualMaterials[key] = material;
            return material;
        }

        private static int GetColorKey(Color32 color)
        {
            unchecked
            {
                return ((int)color.r << 24)
                       | ((int)color.g << 16)
                       | ((int)color.b << 8)
                       | color.a;
            }
        }

        private static Material CreateWorldSignMaterial(Color32 color)
        {
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Diffuse");
            Material material = new Material(shader);
            material.color = color;
            return material;
        }

        private static string GetPrefabName(PrefabInfo prefab)
        {
            return prefab != null && !string.IsNullOrEmpty(prefab.name) ? prefab.name : "(unnamed)";
        }

        private bool WorldToGuiPoint(Camera camera, Vector3 world, out Vector2 point)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                point = Vector2.zero;
                return false;
            }

            point = new Vector2(screen.x, Screen.height - screen.y);
            return point.x >= -80f
                   && point.x <= Screen.width + 80f
                   && point.y >= -80f
                   && point.y <= Screen.height + 80f;
        }

        private void EnsureGuiResources()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = LabelFontSize,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip,
                    fixedWidth = LabelWidth,
                    fixedHeight = LabelHeight,
                    stretchWidth = false,
                    stretchHeight = false
                };
                _labelStyle.normal.textColor = Color.white;
            }

            if (_statusTitleStyle == null)
            {
                _statusTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = StatusBubbleTitleFontSize,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    richText = false
                };
                _statusTitleStyle.normal.textColor = Color.white;
            }

            if (_statusLineStyle == null)
            {
                _statusLineStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = StatusBubbleFontSize,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    richText = false
                };
                _statusLineStyle.normal.textColor = Color.white;
            }

            if (_statusMutedStyle == null)
            {
                _statusMutedStyle = new GUIStyle(_statusLineStyle);
                _statusMutedStyle.normal.textColor = new Color(0.82f, 0.96f, 0.86f, 0.96f);
            }

            if (_statusRightStyle == null)
            {
                _statusRightStyle = new GUIStyle(_statusLineStyle)
                {
                    alignment = TextAnchor.MiddleRight
                };
                _statusRightStyle.normal.textColor = _statusMutedStyle.normal.textColor;
            }

            if (_statusToggleStyle == null)
            {
                _statusToggleStyle = new GUIStyle(_statusLineStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fixedWidth = StatusBubbleToggleSize,
                    fixedHeight = StatusBubbleToggleSize,
                    stretchWidth = false,
                    stretchHeight = false
                };
                _statusToggleStyle.normal.textColor = Color.white;
            }
        }

        private struct StopGeometry
        {
            public readonly ushort SegmentId;
            public readonly uint LaneId;
            public readonly NetLane Lane;
            public readonly NetInfo.Lane LaneInfo;
            public readonly bool ReverseLane;
            public readonly float StopOffset;
            public readonly float PropSideOffset;
            public readonly bool HasPitOffset;
            public readonly float FirstBerthLaneOffset;
            public readonly Vector3 FirstBerthPosition;
            public readonly Vector3 DepartureBoardPosition;
            public readonly float DepartureBoardAngle;

            public StopGeometry(
                ushort segmentId,
                uint laneId,
                NetLane lane,
                NetInfo.Lane laneInfo,
                bool reverseLane,
                float stopOffset,
                float propSideOffset,
                bool hasPitOffset,
                float firstBerthLaneOffset,
                Vector3 firstBerthPosition,
                Vector3 departureBoardPosition,
                float departureBoardAngle)
            {
                SegmentId = segmentId;
                LaneId = laneId;
                Lane = lane;
                LaneInfo = laneInfo;
                ReverseLane = reverseLane;
                StopOffset = stopOffset;
                PropSideOffset = propSideOffset;
                HasPitOffset = hasPitOffset;
                FirstBerthLaneOffset = firstBerthLaneOffset;
                FirstBerthPosition = firstBerthPosition;
                DepartureBoardPosition = departureBoardPosition;
                DepartureBoardAngle = departureBoardAngle;
            }
        }

        internal struct StopServiceZone
        {
            public readonly ushort LineId;
            public readonly ushort StopNode;
            public readonly ushort NextStop;
            public readonly ushort SegmentId;
            public readonly uint LaneId;
            public readonly bool ReverseLane;
            public readonly bool HasPitOffset;
            public readonly float StopOffset;
            public readonly float FirstBerthLaneOffset;
            public readonly Vector3 FirstBerthPosition;
            public readonly float ServiceLength;
            public readonly int BerthCount;

            public StopServiceZone(
                ushort lineId,
                ushort stopNode,
                ushort nextStop,
                ushort segmentId,
                uint laneId,
                bool reverseLane,
                bool hasPitOffset,
                float stopOffset,
                float firstBerthLaneOffset,
                Vector3 firstBerthPosition,
                float serviceLength,
                int berthCount)
            {
                LineId = lineId;
                StopNode = stopNode;
                NextStop = nextStop;
                SegmentId = segmentId;
                LaneId = laneId;
                ReverseLane = reverseLane;
                HasPitOffset = hasPitOffset;
                StopOffset = stopOffset;
                FirstBerthLaneOffset = firstBerthLaneOffset;
                FirstBerthPosition = firstBerthPosition;
                ServiceLength = Mathf.Max(BerthSpacing, serviceLength);
                BerthCount = Mathf.Max(1, berthCount);
            }
        }

        private sealed class PassengerRefreshWorkLimiter
        {
            private readonly int _gridCellsPerFrame;
            private readonly int _citizenInspectionsPerFrame;
            private int _gridCellsThisFrame;
            private int _citizenInspectionsThisFrame;

            public int TotalGridCells { get; private set; }
            public int GridCellYields { get; private set; }
            public int TotalCitizenInspections { get; private set; }
            public int CitizenInspectionYields { get; private set; }

            public PassengerRefreshWorkLimiter(int gridCellsPerFrame, int citizenInspectionsPerFrame)
            {
                _gridCellsPerFrame = Mathf.Max(1, gridCellsPerFrame);
                _citizenInspectionsPerFrame = Mathf.Max(1, citizenInspectionsPerFrame);
            }

            public bool ShouldYieldAfterGridCell()
            {
                TotalGridCells++;
                _gridCellsThisFrame++;
                if (_gridCellsThisFrame < _gridCellsPerFrame)
                    return false;

                GridCellYields++;
                ResetFrame();
                return true;
            }

            public bool ShouldYieldAfterCitizenInspection()
            {
                TotalCitizenInspections++;
                _citizenInspectionsThisFrame++;
                if (_citizenInspectionsThisFrame < _citizenInspectionsPerFrame)
                    return false;

                CitizenInspectionYields++;
                ResetFrame();
                return true;
            }

            public void ResetFrame()
            {
                _gridCellsThisFrame = 0;
                _citizenInspectionsThisFrame = 0;
            }
        }

        private sealed class PassengerRefreshRateLimiter
        {
            private readonly float _ratePerSecond;
            private readonly float _maximumTokens;
            private float _availableTokens;
            private float _lastRefillTime;

            public PassengerRefreshRateLimiter(float ratePerSecond, float maximumTokens)
            {
                _ratePerSecond = Mathf.Max(1f, ratePerSecond);
                _maximumTokens = Mathf.Max(1f, maximumTokens);
                _availableTokens = _maximumTokens;
                _lastRefillTime = Time.realtimeSinceStartup;
            }

            public bool TryConsume()
            {
                float now = Time.realtimeSinceStartup;
                float elapsed = Mathf.Max(0f, now - _lastRefillTime);
                _lastRefillTime = now;
                _availableTokens = Mathf.Min(_maximumTokens, _availableTokens + (elapsed * _ratePerSecond));
                if (_availableTokens < 1f)
                    return false;

                _availableTokens -= 1f;
                return true;
            }
        }

        private sealed class PacedWaitingScanResult
        {
            public int WaitingCount;
            public int AssignedCount;
        }

        private sealed class PitStatusBubble
        {
            public readonly List<PitStatusLine> Lines = new List<PitStatusLine>(8);
            public readonly List<ushort> StopNodes = new List<ushort>(4);
            private readonly List<BerthSlot> _berthSlots = new List<BerthSlot>(8);
            public ushort AnchorStop;
            public Vector3 AnchorPosition;
            public string Title;
            public int TotalWaiting;
            public readonly bool Disabled;

            public PitStatusBubble(ushort anchorStop, Vector3 anchorPosition, string title, bool disabled)
            {
                AnchorStop = anchorStop;
                AnchorPosition = anchorPosition;
                Title = string.IsNullOrEmpty(title) ? "Bus Stop" : title;
                Disabled = disabled;
                AddStopNode(anchorStop);
            }

            public void IncludeStop(ushort stopNode, Vector3 position)
            {
                if (AnchorStop == 0)
                    AnchorStop = stopNode;

                AddStopNode(stopNode);
                AnchorPosition = (AnchorPosition + position) * 0.5f;
            }

            private void AddStopNode(ushort stopNode)
            {
                if (stopNode == 0)
                    return;

                for (int i = 0; i < StopNodes.Count; i++)
                {
                    if (StopNodes[i] == stopNode)
                        return;
                }

                StopNodes.Add(stopNode);
            }

            public void SetBerths(List<BerthSlot> berthSlots)
            {
                if (_berthSlots.Count > 0 || berthSlots == null || berthSlots.Count == 0)
                    return;

                for (int i = 0; i < berthSlots.Count; i++)
                    _berthSlots.Add(berthSlots[i]);
            }

            public PitStatusLine GetOrCreateLine(ushort lineId, ushort stopNode, ushort nextStop, string lineName, int routeStopNumber, int totalLineStops)
            {
                for (int i = 0; i < Lines.Count; i++)
                {
                    PitStatusLine existing = Lines[i];
                    if (existing.LineId != lineId || existing.StopNode != stopNode)
                        continue;

                    return existing;
                }

                BerthSlot slot = GetAssignmentSlot(Lines.Count);
                PitStatusLine created = new PitStatusLine(this, lineId, stopNode, nextStop, lineName, routeStopNumber, totalLineStops, slot.BerthNumber, slot.WaitingPosition);
                Lines.Add(created);
                return created;
            }

            private BerthSlot GetAssignmentSlot(int assignmentIndex)
            {
                if (_berthSlots.Count == 0)
                    return new BerthSlot(0, 0u, 0, AnchorPosition, AnchorPosition, 0f);

                return _berthSlots[Mathf.Abs(assignmentIndex) % _berthSlots.Count];
            }
        }

        private sealed class PitStatusLine
        {
            public readonly PitStatusBubble Owner;
            public readonly ushort LineId;
            public readonly ushort StopNode;
            public readonly ushort NextStop;
            public readonly string LineName;
            public readonly string RouteStopLabel;
            public readonly int AssignedBerthNumber;
            public readonly Vector3 AssignedWaitingPosition;
            public int WaitingPassengers;

            public PitStatusLine(
                PitStatusBubble owner,
                ushort lineId,
                ushort stopNode,
                ushort nextStop,
                string lineName,
                int routeStopNumber,
                int totalLineStops,
                int assignedBerthNumber,
                Vector3 assignedWaitingPosition)
            {
                Owner = owner;
                LineId = lineId;
                StopNode = stopNode;
                NextStop = nextStop;
                LineName = string.IsNullOrEmpty(lineName) ? "Line " + lineId.ToString() : lineName;
                RouteStopLabel = FormatRouteStopLabel(routeStopNumber, totalLineStops);
                AssignedBerthNumber = assignedBerthNumber;
                AssignedWaitingPosition = assignedWaitingPosition;
            }

            private static string FormatRouteStopLabel(int routeStopNumber, int totalLineStops)
            {
                int safeTotal = Mathf.Max(1, totalLineStops);
                int safeNumber = Mathf.Clamp(routeStopNumber, 1, safeTotal);
                return safeNumber.ToString() + "/" + safeTotal.ToString();
            }

            public void AddWaiting(int waitingPassengers)
            {
                int clampedWaiting = Mathf.Max(0, waitingPassengers);
                WaitingPassengers += clampedWaiting;
                if (Owner != null)
                    Owner.TotalWaiting += clampedWaiting;
            }

            public bool SetWaiting(int waitingPassengers)
            {
                int clampedWaiting = Mathf.Max(0, waitingPassengers);
                if (WaitingPassengers == clampedWaiting)
                    return false;

                WaitingPassengers = clampedWaiting;
                return true;
            }
        }

        private struct BerthSlot
        {
            public readonly ushort SegmentId;
            public readonly uint LaneId;
            public readonly int BerthNumber;
            public readonly Vector3 MarkerPosition;
            public readonly Vector3 WaitingPosition;
            public readonly float PropAngle;

            public BerthSlot(
                ushort segmentId,
                uint laneId,
                int berthNumber,
                Vector3 markerPosition,
                Vector3 waitingPosition,
                float propAngle)
            {
                SegmentId = segmentId;
                LaneId = laneId;
                BerthNumber = berthNumber;
                MarkerPosition = markerPosition;
                WaitingPosition = waitingPosition;
                PropAngle = propAngle;
            }
        }

        private struct VisualBerth
        {
            public readonly ushort SegmentId;
            public readonly uint LaneId;
            public readonly int BerthNumber;
            public readonly Vector3 MarkerPosition;
            public readonly Vector3 PropPosition;
            public readonly float PropAngle;

            public VisualBerth(
                ushort segmentId,
                uint laneId,
                int berthNumber,
                Vector3 markerPosition,
                Vector3 propPosition,
                float propAngle)
            {
                SegmentId = segmentId;
                LaneId = laneId;
                BerthNumber = berthNumber;
                MarkerPosition = markerPosition;
                PropPosition = propPosition;
                PropAngle = propAngle;
            }
        }

        private struct LegacyNativePropAnchor
        {
            public readonly Vector3 Position;
            public readonly float Angle;
            public readonly bool DisabledStop;

            public LegacyNativePropAnchor(Vector3 position, float angle, bool disabledStop)
            {
                Position = position;
                Angle = angle;
                DisabledStop = disabledStop;
            }
        }

        private struct VisualDepartureBoard
        {
            public readonly ushort SegmentId;
            public readonly Vector3 Position;
            public readonly float Angle;

            public VisualDepartureBoard(ushort segmentId, Vector3 position, float angle)
            {
                SegmentId = segmentId;
                Position = position;
                Angle = angle;
            }
        }
    }
}
