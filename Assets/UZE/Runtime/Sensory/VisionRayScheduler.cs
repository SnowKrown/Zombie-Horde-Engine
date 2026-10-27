using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UZE.Core;

namespace UZE.Sensory
{
    public class VisionRayScheduler : IDisposable
    {
        private const int SLICE_COUNT = 4;
        private const int MIN_COMMANDS_PER_JOB = 16;
        private const int BATCH_SIZE = 32;

        private NativeArray<RaycastCommand> commands;
        private NativeArray<RaycastHit> hits;
        private NativeArray<int> queryAgentIndices;
        private int currentSliceIndex;
        private int maxSliceCapacity;
        private JobHandle activeJobHandle;
        private bool isDisposed;

        public VisionRayScheduler(int maxAgentCapacity)
        {
            maxSliceCapacity = (maxAgentCapacity + SLICE_COUNT - 1) / SLICE_COUNT;
            commands = new NativeArray<RaycastCommand>(maxSliceCapacity, Allocator.Persistent);
            hits = new NativeArray<RaycastHit>(maxSliceCapacity, Allocator.Persistent);
            queryAgentIndices = new NativeArray<int>(maxSliceCapacity, Allocator.Persistent);
        }

        public JobHandle ScheduleVisionBatch(
            NativeArray<ZombieInstanceData> agents,
            float3 targetPosition,
            float eyeHeight,
            float visionRange,
            float fovAngle,
            int obstacleLayerMask,
            JobHandle dependency = default)
        {
            if (isDisposed || agents.Length == 0)
                return dependency;

            EnsureCapacity(agents.Length);
            activeJobHandle.Complete();

            int sliceSize = (agents.Length + SLICE_COUNT - 1) / SLICE_COUNT;
            int sliceStart = currentSliceIndex * sliceSize;
            int currentBatchCount = math.max(0, math.min(sliceSize, agents.Length - sliceStart));
            currentSliceIndex = (currentSliceIndex + 1) % SLICE_COUNT;

            if (currentBatchCount <= 0)
                return dependency;

            return ScheduleJobs(agents, targetPosition, eyeHeight, visionRange, fovAngle, obstacleLayerMask, sliceStart, currentBatchCount, dependency);
        }

        private JobHandle ScheduleJobs(
            NativeArray<ZombieInstanceData> agents,
            float3 targetPosition,
            float eyeHeight,
            float visionRange,
            float fovAngle,
            int obstacleLayerMask,
            int sliceStart,
            int count,
            JobHandle dependency)
        {
            NativeArray<RaycastCommand> sliceCommands = commands.GetSubArray(0, count);
            NativeArray<RaycastHit> sliceHits = hits.GetSubArray(0, count);
            NativeArray<int> sliceIndices = queryAgentIndices.GetSubArray(0, count);
            NativeArray<ZombieInstanceData> sliceAgents = agents.GetSubArray(sliceStart, count);

            JobHandle rayHandle = ScheduleRaycastBatch(agents, targetPosition, eyeHeight, visionRange, fovAngle, obstacleLayerMask, sliceStart, count, sliceCommands, sliceHits, sliceIndices, dependency);
            return ScheduleProcessHits(sliceAgents, sliceHits, sliceIndices, count, rayHandle);
        }

        private JobHandle ScheduleRaycastBatch(
            NativeArray<ZombieInstanceData> agents,
            float3 targetPosition,
            float eyeHeight,
            float visionRange,
            float fovAngle,
            int obstacleLayerMask,
            int sliceStart,
            int count,
            NativeArray<RaycastCommand> sliceCommands,
            NativeArray<RaycastHit> sliceHits,
            NativeArray<int> sliceIndices,
            JobHandle dependency)
        {
            FilterVisionJob filterJob = new FilterVisionJob
            {
                agents = agents,
                sliceStart = sliceStart,
                targetPosition = targetPosition,
                eyeHeight = eyeHeight,
                visionRangeSq = visionRange * visionRange,
                cosHalfFov = math.cos(math.radians(fovAngle * 0.5f)),
                obstacleLayerMask = obstacleLayerMask,
                commands = sliceCommands,
                queryAgentIndices = sliceIndices
            };

            JobHandle filterHandle = filterJob.Schedule(count, BATCH_SIZE, dependency);
            return RaycastCommand.ScheduleBatch(sliceCommands, sliceHits, MIN_COMMANDS_PER_JOB, filterHandle);
        }

        private JobHandle ScheduleProcessHits(
            NativeArray<ZombieInstanceData> sliceAgents,
            NativeArray<RaycastHit> sliceHits,
            NativeArray<int> sliceIndices,
            int count,
            JobHandle rayHandle)
        {
            ProcessVisionHitsJob hitsJob = new ProcessVisionHitsJob
            {
                hits = sliceHits,
                queryAgentIndices = sliceIndices,
                sliceAgents = sliceAgents
            };

            activeJobHandle = hitsJob.Schedule(count, BATCH_SIZE, rayHandle);
            return activeJobHandle;
        }

        private void EnsureCapacity(int agentCount)
        {
            int requiredSliceCapacity = (agentCount + SLICE_COUNT - 1) / SLICE_COUNT;
            if (requiredSliceCapacity <= maxSliceCapacity)
                return;

            CompleteActiveJobSafe();
            DisposeContainers();

            maxSliceCapacity = requiredSliceCapacity;
            commands = new NativeArray<RaycastCommand>(maxSliceCapacity, Allocator.Persistent);
            hits = new NativeArray<RaycastHit>(maxSliceCapacity, Allocator.Persistent);
            queryAgentIndices = new NativeArray<int>(maxSliceCapacity, Allocator.Persistent);
        }

        private void CompleteActiveJobSafe()
        {
            try
            {
                activeJobHandle.Complete();
            }
            catch (Exception)
            {
            }
        }

        private void DisposeContainers()
        {
            if (commands.IsCreated)
                commands.Dispose();

            if (hits.IsCreated)
                hits.Dispose();

            if (queryAgentIndices.IsCreated)
                queryAgentIndices.Dispose();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            CompleteActiveJobSafe();
            DisposeContainers();
            isDisposed = true;
        }
    }
}
