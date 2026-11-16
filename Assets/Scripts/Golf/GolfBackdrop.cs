using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class GolfBackdrop : MonoBehaviour
{
    [SerializeField] Sprite gradient;
    [SerializeField] Sprite[] shapes;
    [SerializeField] Material unlitMaterial;
    [Min(1f), SerializeField] float cycleSeconds = 18f;
    [Range(6, 100), SerializeField] int particleCount = 48;

    sealed class Mote
    {
        public SpriteRenderer renderer;
        public Vector2 start, end;
        public float age, lifetime, height, tilt, spin, opacity, drift;
    }

    readonly System.Random random = new System.Random();
    Camera golfCamera;
    GameObject visuals;
    SpriteRenderer backdrop;
    Mote[] motes;
    float phase;

    void Start()
    {
        if (!gradient || !unlitMaterial || shapes == null || shapes.Length == 0 ||
            Array.Exists(shapes, shape => !shape))
        {
            Debug.LogWarning("Golf backdrop requires its gradient, material and shape sprites.", this);
            enabled = false;
            return;
        }

        golfCamera = GetComponent<Camera>();
        visuals = new GameObject("Golf background");
        visuals.transform.SetParent(transform, false);
        backdrop = MakeRenderer("Gradient", -32001);
        backdrop.sprite = gradient;
        motes = new Mote[particleCount];
        for (int i = 0; i < motes.Length; i++)
        {
            var mote = new Mote { renderer = MakeRenderer("Floating shape", -32000) };
            Spawn(mote);
            mote.age = (i + Range(0f, 1f)) / motes.Length * mote.lifetime;
            motes[i] = mote;
        }
        Advance(0f);
    }

    SpriteRenderer MakeRenderer(string label, int order)
    {
        var part = new GameObject(label);
        part.transform.SetParent(visuals.transform, false);
        part.layer = LayerMask.NameToLayer("TransparentFX");
        var renderer = part.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = unlitMaterial;
        renderer.sortingOrder = order;
        return renderer;
    }

    float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    void Spawn(Mote mote)
    {
        float aspect = golfCamera.aspect;
        float edge = Range(0f, 2f * (aspect + 1f));
        if (edge < aspect)
            mote.start = new Vector2(edge / aspect, 1.012f);
        else if (edge < aspect + 1f)
            mote.start = new Vector2(1.012f, edge - aspect);
        else if (edge < 2f * aspect + 1f)
            mote.start = new Vector2((edge - aspect - 1f) / aspect, -0.012f);
        else
            mote.start = new Vector2(-0.012f, edge - 2f * aspect - 1f);

        mote.end = new Vector2(Range(.47f, .53f), Range(.46f, .54f));
        mote.age = 0f;
        mote.lifetime = Range(7f, 11f);
        mote.height = Range(.038f, .082f);
        mote.tilt = Range(0f, 360f);
        mote.spin = Range(22f, 60f) * (random.Next(2) == 0 ? -1f : 1f);
        mote.opacity = Range(.52f, .85f);
        mote.drift = Range(-.045f, .045f);
        mote.renderer.sprite = shapes[random.Next(shapes.Length)];
    }

    void LateUpdate() => Advance(Time.deltaTime);

    void Advance(float elapsed)
    {
        phase = Mathf.Repeat(phase + elapsed / Mathf.Max(1f, cycleSeconds), 1f);
        // Both planes follow this camera's own viewport, behind the playable course.
        float far = golfCamera.farClipPlane;
        var gradientFrame = FrameAt(Mathf.Lerp(golfCamera.nearClipPlane, far, .85f));
        var shapeFrame = FrameAt(Mathf.Lerp(golfCamera.nearClipPlane, far, .80f));
        var size = gradient.bounds.size;
        backdrop.transform.localScale = new Vector3(4f * gradientFrame.size.x / size.x,
            1.01f * gradientFrame.size.y / size.y, 1f);
        // The strip contains two identical periods. Moving half its width wraps seamlessly.
        backdrop.transform.localPosition = gradientFrame.center +
            Vector3.right * ((1f - 2f * phase) * gradientFrame.size.x);

        foreach (var mote in motes)
        {
            mote.age += elapsed;
            if (mote.age >= mote.lifetime)
            {
                float remainder = mote.age - mote.lifetime;
                Spawn(mote);
                mote.age = Mathf.Repeat(remainder, mote.lifetime);
            }
            float t = mote.age / mote.lifetime;
            Vector2 uv = Vector2.Lerp(mote.start, mote.end, Mathf.Lerp(t * t, t, .6f));
            Vector2 path = mote.end - mote.start;
            uv += new Vector2(-path.y, path.x) * (mote.drift * Mathf.Sin(Mathf.PI * t));
            var part = mote.renderer.transform;
            part.localPosition = new Vector3(shapeFrame.min.x + uv.x * shapeFrame.size.x,
                shapeFrame.min.y + uv.y * shapeFrame.size.y, shapeFrame.center.z);
            part.localRotation = Quaternion.Euler(0f, 0f, mote.tilt + mote.spin * mote.age);
            float scale = shapeFrame.size.y * mote.height * (1f - t) /
                mote.renderer.sprite.bounds.size.y;
            part.localScale = new Vector3(scale, scale, 1f);
            float alpha = Mathf.SmoothStep(0f, 1f, t / .08f) *
                (1f - Mathf.SmoothStep(0f, 1f, (t - .15f) / .85f)) * mote.opacity;
            mote.renderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    Bounds FrameAt(float depth)
    {
        var a = transform.InverseTransformPoint(golfCamera.ViewportToWorldPoint(new Vector3(0, 0, depth)));
        var b = transform.InverseTransformPoint(golfCamera.ViewportToWorldPoint(new Vector3(1, 1, depth)));
        var bounds = new Bounds();
        bounds.SetMinMax(Vector3.Min(a, b), Vector3.Max(a, b));
        return bounds;
    }

    void OnEnable() { if (visuals) visuals.SetActive(true); }
    void OnDisable() { if (visuals) visuals.SetActive(false); }
    void OnDestroy() { if (visuals) Destroy(visuals); }
}

