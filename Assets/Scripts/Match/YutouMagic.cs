using UnityEngine;

[System.Serializable]
public class MaidMagicStyle
{
    public RuntimeAnimatorController controller;
    public Sprite core, solid, outline;
    public Color mistColor;
    public Texture2D shield;
    public string freeWeapon, heldWeapon;
    public Vector2 emissionOffset;
}

[System.Serializable]
public class MaidMagic
{
    public MaidMagicStyle[] styles;
    public Sprite star, thinStar, cometMist;
    public Material material;
    public Texture2D shieldSpark;

    public MaidMagicStyle For(Animator maid)
    {
        if (!maid || !maid.runtimeAnimatorController || styles == null) return null;
        foreach (var style in styles)
            if (style != null && style.controller == maid.runtimeAnimatorController) return style;
        return null;
    }
}

// Both players can cast independently with the same flight and particle timings.
public sealed class MaidBolt
{
    readonly GameObject root;
    readonly MaidMagic art;
    readonly MaidMagicStyle style;
    readonly Vector3 start, destination, shieldPoint, returnPoint;
    readonly Transform impactBody;
    readonly bool reflected;
    readonly float direction;
    readonly SpriteRenderer head;
    readonly SpriteRenderer[] mist = new SpriteRenderer[17];
    const int DropCount = 16;
    const int RingCount = 9;
    const int StarCount = 12;
    readonly SpriteRenderer[] motes = new SpriteRenderer[DropCount + RingCount + StarCount];
    readonly SpriteRenderer[] debris = new SpriteRenderer[15];
    readonly SpriteRenderer[] glints = new SpriteRenderer[5];
    public MaidBolt(MaidMagic art, MaidMagicStyle style, Transform stage, Transform source,
        Animator caster, Transform target, Animator receiver, bool reflected)
    {
        this.art = art;
        this.style = style;
        this.reflected = reflected;
        direction = Mathf.Sign(target.position.x - source.position.x);
        root = new GameObject(style.controller.name + " magic");
        root.transform.SetParent(stage, true);
        root.layer = source.gameObject.layer;
        // The slots themselves can be on Default. Use the same layer as the maid.
        var chest = Chest(caster);
        if (chest) root.layer = chest.gameObject.layer;
        else root.layer = caster.gameObject.layer;
        start = LaunchPoint(caster, source, direction, style);
        destination = ChestPoint(receiver, target);
        returnPoint = ChestPoint(caster, source);
        shieldPoint = MaidShield.ContactPoint(receiver, target, direction);
        impactBody = reflected ? chest : Chest(receiver);

        head = Piece(style.core, 330);
        for (int i = 0; i < mist.Length; i++) mist[i] = Piece(art.cometMist, 300);
        for (int i = 0; i < motes.Length; i++)
        {
            Sprite sprite = style.solid;
            if (i >= DropCount + RingCount) sprite = i % 4 == 0 ? art.thinStar : art.star;
            else if (i >= DropCount) sprite = style.outline;
            motes[i] = Piece(sprite, 314);
        }
        for (int i = 0; i < debris.Length; i++)
            debris[i] = Piece(i % 5 == 0 ? style.outline : style.solid, 325);
        for (int i = 0; i < glints.Length; i++)
            glints[i] = Piece(i % 2 == 0 ? art.star : art.thinStar, 335);

        DrawFlight(0f);
    }

    static Transform Chest(Animator maid) => maid ? maid.transform.Find("upper_body") : null;

    internal static Vector3 ChestPoint(Animator maid, Transform slot)
    {
        var chest = Chest(maid);
        if (chest && chest.TryGetComponent<SpriteRenderer>(out var renderer))
            return renderer.bounds.center;
        return slot.position;
    }

    static Vector3 LaunchPoint(Animator maid, Transform slot, float sign, MaidMagicStyle style)
    {
        if (TryWeaponPoint(maid.transform.Find(style.freeWeapon), style.emissionOffset, out var point)
            || TryWeaponPoint(maid.transform.Find(style.heldWeapon), style.emissionOffset, out point))
            return point;
        return ChestPoint(maid, slot) + new Vector3(sign * 0.64f, 0.25f);
    }

