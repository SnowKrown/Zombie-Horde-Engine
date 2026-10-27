using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace UZE.Navigation
{
    [BurstCompile]
    public static class FunnelSteeringCalculator
    {
        private static float TriArea2D(float3 a, float3 b, float3 c) => (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);

        public static float3 CalculateSteeringPoint(
            float3 agentPos,
            int currentPolyIndex,
            int targetPolyIndex,
            float3 playerPos,
            float agentRadius,
            int maxLookahead,
            [ReadOnly] NativeArray<NavPolyNode> nodes,
            [ReadOnly] NativeArray<NavPolyEdge> edges,
            [ReadOnly] NativeArray<int> bestNextEdge)
        {
            if (currentPolyIndex == targetPolyIndex)
                return playerPos;

            float3 apex = agentPos;
            float3 portalLeft = agentPos;
            float3 portalRight = agentPos;
            int activePoly = currentPolyIndex;
            bool isInitialized = false;

            for (int step = 0; step < maxLookahead; step++)
            {
                if (activePoly == targetPolyIndex)
                    return ResolveTarget(apex, portalLeft, portalRight, playerPos);

                if (StepPortal(ref activePoly, ref apex, ref portalLeft, ref portalRight, ref isInitialized, agentRadius, edges, bestNextEdge, out float3 exitPoint))
                    return exitPoint;
            }

            return (portalLeft + portalRight) * 0.5f;
        }

        private static bool StepPortal(
            ref int activePoly,
            ref float3 apex,
            ref float3 portalLeft,
            ref float3 portalRight,
            ref bool isInitialized,
            float agentRadius,
            [ReadOnly] NativeArray<NavPolyEdge> edges,
            [ReadOnly] NativeArray<int> bestNextEdge,
            out float3 exitPoint)
        {
            exitPoint = float3.zero;
            int edgeIndex = bestNextEdge[activePoly];
            if (edgeIndex < 0)
                return false;

            NavPolyEdge edge = edges[edgeIndex];
            ComputeShrunkPortal(edge, agentRadius, out float3 nextLeft, out float3 nextRight);
            activePoly = edge.toPolyIndex;

            if (!isInitialized)
            {
                portalLeft = nextLeft;
                portalRight = nextRight;
                isInitialized = true;
                return false;
            }

            return UpdatePortalBounds(ref apex, ref portalLeft, ref portalRight, nextLeft, nextRight, out exitPoint);
        }

        private static void ComputeShrunkPortal(NavPolyEdge edge, float agentRadius, out float3 shrunkLeft, out float3 shrunkRight)
        {
            float3 edgeDelta = edge.rightVertex - edge.leftVertex;
            float portalWidth = math.length(edgeDelta);
            float3 dir = edgeDelta / math.max(portalWidth, 0.0001f);
            float shrink = math.min(agentRadius, portalWidth * 0.35f);

            shrunkLeft = edge.leftVertex + dir * shrink;
            shrunkRight = edge.rightVertex - dir * shrink;
        }

        private static bool UpdatePortalBounds(
            ref float3 apex,
            ref float3 portalLeft,
            ref float3 portalRight,
            float3 nextLeft,
            float3 nextRight,
            out float3 exitPoint)
        {
            exitPoint = float3.zero;

            if (TriArea2D(apex, portalRight, nextRight) <= 0.0f)
            {
                if (math.all(apex == portalRight) || TriArea2D(apex, portalLeft, nextRight) > 0.0f)
                {
                    portalRight = nextRight;
                }
                else
                {
                    exitPoint = portalLeft;
                    return true;
                }
            }

            if (TriArea2D(apex, portalLeft, nextLeft) >= 0.0f)
            {
                if (math.all(apex == portalLeft) || TriArea2D(apex, portalRight, nextLeft) < 0.0f)
                {
                    portalLeft = nextLeft;
                }
                else
                {
                    exitPoint = portalRight;
                    return true;
                }
            }

            return false;
        }

        private static float3 ResolveTarget(float3 apex, float3 left, float3 right, float3 target)
        {
            if (TriArea2D(apex, left, target) <= 0.0f && TriArea2D(apex, right, target) >= 0.0f)
                return target;

            if (TriArea2D(apex, left, target) > 0.0f)
                return left;

            return right;
        }
    }
}
