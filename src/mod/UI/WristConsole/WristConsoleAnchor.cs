using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class WristConsoleAnchor
{
    private const float ShoulderSideOffsetMeters = 0.18f;
    private const float ShoulderDownOffsetMeters = 0.20f;
    private const float ForearmOffsetMeters = 0.12f;
    private const float ForearmNormalOffsetMeters = 0.04f;
    private const float HeadSideOffsetMeters = 0.02f;
    private const float HeadDownOffsetMeters = 0.02f;
    private const float MinimumForearmLengthMetersSquared = 0.0001f;
    private const float MinimumNormalMagnitudeSquared = 0.01f;

    public Pose GetPose(Vector3 handPosition, Transform head)
    {
        var shoulder = head.position - head.right * ShoulderSideOffsetMeters - Vector3.up * ShoulderDownOffsetMeters;
        var forearm = handPosition - shoulder;
        if (forearm.sqrMagnitude < MinimumForearmLengthMetersSquared)
            forearm = head.forward;
        forearm.Normalize();
        var normal = Vector3.ProjectOnPlane(Vector3.up, forearm);
        if (normal.sqrMagnitude < MinimumNormalMagnitudeSquared)
            normal = Vector3.ProjectOnPlane(-head.forward, forearm);
        if (normal.sqrMagnitude < MinimumNormalMagnitudeSquared)
            normal = Vector3.ProjectOnPlane(head.right, forearm);
        normal.Normalize();
        var position =
            handPosition
            - forearm * ForearmOffsetMeters
            + normal * ForearmNormalOffsetMeters
            - head.right * HeadSideOffsetMeters
            - head.up * HeadDownOffsetMeters;

        return new Pose(position, Quaternion.LookRotation(position - head.position, head.up));
    }
}
