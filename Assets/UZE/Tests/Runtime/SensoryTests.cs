using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UZE.Core;
using UZE.Sensory;

namespace UZE.Tests.Runtime
{
    public class SensoryTests
    {
        [Test]
        public void EvaluateHearingJob_WhenAlertAboveThreshold_TransitionsToAggroHorde()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<NoiseEvent> noiseEvents = new NativeArray<NoiseEvent>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Idle;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agents[0] = agent;

            noiseEvents[0] = new NoiseEvent(new float3(2.0f, 0.0f, 0.0f), 5.0f, 0.9f);

            EvaluateHearingJob job = new EvaluateHearingJob { noiseEvents = noiseEvents, agents = agents };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.AggroHorde, agents[0].state);

            agents.Dispose();
            noiseEvents.Dispose();
        }

        [Test]
        public void EvaluateHearingJob_WhenAlertBelowThreshold_TransitionsToInvestigating()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<NoiseEvent> noiseEvents = new NativeArray<NoiseEvent>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Wandering;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agents[0] = agent;

            noiseEvents[0] = new NoiseEvent(new float3(1.0f, 0.0f, 0.0f), 5.0f, 0.4f);

            EvaluateHearingJob job = new EvaluateHearingJob { noiseEvents = noiseEvents, agents = agents };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.Investigating, agents[0].state);

            agents.Dispose();
            noiseEvents.Dispose();
        }

        [Test]
        public void EvaluateHearingJob_WhenOutOfRadius_KeepsPreviousState()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<NoiseEvent> noiseEvents = new NativeArray<NoiseEvent>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Idle;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agents[0] = agent;

            noiseEvents[0] = new NoiseEvent(new float3(10.0f, 0.0f, 0.0f), 5.0f, 0.9f);

            EvaluateHearingJob job = new EvaluateHearingJob { noiseEvents = noiseEvents, agents = agents };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.Idle, agents[0].state);

            agents.Dispose();
            noiseEvents.Dispose();
        }

        [Test]
        public void FilterVisionJob_WhenTargetInFovAndRange_GeneratesValidQuery()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(1, Allocator.TempJob);
            NativeArray<int> queryIndices = new NativeArray<int>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Idle;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agent.forward = new float3(0.0f, 0.0f, 1.0f);
            agents[0] = agent;

            FilterVisionJob job = new FilterVisionJob
            {
                agents = agents,
                sliceStart = 0,
                targetPosition = new float3(0.0f, 0.0f, 10.0f),
                eyeHeight = 1.6f,
                visionRangeSq = 25.0f * 25.0f,
                cosHalfFov = math.cos(math.radians(55.0f)),
                obstacleLayerMask = ~0,
                commands = commands,
                queryAgentIndices = queryIndices
            };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(0, queryIndices[0]);

            agents.Dispose();
            commands.Dispose();
            queryIndices.Dispose();
        }

        [Test]
        public void FilterVisionJob_WhenTargetBehindAgent_DoesNotGenerateQuery()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(1, Allocator.TempJob);
            NativeArray<int> queryIndices = new NativeArray<int>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Idle;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agent.forward = new float3(0.0f, 0.0f, 1.0f);
            agents[0] = agent;

            FilterVisionJob job = new FilterVisionJob
            {
                agents = agents,
                sliceStart = 0,
                targetPosition = new float3(0.0f, 0.0f, -10.0f),
                eyeHeight = 1.6f,
                visionRangeSq = 25.0f * 25.0f,
                cosHalfFov = math.cos(math.radians(55.0f)),
                obstacleLayerMask = ~0,
                commands = commands,
                queryAgentIndices = queryIndices
            };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(-1, queryIndices[0]);

            agents.Dispose();
            commands.Dispose();
            queryIndices.Dispose();
        }

        [Test]
        public void ProcessVisionHitsJob_WhenRayClear_TransitionsToAggroHorde()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<RaycastHit> hits = new NativeArray<RaycastHit>(1, Allocator.TempJob);
            NativeArray<int> queryIndices = new NativeArray<int>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Idle;
            agents[0] = agent;

            hits[0] = default;
            queryIndices[0] = 0;

            ProcessVisionHitsJob job = new ProcessVisionHitsJob { hits = hits, queryAgentIndices = queryIndices, sliceAgents = agents };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.AggroHorde, agents[0].state);

            agents.Dispose();
            hits.Dispose();
            queryIndices.Dispose();
        }

        [Test]
        public void ApplyRootMotionJob_AdvancesPositionAndOrientation()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<RootMotionData> frames = new NativeArray<RootMotionData>(2, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.LungeAttack;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agent.forward = new float3(0.0f, 0.0f, 1.0f);
            agent.stateTimer = 0.0f;
            agents[0] = agent;

            RootMotionData frame0 = default;
            frame0.translationDelta = new float3(0.0f, 0.0f, 0.5f);
            frame0.rotationDeltaY = 5.0f;
            frames[0] = frame0;

            ApplyRootMotionJob job = new ApplyRootMotionJob
            {
                motionFrames = frames,
                framesPerSecond = 30.0f,
                deltaTime = 0.033f,
                attackDuration = 0.5f,
                agents = agents
            };
            job.Schedule(1, 1).Complete();

            Assert.Greater(agents[0].position.z, 0.1f);
            Assert.AreNotEqual(0.0f, agents[0].forward.x);

            agents.Dispose();
            frames.Dispose();
        }

        [Test]
        public void ApplyRootMotionJob_WhenDurationExceeded_TransitionsToAggroHorde()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            NativeArray<RootMotionData> frames = new NativeArray<RootMotionData>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.LungeAttack;
            agent.stateTimer = 0.45f;
            agents[0] = agent;

            ApplyRootMotionJob job = new ApplyRootMotionJob
            {
                motionFrames = frames,
                framesPerSecond = 30.0f,
                deltaTime = 0.1f,
                attackDuration = 0.5f,
                agents = agents
            };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.AggroHorde, agents[0].state);
            Assert.AreEqual(0.0f, agents[0].stateTimer);

            agents.Dispose();
            frames.Dispose();
        }

        [Test]
        public void NoiseEventRegistry_RegistersAndClearsEvents()
        {
            NoiseEventRegistry registry = new NoiseEventRegistry(16);
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);

            ZombieInstanceData agent = default;
            agent.state = ZombieState.Idle;
            agents[0] = agent;

            registry.RegisterNoise(new float3(1.0f, 0.0f, 0.0f), 5.0f, 0.9f);
            registry.ScheduleHearing(agents).Complete();

            Assert.AreEqual(ZombieState.AggroHorde, agents[0].state);

            registry.Clear();
            registry.Dispose();
            agents.Dispose();
        }

        [Test]
        public void VisionRayScheduler_ExecutesAllSlices_WithoutSafetyExceptions()
        {
            VisionRayScheduler scheduler = new VisionRayScheduler(100);
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(100, Allocator.TempJob);

            for (int i = 0; i < agents.Length; i++)
            {
                ZombieInstanceData agent = default;
                agent.entityId = i;
                agent.state = ZombieState.Idle;
                agent.position = new float3(0.0f, 0.0f, i * 0.1f);
                agent.forward = new float3(0.0f, 0.0f, 1.0f);
                agents[i] = agent;
            }

            for (int slice = 0; slice < 4; slice++)
            {
                JobHandle handle = scheduler.ScheduleVisionBatch(agents, new float3(0.0f, 0.0f, 5.0f), 1.6f, 25.0f, 110.0f, 0);
                handle.Complete();
            }

            scheduler.Dispose();
            agents.Dispose();
        }

        [Test]
        public void OrientLungeWindupJob_RotatesAgentTowardsTarget()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            ZombieInstanceData agent = default;
            agent.state = ZombieState.LungeWindup;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agent.forward = new float3(1.0f, 0.0f, 0.0f);
            agents[0] = agent;

            OrientLungeWindupJob job = new OrientLungeWindupJob
            {
                targetPosition = new float3(0.0f, 0.0f, 10.0f),
                deltaTime = 0.1f,
                turnSpeedRadiansPerSec = math.PI * 0.5f,
                cosAlignThreshold = 0.99f,
                agents = agents
            };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.LungeWindup, agents[0].state);
            Assert.Greater(agents[0].forward.z, 0.05f);
            Assert.Less(agents[0].forward.x, 1.0f);

            agents.Dispose();
        }

        [Test]
        public void OrientLungeWindupJob_WhenAligned_TransitionsToLungeAttack()
        {
            NativeArray<ZombieInstanceData> agents = new NativeArray<ZombieInstanceData>(1, Allocator.TempJob);
            ZombieInstanceData agent = default;
            agent.state = ZombieState.LungeWindup;
            agent.position = new float3(0.0f, 0.0f, 0.0f);
            agent.forward = math.normalize(new float3(0.02f, 0.0f, 1.0f));
            agents[0] = agent;

            OrientLungeWindupJob job = new OrientLungeWindupJob
            {
                targetPosition = new float3(0.0f, 0.0f, 10.0f),
                deltaTime = 0.1f,
                turnSpeedRadiansPerSec = math.PI,
                cosAlignThreshold = 0.98f,
                agents = agents
            };
            job.Schedule(1, 1).Complete();

            Assert.AreEqual(ZombieState.LungeAttack, agents[0].state);
            Assert.AreEqual(0.0f, agents[0].stateTimer);
            Assert.AreEqual(new float3(0.0f, 0.0f, 1.0f), agents[0].forward);

            agents.Dispose();
        }
    }
}
