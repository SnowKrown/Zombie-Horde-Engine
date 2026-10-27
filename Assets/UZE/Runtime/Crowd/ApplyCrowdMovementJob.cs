using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UZE.Crowd
{
    [BurstCompile]
    public struct ApplyCrowdMovementJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<CrowdAgentOutput> outputs;
        public float deltaTime;

        public NativeArray<float3> positions;
        public NativeArray<float3> forwardDirections;

        public void Execute(int index)
        {
            CrowdAgentOutput output = outputs[index];
            float3 velocity = output.adjustedVelocity;
            positions[index] += velocity * deltaTime;

            float speedSq = math.lengthsq(velocity.xz);
            if (speedSq > 0.001f)
            {
                float3 newForward = math.normalize(new float3(velocity.x, 0.0f, velocity.z));
                forwardDirections[index] = math.normalize(math.lerp(forwardDirections[index], newForward, deltaTime * 10.0f));
            }
        }
    }
}
