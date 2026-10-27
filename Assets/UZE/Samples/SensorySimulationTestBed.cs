using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UZE.Core;
using UZE.Sensory;

namespace UZE.Samples
{
    public class SensorySimulationTestBed : MonoBehaviour
    {
        [Header("Simulation Setup")]
        [SerializeField] private int agentCount = 100;
        [SerializeField] private float spawnRadius = 10.0f;
        [SerializeField] private Transform targetTransform;
        [SerializeField] private LayerMask obstacleLayerMask;

        [Header("Perception Parameters")]
        [SerializeField] private float sightRange = 20.0f;
        [SerializeField] private float fieldOfViewAngle = 110.0f;
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private float gunshotRadius = 15.0f;
        [SerializeField] private float footstepRadius = 4.0f;

        [Header("Attack Parameters")]
        [SerializeField] private float lungeTriggerDistance = 2.5f;
        [SerializeField] private float attackDuration = 0.6f;
        [SerializeField] private float turnSpeedDegrees = 360.0f;
        [SerializeField] private float alignAngleThreshold = 15.0f;

        private NativeArray<ZombieInstanceData> agents;
        private NativeArray<RootMotionData> attackFrames;
        private VisionRayScheduler visionScheduler;
        private NoiseEventRegistry noiseRegistry;
        private JobHandle activeHandle;
        private bool isInitialized;

        private void Start()
        {
            InitializeData();
            SpawnAgents();
        }

        private void InitializeData()
        {
            agents = new NativeArray<ZombieInstanceData>(agentCount, Allocator.Persistent);
            attackFrames = new NativeArray<RootMotionData>(18, Allocator.Persistent);
            GenerateAttackFrames();

            visionScheduler = new VisionRayScheduler(agentCount);
            noiseRegistry = new NoiseEventRegistry(32);
            isInitialized = true;
        }

        private void GenerateAttackFrames()
        {
            for (int i = 0; i < attackFrames.Length; i++)
            {
                RootMotionData frame = default;
                frame.translationDelta = new float3(0.0f, 0.0f, 0.25f);
                frame.rotationDeltaY = 0.0f;
                attackFrames[i] = frame;
            }
        }

        private void SpawnAgents()
        {
            Unity.Mathematics.Random rnd = new Unity.Mathematics.Random(9999);
            for (int i = 0; i < agentCount; i++)
            {
                float2 circle = rnd.NextFloat2Direction() * rnd.NextFloat(2.0f, spawnRadius);
                ZombieInstanceData agent = default;
                agent.entityId = i;
                agent.state = ZombieState.Idle;
                agent.position = new float3(transform.position.x + circle.x, transform.position.y, transform.position.z + circle.y);
                agent.forward = math.normalize(new float3(rnd.NextFloat(-1f, 1f), 0.0f, rnd.NextFloat(-1f, 1f)));
                agent.currentSpeed = 3.5f;
                agent.maxHealth = 100.0f;
                agent.currentHealth = 100.0f;
                agents[i] = agent;
            }
        }

        private void Update()
        {
            if (!isInitialized)
                return;

            activeHandle.Complete();
            noiseRegistry.Clear();
            HandleInput();

            float3 targetPos = targetTransform != null ? (float3)targetTransform.position : (float3)transform.position;
            CheckAttackTriggers(targetPos);

            JobHandle hearingHandle = noiseRegistry.ScheduleHearing(agents);
            JobHandle visionHandle = visionScheduler.ScheduleVisionBatch(agents, targetPos, eyeHeight, sightRange, fieldOfViewAngle, obstacleLayerMask.value, hearingHandle);

            OrientLungeWindupJob orientJob = new OrientLungeWindupJob
            {
                targetPosition = targetPos,
                deltaTime = Time.deltaTime,
                turnSpeedRadiansPerSec = math.radians(turnSpeedDegrees),
                cosAlignThreshold = math.cos(math.radians(alignAngleThreshold)),
                agents = agents
            };
            JobHandle orientHandle = orientJob.Schedule(agentCount, 32, visionHandle);

            ApplyRootMotionJob attackJob = new ApplyRootMotionJob
            {
                motionFrames = attackFrames,
                framesPerSecond = 30.0f,
                deltaTime = Time.deltaTime,
                attackDuration = attackDuration,
                agents = agents
            };

            activeHandle = attackJob.Schedule(agentCount, 32, orientHandle);
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || targetTransform == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                noiseRegistry.RegisterNoise(targetTransform.position, gunshotRadius, 0.95f);

            if (keyboard.fKey.wasPressedThisFrame)
                noiseRegistry.RegisterNoise(targetTransform.position, footstepRadius, 0.4f);
        }

        private void CheckAttackTriggers(float3 targetPos)
        {
            float triggerDistSq = lungeTriggerDistance * lungeTriggerDistance;
            for (int i = 0; i < agents.Length; i++)
            {
                ZombieInstanceData agent = agents[i];
                if (agent.state == ZombieState.AggroHorde && math.distancesq(agent.position, targetPos) <= triggerDistSq)
                {
                    agent.state = ZombieState.LungeWindup;
                    agent.stateTimer = 0.0f;
                    agents[i] = agent;
                }
            }
        }

        private void LateUpdate()
        {
            activeHandle.Complete();
        }

        private void OnDestroy()
        {
            if (!isInitialized)
                return;

            try
            {
                activeHandle.Complete();
            }
            catch (System.Exception)
            {
            }

            if (agents.IsCreated)
                agents.Dispose();

            if (attackFrames.IsCreated)
                attackFrames.Dispose();

            visionScheduler?.Dispose();
            noiseRegistry?.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (!isInitialized || !agents.IsCreated)
                return;

            for (int i = 0; i < agents.Length; i++)
            {
                ZombieInstanceData agent = agents[i];
                Gizmos.color = GetStateColor(agent.state);
                Gizmos.DrawSphere(agent.position, 0.35f);
                Gizmos.DrawRay(agent.position, (Vector3)agent.forward * 0.8f);
            }
        }

        private static Color GetStateColor(ZombieState state)
        {
            switch (state)
            {
                case ZombieState.Idle:
                    return Color.green;
                case ZombieState.Wandering:
                    return Color.cyan;
                case ZombieState.Investigating:
                    return Color.yellow;
                case ZombieState.AggroHorde:
                    return Color.red;
                case ZombieState.LungeWindup:
                    return new Color(1.0f, 0.5f, 0.0f);
                case ZombieState.LungeAttack:
                    return Color.magenta;
                default:
                    return Color.white;
            }
        }
    }
}
