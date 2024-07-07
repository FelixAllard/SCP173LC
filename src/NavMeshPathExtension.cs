using UnityEngine;
using UnityEngine.AI;

namespace SCP173;

public static class NavMeshPathExtensions
{
    public static Vector3 SamplePathPosition(NavMeshPath path, float distance)
    {
        Vector3 sampledPoint = path.corners[0];
        float remainingDistance = distance;

        for (int i = 1; i < path.corners.Length; i++)
        {
            float segmentLength = Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (remainingDistance < segmentLength)
            {
                sampledPoint = Vector3.Lerp(path.corners[i - 1], path.corners[i], remainingDistance / segmentLength);
                break;
            }
            remainingDistance -= segmentLength;
        }

        return sampledPoint;
    }

    public static bool CheckCondition(Vector3 point)
    {
        // Replace this with your actual condition check
        // For example, checking if the y-coordinate is above a certain value:
        return point.y > 1.0f;
    }
    public static float GetPathLength(this NavMeshPath path)
    {
        float length = 0f;

        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return length;
    }
}