    static bool TryWeaponPoint(Transform weapon, Vector2 offset, out Vector3 point)
    {
        point = default;
        if (!weapon || !weapon.gameObject.activeInHierarchy ||
            !weapon.TryGetComponent<SpriteRenderer>(out var renderer) || !renderer.enabled ||
            !renderer.sprite || renderer.color.a <= 0.002f) return false;
        Bounds bounds = renderer.sprite.bounds;
        var local = new Vector3(offset.x * bounds.size.x, offset.y * bounds.size.y);
        point = weapon.TransformPoint(bounds.center + local);
        return true;
    }

    SpriteRenderer Piece(Sprite sprite, int order)
    {
        var child = new GameObject(sprite.name);
        child.layer = root.layer;
        child.transform.SetParent(root.transform, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (art.material) renderer.sharedMaterial = art.material;
        renderer.sortingOrder = order;
        renderer.enabled = false;
        return renderer;
    }

    // Star01/02 have generous transparent margins. Use visible desktop art extents and
    // compensate their off-centre artwork when positioning them.
    void Place(SpriteRenderer r, Vector3 position, float width, float angle, float alpha)
    {
        r.enabled = alpha > 0.002f && width > 0.001f;
        if (!r.enabled) return;
        var s = r.sprite;
        Vector2 offset = Vector2.zero;
        float visibleWidth = s.rect.width;
        if (s == art.star) { visibleWidth *= 127f / 893f; offset = new Vector2(-5f / 893f, 171f / 1718f); }
        else if (s == art.thinStar) { visibleWidth *= 444f / 893f; offset = new Vector2(120f / 893f, -161f / 1718f); }

        float scale = width * s.pixelsPerUnit / visibleWidth;
        var rotation = Quaternion.Euler(0f, 0f, angle);
        Vector3 localOffset = new Vector3(offset.x * s.rect.width, offset.y * s.rect.height) / s.pixelsPerUnit;
        r.transform.SetPositionAndRotation(position - rotation * (localOffset * scale), rotation);
        r.transform.localScale = Vector3.one * scale;
        r.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    }

    Vector3 Path(float t)
    {
        t = Mathf.Clamp01(t);
        // Let the orb gather at the staff before it travels. Keep shield contact at half-time.
        if (t < 0.5f) t = 0.5f * Mathf.InverseLerp(0.11f, 0.5f, t);
        Vector3 a = start, b = destination;
        if (reflected)
        {
            if (t < 0.5f) { b = shieldPoint; t *= 2f; }
            else { a = shieldPoint; b = returnPoint; t = (t - 0.5f) * 2f; }
        }
        var p = Vector3.Lerp(a, b, t);
        p.y += 0.21f * Mathf.Sin(Mathf.PI * t);
        return p;
    }

    static void Stretch(SpriteRenderer renderer, Vector3 centre, float width, float height,
        float angle, Color tint, float alpha)
    {
        renderer.enabled = alpha > 0.004f && width > 0.001f && height > 0.001f;
        if (!renderer.enabled) return;
        var sprite = renderer.sprite;
        renderer.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, 0f, angle));
        renderer.transform.localScale = new Vector3(width * sprite.pixelsPerUnit / sprite.rect.width,
            height * sprite.pixelsPerUnit / sprite.rect.height, 1f);
        tint.a = Mathf.Clamp01(alpha);
        renderer.color = tint;
    }

    Vector3 FlightDirection(float t)
    {
        float begin = reflected && t >= 0.5f ? 0.5f : 0f;
        float end = reflected && t < 0.5f ? 0.5f : 1f;
        Vector3 tangent = Path(Mathf.Min(t + 0.008f, end)) - Path(Mathf.Max(t - 0.008f, begin));
        if (tangent.sqrMagnitude < 0.000001f) return Vector3.right * direction;
        return tangent.normalized;
    }

    static Vector3 PlumePoint(Vector3 pos, Vector3 forward, Vector3 across, float u, float t, float extent)
    {
        // The bulb holds steady; the narrow tip sways as the soft contour catches up.
        float bend = u * u * (0.25f * Mathf.Sin(13f * t - 4f * u)
            + 0.12f * Mathf.Sin(23f * t - 5f * u));
        return pos - forward * (2.58f * u * extent) + across * (bend * extent);
    }

    public void DrawFlight(float t)
    {
        Vector3 pos = Path(t);
        Vector3 forward = FlightDirection(t);
        Vector3 across = new Vector3(-forward.y, forward.x);
        float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        float form = Mathf.SmoothStep(0f, 1f, t / 0.17f);
        float extent = Mathf.SmoothStep(0f, 1f, t / 0.23f);
        if (reflected)
        {
            // Tuck the plume into the core for one beat as the flight reverses.
            extent *= Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Abs(t - 0.5f) / 0.09f));
        }
        for (int i = 0; i < mist.Length; i++)
        {
            float u = i / (mist.Length - 1f);
            float taper = 1f - u;
            float ripple = 1f + 0.055f * Mathf.Sin(14f * t - 8f * u);
            var p = PlumePoint(pos, forward, across, u, t, extent);
            float height = (0.13f + 1.59f * taper) * ripple * form;
            float width = (0.33f + 1.28f * taper) * form;
            Stretch(mist[i], p, width, height, angle, style.mistColor, taper * taper * form);
        }

        float diameter = Mathf.Lerp(0.17f, 0.76f, form);
        Place(head, pos, diameter, 0f, form);

        for (int i = 0; i < motes.Length; i++)
        {
            // Each particle emerges near the head, drifts back, then dissolves before recycling.
            // Interleave ages within each family so blue droplets, white rings and stars coexist.
            bool sparkle = i >= DropCount + RingCount;
            float seed = i * 0.618034f;
            float age = Mathf.Repeat(seed + t * (2.15f + 0.54f * Mathf.Repeat(i * 0.37f, 1f)), 1f);
            float u = Mathf.Lerp(0.08f, 0.98f, age);
            float phase = i * 2.39996f;
            float lane = 0.80f * Mathf.Sin(i * 7.13f + 2.7f * Mathf.Sin(i * 2.04f)) + 0.15f * Mathf.Sin(t * 11f + phase);
            Vector3 p = PlumePoint(pos, forward, across, u, t, extent)
                + across * (lane * (0.72f - 0.48f * u) * extent);
            float fadeIn = Mathf.SmoothStep(0f, 1f, age / 0.18f);
            float fadeOut = Mathf.SmoothStep(0f, 1f, (1f - age) / 0.42f);
            float opacity = fadeIn * fadeOut * (1f - 0.52f * u) * extent;
            float width = Mathf.Lerp(0.10f, 0.23f, Mathf.Repeat(i * 0.41f, 1f));
            if (sparkle) width *= 0.72f + 0.62f * Mathf.Sin(Mathf.PI * age);
            float turn = sparkle ? 12f * Mathf.Sin(t * 6f + phase) : 0f;
            Place(motes[i], p, width * form, turn, opacity);
        }

    }

    public void DrawImpact(float seconds, bool showHitStars)
    {
        head.enabled = false;
        foreach (var puff in mist) puff.enabled = false;
        foreach (var mote in motes) mote.enabled = false;
        Vector3 at = reflected ? returnPoint : destination;
        float facing = reflected ? -direction : direction;
        for (int i = 0; i < debris.Length; i++)
        {
            float t = Mathf.Clamp01(seconds / (0.28f + 0.12f * (i % 4)));
            float angle = i * 2.39996f;
            var scatter = new Vector3(Mathf.Cos(angle) + facing * 0.27f, Mathf.Sin(angle));
            var p = at + scatter * (0.14f + (1f - (1f-t)*(1f-t)) * (0.55f + 0.13f * (i % 5)));
            p.y -= 0.36f * t * t;
            Place(debris[i], p, (0.13f + 0.043f * (i % 4)) * (1f - t), angle * 40f + 110f * t, 1f - t);
        }
        Vector3 centre = at;
        if (impactBody && impactBody.TryGetComponent<SpriteRenderer>(out var body))
            centre = body.bounds.center;
        for (int i = 0; i < glints.Length; i++)
        {
            float t = Mathf.Clamp01((seconds - 0.07f - i * 0.035f) / 0.44f);
            float fade = Mathf.Sin(Mathf.PI * t);
            float angle = 0.3f + i * Mathf.PI * 2f / glints.Length;
            Vector3 p = centre + new Vector3(Mathf.Cos(angle) * 0.91f, Mathf.Sin(angle) * 0.67f + 0.10f * t);
            Place(glints[i], p, (0.27f + 0.09f * (i % 3)) * (1f - t), 76f * t - 15f, showHitStars ? fade : 0f);
        }
    }

    public void Dispose()
    {
        if (root) { root.SetActive(false); Object.Destroy(root); }
    }
}


