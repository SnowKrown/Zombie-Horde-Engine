using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UZE.Core;

namespace UZE.Sensory
{
    public class NoiseEventRegistry : IDisposable
    {
        private const int DEFAULT_CAPACITY = 64;
        private const int BATCH_SIZE = 64;

        private NativeList<NoiseEvent> noiseEvents;
        private bool isDisposed;

        public NoiseEventRegistry(int initialCapacity = DEFAULT_CAPACITY)
        {
            noiseEvents = new NativeList<NoiseEvent>(initialCapacity, Allocator.Persistent);
        }

        public void RegisterNoise(float3 position, float radius, float alertThreshold)
        {
            if (isDisposed)
                return;

            noiseEvents.Add(new NoiseEvent(position, radius, alertThreshold));
        }

        public JobHandle ScheduleHearing(NativeArray<ZombieInstanceData> agents, JobHandle dependency = default)
        {
            if (isDisposed || noiseEvents.Length == 0)
                return dependency;

            EvaluateHearingJob job = new EvaluateHearingJob
            {
                noiseEvents = noiseEvents.AsArray(),
                agents = agents
            };

            return job.Schedule(agents.Length, BATCH_SIZE, dependency);
        }

        public void Clear()
        {
            if (noiseEvents.IsCreated)
                noiseEvents.Clear();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            if (noiseEvents.IsCreated)
                noiseEvents.Dispose();

            isDisposed = true;
        }
    }
}
