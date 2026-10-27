using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UZE.Navigation
{
    [BurstCompile]
    public struct TrackCurrentPolygonJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> agentPositions;
        [ReadOnly] public NativeArray<NavPolyNode> nodes;
        [ReadOnly] public NativeArray<NavPolyEdge> edges;

        public NativeArray<int> currentPolyIndices;

        public void Execute(int index)
        {
            float3 position = agentPositions[index];
            int currentPoly = currentPolyIndices[index];

            if (currentPoly < 0 || currentPoly >= nodes.Length)
            {
                currentPolyIndices[index] = FindNearestPolygon(position);
                return;
            }

            if (IsInsidePolygon(currentPoly, position))
                return;

            currentPolyIndices[index] = CheckNeighborsOrFindNearest(currentPoly, position);
        }

        private int CheckNeighborsOrFindNearest(int currentPoly, float3 position)
        {
            NavPolyNode node = nodes[currentPoly];
            for (int i = 0; i < node.edgeCount; i++)
            {
                int edgeIndex = node.edgeStartIndex + i;
                int neighbor = edges[edgeIndex].toPolyIndex;
                if (neighbor >= 0 && neighbor < nodes.Length)
                {
                    if (IsInsidePolygon(neighbor, position))
                        return neighbor;
                }
            }

            return FindNearestPolygon(position);
        }

        private bool IsInsidePolygon(int polyIndex, float3 pos)
        {
            NavPolyNode node = nodes[polyIndex];
            if (node.edgeCount == 0)
                return false;

            for (int i = 0; i < node.edgeCount; i++)
            {
                int edgeIndex = node.edgeStartIndex + i;
                NavPolyEdge edge = edges[edgeIndex];
                if (CrossProduct2D(edge.leftVertex, edge.rightVertex, pos) < -0.05f)
                    return false;
            }

            return true;
        }

        private int FindNearestPolygon(float3 pos)
        {
            int bestIndex = 0;
            float bestDistanceSq = float.MaxValue;
            int totalNodes = nodes.Length;

            for (int i = 0; i < totalNodes; i++)
            {
                float distanceSq = math.distancesq(pos, nodes[i].centroid);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float CrossProduct2D(float3 a, float3 b, float3 p)
        {
            return (b.x - a.x) * (p.z - a.z) - (b.z - a.z) * (p.x - a.x);
        }
    }
}
