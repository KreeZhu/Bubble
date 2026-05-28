using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("目标与基础偏移")]
    public Transform target;
    public Vector3 cameraOffset = new Vector3(0, 3.5f, -7);
    public float lookAheadDistance = 0.5f;       // 左右偏移幅度
    public float jumpLookUpOffset = 1f;          // 跳跃向上偏移
    public float fallLookDownOffset = 0.5f;      // 下落向下偏移

    [Header("平滑参数")]
    public float followSmoothTime = 0.15f;
    public float lookSmoothTime = 0.1f;

    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 currentLookVelocity = Vector3.zero;
    private Vector3 smoothedLookTarget;

    private PlayerMovement playerMovement;

    void Start()
    {
        if (target != null)
            playerMovement = target.GetComponent<PlayerMovement>();

        smoothedLookTarget = target.position + Vector3.up * 1.5f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        HandleCameraPosition();
        HandleLookTarget();
    }

    void HandleCameraPosition()
    {
        // 摄像机位置始终跟随玩家中心 + offset
        Vector3 desiredPosition = target.position + cameraOffset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, followSmoothTime);
    }

    void HandleLookTarget()
    {
        // 玩家头部中心
        Vector3 lookTarget = target.position + Vector3.up * 1.5f;

        if (playerMovement != null)
        {
            // 左右偏移仅影响LookAt，不改变摄像机位置
            Vector3 inputDir = playerMovement.GetMoveInput();
            if (inputDir.sqrMagnitude > 0.01f)
            {
                Vector3 horizontalOffset = new Vector3(inputDir.x, 0, inputDir.z).normalized * lookAheadDistance;
                lookTarget += horizontalOffset;
            }

            // 上下跳跃偏移
            float verticalVelocity = playerMovement.GetVerticalVelocity();
            if (verticalVelocity > 0.1f)
                lookTarget += Vector3.up * jumpLookUpOffset;
            else if (verticalVelocity < -0.1f)
                lookTarget += Vector3.down * fallLookDownOffset;
        }

        // 平滑 LookAt 目标
        smoothedLookTarget = Vector3.SmoothDamp(smoothedLookTarget, lookTarget, ref currentLookVelocity, lookSmoothTime);
        transform.LookAt(smoothedLookTarget);
    }
}
