using Unity.Mathematics;

namespace UZE.Crowd
{
    public static class SpatialHashGrid
    {
        public static int Hash(float2 position, float cellSize)
        {
            int2 gridPos = (int2)math.floor(position / cellSize);
            return (gridPos.x * 73856093) ^ (gridPos.y * 19349663);
        }

        public static int2 GetCellCoords(float2 position, float cellSize)
        {
            return (int2)math.floor(position / cellSize);
        }

        public static int HashCell(int2 gridPos)
        {
            return (gridPos.x * 73856093) ^ (gridPos.y * 19349663);
        }

        public static float3 FilterSeparation(float3 rawSeparation, float3 forwardDir, float backwardDamping = 0.1f)
        {
            float dotForward = math.dot(rawSeparation, forwardDir);
            if (dotForward < 0.0f)
                rawSeparation -= forwardDir * (dotForward * (1.0f - backwardDamping));

            return rawSeparation;
        }

        public static float3 CalculateRepulsion(float3 currentPos, float3 otherPos, float combinedRadius)
        {
            float3 delta = currentPos - otherPos;
            delta.y = 0.0f;
            float distSq = math.lengthsq(delta);
            float radiusSq = combinedRadius * combinedRadius;

            if (distSq >= radiusSq || distSq < 0.0001f)
                return float3.zero;

            float distance = math.sqrt(distSq);
            float penetration = combinedRadius - distance;
            return (delta / distance) * (penetration / combinedRadius);
        }
    }
}
