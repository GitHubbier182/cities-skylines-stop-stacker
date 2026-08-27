using ColossalFramework.UI;
using UnityEngine;
using ExternalUnifiedUiBridge = ScratchyBald.CitiesSkylines.UI.ExternalUnifiedUiBridge;
using UnifiedTransitLauncherToolbar = ScratchyBald.CitiesSkylines.UI.UnifiedTransitLauncherToolbar;

namespace StopStacker
{
    public class StopStackerLauncherButton : UIButton
    {
        private const string ButtonName = "StopStackerLauncherButton";
        private const string IconAtlasName = "StopStackerLauncherAtlas";
        private const string IconSpriteNameOff = "StopStacker_BusStopLauncherIcon_Off";
        private const string IconSpriteNameOn = "StopStacker_BusStopLauncherIcon_On";

        public static StopStackerLauncherButton Instance;
        public static bool Selected;

        private static UITextureAtlas _iconAtlas;

        private UISprite _iconSprite;

        public override void Start()
        {
            base.Start();

            Instance = this;
            name = ButtonName;
            width = 42f;
            height = 42f;
            text = string.Empty;
            tooltip = "Stop Stacker";
            canFocus = true;
            isInteractive = true;
            isVisible = true;

            normalBgSprite = "ButtonMenu";
            hoveredBgSprite = "ButtonMenuHovered";
            pressedBgSprite = "ButtonMenuPressed";
            disabledBgSprite = "ButtonMenuDisabled";

            relativePosition = UnifiedTransitLauncherToolbar.GetButtonPosition(0);
            AddLauncherIcon();
            UnifiedTransitLauncherToolbar.RegisterDragSurface(this);
            UnifiedTransitLauncherToolbar.RefreshLayout(this);
            BringToFront();
            UpdateVisualState();

            eventClick += OnLauncherClicked;
        }

        public override void OnDestroy()
        {
            UIComponent toolbar = parent;
            eventClick -= OnLauncherClicked;
            UnifiedTransitLauncherToolbar.UnregisterDragSurface(this);

            if (Instance == this)
                Instance = null;

            base.OnDestroy();
            UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
        }

        public static void CreateIfNeeded(UIView view)
        {
            if (view == null || Instance != null)
                return;

            UITextureAtlas iconAtlas = GetOrCreateIconAtlas();
            if (ExternalUnifiedUiBridge.TryRegisterButton(
                    ButtonName,
                    "Stop Stacker",
                    iconAtlas,
                    IconSpriteNameOff,
                    IconSpriteNameOff,
                    IconSpriteNameOn,
                    IconSpriteNameOff,
                    SetSelected))
            {
                ExternalUnifiedUiBridge.SetPressed(ButtonName, Selected);
                return;
            }

            UIPanel toolbar = UnifiedTransitLauncherToolbar.GetOrCreate(view);
            if (toolbar == null)
                return;

            StopStackerLauncherButton existing = toolbar.Find<StopStackerLauncherButton>(ButtonName);
            if (existing != null)
            {
                Instance = existing;
                existing.isVisible = true;
                existing.UpdateVisualState();
                UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
                return;
            }

            UIComponent component = toolbar.AddUIComponent(typeof(StopStackerLauncherButton));
            if (component != null)
            {
                component.name = ButtonName;
                component.isVisible = true;
            }

            UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
        }

        public static void DestroyInstance()
        {
            ExternalUnifiedUiBridge.ReleaseButton(ButtonName);
            if (Instance == null)
            {
                Selected = false;
                return;
            }

            UIPanel toolbar = UnifiedTransitLauncherToolbar.Current;
            Instance.isVisible = false;
            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
            Selected = false;
            UnifiedTransitLauncherToolbar.RefreshLayout(toolbar);
        }

        public static void SetSelected(bool selected)
        {
            if (Selected == selected)
                return;

            Selected = selected;
            if (Instance != null)
                Instance.UpdateVisualState();
            ExternalUnifiedUiBridge.SetPressed(ButtonName, Selected);

            StopStackerBerthOverlay.SetVisible(Selected);
            StopStackerDiagnostics.Advanced("[StopStacker] LAUNCHER_TRIGGER_SELECTED: selected=" + Selected + " action=none");
        }

        private void OnLauncherClicked(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (UnifiedTransitLauncherToolbar.ConsumeDragClick())
                return;

            SetSelected(!Selected);
        }

        private void UpdateVisualState()
        {
            ApplyButtonBackground(Selected);
            tooltip = Selected ? "Stop Stacker - selected" : "Stop Stacker";

            if (_iconSprite != null)
                _iconSprite.spriteName = Selected ? IconSpriteNameOn : IconSpriteNameOff;
            else
            {
                text = "Pit";
                textColor = Selected ? new Color32(88, 255, 126, 255) : new Color32(255, 255, 255, 255);
                hoveredTextColor = textColor;
                pressedTextColor = Selected ? new Color32(72, 230, 108, 255) : new Color32(220, 220, 220, 255);
            }
        }

        private void ApplyButtonBackground(bool selected)
        {
            ApplyDefaultButtonBackground();

            if (!selected)
                return;

            hoveredBgSprite = normalBgSprite;
            pressedBgSprite = normalBgSprite;
            focusedBgSprite = normalBgSprite;
            disabledBgSprite = normalBgSprite;
        }

