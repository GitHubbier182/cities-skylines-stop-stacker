using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework.UI;
using UnityEngine;

namespace ScratchyBald.CitiesSkylines.UI
{
    internal static class UnifiedTransitLauncherToolbar
    {
        private const string ToolbarName = "UnifiedTransitToolkitLauncherToolbar";
        private const string PositionFileName = "UnifiedTransitToolkitToolbarPosition.json";
        private const float ToolbarHeight = 50f;
        private const float ButtonSize = 42f;
        private const float ButtonGap = 8f;
        private const float ButtonInset = 4f;
        private const float DefaultTopGap = 50f;
        private const int MaxSlots = 6;
        private const string LauncherButtonSuffix = "LauncherButton";
        private const string SlotUserDataPrefix = "UnifiedTransitSlot:";

        private static bool _dragging;
        private static bool _wasDragged;
        private static bool _positionInitialized;
        private static float _lastViewWidth;
        private static float _lastViewHeight;
        private static float _normalizedX;
        private static float _normalizedY;
        private static Vector2 _dragStartMouse;
        private static Vector3 _dragStartPosition;

        [Serializable]
        private class ToolbarPosition
        {
            public bool HasPosition;
            public float X;
            public float Y;
            public bool HasNormalizedPosition;
            public float NormalizedX;
            public float NormalizedY;
            public float ViewWidth;
            public float ViewHeight;
        }

        private static string PositionPath
        {
            get { return Path.Combine(Application.dataPath, PositionFileName); }
        }

        public static UIPanel Current
        {
            get
            {
                UIView view = UIView.GetAView();
                return view == null ? null : UIView.Find<UIPanel>(ToolbarName);
            }
        }

        public static UIPanel GetOrCreate(UIView view)
        {
            if (view == null)
                return null;

            UIPanel toolbar = UIView.Find<UIPanel>(ToolbarName);
            if (toolbar == null)
                toolbar = (UIPanel)view.AddUIComponent(typeof(UIPanel));

            toolbar.name = ToolbarName;
            ApplyToolbarStyle(toolbar);

            ApplySavedOrDefaultPosition(toolbar, view);
            RegisterDragSurface(toolbar);
            RefreshLayout(toolbar);
            toolbar.BringToFront();
            return toolbar;
        }

        public static Vector3 GetButtonPosition(int slot)
        {
            int clampedSlot = Mathf.Clamp(slot, 0, MaxSlots - 1);
            return new Vector3(ButtonInset + (clampedSlot * (ButtonSize + ButtonGap)), ButtonInset);
        }

        public static void RefreshLayout(UIComponent component)
        {
            RefreshLayout(GetToolbar(component));
        }

        public static void RefreshLayoutIfOwned(UIComponent component)
        {
            UIPanel toolbar = GetToolbar(component);
            if (toolbar != null)
                RefreshLayout(toolbar);
        }

        public static void RefreshLayout(UIPanel toolbar)
        {
            if (toolbar == null)
                return;

            var children = toolbar.components;
            bool[] occupiedSlots = new bool[MaxSlots];
            for (int i = 0; i < children.Count; i++)
            {
                UIComponent button = children[i];
                if (!IsLauncherButton(toolbar, button))
                    continue;

                int slot = GetStoredSlot(button);
                if (slot < 0)
                    continue;

                if (!occupiedSlots[slot])
                    occupiedSlots[slot] = true;
                else
                    button.objectUserData = null;
            }

            int highestSlot = -1;
            for (int i = 0; i < children.Count; i++)
            {
                UIComponent button = children[i];
                if (!IsLauncherButton(toolbar, button))
                    continue;

                int slot = GetStoredSlot(button);
                if (slot < 0)
                {
                    slot = ClaimFirstFreeSlot(occupiedSlots);
                    if (slot >= 0)
                    {
                        occupiedSlots[slot] = true;
                        StoreSlot(button, slot);
                    }
                }

                if (slot < 0)
                {
                    button.isVisible = false;
                    continue;
                }

                highestSlot = Mathf.Max(highestSlot, slot);
                button.isVisible = true;
                button.relativePosition = GetButtonPosition(slot);
            }

            toolbar.isVisible = highestSlot >= 0;
            toolbar.width = highestSlot < 0 ? 0f : GetToolbarWidth(highestSlot + 1);
            toolbar.height = highestSlot < 0 ? 0f : ToolbarHeight;
            ReapplyPositionAfterViewChange(toolbar);
            ClampToView(toolbar);
        }

