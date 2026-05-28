using UnityEngine;

public class BubbleShooter : MonoBehaviour
{
    [Header("Input")]
    public KeyCode fireKey = KeyCode.E;
    public bool allowMouseFire = true;

    [Header("Spawn")]
    public BubbleProjectile bubblePrefab;
    public Transform spawnPoint;
    public Vector3 localSpawnOffset = new Vector3(0f, 0.25f, 0.85f);
    public float bubbleScale = 1.2f;

    [Header("Bubble Tuning")]
    public float launchSpeed = 6f;
    public float riseSpeed = 1.5f;
    public float bubbleBounceBackDistance = 0.2f;
    public float playerBounceForce = 9f;
    public float lifeTime = 8f;

    private PlayerMovement playerMovement;
    private BubbleProjectile currentBubble;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(fireKey) || (allowMouseFire && Input.GetMouseButtonDown(0)))
            FireBubble();
    }

    public void FireBubble()
    {
        if (currentBubble != null)
            Destroy(currentBubble.gameObject);

        Vector3 direction = GetFireDirection();
        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.TransformPoint(localSpawnOffset);

        currentBubble = bubblePrefab != null
            ? Instantiate(bubblePrefab, position, Quaternion.identity)
            : CreateRuntimeBubble(position);

        currentBubble.launchSpeed = launchSpeed;
        currentBubble.riseSpeed = riseSpeed;
        currentBubble.bounceBackDistance = bubbleBounceBackDistance;
        currentBubble.playerBounceForce = playerBounceForce;
        currentBubble.lifeTime = lifeTime;
        currentBubble.Initialize(transform, direction);
    }

    private Vector3 GetFireDirection()
    {
        if (playerMovement != null)
        {
            Vector3 input = playerMovement.GetMoveInput();
            if (input.sqrMagnitude > 0.01f)
                return input;
        }

        return transform.forward;
    }

    private BubbleProjectile CreateRuntimeBubble(Vector3 position)
    {
        GameObject bubbleObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubbleObject.name = "Bubble";
        bubbleObject.transform.position = position;
        bubbleObject.transform.localScale = Vector3.one * bubbleScale;

        Collider solidCollider = bubbleObject.GetComponent<Collider>();
        if (solidCollider != null)
            Destroy(solidCollider);

        SphereCollider trigger = bubbleObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;

        Rigidbody bubbleRigidbody = bubbleObject.AddComponent<Rigidbody>();
        bubbleRigidbody.useGravity = false;
        bubbleRigidbody.isKinematic = true;

        Renderer renderer = bubbleObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.color = new Color(0.35f, 0.85f, 1f, 0.45f);
            renderer.material = material;
        }

        return bubbleObject.AddComponent<BubbleProjectile>();
    }
}