        private void ApplyDefaultButtonBackground()
        {
            UIView view = UIView.GetAView();
            if (view != null && view.defaultAtlas != null)
                atlas = view.defaultAtlas;

            normalBgSprite = "ButtonMenu";
            hoveredBgSprite = "ButtonMenuHovered";
            pressedBgSprite = "ButtonMenuPressed";
            focusedBgSprite = "ButtonMenuHovered";
            disabledBgSprite = "ButtonMenuDisabled";
            color = new Color32(255, 255, 255, 255);
            hoveredColor = new Color32(255, 255, 255, 255);
            pressedColor = new Color32(255, 255, 255, 255);
            focusedColor = new Color32(255, 255, 255, 255);
        }

        private void AddLauncherIcon()
        {
            UITextureAtlas iconAtlas = GetOrCreateIconAtlas();
            if (iconAtlas == null)
            {
                text = "Pit";
                textScale = 0.68f;
                return;
            }

            _iconSprite = AddUIComponent<UISprite>();
            _iconSprite.atlas = iconAtlas;
            _iconSprite.spriteName = IconSpriteNameOff;
            _iconSprite.width = 30f;
            _iconSprite.height = 30f;
            _iconSprite.relativePosition = new Vector3(6f, 6f);
            _iconSprite.isInteractive = false;
        }

        private static UITextureAtlas GetOrCreateIconAtlas()
        {
            if (_iconAtlas != null)
                return _iconAtlas;

            UIView view = UIView.GetAView();
            if (view == null || view.defaultAtlas == null || view.defaultAtlas.material == null)
                return null;

            Texture2D texture = CreateBusStopIconTexture();
            Material material = new Material(view.defaultAtlas.material);
            material.mainTexture = texture;

            _iconAtlas = ScriptableObject.CreateInstance<UITextureAtlas>();
            _iconAtlas.name = IconAtlasName;
            _iconAtlas.material = material;
            AddAtlasSprite(_iconAtlas, texture, IconSpriteNameOff, 0, 0, 32, 32, null);
            AddAtlasSprite(_iconAtlas, texture, IconSpriteNameOn, 32, 0, 32, 32, null);

            return _iconAtlas;
        }

        private static void AddAtlasSprite(UITextureAtlas atlas, Texture2D texture, string name, int x, int y, int width, int height, RectOffset border)
        {
            atlas.AddSprite(new UITextureAtlas.SpriteInfo
            {
                name = name,
                texture = texture,
                region = new Rect(
                    (float)x / texture.width,
                    (float)y / texture.height,
                    (float)width / texture.width,
                    (float)height / texture.height),
                border = border ?? new RectOffset()
            });
        }

        private static Texture2D CreateBusStopIconTexture()
        {
            const int iconSpriteSize = 32;
            const int textureWidth = 64;
            const int textureHeight = 32;
            Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
            Color32[] pixels = new Color32[textureWidth * textureHeight];
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            DrawBusStopIcon(pixels, textureWidth, 0, new Color32(112, 130, 142, 255));
            DrawBusStopIcon(pixels, textureWidth, iconSpriteSize, new Color32(84, 236, 112, 255));
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static void DrawBusStopIcon(Color32[] pixels, int textureWidth, int xOffset, Color32 border)
        {
            Color32 sign = new Color32(245, 248, 250, 255);
            Color32 post = new Color32(132, 222, 206, 255);
            Color32 bus = new Color32(62, 184, 203, 255);
            Color32 glass = new Color32(33, 39, 45, 255);
            Color32 wheel = new Color32(24, 28, 32, 255);

            FillRect(pixels, textureWidth, xOffset + 3, 3, 26, 2, border);
            FillRect(pixels, textureWidth, xOffset + 3, 27, 26, 2, border);
            FillRect(pixels, textureWidth, xOffset + 3, 3, 2, 26, border);
            FillRect(pixels, textureWidth, xOffset + 27, 3, 2, 26, border);

            FillRect(pixels, textureWidth, xOffset + 15, 7, 3, 18, post);
            FillRect(pixels, textureWidth, xOffset + 10, 7, 13, 8, sign);
            FillRect(pixels, textureWidth, xOffset + 12, 9, 3, 4, bus);
            FillRect(pixels, textureWidth, xOffset + 17, 9, 4, 4, glass);

            FillRect(pixels, textureWidth, xOffset + 8, 18, 17, 7, bus);
            FillRect(pixels, textureWidth, xOffset + 10, 19, 5, 3, sign);
            FillRect(pixels, textureWidth, xOffset + 17, 19, 5, 3, sign);
            FillRect(pixels, textureWidth, xOffset + 10, 25, 3, 2, wheel);
            FillRect(pixels, textureWidth, xOffset + 20, 25, 3, 2, wheel);
        }

        private static void FillRect(Color32[] pixels, int textureWidth, int x, int y, int width, int height, Color32 color)
        {
            int textureHeight = pixels.Length / textureWidth;
            int maxX = Mathf.Min(textureWidth, x + width);
            int maxY = Mathf.Min(textureHeight, y + height);

            for (int row = Mathf.Max(0, y); row < maxY; row++)
            {
                for (int col = Mathf.Max(0, x); col < maxX; col++)
                    pixels[(row * textureWidth) + col] = color;
            }
        }
    }
}
