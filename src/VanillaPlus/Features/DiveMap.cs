using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VanillaPlus.Features;

// A dive map you switch on when lost: Off -> round corner map -> full level map -> Off.
//
// How the picture is made is adapted from the DiveMap feature of WhiteMinds/dave-diver-expansion (MIT):
// https://github.com/WhiteMinds/dave-diver-expansion
// A second camera copied from the game's own is drawn by hand into a small texture every few frames, and
// the level size comes from the game's camera sub-bounds. Fish, chest, item and ore markers, mouse pan and
// zoom and the legend are left out on purpose: only Dave and the ways out are marked.
//
// The look is built to sit next to the game's own dive HUD. The corner map is a round instrument like the
// oxygen dial (same navy disc and steel-blue ring, colours sampled from the game's HUD art), Dave is marked
// with the game's own pink "this character" arrow and exits with its boat icon. Those two sprites are looked
// up from what the game already has loaded; nothing from the game is shipped with the mod.
//
// While the map is Off nothing is rendered, so it costs nothing.
public enum DiveMapMode { Off, Mini, Big }

public enum DiveMapCorner { TopRight, TopLeft, BottomRight, BottomLeft }

public enum DiveMapControllerToggle { None, BothStickClicks, SelectPlusRightStickClick }

internal static class DiveMap
{
    private const int RenderEveryNFrames = 3;
    private const int BigTextureSize = 1024;
    private const float ScanInterval = 1f;
    private const float AppearSeconds = 0.14f;
    // The oxygen dial sits 56 units in from the side of the screen; the map mirrors that.
    private const float CornerMarginX = 110f;   // clear of the mission icon the game pins to the right edge
    private const float CornerMarginY = 44f;
    private const float RimRadius = 0.78f;      // where an off-map exit is pinned, as a share of the dial radius
    private const float InsideRadius = 0.84f;   // exits nearer than this are drawn at their real spot
    private const int MaxErrorLogs = 5;

    // Sampled from the game's oxygen dial sprites (UI_O2_Frame_re, UI_O2_Stroke_re, UI_O2_Frame_New_02).
    private static readonly Color32 Navy = new(0x18, 0x28, 0x38, 0xff);
    private static readonly Color NavyColor = new(0x18 / 255f, 0x28 / 255f, 0x38 / 255f, 1f);
    private static readonly Color32 Steel = new(0x3b, 0x5d, 0x7b, 0xff);
    private static readonly Color32 SteelLight = new(0x48, 0x88, 0xa8, 0xff);
    private static readonly Color32 Shade = new(0x0c, 0x16, 0x20, 0xff);

    private const string DaveSpriteName = "UI_Character_Focus";
    private const string ExitSpriteName = "UI_Ingame_Exit_Icon_01";

    private static DiveMapMode mode;
    private static bool modeLoaded;
    private static DiveMapMode shownMode;
    private static float appear;

    private static GameObject canvasObject;
    private static Canvas canvas;
    private static Image dim;
    private static RectTransform root;
    private static CanvasGroup rootGroup;
    private static Image shadow;
    private static Image body;
    private static RawImage mapImage;
    private static RectTransform markerPanel;
    private static Image frame;
    private static Image daveMarker;
    private static Image rimMarker;
    private static readonly List<Image> exitMarkers = new();
    private static readonly List<Image> fishMarkers = new();
    private static readonly List<(Transform at, bool threeStar)> fishShown = new();
    private static readonly Dictionary<int, int> gradeBySpecies = new();
    private static float nextFishScan, nextGradeRefresh;
    private static int fishLogs;
    private const float StickComboWindow = 0.35f;
    private static float lastLeftClick = -1f, lastRightClick = -1f;

    private static Sprite discSprite, ringSprite, panelSprite, panelFrameSprite, dotSprite;
    private static Sprite daveSprite, exitSprite;

    private static Camera mapCamera;
    private static RenderTexture texture;
    private static int textureSize;

    private static int levelKey;
    private static Vector2 boundsMin, boundsMax;
    private static Vector2 viewMin, viewMax;
    private static float levelAspect = 1f;
    private static float cameraZ;

    private static readonly List<Vector3> exits = new();
    private static readonly List<Renderer> headlightOverlays = new();
    private static readonly List<bool> overlayWasEnabled = new();
    private static bool overlaysFound;
    private static float nextScan;
    private static int frameCounter;
    private static int errorLogs;
    private static int lastTickFrame = -1;
    private static string lastState = "";
    private static int inputLogs;
    private static bool tickedByBehaviour, tickedByPlayer;

