using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UZE.Core;

namespace UZE.Sensory
{
    [BurstCompile]
    public struct EvaluateHearingJob : IJobParallelFor
    {
        private const float AGGRO_ALERT_THRESHOLD = 0.8f;

        [ReadOnly] public NativeArray<NoiseEvent> noiseEvents;
        public NativeArray<ZombieInstanceData> agents;

        public void Execute(int index)
        {
            ZombieInstanceData agent = agents[index];
            if (agent.state == ZombieState.AggroHorde || agent.state == ZombieState.LungeWindup || agent.state == ZombieState.LungeAttack)
                return;

            for (int i = 0; i < noiseEvents.Length; i++)
            {
                NoiseEvent noise = noiseEvents[i];
                if (math.distancesq(agent.position, noise.position) <= noise.radiusSq)
                {
                    ApplyHearingReaction(ref agent, noise);
                    agents[index] = agent;
                    if (agent.state == ZombieState.AggroHorde)
                        return;
                }
            }
        }

        private static void ApplyHearingReaction(ref ZombieInstanceData agent, NoiseEvent noise)
        {
            if (noise.alertThreshold >= AGGRO_ALERT_THRESHOLD)
            {
                agent.state = ZombieState.AggroHorde;
                agent.stateTimer = 0.0f;
                return;
            }

            if (agent.state != ZombieState.AggroHorde)
            {
                agent.state = ZombieState.Investigating;
                agent.stateTimer = 0.0f;
            }
        }
    }
}
