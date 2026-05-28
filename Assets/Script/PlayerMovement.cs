using UnityEngine;

/// <summary>
/// 通用玩家移动系统（支持任意物体跳跃接触）
/// 作者: David (优化 by GPT-5)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float groundFriction = 12f; // 🟢 地面摩擦控制，越大越不滑

    [Header("跳跃设置")]
    public float jumpForce = 10f;
    public float lowJumpMultiplier = 3f;
    public float fallMultiplier = 4f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("检测设置")]
    public float groundCheckDistance = 0.6f;
    public bool allowAirControl = true;
    public bool smoothRotation = true;

    private Rigidbody rb;
    private Vector3 moveInput;
    private bool isGrounded;
    private bool isJumping;
    private float lastGroundedTime;
    private float lastJumpPressedTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        HandleInput();
        HandleTimers();
    }

    void FixedUpdate()
    {
        CheckGrounded();
        HandleMovement();
        HandleJump();
        ApplyBetterJumpPhysics();
        ApplyGroundFriction(); // ✅ 新增：地面防滑逻辑
    }

    #region 输入
    void HandleInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(moveX, 0, moveZ).normalized;

        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;
    }
    #endregion

    #region 移动
    void HandleMovement()
    {
        if (!allowAirControl && !isGrounded) return;

        Vector3 move = moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        if (moveInput.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveInput, Vector3.up);
            if (smoothRotation)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
            else
                rb.rotation = targetRot;
        }
    }
    #endregion

    #region 跳跃系统
    void HandleJump()
    {
        bool canJump = (Time.time - lastGroundedTime <= coyoteTime);
        bool bufferedJump = (Time.time - lastJumpPressedTime <= jumpBufferTime);

        if (bufferedJump && canJump && !isJumping)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isJumping = true;
            lastJumpPressedTime = -999f;
        }

        if (isGrounded && rb.linearVelocity.y <= 0)
        {
            isJumping = false;
        }
    }

    void ApplyBetterJumpPhysics()
    {
        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
    }
    #endregion

    #region 防止地面滑动
    void ApplyGroundFriction()
    {
        if (isGrounded && moveInput.sqrMagnitude < 0.01f)
        {
            // 当玩家在地面上且没有输入时，施加额外阻力让其停止
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            Vector3 counterForce = -horizontalVel * groundFriction;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }
    }
    #endregion

    #region 地面检测
    void CheckGrounded()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, groundCheckDistance))
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
        }
        else
        {
            isGrounded = false;
        }
    }

    void HandleTimers()
    {
        if (Time.time - lastGroundedTime > 5f) lastGroundedTime = 0;
        if (Time.time - lastJumpPressedTime > 5f) lastJumpPressedTime = 0;
    }
    #endregion

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
#endif

    #region 摄像机接口
    public Vector3 GetMoveInput() => moveInput;
    public float GetVerticalVelocity() => rb.linearVelocity.y;
    #endregion

    public void BounceFromBubble(float bounceForce)
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);
        isJumping = true;
        isGrounded = false;
        lastGroundedTime = -999f;
        lastJumpPressedTime = -999f;
    }
}

