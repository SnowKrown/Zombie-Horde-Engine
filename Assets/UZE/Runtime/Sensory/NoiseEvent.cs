using Unity.Mathematics;

namespace UZE.Sensory
{
    public struct NoiseEvent
    {
        public float3 position;
        public float radiusSq;
        public float alertThreshold;

        public NoiseEvent(float3 position, float radius, float alertThreshold)
        {
            this.position = position;
            this.radiusSq = radius * radius;
            this.alertThreshold = alertThreshold;
        }
    }
}
