using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UZE.Core;

namespace UZE.Sensory
{
    [BurstCompile]
    public struct ProcessVisionHitsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RaycastHit> hits;
        [ReadOnly] public NativeArray<int> queryAgentIndices;
        public NativeArray<ZombieInstanceData> sliceAgents;

        public void Execute(int index)
        {
            if (queryAgentIndices[index] < 0)
                return;

            RaycastHit hit = hits[index];
            if (hit.colliderInstanceID == 0)
            {
                ZombieInstanceData agent = sliceAgents[index];
                agent.state = ZombieState.AggroHorde;
                agent.stateTimer = 0.0f;
                sliceAgents[index] = agent;
            }
        }
    }
}
