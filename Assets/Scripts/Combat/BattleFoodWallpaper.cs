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

    [SerializeField] Camera golfCamera;
    [SerializeField] Texture2D gradientStrip;
    [Min(1f), SerializeField] float gradientCycleSeconds = 9f;
    [SerializeField] Food[] foods;
    [Range(0f, 1f), SerializeField] float foodOpacity = .42f;
    [Min(0f), SerializeField] float scrollSpeed = 16f;

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
    RawImage gradient;
    float gradientPhase;
    Tile[] tiles;
    float width;
    int columns;

    void Start()
    {
        if (!golfCamera || !gradientStrip || foods == null || foods.Length == 0 ||
            Array.Exists(foods, f => !f.texture || f.uv.width <= 0 || f.uv.height <= 0 || f.artworkAspect <= 0))
        {
            Debug.LogWarning("Battle food wallpaper is missing its viewport, gradient or food images.", this);
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
        board.AddComponent<RectMask2D>();
        var paper = new GameObject("Scrolling gradient", typeof(RectTransform), typeof(RawImage));
        paper.layer = board.layer;
        paper.transform.SetParent(panel, false);
        paper.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        gradient = paper.GetComponent<RawImage>();
        gradient.raycastTarget = false;
        gradient.texture = gradientStrip;
        Advance(0f);
    }

    float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

    void FitPanel()
    {
        float depth = Mathf.Lerp(stage.nearClipPlane, stage.farClipPlane, .75f);
        // Keep the same split even when the upper camera is hidden during combat.
        float split = Mathf.InverseLerp(stage.rect.yMin, stage.rect.yMax, golfCamera.rect.yMin);
        var a = transform.InverseTransformPoint(stage.ViewportToWorldPoint(new Vector3(0, 0, depth)));
        var b = transform.InverseTransformPoint(stage.ViewportToWorldPoint(new Vector3(1, split, depth)));
        float scale = Mathf.Max(.00001f, (b.y - a.y) / Height);
        float newWidth = (b.x - a.x) / scale;
        panel.localPosition = (a + b) * .5f;
        panel.localScale = new Vector3(scale, scale, scale);
        if (Mathf.Abs(width - newWidth) < .01f && tiles != null) return;

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
            return;
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
            item.transform.SetParent(panel, false);
            var tile = new Tile { image = item.GetComponent<RawImage>() };
            tile.image.raycastTarget = false;
            float x = (column + .25f + (row % 2) * .5f + Range(-.12f, .12f)) * span.x / columns;
            float y = (row + .5f + Range(-.14f, .14f)) * span.y / Rows;
            tile.position = new Vector2(x - span.x / 2f, y - span.y / 2f);
            PickFood(tile);
            tiles[row * columns + column] = tile;
        }
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
        FitPanel();
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

    void OnDisable() { if (panel) panel.gameObject.SetActive(false); }
    void OnEnable() { if (panel) panel.gameObject.SetActive(true); }
    void OnDestroy() { if (panel) Destroy(panel.gameObject); }
}

