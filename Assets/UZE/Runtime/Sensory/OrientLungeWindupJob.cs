using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UZE.Core;

namespace UZE.Sensory
{
    [BurstCompile]
    public struct OrientLungeWindupJob : IJobParallelFor
    {
        private const float MIN_DISTANCE_SQ = 0.0001f;

        public float3 targetPosition;
        public float deltaTime;
        public float turnSpeedRadiansPerSec;
        public float cosAlignThreshold;
        public NativeArray<ZombieInstanceData> agents;

        public void Execute(int index)
        {
            ZombieInstanceData agent = agents[index];
            if (agent.state != ZombieState.LungeWindup)
                return;

            float3 toTarget = targetPosition - agent.position;
            toTarget.y = 0.0f;
            float distSq = math.lengthsq(toTarget);
            if (distSq < MIN_DISTANCE_SQ)
            {
                agent.state = ZombieState.LungeAttack;
                agent.stateTimer = 0.0f;
                agents[index] = agent;
                return;
            }

            RotateTowardsTarget(ref agent, toTarget / math.sqrt(distSq));
            agents[index] = agent;
        }

        private void RotateTowardsTarget(ref ZombieInstanceData agent, float3 targetDir)
        {
            float dot = math.clamp(math.dot(agent.forward, targetDir), -1.0f, 1.0f);
            float currentAngle = math.acos(dot);
            float maxStep = turnSpeedRadiansPerSec * deltaTime;

            if (dot >= cosAlignThreshold || currentAngle <= maxStep)
            {
                agent.forward = targetDir;
                agent.state = ZombieState.LungeAttack;
                agent.stateTimer = 0.0f;
                return;
            }

            float3 cross = math.cross(agent.forward, targetDir);
            float turnSign = cross.y >= 0.0f ? 1.0f : -1.0f;
            quaternion rotation = quaternion.RotateY(turnSign * maxStep);
            agent.forward = math.normalize(math.rotate(rotation, agent.forward));
            agent.stateTimer += deltaTime;
        }
    }
}
