using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HeroineVisualSway : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float responseSpeed = 8f;
    [SerializeField] private float returnSpeed = 5f;
    [SerializeField] private float maxHairAngle = 13f;
    [SerializeField] private float maxClothAngle = 17f;
    [SerializeField] private float idleAmplitude = 1.4f;
    [SerializeField] private float idleFrequency = 2.4f;

    private readonly List<SwayTarget> targets = new List<SwayTarget>();
    private Rigidbody playerBody;

    private enum SwayKind
    {
        Hair,
        Cape,
        Cloth
    }

    private struct SwayTarget
    {
        public Transform transform;
        public Quaternion baseRotation;
        public SwayKind kind;
        public float side;
    }

    private void Awake()
    {
        RefreshTargets();
    }

    private void OnEnable()
    {
        RefreshTargets();
    }

    private void Update()
    {
        if (!enableSway)
            return;

        if (playerBody == null)
            playerBody = GetComponentInParent<Rigidbody>();

        Vector3 localVelocity = playerBody != null
            ? transform.InverseTransformDirection(playerBody.linearVelocity)
            : Vector3.zero;

        float horizontalSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
        float forwardTrail = Mathf.Clamp(localVelocity.z * 1.8f, -1f, 1f);
        float sideTrail = Mathf.Clamp(localVelocity.x * 1.4f, -1f, 1f);
        float verticalTrail = Mathf.Clamp(localVelocity.y * 0.9f, -1f, 1f);
        float idleWave = Mathf.Sin(Time.time * idleFrequency) * idleAmplitude;

        for (int i = 0; i < targets.Count; i++)
        {
            SwayTarget target = targets[i];
            if (target.transform == null)
                continue;

            float angleLimit = target.kind == SwayKind.Hair ? maxHairAngle : maxClothAngle;
            float motionStrength = Mathf.Clamp01(horizontalSpeed / 5f + Mathf.Abs(localVelocity.y) / 8f);
            float pitch = Mathf.Clamp((-forwardTrail * angleLimit * 0.55f) + (-verticalTrail * angleLimit * 0.42f), -angleLimit, angleLimit);
            float roll = Mathf.Clamp((-sideTrail * angleLimit * 0.38f) + idleWave * target.side, -angleLimit, angleLimit);
            float yaw = Mathf.Clamp(-sideTrail * angleLimit * 0.22f, -angleLimit * 0.6f, angleLimit * 0.6f);

            if (target.kind == SwayKind.Cape)
            {
                pitch *= 1.18f;
                roll *= 0.72f;
            }
            else if (target.kind == SwayKind.Cloth)
            {
                pitch *= 0.86f;
                roll *= 1.15f;
            }

            Quaternion targetRotation = target.baseRotation * Quaternion.Euler(pitch * motionStrength, yaw * motionStrength, roll);
            float speed = motionStrength > 0.05f ? responseSpeed : returnSpeed;
            target.transform.localRotation = Quaternion.Slerp(target.transform.localRotation, targetRotation, Time.deltaTime * speed);
        }
    }

    public void RefreshTargets()
    {
        targets.Clear();
        playerBody = GetComponentInParent<Rigidbody>();

        AddTargets(SwayKind.Hair,
            "BackHairCenter", "BackHairLeft", "BackHairRight", "SideHairLeft", "SideHairRight",
            "BackHairLowerTip", "HairHighlightBackRibbon");

        AddTargets(SwayKind.Cape,
            "LeftCapeTail", "RightCapeTail", "BackCapeletLeft", "BackCapeletRight");

        AddTargets(SwayKind.Cloth,
            "LongLeftSkirtPanel", "IvoryFrontSkirtPanel", "BackAsymSkirtPanel", "RightRearTealPanel",
            "LeftAirySideSash", "RightAirySideSash", "BackTealWindPanel");
    }

    private void AddTargets(SwayKind kind, params string[] targetNames)
    {
        foreach (string targetName in targetNames)
        {
            Transform child = transform.Find(targetName);
            if (child == null)
                continue;

            targets.Add(new SwayTarget
            {
                transform = child,
                baseRotation = child.localRotation,
                kind = kind,
                side = Mathf.Approximately(child.localPosition.x, 0f) ? 1f : Mathf.Sign(child.localPosition.x)
            });
        }
    }
}