        public static void RegisterDragSurface(UIComponent component)
        {
            if (component == null)
                return;

            UnregisterDragSurface(component);
            component.eventMouseDown += OnDragMouseDown;
            component.eventMouseMove += OnDragMouseMove;
            component.eventMouseUp += OnDragMouseUp;
        }

        public static void UnregisterDragSurface(UIComponent component)
        {
            if (component == null)
                return;

            component.eventMouseDown -= OnDragMouseDown;
            component.eventMouseMove -= OnDragMouseMove;
            component.eventMouseUp -= OnDragMouseUp;
            _dragging = false;
            _wasDragged = false;
        }

        public static bool ConsumeDragClick()
        {
            if (!_wasDragged)
                return false;

            _wasDragged = false;
            return true;
        }

        private static void ApplyToolbarStyle(UIPanel toolbar)
        {
            if (toolbar == null)
                return;

            toolbar.width = Mathf.Max(toolbar.width, GetToolbarWidth(1));
            toolbar.height = Mathf.Max(toolbar.height, ToolbarHeight);
            toolbar.backgroundSprite = "MenuPanel";
            toolbar.color = new Color32(40, 48, 56, 230);
            toolbar.canFocus = true;
            toolbar.isInteractive = true;
        }

        private static float GetToolbarWidth(int buttonCount)
        {
            if (buttonCount <= 0)
                return 0f;

            int clampedCount = Mathf.Clamp(buttonCount, 0, MaxSlots);
            return ButtonInset * 2f + (clampedCount * ButtonSize) + ((clampedCount - 1) * ButtonGap);
        }

        private static bool IsLauncherButton(UIPanel toolbar, UIComponent component)
        {
            return component != null
                   && component.parent == toolbar
                   && component is UIButton
                   && !string.IsNullOrEmpty(component.name)
                   && component.name.EndsWith(LauncherButtonSuffix, StringComparison.Ordinal);
        }

        private static int GetStoredSlot(UIComponent button)
        {
            string value = button == null ? null : button.objectUserData as string;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(SlotUserDataPrefix, StringComparison.Ordinal))
                return -1;

            int slot;
            if (!int.TryParse(value.Substring(SlotUserDataPrefix.Length), out slot))
                return -1;

            return slot >= 0 && slot < MaxSlots ? slot : -1;
        }

        private static void StoreSlot(UIComponent button, int slot)
        {
            if (button != null)
                button.objectUserData = SlotUserDataPrefix + slot.ToString();
        }

        private static int ClaimFirstFreeSlot(bool[] occupiedSlots)
        {
            for (int slot = 0; slot < MaxSlots; slot++)
            {
                if (!occupiedSlots[slot])
                    return slot;
            }

            return -1;
        }

        private static void ApplySavedOrDefaultPosition(UIPanel toolbar, UIView view)
        {
            ToolbarPosition saved = LoadPosition();
            if (saved != null && saved.HasPosition)
            {
                if (saved.HasNormalizedPosition)
                    toolbar.relativePosition = FromNormalizedPosition(saved.NormalizedX, saved.NormalizedY, view);
                else
                    toolbar.relativePosition = new Vector3(saved.X, saved.Y);
            }
            else
                toolbar.relativePosition = new Vector3(Mathf.Max(0f, (view.fixedWidth - GetToolbarWidth(1)) * 0.5f), DefaultTopGap);

            ClampToView(toolbar);
            UpdateNormalizedPosition(toolbar, view);
        }