// A successful Reflect owns its shield, even when the opponent misses their shot.
public sealed class MaidShield
{
    public const float CastLead = 0.38f;
    readonly GameObject root;
    readonly SpriteRenderer cover, spark;
    readonly Animator maid;
    readonly Transform slot;
    readonly float incomingDirection;

    public static Vector3 ContactPoint(Animator maid, Transform slot, float incomingDirection)
    {
        return MaidBolt.ChestPoint(maid, slot) - Vector3.right * incomingDirection * 0.78f;
    }

    public MaidShield(MaidMagic art, MaidMagicStyle style, Transform stage, Animator maid,
        Transform slot, float incomingDirection)
    {
        this.maid = maid;
        this.slot = slot;
        this.incomingDirection = incomingDirection;
        root = new GameObject(maid.name + " reflect shield");
        root.transform.SetParent(stage, true);
        root.layer = maid.gameObject.layer;
        var texture = style.shield;
        cover = MakeRenderer(texture, new Rect(0, 0, texture.width, texture.height),
            art.material, 308, "Shield");
        if (art.shieldSpark)
        {
            // The painted cross occupies only this part of its 893 x 1718 canvas.
            // Crop proportional to the imported texture size to keep its pivot on the glint.
            texture = art.shieldSpark;
            var painted = new Rect(108f / 893f * texture.width, 1139f / 1718f * texture.height,
                328f / 893f * texture.width, 320f / 1718f * texture.height);
            spark = MakeRenderer(texture, painted, art.material, 345, "Contact sparkle");
        }
        FollowMaid();
        Draw(0f, -1f, false);
    }

