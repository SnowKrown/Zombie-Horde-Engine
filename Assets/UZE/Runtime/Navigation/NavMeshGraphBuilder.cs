using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

namespace UZE.Navigation
{
    public static class NavMeshGraphBuilder
    {
        public static bool TryBuildFromNavMesh(
            Allocator allocator,
            out NativeArray<NavPolyNode> nodes,
            out NativeArray<NavPolyEdge> edges)
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            if (tri.indices == null || tri.indices.Length < 3)
            {
                nodes = new NativeArray<NavPolyNode>(0, allocator);
                edges = new NativeArray<NavPolyEdge>(0, allocator);
                return false;
            }

            BuildFromTriangulation(tri, allocator, out nodes, out edges);
            return true;
        }

        public static void BuildFromTriangulation(
            in NavMeshTriangulation tri,
            Allocator allocator,
            out NativeArray<NavPolyNode> nodes,
            out NativeArray<NavPolyEdge> edges)
        {
            int triangleCount = tri.indices.Length / 3;
            nodes = new NativeArray<NavPolyNode>(triangleCount, allocator);
            edges = new NativeArray<NavPolyEdge>(triangleCount * 3, allocator);

            NativeParallelMultiHashMap<EdgeKey, int> edgeMap = new NativeParallelMultiHashMap<EdgeKey, int>(triangleCount * 3, Allocator.Temp);
            PopulateEdgeMap(tri, triangleCount, ref edgeMap);
            BuildNodesAndEdges(tri, triangleCount, ref edgeMap, ref nodes, ref edges);
            edgeMap.Dispose();
        }

        private static void PopulateEdgeMap(
            in NavMeshTriangulation tri,
            int triangleCount,
            ref NativeParallelMultiHashMap<EdgeKey, int> edgeMap)
        {
            for (int t = 0; t < triangleCount; t++)
            {
                int baseIndex = t * 3;
                int i0 = tri.indices[baseIndex];
                int i1 = tri.indices[baseIndex + 1];
                int i2 = tri.indices[baseIndex + 2];

                edgeMap.Add(new EdgeKey(i0, i1), t);
                edgeMap.Add(new EdgeKey(i1, i2), t);
                edgeMap.Add(new EdgeKey(i2, i0), t);
            }
        }

        private static void BuildNodesAndEdges(
            in NavMeshTriangulation tri,
            int triangleCount,
            ref NativeParallelMultiHashMap<EdgeKey, int> edgeMap,
            ref NativeArray<NavPolyNode> nodes,
            ref NativeArray<NavPolyEdge> edges)
        {
            for (int t = 0; t < triangleCount; t++)
            {
                int baseIndex = t * 3;
                float3 v0 = tri.vertices[tri.indices[baseIndex]];
                float3 v1 = tri.vertices[tri.indices[baseIndex + 1]];
                float3 v2 = tri.vertices[tri.indices[baseIndex + 2]];
                float3 centroid = (v0 + v1 + v2) / 3.0f;

                int edgeStart = t * 3;
                edges[edgeStart] = CreateEdge(tri.indices[baseIndex], tri.indices[baseIndex + 1], v0, v1, centroid, t, ref edgeMap, tri);
                edges[edgeStart + 1] = CreateEdge(tri.indices[baseIndex + 1], tri.indices[baseIndex + 2], v1, v2, centroid, t, ref edgeMap, tri);
                edges[edgeStart + 2] = CreateEdge(tri.indices[baseIndex + 2], tri.indices[baseIndex], v2, v0, centroid, t, ref edgeMap, tri);

                nodes[t] = new NavPolyNode { centroid = centroid, edgeStartIndex = edgeStart, edgeCount = 3 };
            }
        }

        private static NavPolyEdge CreateEdge(
            int idxA,
            int idxB,
            float3 left,
            float3 right,
            float3 fromCentroid,
            int currentTriangle,
            ref NativeParallelMultiHashMap<EdgeKey, int> edgeMap,
            in NavMeshTriangulation tri)
        {
            int neighbor = FindNeighborTriangle(new EdgeKey(idxA, idxB), currentTriangle, ref edgeMap);
            float3 portalCenter = (left + right) * 0.5f;
            float cost = math.distance(fromCentroid, portalCenter);

            if (neighbor >= 0)
            {
                float3 n0 = tri.vertices[tri.indices[neighbor * 3]];
                float3 n1 = tri.vertices[tri.indices[neighbor * 3 + 1]];
                float3 n2 = tri.vertices[tri.indices[neighbor * 3 + 2]];
                float3 toCentroid = (n0 + n1 + n2) / 3.0f;
                cost += math.distance(portalCenter, toCentroid);
            }

            return new NavPolyEdge { toPolyIndex = neighbor, leftVertex = left, rightVertex = right, portalCenter = portalCenter, edgeCost = cost };
        }

        private static int FindNeighborTriangle(EdgeKey key, int currentTriangle, ref NativeParallelMultiHashMap<EdgeKey, int> edgeMap)
        {
            if (edgeMap.TryGetFirstValue(key, out int candidate, out NativeParallelMultiHashMapIterator<EdgeKey> it))
            {
                do
                {
                    if (candidate != currentTriangle)
                        return candidate;
                }
                while (edgeMap.TryGetNextValue(out candidate, ref it));
            }

            return -1;
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public int minIndex;
            public int maxIndex;

            public EdgeKey(int a, int b)
            {
                if (a < b)
                {
                    minIndex = a;
                    maxIndex = b;
                }
                else
                {
                    minIndex = b;
                    maxIndex = a;
                }
            }

            public bool Equals(EdgeKey other)
            {
                return minIndex == other.minIndex && maxIndex == other.maxIndex;
            }

            public override int GetHashCode()
            {
                return (minIndex * 397) ^ maxIndex;
            }
        }
    }
}
