using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UZE.Navigation;

namespace UZE.Tests.Runtime
{
    public class NavigationTests
    {
        [Test]
        public void GeneratePolyFlowField_ComputesShortestPath_AcrossConnectedPolygons()
        {
            NativeArray<NavPolyNode> nodes = new NativeArray<NavPolyNode>(3, Allocator.TempJob);
            NativeArray<NavPolyEdge> edges = new NativeArray<NavPolyEdge>(6, Allocator.TempJob);
            NativeArray<float> costs = new NativeArray<float>(3, Allocator.TempJob);
            NativeArray<int> bestNextEdge = new NativeArray<int>(3, Allocator.TempJob);

            SetupLinearThreePolygonGraph(ref nodes, ref edges);

            GeneratePolyFlowFieldWithEdgesJob job = new GeneratePolyFlowFieldWithEdgesJob
            {
                targetPolyIndex = 2,
                nodes = nodes,
                edges = edges,
                accumulatedCosts = costs,
                bestNextEdge = bestNextEdge
            };

            job.Schedule().Complete();

            Assert.AreEqual(0.0f, costs[2]);
            Assert.AreEqual(10.0f, costs[1]);
            Assert.AreEqual(20.0f, costs[0]);
            Assert.AreEqual(0, bestNextEdge[0]);
            Assert.AreEqual(2, bestNextEdge[1]);

            nodes.Dispose();
            edges.Dispose();
            costs.Dispose();
            bestNextEdge.Dispose();
        }

        [Test]
        public void FunnelSteering_ReturnsPlayerPos_WhenInTargetPolygon()
        {
            NativeArray<NavPolyNode> nodes = new NativeArray<NavPolyNode>(1, Allocator.Temp);
            NativeArray<NavPolyEdge> edges = new NativeArray<NavPolyEdge>(1, Allocator.Temp);
            NativeArray<int> bestNextEdge = new NativeArray<int>(1, Allocator.Temp);

            float3 agentPos = new float3(1.0f, 0.0f, 1.0f);
            float3 playerPos = new float3(5.0f, 0.0f, 5.0f);

            float3 steering = FunnelSteeringCalculator.CalculateSteeringPoint(
                agentPos, 0, 0, playerPos, 0.35f, 3, nodes, edges, bestNextEdge);

            Assert.AreEqual(playerPos, steering);

            nodes.Dispose();
            edges.Dispose();
            bestNextEdge.Dispose();
        }

        [Test]
        public void TrackCurrentPolygonJob_KeepsAgent_WhenInsidePolygon()
        {
            NativeArray<float3> positions = new NativeArray<float3>(1, Allocator.TempJob);
            NativeArray<int> polyIndices = new NativeArray<int>(1, Allocator.TempJob);
            NativeArray<NavPolyNode> nodes = new NativeArray<NavPolyNode>(1, Allocator.TempJob);
            NativeArray<NavPolyEdge> edges = new NativeArray<NavPolyEdge>(3, Allocator.TempJob);

            positions[0] = new float3(1.0f, 0.0f, 1.0f);
            polyIndices[0] = 0;
            nodes[0] = new NavPolyNode { centroid = new float3(1.0f, 0.0f, 1.0f), edgeStartIndex = 0, edgeCount = 3 };

            edges[0] = new NavPolyEdge { toPolyIndex = -1, leftVertex = new float3(0.0f, 0.0f, 0.0f), rightVertex = new float3(3.0f, 0.0f, 0.0f) };
            edges[1] = new NavPolyEdge { toPolyIndex = -1, leftVertex = new float3(3.0f, 0.0f, 0.0f), rightVertex = new float3(0.0f, 0.0f, 3.0f) };
            edges[2] = new NavPolyEdge { toPolyIndex = -1, leftVertex = new float3(0.0f, 0.0f, 3.0f), rightVertex = new float3(0.0f, 0.0f, 0.0f) };

            TrackCurrentPolygonJob job = new TrackCurrentPolygonJob
            {
                agentPositions = positions,
                currentPolyIndices = polyIndices,
                nodes = nodes,
                edges = edges
            };

            job.Schedule(1, 1).Complete();

            Assert.AreEqual(0, polyIndices[0]);

            positions.Dispose();
            polyIndices.Dispose();
            nodes.Dispose();
            edges.Dispose();
        }

        [Test]
        public void NavMeshGraphBuilder_BuildsConnectedGraph_FromTriangulation()
        {
            NavMeshTriangulation tri = new NavMeshTriangulation
            {
                vertices = new Vector3[]
                {
                    new Vector3(0.0f, 0.0f, 0.0f),
                    new Vector3(2.0f, 0.0f, 0.0f),
                    new Vector3(2.0f, 0.0f, 2.0f),
                    new Vector3(0.0f, 0.0f, 2.0f)
                },
                indices = new int[]
                {
                    0, 1, 2,
                    0, 2, 3
                }
            };

            NavMeshGraphBuilder.BuildFromTriangulation(tri, Allocator.Temp, out NativeArray<NavPolyNode> nodes, out NativeArray<NavPolyEdge> edges);

            Assert.AreEqual(2, nodes.Length);
            Assert.AreEqual(6, edges.Length);
            Assert.AreEqual(1, edges[2].toPolyIndex);
            Assert.AreEqual(0, edges[3].toPolyIndex);
            Assert.AreEqual(-1, edges[5].toPolyIndex);

            nodes.Dispose();
            edges.Dispose();
        }

        private static void SetupLinearThreePolygonGraph(ref NativeArray<NavPolyNode> nodes, ref NativeArray<NavPolyEdge> edges)
        {
            nodes[0] = new NavPolyNode { centroid = new float3(0.0f, 0.0f, 0.0f), edgeStartIndex = 0, edgeCount = 1 };
            nodes[1] = new NavPolyNode { centroid = new float3(10.0f, 0.0f, 0.0f), edgeStartIndex = 1, edgeCount = 2 };
            nodes[2] = new NavPolyNode { centroid = new float3(20.0f, 0.0f, 0.0f), edgeStartIndex = 3, edgeCount = 1 };

            edges[0] = new NavPolyEdge { toPolyIndex = 1, edgeCost = 10.0f };
            edges[1] = new NavPolyEdge { toPolyIndex = 0, edgeCost = 10.0f };
            edges[2] = new NavPolyEdge { toPolyIndex = 2, edgeCost = 10.0f };
            edges[3] = new NavPolyEdge { toPolyIndex = 1, edgeCost = 10.0f };
        }
    }
}
