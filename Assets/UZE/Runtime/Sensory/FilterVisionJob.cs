using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UZE.Core;

namespace UZE.Sensory
{
    [BurstCompile]
    public struct FilterVisionJob : IJobParallelFor
    {
        private const float MIN_DISTANCE_SQ = 0.001f;

        [ReadOnly] public NativeArray<ZombieInstanceData> agents;
        public int sliceStart;
        public float3 targetPosition;
        public float eyeHeight;
        public float visionRangeSq;
        public float cosHalfFov;
        public int obstacleLayerMask;

        [WriteOnly] public NativeArray<RaycastCommand> commands;
        [WriteOnly] public NativeArray<int> queryAgentIndices;

        public void Execute(int index)
        {
            int agentIndex = sliceStart + index;
            if (agentIndex >= agents.Length)
            {
                commands[index] = default;
                queryAgentIndices[index] = -1;
                return;
            }

            ZombieInstanceData agent = agents[agentIndex];
            if (agent.state == ZombieState.AggroHorde || agent.state == ZombieState.LungeWindup || agent.state == ZombieState.LungeAttack)
            {
                commands[index] = default;
                queryAgentIndices[index] = -1;
                return;
            }

            EvaluateVision(index, agentIndex, agent);
        }

        private void EvaluateVision(int index, int agentIndex, ZombieInstanceData agent)
        {
            float3 eyePos = agent.position + new float3(0.0f, eyeHeight, 0.0f);
            float3 toTarget = targetPosition - eyePos;
            float distSq = math.lengthsq(toTarget);

            if (distSq < MIN_DISTANCE_SQ || distSq > visionRangeSq)
            {
                commands[index] = default;
                queryAgentIndices[index] = -1;
                return;
            }

            float dist = math.sqrt(distSq);
            float3 dir = toTarget / dist;
            if (math.dot(agent.forward, dir) < cosHalfFov)
            {
                commands[index] = default;
                queryAgentIndices[index] = -1;
                return;
            }

            QueryParameters queryParams = new QueryParameters(obstacleLayerMask, false, QueryTriggerInteraction.Ignore, false);
            commands[index] = new RaycastCommand(eyePos, dir, queryParams, dist);
            queryAgentIndices[index] = agentIndex;
        }
    }
}