    public static void Start()
    {
        ClassInjector.RegisterTypeInIl2Cpp<DiveMapBehaviour>();
        var host = new GameObject("VanillaPlus_DiveMap");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<DiveMapBehaviour>();
    }

    // Called from the injected behaviour and, as a backup, from the player's own Update; runs once per frame.
    internal static void Tick(bool fromPlayer = false)
    {
        if (fromPlayer && !tickedByPlayer)
        {
            tickedByPlayer = true;
            Plugin.Logger.LogInfo("DiveMap: receiving frames from the player update");
        }
        else if (!fromPlayer && !tickedByBehaviour)
        {
            tickedByBehaviour = true;
            Plugin.Logger.LogInfo("DiveMap: receiving frames from its own behaviour");
        }

        int frameNumber = Time.frameCount;
        if (frameNumber == lastTickFrame) return;
        lastTickFrame = frameNumber;

        try
        {
            Run();
        }
        catch (Exception e)
        {
            if (errorLogs++ < MaxErrorLogs) Plugin.Logger.LogError($"DiveMap: {e}");
        }
    }

    private static void Run()
    {
        if (!modeLoaded)
        {
            mode = ModConfig.DiveMapStartMode.Value;
            modeLoaded = true;
        }

        var manager = Singleton<InGameManager>._instance;
        PlayerCharacter player = manager != null ? manager.playerCharacter : null;
        Camera mainCamera = player != null ? Camera.main : null;
        if (player != null && mainCamera == null)
        {
            var resolution = Singleton<CameraResolution>._instance;
            if (resolution != null) mainCamera = resolution.MainCamera;
        }
        State(manager == null ? "no dive manager" : player == null ? "no player" : mainCamera == null ? "no main camera" : "in dive");

        // The Sea People Village has its own map.
        string scene = SceneManager.GetActiveScene().name ?? "";
        if (mainCamera == null || scene.Contains("MermanVillage") || scene.StartsWith("MV_"))
        {
            if (canvasObject != null || mapCamera != null) Cleanup();
            return;
        }

        if (TogglePressed())
        {
            mode = mode == DiveMapMode.Off ? DiveMapMode.Mini : mode == DiveMapMode.Mini ? DiveMapMode.Big : DiveMapMode.Off;
            Plugin.Logger.LogInfo($"DiveMap: {mode}");
        }

        if (mode == DiveMapMode.Off || PauseMenuOpen() || player.IsScenarioPlaying || player.IsActionLock)
        {
            if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false);
            shownMode = DiveMapMode.Off;
            return;
        }

        // A new level means a new manager or a new scene; the map camera also dies with its scene.
        int key = manager.GetInstanceID() ^ scene.GetHashCode();
        if (mapCamera == null || canvasObject == null || key != levelKey)
        {
            Cleanup();
            if (!Build(manager, mainCamera)) return;
            levelKey = key;
        }

