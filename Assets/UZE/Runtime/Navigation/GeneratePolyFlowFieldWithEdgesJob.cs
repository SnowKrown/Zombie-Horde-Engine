using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UZE.Navigation
{
    [BurstCompile]
    public struct GeneratePolyFlowFieldWithEdgesJob : IJob
    {
        public int targetPolyIndex;
        [ReadOnly] public NativeArray<NavPolyNode> nodes;
        [ReadOnly] public NativeArray<NavPolyEdge> edges;

        public NativeArray<float> accumulatedCosts;
        [WriteOnly] public NativeArray<int> bestNextEdge;

        public void Execute()
        {
            if (targetPolyIndex < 0 || targetPolyIndex >= nodes.Length)
                return;

            InitializeCosts();
            PropagateDijkstra();
        }

        private void InitializeCosts()
        {
            int totalNodes = nodes.Length;
            for (int i = 0; i < totalNodes; i++)
            {
                accumulatedCosts[i] = float.MaxValue;
                bestNextEdge[i] = -1;
            }

            accumulatedCosts[targetPolyIndex] = 0.0f;
        }

        private void PropagateDijkstra()
        {
            NativeQueue<int> frontier = new NativeQueue<int>(Allocator.Temp);
            frontier.Enqueue(targetPolyIndex);

            while (frontier.TryDequeue(out int currentPoly))
                RelaxNeighbors(currentPoly, ref frontier);

            frontier.Dispose();
        }

        private void RelaxNeighbors(int currentPoly, ref NativeQueue<int> frontier)
        {
            float currentCost = accumulatedCosts[currentPoly];
            NavPolyNode node = nodes[currentPoly];

            for (int i = 0; i < node.edgeCount; i++)
            {
                int edgeIndex = node.edgeStartIndex + i;
                NavPolyEdge edge = edges[edgeIndex];
                int neighbor = edge.toPolyIndex;
                if (neighbor < 0 || neighbor >= nodes.Length)
                    continue;

                float newCost = currentCost + edge.edgeCost;
                if (newCost < accumulatedCosts[neighbor])
                {
                    accumulatedCosts[neighbor] = newCost;
                    bestNextEdge[neighbor] = FindOppositeEdgeIndex(neighbor, currentPoly);
                    frontier.Enqueue(neighbor);
                }
            }
        }

        private int FindOppositeEdgeIndex(int fromPoly, int toPoly)
        {
            NavPolyNode node = nodes[fromPoly];
            for (int j = 0; j < node.edgeCount; j++)
            {
                int index = node.edgeStartIndex + j;
                if (edges[index].toPolyIndex == toPoly)
                    return index;
            }

            return -1;
        }
    }
}