        private static ToolbarPosition LoadPosition()
        {
            try
            {
                if (!File.Exists(PositionPath))
                    return null;

                return JsonUtility.FromJson<ToolbarPosition>(File.ReadAllText(PositionPath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ScratchyBaldUUI] Failed to load unified toolbar position: " + e.Message);
                return null;
            }
        }

        private static void SavePosition(UIPanel toolbar)
        {
            if (toolbar == null)
                return;

            try
            {
                ToolbarPosition position = new ToolbarPosition
                {
                    HasPosition = true,
                    X = toolbar.relativePosition.x,
                    Y = toolbar.relativePosition.y
                };

                UIView view = UIView.GetAView();
                if (view != null)
                {
                    position.HasNormalizedPosition = true;
                    position.NormalizedX = GetNormalized(toolbar.relativePosition.x, view.fixedWidth);
                    position.NormalizedY = GetNormalized(toolbar.relativePosition.y, view.fixedHeight);
                    position.ViewWidth = view.fixedWidth;
                    position.ViewHeight = view.fixedHeight;
                    UpdateNormalizedPosition(toolbar, view);
                }

                File.WriteAllText(PositionPath, JsonUtility.ToJson(position, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ScratchyBaldUUI] Failed to save unified toolbar position: " + e.Message);
            }
        }

        private static void OnDragMouseDown(UIComponent component, UIMouseEventParameter p)
        {
            UIPanel toolbar = GetToolbar(component);
            if (toolbar == null)
                return;

            _dragging = true;
            _wasDragged = false;
            _dragStartMouse = p.position;
            _dragStartPosition = toolbar.relativePosition;
            toolbar.BringToFront();
        }

        private static void OnDragMouseMove(UIComponent component, UIMouseEventParameter p)
        {
            if (!_dragging)
                return;

            UIPanel toolbar = GetToolbar(component);
            if (toolbar == null)
                return;

            Vector2 delta = p.position - _dragStartMouse;
            if (delta.sqrMagnitude > 9f)
                _wasDragged = true;

            toolbar.relativePosition = new Vector3(_dragStartPosition.x + delta.x, _dragStartPosition.y - delta.y);
            ClampToView(toolbar);
        }

        private static void OnDragMouseUp(UIComponent component, UIMouseEventParameter p)
        {
            if (!_dragging)
                return;

            _dragging = false;
            UIPanel toolbar = GetToolbar(component);
            if (toolbar == null)
                return;

            ClampToView(toolbar);
            UIView view = UIView.GetAView();
            if (view != null)
                UpdateNormalizedPosition(toolbar, view);

            SavePosition(toolbar);
        }

        private static UIPanel GetToolbar(UIComponent component)
        {
            while (component != null)
            {
                if (component.name == ToolbarName)
                    return component as UIPanel;

                component = component.parent;
            }

            return null;
        }

        private static void ClampToView(UIPanel toolbar)
        {
            UIView view = UIView.GetAView();
            if (view == null || toolbar == null)
                return;

            float maxX = Mathf.Max(0f, view.fixedWidth - toolbar.width);
            float maxY = Mathf.Max(0f, view.fixedHeight - toolbar.height);
            toolbar.relativePosition = new Vector3(
                Mathf.Clamp(toolbar.relativePosition.x, 0f, maxX),
                Mathf.Clamp(toolbar.relativePosition.y, 0f, maxY),
                toolbar.relativePosition.z);
        }

        private static void ReapplyPositionAfterViewChange(UIPanel toolbar)
        {
            UIView view = UIView.GetAView();
            if (view == null || toolbar == null)
                return;

            if (!_positionInitialized)
            {
                UpdateNormalizedPosition(toolbar, view);
                return;
            }

            if (_dragging)
                return;

            if (Mathf.Approximately(_lastViewWidth, view.fixedWidth) && Mathf.Approximately(_lastViewHeight, view.fixedHeight))
                return;

            toolbar.relativePosition = FromNormalizedPosition(_normalizedX, _normalizedY, view);
            ClampToView(toolbar);
            UpdateNormalizedPosition(toolbar, view);
        }

        private static Vector3 FromNormalizedPosition(float normalizedX, float normalizedY, UIView view)
        {
            if (view == null)
                return Vector3.zero;

            return new Vector3(
                Mathf.Clamp01(normalizedX) * view.fixedWidth,
                Mathf.Clamp01(normalizedY) * view.fixedHeight);
        }

        private static void UpdateNormalizedPosition(UIPanel toolbar, UIView view)
        {
            if (toolbar == null || view == null)
                return;

            _normalizedX = GetNormalized(toolbar.relativePosition.x, view.fixedWidth);
            _normalizedY = GetNormalized(toolbar.relativePosition.y, view.fixedHeight);
            _lastViewWidth = view.fixedWidth;
            _lastViewHeight = view.fixedHeight;
            _positionInitialized = true;
        }

        private static float GetNormalized(float value, float size)
        {
            if (size <= 1f)
                return 0f;

            return Mathf.Clamp01(value / size);
        }
    }

    internal static class ExternalUnifiedUiBridge
    {
        private const string ApiTypeName = "UnifiedUI.API.UUIAPI";
        private const string MainPanelTypeName = "UnifiedUI.GUI.MainPanel";
        private const string GroupName = "ScratchyBald";

        private static readonly Dictionary<string, UIComponent> RegisteredButtons =
            new Dictionary<string, UIComponent>(StringComparer.Ordinal);

        public static bool TryRegisterButton(
            string name,
            string tooltip,
            UITextureAtlas atlas,
            string normalSprite,
            string hoveredSprite,
            string pressedSprite,
            string disabledSprite,
            Action<bool> onToggle)
        {
            if (string.IsNullOrEmpty(name) || atlas == null || onToggle == null)
                return false;

            UIComponent existing;
            if (RegisteredButtons.TryGetValue(name, out existing) && existing != null)
                return true;

            try
            {
                Type apiType = FindApiType();
                MethodInfo register = FindAtlasRegistrationMethod(apiType);
                if (register == null)
                    return false;

                ParameterInfo[] registrationParameters = register.GetParameters();
                object[] arguments = new object[registrationParameters.Length];
                arguments[0] = name;
                arguments[1] = GroupName;
                arguments[2] = tooltip;
                arguments[3] = atlas;
                arguments[4] = new[] { normalSprite, hoveredSprite, pressedSprite, disabledSprite };
                arguments[5] = onToggle;

                object result = register.Invoke(
                    null,
                    arguments);

                UIComponent button = result as UIComponent;
                if (button == null)
                    return false;

                RegisteredButtons[name] = button;
                Debug.Log("[ScratchyBaldUUI] Registered " + name + " with external Unified UI.");
                return true;
            }
            catch (TargetInvocationException e)
            {
                Exception cause = e.InnerException ?? e;
                Debug.LogWarning("[ScratchyBaldUUI] External Unified UI registration failed for "
                                 + name
                                 + ": "
                                 + cause.Message);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ScratchyBaldUUI] External Unified UI registration failed for "
                                 + name
                                 + ": "
                                 + e.Message);
                return false;
            }
        }

