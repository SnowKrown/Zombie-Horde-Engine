using Unity.Mathematics;

namespace UZE.Crowd
{
    public struct CrowdAgentOutput
    {
        public float3 adjustedVelocity;
        public float3 filteredSeparation;
        public float speedMultiplier;
        public CrowdStressType stressType;
        public float rearPressure;
        public float frontResistance;
    }
}