    SpriteRenderer MakeRenderer(Texture2D texture, Rect rect, Material material, int order, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.layer = root.layer;
        var r = child.AddComponent<SpriteRenderer>();
        r.sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        if (material) r.sharedMaterial = material;
        r.sortingOrder = order;
        return r;
    }

    // Track the action pose while rising, then anchor the shield where the bolt will rebound.
    public void FollowMaid() => root.transform.position = ContactPoint(maid, slot, incomingDirection);

    static void SetSizeAndAlpha(SpriteRenderer r, float width, float alpha)
    {
        r.enabled = alpha > 0f;
        r.transform.localScale = Vector3.one * (width / r.sprite.bounds.size.x);
        r.color = new Color(1f, 1f, 1f, alpha);
    }

    public void Draw(float age, float flight, bool incoming)
    {
        float rise = Mathf.SmoothStep(0f, 1f, age / 0.19f);
        float spring = Mathf.Clamp01((age - 0.19f) / 0.39f);
        float wave = Mathf.Sin(spring * Mathf.PI * 2f);
        // Two compressions and recoveries. The second dip has less amplitude.
        float size = Mathf.Lerp(0.10f, 1f, rise) * (1f - 0.18f * (1f - 0.78f * spring) * wave * wave);
        float release = incoming ? 0.50f : 0.63f;
        float sinceRelease = Mathf.InverseLerp(release, 0.99f, flight);
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 1f, sinceRelease));
        float alpha = rise * fade;
        SetSizeAndAlpha(cover, 1.68f * size, alpha);
        if (!spark) return;
        bool struck = incoming && flight >= 0.5f;
        float flick = Mathf.Sin(sinceRelease * Mathf.PI * 2f) * (1f - sinceRelease);
        float sparkleSize = 0.56f + 0.20f * Mathf.Sin(Mathf.Min(1f, sinceRelease * 2.6f) * Mathf.PI);
        spark.transform.localRotation = Quaternion.Euler(0f, 0f, incomingDirection * 9f * flick);
        spark.transform.localPosition = new Vector3(-incomingDirection * 0.045f, 0.06f, 0f) * flick;
        SetSizeAndAlpha(spark, sparkleSize, struck ? alpha : 0f);
    }

    public void Dispose()
    {
        root.SetActive(false);
        Object.Destroy(cover.sprite);
        if (spark) Object.Destroy(spark.sprite);
        Object.Destroy(root);
    }
}