        public static void SetPressed(string name, bool pressed)
        {
            UIComponent button;
            if (string.IsNullOrEmpty(name)
                || !RegisteredButtons.TryGetValue(name, out button)
                || button == null)
            {
                return;
            }

            try
            {
                PropertyInfo property = button.GetType().GetProperty(
                    "IsActive",
                    BindingFlags.Instance | BindingFlags.Public);
                if (property != null && property.CanWrite)
                    property.SetValue(button, pressed, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ScratchyBaldUUI] Failed to synchronize " + name + ": " + e.Message);
            }
        }

        public static void SetEnabled(string name, bool enabled)
        {
            UIComponent button;
            if (string.IsNullOrEmpty(name)
                || !RegisteredButtons.TryGetValue(name, out button)
                || button == null)
            {
                return;
            }

            button.isEnabled = enabled;
        }

        public static void SetVisualState(string name, Color32 color, string tooltip)
        {
            UIComponent button;
            if (string.IsNullOrEmpty(name)
                || !RegisteredButtons.TryGetValue(name, out button)
                || button == null)
            {
                return;
            }

            button.color = color;
            button.tooltip = tooltip ?? string.Empty;
        }

        public static void ReleaseButton(string name)
        {
            UIComponent button;
            if (string.IsNullOrEmpty(name) || !RegisteredButtons.TryGetValue(name, out button))
                return;

            RegisteredButtons.Remove(name);
            if (button != null)
                UnityEngine.Object.Destroy(button.gameObject);
        }

        private static Type FindApiType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type apiType = assemblies[i].GetType(ApiTypeName, false);
                if (apiType != null && HasActiveMainPanel(assemblies[i]))
                    return apiType;
            }

            return null;
        }

        private static bool HasActiveMainPanel(Assembly assembly)
        {
            if (assembly == null)
                return false;

            try
            {
                Type mainPanelType = assembly.GetType(MainPanelTypeName, false);
                if (mainPanelType == null)
                    return false;

                PropertyInfo exists = mainPanelType.GetProperty(
                    "Exists",
                    BindingFlags.Public | BindingFlags.Static);
                if (exists != null && exists.PropertyType == typeof(bool))
                    return (bool)exists.GetValue(null, null);

                PropertyInfo rowInstance = mainPanelType.GetProperty(
                    "RowInstance_",
                    BindingFlags.Public | BindingFlags.Static);
                if (rowInstance == null)
                    return false;

                UnityEngine.Object activePanel =
                    rowInstance.GetValue(null, null) as UnityEngine.Object;
                return activePanel != null;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo FindAtlasRegistrationMethod(Type apiType)
        {
            if (apiType == null)
                return null;

            MethodInfo[] methods = apiType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Register", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if ((parameters.Length == 8 || parameters.Length == 9)
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == typeof(string)
                    && parameters[2].ParameterType == typeof(string)
                    && parameters[3].ParameterType == typeof(UITextureAtlas)
                    && parameters[4].ParameterType == typeof(string[])
                    && parameters[5].ParameterType == typeof(Action<bool>))
                {
                    return method;
                }
            }

            return null;
        }
    }
}
