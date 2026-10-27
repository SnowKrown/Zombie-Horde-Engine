using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UZE.Crowd;

namespace UZE.Samples
{
    public class CrowdSimulationTestBed : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [SerializeField] private int agentCount = 200;
        [SerializeField] private float spawnRadius = 8.0f;
        [SerializeField] private float agentRadius = 0.35f;
        [SerializeField] private float baseSpeed = 3.5f;
        [SerializeField] private float basePoise = 50.0f;
        [SerializeField] private Transform targetTransform;

        [Header("Crowd Parameters")]
        [SerializeField] private float cellSize = 1.5f;
        [SerializeField] private float stumbleThreshold = 1.2f;
        [SerializeField] private float crushThreshold = 2.0f;
        [SerializeField] private float lateralLimit = 1.5f;

        private NativeArray<float3> positions;
        private NativeArray<float3> desiredVelocities;
        private NativeArray<float3> forwardDirections;
        private NativeArray<float> radii;
        private NativeArray<float> poises;
        private NativeArray<CrowdAgentOutput> outputs;
        private NativeParallelMultiHashMap<int, int> spatialMap;
        private bool isInitialized;

        private void Start()
        {
            InitializeBuffers();
            SpawnAgents();
        }

        private void InitializeBuffers()
        {
            positions = new NativeArray<float3>(agentCount, Allocator.Persistent);
            desiredVelocities = new NativeArray<float3>(agentCount, Allocator.Persistent);
            forwardDirections = new NativeArray<float3>(agentCount, Allocator.Persistent);
            radii = new NativeArray<float>(agentCount, Allocator.Persistent);
            poises = new NativeArray<float>(agentCount, Allocator.Persistent);
            outputs = new NativeArray<CrowdAgentOutput>(agentCount, Allocator.Persistent);
            spatialMap = new NativeParallelMultiHashMap<int, int>(agentCount * 4, Allocator.Persistent);
            isInitialized = true;
        }

        private void SpawnAgents()
        {
            Unity.Mathematics.Random rnd = new Unity.Mathematics.Random(12345);
            for (int i = 0; i < agentCount; i++)
            {
                float2 circle = rnd.NextFloat2Direction() * rnd.NextFloat(0.5f, spawnRadius);
                positions[i] = new float3(transform.position.x + circle.x, transform.position.y, transform.position.z + circle.y);
                forwardDirections[i] = new float3(0.0f, 0.0f, 1.0f);
                radii[i] = agentRadius;
                poises[i] = basePoise;
            }
        }

        private void Update()
        {
            if (!isInitialized)
                return;

            float dt = Time.deltaTime;
            UpdateDesiredVelocities();
            spatialMap.Clear();

            BuildSpatialHashJob hashJob = new BuildSpatialHashJob
            {
                positions = positions,
                cellSize = cellSize,
                spatialMap = spatialMap.AsParallelWriter()
            };
            JobHandle hashHandle = hashJob.Schedule(agentCount, 32);

            EvaluateHordeCrowdStressJob stressJob = new EvaluateHordeCrowdStressJob
            {
                positions = positions,
                desiredVelocities = desiredVelocities,
                forwardDirections = forwardDirections,
                radii = radii,
                basePoises = poises,
                spatialMap = spatialMap,
                cellSize = cellSize,
                stumbleRearThreshold = stumbleThreshold,
                crowdCrushThreshold = crushThreshold,
                lateralInstabilityLimit = lateralLimit,
                maxSurgeMultiplier = 1.45f,
                minCongestionMultiplier = 0.25f,
                separationWeight = 1.2f,
                outputs = outputs
            };
            JobHandle stressHandle = stressJob.Schedule(agentCount, 16, hashHandle);

            ApplyCrowdMovementJob moveJob = new ApplyCrowdMovementJob
            {
                outputs = outputs,
                deltaTime = dt,
                positions = positions,
                forwardDirections = forwardDirections
            };
            JobHandle moveHandle = moveJob.Schedule(agentCount, 32, stressHandle);
            moveHandle.Complete();
        }

        private void UpdateDesiredVelocities()
        {
            float3 targetPos = targetTransform != null ? (float3)targetTransform.position : (float3)transform.position;
            for (int i = 0; i < agentCount; i++)
            {
                float3 delta = targetPos - positions[i];
                delta.y = 0.0f;
                float distSq = math.lengthsq(delta);
                if (distSq > 0.01f)
                    desiredVelocities[i] = math.normalize(delta) * baseSpeed;
                else
                    desiredVelocities[i] = float3.zero;
            }
        }

        private void OnDestroy()
        {
            if (!isInitialized)
                return;

            positions.Dispose();
            desiredVelocities.Dispose();
            forwardDirections.Dispose();
            radii.Dispose();
            poises.Dispose();
            outputs.Dispose();
            spatialMap.Dispose();
            isInitialized = false;
        }

        private void OnDrawGizmos()
        {
            if (!isInitialized || !positions.IsCreated)
                return;

            for (int i = 0; i < positions.Length; i++)
            {
                CrowdStressType stress = outputs[i].stressType;
                Gizmos.color = GetColorForStress(stress);
                Gizmos.DrawSphere(positions[i], radii[i]);
            }
        }

        private static Color GetColorForStress(CrowdStressType stress)
        {
            if ((stress & CrowdStressType.FrontalStumble) != 0)
                return Color.magenta;
            if ((stress & CrowdStressType.CrowdCrush) != 0)
                return Color.red;
            if ((stress & CrowdStressType.Congested) != 0)
                return Color.yellow;
            if ((stress & CrowdStressType.Surging) != 0)
                return Color.cyan;

            return Color.green;
        }
    }
}
