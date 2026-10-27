using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UZE.Core;

namespace UZE.Sensory
{
    [BurstCompile]
    public struct ApplyRootMotionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RootMotionData> motionFrames;
        public float framesPerSecond;
        public float deltaTime;
        public float attackDuration;
        public NativeArray<ZombieInstanceData> agents;

        public void Execute(int index)
        {
            ZombieInstanceData agent = agents[index];
            if (agent.state != ZombieState.LungeAttack)
                return;

            if (motionFrames.Length == 0)
                return;

            int frameIndex = (int)math.floor(agent.stateTimer * framesPerSecond);
            frameIndex = math.clamp(frameIndex, 0, motionFrames.Length - 1);
            ApplyFrameMotion(ref agent, motionFrames[frameIndex]);

            agent.stateTimer += deltaTime;
            if (agent.stateTimer >= attackDuration)
            {
                agent.state = ZombieState.AggroHorde;
                agent.stateTimer = 0.0f;
            }

            agents[index] = agent;
        }

        private static void ApplyFrameMotion(ref ZombieInstanceData agent, RootMotionData frameData)
        {
            quaternion yawRotation = quaternion.RotateY(math.radians(frameData.rotationDeltaY));
            agent.forward = math.normalize(math.rotate(yawRotation, agent.forward));

            quaternion orientation = quaternion.LookRotationSafe(agent.forward, new float3(0.0f, 1.0f, 0.0f));
            float3 worldTranslation = math.rotate(orientation, frameData.translationDelta);
            agent.position += worldTranslation;
        }
    }
}
