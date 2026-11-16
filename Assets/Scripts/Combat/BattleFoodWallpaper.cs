using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class BattleFoodWallpaper : MonoBehaviour
{
    [Serializable]
    struct Food
    {
        public Texture2D texture;
        // Normalized bounds of the drawing inside its original, transparent PNG.
        public Rect uv;
        public float artworkAspect;
    }

    [Tooltip("底板占整个屏幕高度的比例。0.333 表示下面三分之一。")]
    [Range(0f, 1f), SerializeField] float bottomSplit = 0.333f;
    [SerializeField] Texture2D gradientStrip;
    [Min(1f), SerializeField] float gradientCycleSeconds = 9f;
    [SerializeField] Food[] foods;
    [Range(0f, 1f), SerializeField] float foodOpacity = .42f;
    [Min(0f), SerializeField] float scrollSpeed = 16f;

    [Header("Wallpaper Frame")]
    [Tooltip("底部横幅。形状内部实心，外部透明。")]
    [SerializeField] Sprite boardMask;
    [Tooltip("与 Board Mask 使用同一裁切范围，只有描边和装饰可见。")]
    [SerializeField] Sprite boardOutline;
    [Tooltip("留空时黑边在舞台人物后面。指定主界面的 Overlay Canvas 会把黑边画到人物前面。")]
    [SerializeField] Canvas outlineCanvas;
    [Tooltip("拖入中央蝴蝶结的透明 Sprite。它会和甜品背板一起显示在舞台人物后面。")]
    [SerializeField] Sprite boardBow;
    [Tooltip("蝴蝶结顶边在背板内的高度。0 是底部，1 是顶部，调大可上移。")]
    [Range(0f, 1f), SerializeField] float bowTop = .85f;
    [Tooltip("蝴蝶结宽度占背板宽度的比例，图片自动保持原比例。")]
    [Range(.01f, .3f), SerializeField] float bowWidthFraction = .074f;

    const float Height = 300f;
    const float Margin = 64f;
    const int Rows = 3;
    readonly System.Random random = new System.Random();

    sealed class Tile
    {
        public RawImage image;
        public Vector2 position;
        public float turnIn;
        public int tilt;
    }

    Camera stage;
    RectTransform panel;
    RectTransform content;
    RectTransform hudOutline;
    Image bow;
    RawImage gradient;
    float gradientPhase;
    Tile[] tiles;
    float width;
    int columns;

    void Start()
    {
        if (!gradientStrip || foods == null || foods.Length == 0 ||
            Array.Exists(foods, f => !f.texture || f.uv.width <= 0 || f.uv.height <= 0 || f.artworkAspect <= 0))
        {
            Debug.LogWarning("Battle food wallpaper is missing its gradient or food images.", this);
            enabled = false;
            return;
        }

        stage = GetComponent<Camera>();
        var board = new GameObject("Food wallpaper", typeof(RectTransform), typeof(Canvas));
        board.layer = LayerMask.NameToLayer("Stage");
        panel = (RectTransform)board.transform;
        panel.SetParent(transform, false);
        var canvas = board.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = stage;
        canvas.sortingOrder = -32000;
        CreateFrame();

        var paper = new GameObject("Scrolling gradient", typeof(RectTransform), typeof(RawImage));
        paper.layer = board.layer;
        paper.transform.SetParent(content, false);
        paper.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        gradient = paper.GetComponent<RawImage>();
        gradient.raycastTarget = false;
        gradient.texture = gradientStrip;
        if (boardBow)
        {
            // The static bow belongs to the stage, outside the moving, masked content.
            bow = CreateFrameImage("Food bow", panel, boardBow);
            var rect = bow.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.pivot = new Vector2(.5f, 1f);
        }
        Advance(0f);
    }

    void CreateFrame()
    {
        content = panel;
        if (!boardMask)
        {
            panel.gameObject.AddComponent<RectMask2D>();
            return;
        }

        var shape = CreateFrameImage("Food mask", panel, boardMask);
        content = shape.rectTransform;
        shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        if (!boardOutline) return;
        if (outlineCanvas && outlineCanvas.isRootCanvas &&
            outlineCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
            outlineCanvas.targetDisplay == stage.targetDisplay)
        {
            hudOutline = CreateFrameImage("Food outline", outlineCanvas.transform, boardOutline).rectTransform;
            hudOutline.SetAsFirstSibling();
        }
        else
        {
            // The outline is a later sibling of the mask, so its outer stroke stays visible.
            CreateFrameImage("Food outline", panel, boardOutline);
        }
    }

    static Image CreateFrameImage(string objectName, Transform parent, Sprite sprite)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.layer = parent.gameObject.layer;
        var image = obj.GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

    void FitBow(float panelWidth)
    {
        float bowWidth = panelWidth * bowWidthFraction;
        Rect sprite = bow.sprite.rect;
        var size = new Vector2(bowWidth, bowWidth * sprite.height / sprite.width);
        var top = new Vector2(0f, Height * bowTop);
        var rect = bow.rectTransform;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (rect.anchoredPosition != top) rect.anchoredPosition = top;
    }

    bool FitPanel()
    {
        // The wallpaper always fills the bottom part of the stage camera.
        // The split is set here instead of being read from the golf camera,
        // which is now an overlay camera without its own viewport rect.
        Rect viewport = stage.rect;
        float split = bottomSplit;
        bool hasArea = stage.pixelWidth > 0 && stage.pixelHeight * split >= 1f;
        if (hudOutline)
        {
            bool visible = hasArea && stage.isActiveAndEnabled && panel.gameObject.activeInHierarchy;
            if (hudOutline.gameObject.activeSelf != visible)
                hudOutline.gameObject.SetActive(visible);
            // A root overlay Canvas uses the same normalized display coordinates as Camera.rect.
            Vector2 min = viewport.min;
            Vector2 max = new Vector2(viewport.xMax, Mathf.Lerp(viewport.yMin, viewport.yMax, split));
            if (hudOutline.anchorMin != min) hudOutline.anchorMin = min;
            if (hudOutline.anchorMax != max) hudOutline.anchorMax = max;
        }
        if (!hasArea)
        {
            panel.localScale = Vector3.zero;
            return false;
        }

        float depth = Mathf.Lerp(stage.nearClipPlane, stage.farClipPlane, .75f);
        var a = transform.InverseTransformPoint(stage.ViewportToWorldPoint(new Vector3(0, 0, depth)));
        var b = transform.InverseTransformPoint(stage.ViewportToWorldPoint(new Vector3(1, split, depth)));
        float scale = Mathf.Max(.00001f, (b.y - a.y) / Height);
        float newWidth = (b.x - a.x) / scale;
        panel.localPosition = (a + b) * .5f;
        panel.localScale = new Vector3(scale, scale, scale);
        if (bow) FitBow(newWidth);
        if (Mathf.Abs(width - newWidth) < .01f && tiles != null) return true;

        float oldSpan = width + 2f * Margin;
        width = newWidth;
        panel.sizeDelta = new Vector2(width, Height);
        // At 45 degrees both projected panel spans are (width + height) / sqrt(2).
        gradient.rectTransform.sizeDelta = Vector2.one * ((width + Height) / Mathf.Sqrt(2f) + 2f);
        int count = Mathf.Max(1, Mathf.RoundToInt((width + 2f * Margin) / 205f));
        if (tiles != null && count == columns)
        {
            float ratio = (width + 2f * Margin) / oldSpan;
            foreach (var tile in tiles) tile.position.x *= ratio;
            return true;
        }

        if (tiles != null)
            foreach (var tile in tiles)
            {
                tile.image.gameObject.SetActive(false);
                Destroy(tile.image.gameObject);
            }
        columns = count;
        tiles = new Tile[Rows * columns];
        Vector2 span = new Vector2(width, Height) + Vector2.one * (2f * Margin);
        for (int row = 0; row < Rows; row++)
            for (int column = 0; column < columns; column++)
            {
                var item = new GameObject("Food", typeof(RectTransform), typeof(RawImage));
                item.layer = panel.gameObject.layer;
                item.transform.SetParent(content, false);
                var tile = new Tile { image = item.GetComponent<RawImage>() };
                tile.image.raycastTarget = false;
                float x = (column + .25f + (row % 2) * .5f + Range(-.12f, .12f)) * span.x / columns;
                float y = (row + .5f + Range(-.14f, .14f)) * span.y / Rows;
                tile.position = new Vector2(x - span.x / 2f, y - span.y / 2f);
                PickFood(tile);
                tiles[row * columns + column] = tile;
            }
        // Without a shaped mask the tiles and bow share a parent.
        if (bow && content == panel) bow.transform.SetAsLastSibling();
        return true;
    }

    void PickFood(Tile tile)
    {
        Food food = foods[random.Next(foods.Length)];
        tile.image.texture = food.texture;
        tile.image.uvRect = food.uv;
        float side = Range(66f, 88f);
        tile.image.rectTransform.sizeDelta = food.artworkAspect >= 1f
            ? new Vector2(side, side / food.artworkAspect)
            : new Vector2(side * food.artworkAspect, side);
        tile.tilt = 0;
        tile.turnIn = Range(.7f, 1.5f);
        tile.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    void LateUpdate() => Advance(Time.deltaTime);

    void Advance(float delta)
    {
        if (!FitPanel()) return;
        gradientPhase = Mathf.Repeat(gradientPhase + delta / Mathf.Max(1f, gradientCycleSeconds), 1f);
        // Sample the centre row. Half the strip is one complete palette cycle.
        // Increasing U on a -45 degree plane makes each band travel up and left.
        gradient.uvRect = new Rect(.125f + .5f * gradientPhase, .5f, .25f, 0f);
        float halfX = width * .5f + Margin;
        const float halfY = Height * .5f + Margin;
        Vector2 drift = new Vector2(-1f, 1f) * (scrollSpeed * delta);
        foreach (var tile in tiles)
        {
            tile.position += drift;
            if (tile.position.x < -halfX || tile.position.y > halfY)
            {
                tile.position.x = Mathf.Repeat(tile.position.x + halfX, 2f * halfX) - halfX;
                tile.position.y = Mathf.Repeat(tile.position.y + halfY, 2f * halfY) - halfY;
                PickFood(tile);
            }
            tile.turnIn -= delta;
            while (tile.turnIn <= 0f)
            {
                // From either limit the next 45-degree step must return to neutral.
                tile.tilt = tile.tilt == 0 ? (random.Next(2) == 0 ? -1 : 1) : 0;
                tile.image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + tile.tilt * 45f);
                tile.turnIn += Range(.7f, 1.5f);
            }
            tile.image.rectTransform.anchoredPosition = tile.position;
            tile.image.color = new Color(1f, 1f, 1f, foodOpacity);
        }
    }

    void SetWallpaperVisible(bool visible)
    {
        if (panel) panel.gameObject.SetActive(visible);
        if (hudOutline) hudOutline.gameObject.SetActive(visible && stage.isActiveAndEnabled);
    }

    void OnDisable() => SetWallpaperVisible(false);
    void OnEnable() => SetWallpaperVisible(true);

    void OnDestroy()
    {
        if (panel) Destroy(panel.gameObject);
        if (hudOutline) Destroy(hudOutline.gameObject);
    }
}