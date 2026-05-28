using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class BubbleProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float launchSpeed = 6f;
    public float riseSpeed = 1.5f;
    public float bounceBackDistance = 0.2f;
    public float lifeTime = 8f;

    [Header("Player Bounce")]
    public float playerBounceForce = 9f;
    public float playerTopDotThreshold = 0.25f;

    private Rigidbody rb;
    private Transform owner;
    private Vector3 moveDirection = Vector3.forward;
    private float spawnTime;
    private float ignoreEnvironmentUntil;
    private bool isRising;

    public void Initialize(Transform ownerTransform, Vector3 direction)
    {
        owner = ownerTransform;
        moveDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;

        if (moveDirection.sqrMagnitude < 0.01f)
            moveDirection = owner != null ? Vector3.ProjectOnPlane(owner.forward, Vector3.up).normalized : Vector3.forward;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        SphereCollider bubbleCollider = GetComponent<SphereCollider>();
        bubbleCollider.isTrigger = true;
        bubbleCollider.radius = Mathf.Max(0.1f, bubbleCollider.radius);
    }

    private void OnEnable()
    {
        spawnTime = Time.time;
    }

    private void FixedUpdate()
    {
        Vector3 velocity = isRising ? Vector3.up * riseSpeed : moveDirection * launchSpeed;
        rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);

        if (Time.time - spawnTime >= lifeTime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsOwner(other))
            return;

        if (TryBouncePlayer(other))
            return;

        HitEnvironment();
    }

    private void OnTriggerStay(Collider other)
    {
        if (TryBouncePlayer(other))
            return;

        if (!IsOwner(other) && isRising && Time.time >= ignoreEnvironmentUntil)
            Destroy(gameObject);
    }

    private bool TryBouncePlayer(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null)
            return false;

        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        if (!isRising || Vector3.Dot(toPlayer, Vector3.up) < playerTopDotThreshold)
            return true;

        player.BounceFromBubble(playerBounceForce);
        Destroy(gameObject);
        return true;
    }

    private bool IsOwner(Collider other)
    {
        return owner != null && other.transform.IsChildOf(owner);
    }

    private void HitEnvironment()
    {
        if (isRising)
        {
            Destroy(gameObject);
            return;
        }

        isRising = true;
        ignoreEnvironmentUntil = Time.time + 0.2f;
        rb.position -= moveDirection * Mathf.Max(0f, bounceBackDistance);
    }
}