        if (!canvasObject.activeSelf) canvasObject.SetActive(true);

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanInterval;
            if (!overlaysFound) ScanHeadlightOverlays(player);
        }

        bool big = mode == DiveMapMode.Big;
        if (mode != shownMode)
        {
            shownMode = mode;
            appear = 0f;
            ApplyStyle(big);
        }

        Vector2 size = Layout(big);
        EnsureTexture(big ? BigTextureSize : MiniTextureSize(size.x));
        PlaceCamera(big, player.transform.position);
        Animate(big);

        if (++frameCounter >= RenderEveryNFrames)
        {
            frameCounter = 0;
            Render();
        }

        PlaceMarkers(big, player.transform.position, size);
        PlaceFish(big, size);
    }

    private static void State(string state)
    {
        if (state == lastState) return;
        lastState = state;
        Plugin.Logger.LogInfo($"DiveMap: state '{state}', scene '{SceneManager.GetActiveScene().name}'");
    }

    // Logs the first few raw button presses so a toggle that does nothing can be told apart from one never seen.
    private static void LogInput(string what)
    {
        if (inputLogs++ < 12) Plugin.Logger.LogInfo($"DiveMap: saw {what}");
    }

    private static bool TogglePressed()
    {
        try
        {
            var probe = UnityEngine.InputSystem.Gamepad.current;
            if (probe == null) { if (inputLogs == 0) { inputLogs++; Plugin.Logger.LogInfo("DiveMap: no controller visible to the input system"); } }
            else
            {
                if (probe.leftStickButton.wasPressedThisFrame) LogInput("left stick click");
                if (probe.rightStickButton.wasPressedThisFrame) LogInput("right stick click");
            }
        }
        catch (Exception e)
        {
            if (inputLogs++ < 3) Plugin.Logger.LogWarning($"DiveMap: controller read failed: {e.Message}");
        }
        try
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                LogInput("M key (input system)");
                if (ModConfig.DiveMapToggleKey.Value == KeyCode.M) return true;
            }
        }
        catch (Exception e)
        {
            if (inputLogs++ < 3) Plugin.Logger.LogWarning($"DiveMap: keyboard read failed: {e.Message}");
        }

        try
        {
            if (Input.GetKeyDown(ModConfig.DiveMapToggleKey.Value)) return true;
        }
        catch
        {
            // Legacy input unavailable; the controller path below still works.
        }

        var combo = ModConfig.DiveMapControllerToggleCombo.Value;
        if (combo == DiveMapControllerToggle.None) return false;

        try
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return false;

            var right = pad.rightStickButton;
            if (combo == DiveMapControllerToggle.SelectPlusRightStickClick)
                return pad.selectButton.isPressed && right.wasPressedThisFrame;

            var left = pad.leftStickButton;
            // The two clicks only have to land within a moment of each other, not on the same frame.
            float now = Time.unscaledTime;
            if (left.wasPressedThisFrame) lastLeftClick = now;
            if (right.wasPressedThisFrame) lastRightClick = now;
            if (lastLeftClick < 0f || lastRightClick < 0f || Mathf.Abs(lastLeftClick - lastRightClick) > StickComboWindow) return false;
            if (now - Mathf.Max(lastLeftClick, lastRightClick) > StickComboWindow) return false;
            lastLeftClick = lastRightClick = -1f;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PauseMenuOpen()
    {
        var canvases = Singleton<MainCanvasManager>._instance;
        if (canvases == null) return false;
        var pause = canvases.pausePopupPanel;
        return pause != null && pause.gameObject.activeSelf;
    }

    // ---------------------------------------------------------------- building

    private static bool Build(InGameManager manager, Camera mainCamera)
    {
        // GetBoundary only covers the current camera region; the sub-bounds together cover the whole level.
        Bounds boundary = manager.GetBoundary();
        boundsMin = new Vector2(boundary.min.x, boundary.min.y);
        boundsMax = new Vector2(boundary.max.x, boundary.max.y);

        int merged = 0;
        var subBounds = manager.SubBoundsCollection;
        var list = subBounds != null ? subBounds.m_BoundsList : null;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var sub = list[i];
                if (sub == null) continue;
                Bounds b = sub.Bounds;
                boundsMin = Vector2.Min(boundsMin, new Vector2(b.min.x, b.min.y));
                boundsMax = Vector2.Max(boundsMax, new Vector2(b.max.x, b.max.y));
                merged++;
            }
        }

        Vector2 size = boundsMax - boundsMin;
        if (size.x <= 1f || size.y <= 1f)
        {
            Bounds current = manager.CurrentCameraBounds;
            boundsMin = new Vector2(current.min.x, current.min.y);
            boundsMax = new Vector2(current.max.x, current.max.y);
            size = boundsMax - boundsMin;
        }
        if (size.x <= 1f || size.y <= 1f)
        {
            if (errorLogs++ < MaxErrorLogs) Plugin.Logger.LogWarning($"DiveMap: level bounds not usable ({size.x:0.#} x {size.y:0.#})");
            return false;
        }

        Vector2 pad = size * 0.02f;
        boundsMin -= pad;
        boundsMax += pad;
        size = boundsMax - boundsMin;
        levelAspect = size.x / size.y;
        cameraZ = mainCamera.transform.position.z;

        // Left in the dive scene on purpose, so it is destroyed with the level and the map is rebuilt for the next one.
        var cameraObject = new GameObject("VanillaPlus_MapCamera");
        mapCamera = cameraObject.AddComponent<Camera>();
        mapCamera.CopyFrom(mainCamera);
        mapCamera.rect = new Rect(0f, 0f, 1f, 1f); // CopyFrom brings the game's letterbox rect along
        mapCamera.tag = "Untagged";
        mapCamera.orthographic = true;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = NavyColor;      // beyond the level edge the dial just shows its own face
        mapCamera.depth = -100f;
        mapCamera.enabled = false;                  // drawn by hand in Render()

        textureSize = 0;
        EnsureTexture(256);
        EnsureSprites();
        BuildCanvas();
        ScanExits();

        overlaysFound = false;
        headlightOverlays.Clear();
        nextScan = 0f;
        shownMode = DiveMapMode.Off;

        Plugin.Logger.LogInfo($"DiveMap: built for scene '{SceneManager.GetActiveScene().name}', level {size.x:0.#} x {size.y:0.#} units " +
                              $"({merged} regions), {exits.Count} exits marked, game icons: Dave={(daveSprite != null ? "yes" : "fallback")}, " +
                              $"exit={(exitSprite != null ? "yes" : "fallback")}");
        return true;
    }

    private static int MiniTextureSize(float diameter)
    {
        // Match the texture to the pixels actually covered on screen so the map stays crisp at any resolution.
        float pixels = diameter * (canvas != null ? canvas.scaleFactor : 1f);
        return pixels > 384f ? 512 : 256;
    }

    private static void EnsureTexture(int size)
    {
        if (size == textureSize && texture != null) return;

        mapCamera.targetTexture = null;
        if (texture != null)
        {
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }

        texture = new RenderTexture(size, size, 24);
        texture.useMipMap = false;
        texture.filterMode = FilterMode.Bilinear;
        textureSize = size;

        mapCamera.targetTexture = texture;
        mapCamera.aspect = 1f;
        if (mapImage != null) mapImage.texture = texture;
        frameCounter = RenderEveryNFrames; // draw straight away
    }

    private static void BuildCanvas()
    {
        canvasObject = new GameObject("VanillaPlus_DiveMapCanvas");
        UnityEngine.Object.DontDestroyOnLoad(canvasObject);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        // Soft darkening behind the full map, like the game's own pop-ups.
        var dimObject = UiObject("Dim", canvasObject);
        Fill(dimObject.GetComponent<RectTransform>());
        dim = dimObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.raycastTarget = false;

        var rootObject = UiObject("Map", canvasObject);
        root = rootObject.GetComponent<RectTransform>();
        rootGroup = rootObject.AddComponent<CanvasGroup>();
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        var shadowObject = UiObject("Shadow", rootObject);
        var shadowRect = shadowObject.GetComponent<RectTransform>();
        Fill(shadowRect);
        shadowRect.anchoredPosition = new Vector2(3f, -5f);
        shadow = shadowObject.AddComponent<Image>();
        shadow.color = new Color(0f, 0f, 0f, 0.35f);
        shadow.raycastTarget = false;

        // The body is both the instrument's dark face and the shape the picture is clipped to.
        var bodyObject = UiObject("Face", rootObject);
        Fill(bodyObject.GetComponent<RectTransform>());
        body = bodyObject.AddComponent<Image>();
        body.color = NavyColor;
        body.raycastTarget = false;
        bodyObject.AddComponent<Mask>().showMaskGraphic = true;

        var imageObject = UiObject("Picture", bodyObject);
        Fill(imageObject.GetComponent<RectTransform>());
        mapImage = imageObject.AddComponent<RawImage>();
        mapImage.texture = texture;
        mapImage.raycastTarget = false;

        var panelObject = UiObject("Markers", bodyObject);
        markerPanel = panelObject.GetComponent<RectTransform>();
        Fill(markerPanel);

        var frameObject = UiObject("Rim", rootObject);
        Fill(frameObject.GetComponent<RectTransform>());
        frame = frameObject.AddComponent<Image>();
        frame.raycastTarget = false;

        exitMarkers.Clear();
        rimMarker = Marker("ExitDirection", exitSprite, new Color(0.35f, 0.9f, 0.45f, 1f));
        daveMarker = Marker("Dave", daveSprite, Color.white);
        // The game's arrow points down at a character from above, so its tip is the anchor.
        if (daveSprite != null) daveMarker.rectTransform.pivot = new Vector2(0.5f, 0.04f);
    }

    private static GameObject UiObject(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image Marker(string name, Sprite gameSprite, Color fallbackColor)
    {
        var go = UiObject(name, markerPanel.gameObject);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (gameSprite != null)
        {
            image.sprite = gameSprite;
            image.color = Color.white;
        }
        else
        {
            image.sprite = dotSprite;
            image.color = fallbackColor;
        }
        return image;
    }

    private static void ApplyStyle(bool big)
    {
        Sprite fill = big ? panelSprite : discSprite;
        var type = big ? Image.Type.Sliced : Image.Type.Simple;

        body.sprite = fill;
        body.type = type;
        shadow.sprite = fill;
        shadow.type = type;
        frame.sprite = big ? panelFrameSprite : ringSprite;
        frame.type = type;
    }

    // ---------------------------------------------------------------- sprites

    private static void EnsureSprites()
    {
        if (discSprite == null) discSprite = MakeSprite(256, 0, DiscPixel);
        if (ringSprite == null) ringSprite = MakeSprite(256, 0, RingPixel);
        if (panelSprite == null) panelSprite = MakeSprite(96, 34, (x, y, s) => PanelPixel(x, y, s, false));
        if (panelFrameSprite == null) panelFrameSprite = MakeSprite(96, 34, (x, y, s) => PanelPixel(x, y, s, true));
        if (dotSprite == null) dotSprite = MakeSprite(32, 0, DotPixel);

        // Borrow the game's own icons from the dive HUD atlas, which is loaded for the whole dive.
        if (daveSprite == null || exitSprite == null)
        {
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite == null) continue;
                string name = sprite.name;
                if (daveSprite == null && name == DaveSpriteName) daveSprite = sprite;
                else if (exitSprite == null && name == ExitSpriteName) exitSprite = sprite;
                if (daveSprite != null && exitSprite != null) break;
            }
        }
    }

    private static Sprite MakeSprite(int size, int border, Func<int, int, int, Color32> pixel)
    {
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel(x, y, size);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            tex.SetPixels32(pixels);
        }
        catch
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color32 p = pixels[y * size + x];
                    tex.SetPixel(x, y, new Color(p.r / 255f, p.g / 255f, p.b / 255f, p.a / 255f));
                }
        }
        tex.Apply(false);

        var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Color32 WithAlpha(Color32 color, float alpha) =>
        new(color.r, color.g, color.b, (byte)(Mathf.Clamp01(alpha) * 255f));

    private static float FromCentre(int x, int y, int size)
    {
        float c = (size - 1) * 0.5f;
        return Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
    }

    private static Color32 DiscPixel(int x, int y, int size)
    {
        float edge = size * 0.5f - 1f - FromCentre(x, y, size);
        return WithAlpha(new Color32(255, 255, 255, 255), edge + 0.5f);
    }

    private static Color32 DotPixel(int x, int y, int size)
    {
        float edge = size * 0.5f - 1f - FromCentre(x, y, size);
        if (edge > 3.5f) return new Color32(255, 255, 255, 255);
        return WithAlpha(edge > 0f ? new Color32(20, 28, 38, 255) : new Color32(0, 0, 0, 255), edge + 0.5f);
    }

    // The oxygen dial's rim: a bright hairline, the steel band, then a soft shade falling onto the face.
    private static Color32 RingPixel(int x, int y, int size)
    {
        float inward = size * 0.5f - 1f - FromCentre(x, y, size);   // 0 at the outer edge, growing inward
        float unit = size / 256f;
        float line = 1.5f * unit, band = 7.5f * unit, shade = 6f * unit;

        if (inward < -0.5f) return new Color32(0, 0, 0, 0);
        if (inward < line) return WithAlpha(SteelLight, inward + 0.5f);
        if (inward < band) return Steel;
        if (inward < band + shade) return WithAlpha(Shade, 0.55f * (1f - (inward - band) / shade));
        return new Color32(0, 0, 0, 0);
    }

    // The HUD's cut-corner panel shape, as used by the oxygen dial's tab and the action text box.
    private static Color32 PanelPixel(int x, int y, int size, bool rimOnly)
    {
        const float cut = 22f;
        float left = x + 0.5f, right = size - x - 0.5f, bottom = y + 0.5f, top = size - y - 0.5f;
        float inward = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        inward = Mathf.Min(inward, (left + top - cut) * 0.7071f);
        inward = Mathf.Min(inward, (right + top - cut) * 0.7071f);
        inward = Mathf.Min(inward, (left + bottom - cut) * 0.7071f);
        inward = Mathf.Min(inward, (right + bottom - cut) * 0.7071f);

        if (inward < -0.5f) return new Color32(0, 0, 0, 0);
        if (!rimOnly) return WithAlpha(new Color32(255, 255, 255, 255), inward + 0.5f);

        if (inward < 1.5f) return WithAlpha(SteelLight, inward + 0.5f);
        if (inward < 5f) return Steel;
        if (inward < 9f) return WithAlpha(Shade, 0.5f * (1f - (inward - 5f) / 4f));
        return new Color32(0, 0, 0, 0);
    }

    // ---------------------------------------------------------------- scanning

    // Escape pods and mirrors never move. Far ones are switched off by the game, so inactive objects count too.
    private static void ScanExits()
    {
        exits.Clear();
        try
        {
            foreach (var pod in UnityEngine.Object.FindObjectsOfType<Interaction.Escape.EscapePodZone>(true))
                if (pod != null) exits.Add(pod.transform.position);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"DiveMap: escape pod scan failed: {e.Message}");
        }
        try
        {
            foreach (var mirror in UnityEngine.Object.FindObjectsOfType<Interaction.Escape.EscapeMirror>(true))
                if (mirror != null) exits.Add(mirror.transform.position);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"DiveMap: mirror scan failed: {e.Message}");
        }

        while (exitMarkers.Count < exits.Count)
            exitMarkers.Add(Marker("Exit", exitSprite, new Color(0.35f, 0.9f, 0.45f, 1f)));
        rimMarker.transform.SetAsLastSibling();
        daveMarker.transform.SetAsLastSibling();
    }

    // The dark vignette around Dave's headlight is a big sprite on the player. Left on, it blacks out the map.
    private static void ScanHeadlightOverlays(PlayerCharacter player)
    {
        headlightOverlays.Clear();
        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.gameObject.name.StartsWith("HeadLightOuter"))
                headlightOverlays.Add(renderer);
        }
        overlaysFound = headlightOverlays.Count > 0;
    }

    // ---------------------------------------------------------------- per frame

    private static Vector2 Layout(bool big)
    {
        if (big)
        {
            const float maxWidth = 1920f * 0.8f, maxHeight = 1080f * 0.8f;
            float height = maxHeight, width = height * levelAspect;
            if (width > maxWidth)
            {
                width = maxWidth;
                height = width / levelAspect;
            }

            var middle = new Vector2(0.5f, 0.5f);
            root.anchorMin = middle;
            root.anchorMax = middle;
            root.pivot = middle;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(width, height);

            mapImage.uvRect = levelAspect < 1f
                ? new Rect(0.5f - levelAspect * 0.5f, 0f, levelAspect, 1f)
                : new Rect(0f, 0.5f - 0.5f / levelAspect, 1f, 1f / levelAspect);
            return root.sizeDelta;
        }

        var corner = ModConfig.DiveMapMiniCorner.Value;
        bool right = corner == DiveMapCorner.TopRight || corner == DiveMapCorner.BottomRight;
        bool top = corner == DiveMapCorner.TopRight || corner == DiveMapCorner.TopLeft;
        var anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        float side = ModConfig.DiveMapMiniSize.Value * 1080f;

        root.anchorMin = anchor;
        root.anchorMax = anchor;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2((right ? -1f : 1f) * (CornerMarginX + side * 0.5f),
                                            (top ? -1f : 1f) * (CornerMarginY + side * 0.5f));
        root.sizeDelta = new Vector2(side, side);
        mapImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        return root.sizeDelta;
    }

    private static void PlaceCamera(bool big, Vector3 playerPosition)
    {
        Vector2 size = boundsMax - boundsMin;
        Vector2 center = (boundsMin + boundsMax) * 0.5f;

        if (big)
        {
            // Square texture covering the longer side; Layout() crops it to the level's shape.
            mapCamera.orthographicSize = Mathf.Max(size.x, size.y) * 0.5f;
            mapCamera.transform.position = new Vector3(center.x, center.y, cameraZ);
            viewMin = boundsMin;
            viewMax = boundsMax;
            return;
        }

        // The corner map always keeps Dave in the middle, like a sonar.
        float half = ModConfig.DiveMapMiniRadius.Value;
        mapCamera.orthographicSize = half;
        mapCamera.transform.position = new Vector3(playerPosition.x, playerPosition.y, cameraZ);
        viewMin = new Vector2(playerPosition.x - half, playerPosition.y - half);
        viewMax = new Vector2(playerPosition.x + half, playerPosition.y + half);
    }

    // A short ease-in when the map opens or changes size, as the game's own HUD pieces do.
    private static void Animate(bool big)
    {
        if (appear < 1f) appear = Mathf.Min(1f, appear + Time.unscaledDeltaTime / AppearSeconds);
        float eased = 1f - (1f - appear) * (1f - appear) * (1f - appear);

        rootGroup.alpha = eased;
        float scale = Mathf.Lerp(0.9f, 1f, eased);
        root.localScale = new Vector3(scale, scale, 1f);

        mapImage.color = new Color(1f, 1f, 1f, ModConfig.DiveMapOpacity.Value);
        dim.color = new Color(0f, 0f, 0f, big ? 0.3f * eased : 0f);
    }

    private static void Render()
    {
        overlayWasEnabled.Clear();
        for (int i = 0; i < headlightOverlays.Count; i++)
        {
            var renderer = headlightOverlays[i];
            bool wasEnabled = renderer != null && renderer.enabled;
            overlayWasEnabled.Add(wasEnabled);
            if (wasEnabled) renderer.enabled = false;
        }

        try
        {
            mapCamera.Render();
        }
        finally
        {
            for (int i = 0; i < headlightOverlays.Count; i++)
            {
                if (overlayWasEnabled[i] && headlightOverlays[i] != null) headlightOverlays[i].enabled = true;
            }
        }
    }

    private static void PlaceMarkers(bool big, Vector3 playerPosition, Vector2 panel)
    {
        float scale = big ? 1.2f : 1f;
        Vector2 daveSize = daveSprite != null ? new Vector2(13f, 15f) : new Vector2(9f, 9f);
        Vector2 exitSize = exitSprite != null ? new Vector2(32f, 19f) : new Vector2(11f, 11f);

        Show(daveMarker, Normalized(playerPosition), panel, daveSize * scale, 1f);

        int nearestOutside = -1;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < exitMarkers.Count; i++)
        {
            if (i >= exits.Count)
            {
                Hide(exitMarkers[i]);
                continue;
            }

            Vector2 at = Normalized(exits[i]);
            bool inside = big
                ? at.x >= 0f && at.x <= 1f && at.y >= 0f && at.y <= 1f
                : (at - new Vector2(0.5f, 0.5f)).magnitude * 2f <= InsideRadius;

            if (inside)
            {
                Show(exitMarkers[i], at, panel, exitSize * scale, 1f);
                continue;
            }

            Hide(exitMarkers[i]);
            float distance = ((Vector2)(exits[i] - playerPosition)).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestOutside = i;
            }
        }

        // On the corner map the nearest exit that is out of range sits on the rim, pointing the way.
        if (!big && nearestOutside >= 0)
        {
            Vector2 direction = ((Vector2)(exits[nearestOutside] - playerPosition)).normalized;
            Vector2 at = new Vector2(0.5f, 0.5f) + direction * (RimRadius * 0.5f);
            Show(rimMarker, at, panel, exitSize * 0.85f, 0.9f);
        }
        else
        {
            Hide(rimMarker);
        }
    }

    private static Vector2 Normalized(Vector3 world) =>
        new((world.x - viewMin.x) / (viewMax.x - viewMin.x), (world.y - viewMin.y) / (viewMax.y - viewMin.y));

    private static void Show(Image marker, Vector2 at, Vector2 panel, Vector2 size, float alpha)
    {
        var go = marker.gameObject;
        if (!go.activeSelf) go.SetActive(true);
        var rect = marker.rectTransform;
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(at.x * panel.x, at.y * panel.y);

        Color color = marker.color;
        if (!Mathf.Approximately(color.a, alpha))
        {
            color.a = alpha;
            marker.color = color;
        }
    }

    // Live fish as small dots: bright yellow while you still lack a 3-star catch of that species, faint once
    // you have one. Positions follow the fish every frame; the list itself is rebuilt twice a second.
    private static void PlaceFish(bool big, Vector2 panel)
    {
        if (!ModConfig.DiveMapShowFish.Value)
        {
            for (int i = 0; i < fishMarkers.Count; i++) Hide(fishMarkers[i]);
            return;
        }

        float now = Time.unscaledTime;
        if (now >= nextGradeRefresh)
        {
            nextGradeRefresh = now + 5f;
            gradeBySpecies.Clear();
        }
        if (now >= nextFishScan)
        {
            nextFishScan = now + 0.5f;
            fishShown.Clear();
            foreach (var body in FishRegistry.All)
            {
                if (fishShown.Count >= MaxFishMarkers) break;
                if (body == null || !body.gameObject.activeInHierarchy) continue;
                var owner = body._ownerFish;
                if (owner == null || owner.IsDead()) continue;
                var spec = owner.FishData;
                if (spec == null) continue;

                int species = spec.FishID;
                if (!gradeBySpecies.TryGetValue(species, out int grade))
                {
                    grade = FishCollectionUtility.GetGrade(species);
                    gradeBySpecies[species] = grade;
                    if (fishLogs++ < 25)
                        Plugin.Logger.LogInfo($"DiveMap: fish '{body.gameObject.name}' id {species}, collection grade {grade}, " +
                                              $"caught grade {SaveDataCaughtFishRouter.GetCaughtFishGrade(species)}");
                }
                fishShown.Add((body.transform, grade >= 3));
            }
        }

        var needed = new Color(1f, 0.83f, 0f, 1f);
        var done = new Color(1f, 1f, 1f, 0.45f);
        float size = big ? 8f : 7f;
        int used = 0;
        for (int i = 0; i < fishShown.Count; i++)
        {
            var (at, threeStar) = fishShown[i];
            if (at == null) continue;
            Vector2 where = Normalized(at.position);
            bool inside = big
                ? where.x >= 0f && where.x <= 1f && where.y >= 0f && where.y <= 1f
                : (where - new Vector2(0.5f, 0.5f)).magnitude * 2f <= InsideRadius;
            if (!inside) continue;

            if (used >= fishMarkers.Count)
            {
                var marker = Marker("Fish", null, needed);
                marker.transform.SetAsFirstSibling(); // under the exits and Dave
                fishMarkers.Add(marker);
            }
            var image = fishMarkers[used++];
            var wanted = threeStar ? done : needed;
            if (image.color != wanted) image.color = wanted;
            Show(image, where, panel, new Vector2(threeStar ? size * 0.75f : size, threeStar ? size * 0.75f : size), wanted.a);
        }
        for (int i = used; i < fishMarkers.Count; i++) Hide(fishMarkers[i]);
    }

    private const int MaxFishMarkers = 80;

    private static void Hide(Image marker)
    {
        var go = marker.gameObject;
        if (go.activeSelf) go.SetActive(false);
    }

    private static void Cleanup()
    {
        if (mapCamera != null)
        {
            mapCamera.targetTexture = null;
            UnityEngine.Object.Destroy(mapCamera.gameObject);
        }
        if (texture != null)
        {
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }
        if (canvasObject != null) UnityEngine.Object.Destroy(canvasObject);

        mapCamera = null;
        texture = null;
        textureSize = 0;
        canvasObject = null;
        canvas = null;
        dim = null;
        root = null;
        rootGroup = null;
        shadow = null;
        body = null;
        mapImage = null;
        markerPanel = null;
        frame = null;
        daveMarker = null;
        rimMarker = null;
        exitMarkers.Clear();
        fishMarkers.Clear();
        fishShown.Clear();
        gradeBySpecies.Clear();
        exits.Clear();
        headlightOverlays.Clear();
        overlaysFound = false;
        shownMode = DiveMapMode.Off;
        levelKey = 0;
        // The game's icons are unloaded with the dive; look them up again next time.
        daveSprite = null;
        exitSprite = null;
    }
}

// Only exists to get a per-frame call that keeps running between dives; all the logic is in DiveMap.
public class DiveMapBehaviour : MonoBehaviour
{
    public DiveMapBehaviour(IntPtr ptr) : base(ptr) { }

    private void Update() => DiveMap.Tick();
}

// Backup driver: if the injected behaviour above never gets its Update, the map still runs during dives.
[HarmonyLib.HarmonyPatch(typeof(PlayerCharacter), nameof(PlayerCharacter.Update))]
internal static class DiveMapPlayerTick
{
    [HarmonyLib.HarmonyPostfix]
    private static void Postfix() => DiveMap.Tick(true);
}
