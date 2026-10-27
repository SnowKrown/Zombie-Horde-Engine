using Unity.Mathematics;

namespace UZE.Navigation
{
    public struct NavPolyNode
    {
        public float3 centroid;
        public int edgeStartIndex;
        public int edgeCount;
    }

    public struct NavPolyEdge
    {
        public int toPolyIndex;
        public float3 leftVertex;
        public float3 rightVertex;
        public float3 portalCenter;
        public float edgeCost;
    }
}
