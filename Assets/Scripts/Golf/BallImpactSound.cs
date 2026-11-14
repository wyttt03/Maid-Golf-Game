using UnityEngine;

// Plays a bounce sound when the ball hits walls or obstacles (not the table).
public class BallImpactSound : MonoBehaviour
{
    public AudioSource source;
    public AudioClip impactClip;

    [Tooltip("Impacts slower than this are silent.")]
    public float minSpeed = 0.5f;
    [Tooltip("Impact speed that plays at full volume.")]
    public float maxSpeed = 6f;
    [Tooltip("Collisions with this layer are ignored (the table surface).")]
    public string ignoreLayer = "Table";
    [Tooltip("Minimum time between two impact sounds.")]
    public float minInterval = 0.05f;

    int ignoreLayerId;
    float lastTime;

    void Awake()
    {
        ignoreLayerId = LayerMask.NameToLayer(ignoreLayer);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!source || !impactClip) return;
        if (collision.gameObject.layer == ignoreLayerId) return;
        if (Time.time - lastTime < minInterval) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minSpeed) return;

        lastTime = Time.time;
        float volume = Mathf.Clamp(Mathf.InverseLerp(minSpeed, maxSpeed, speed), 0.2f, 1f);
        source.pitch = Random.Range(0.95f, 1.05f);
        source.PlayOneShot(impactClip, volume);
    }
}