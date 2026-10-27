using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UZE.Crowd;

namespace UZE.Tests.Runtime
{
    public class CrowdTests
    {
        [Test]
        public void SpatialHashGrid_ProducesIdenticalHash_ForSameCell()
        {
            float2 posA = new float2(1.1f, 1.2f);
            float2 posB = new float2(1.8f, 1.9f);
            float2 posOtherCell = new float2(4.5f, 4.5f);

            int hashA = SpatialHashGrid.Hash(posA, 2.0f);
            int hashB = SpatialHashGrid.Hash(posB, 2.0f);
            int hashOther = SpatialHashGrid.Hash(posOtherCell, 2.0f);

            Assert.AreEqual(hashA, hashB);
            Assert.AreNotEqual(hashA, hashOther);
        }

        [Test]
        public void FilterSeparation_RemovesBackwardDrag_WhenDamped()
        {
            float3 forward = new float3(0.0f, 0.0f, 1.0f);
            float3 rawSeparation = new float3(0.0f, 0.0f, -2.0f);

            float3 filtered = SpatialHashGrid.FilterSeparation(rawSeparation, forward, 0.0f);

            Assert.GreaterOrEqual(filtered.z, -0.001f);
        }

        [Test]
        public void CalculateRepulsion_ReturnsZero_WhenOutsideCombinedRadius()
        {
            float3 posA = new float3(0.0f, 0.0f, 0.0f);
            float3 posB = new float3(5.0f, 0.0f, 0.0f);

            float3 repulsion = SpatialHashGrid.CalculateRepulsion(posA, posB, 1.0f);

            Assert.AreEqual(float3.zero, repulsion);
        }

        [Test]
        public void EvaluateCrowdStress_SurgesAgent_WhenPressedFromBehind()
        {
            NativeArray<float3> positions = new NativeArray<float3>(2, Allocator.TempJob);
            NativeArray<float3> desiredVelocities = new NativeArray<float3>(2, Allocator.TempJob);
            NativeArray<float3> forwardDirections = new NativeArray<float3>(2, Allocator.TempJob);
            NativeArray<float> radii = new NativeArray<float>(2, Allocator.TempJob);
            NativeArray<float> poises = new NativeArray<float>(2, Allocator.TempJob);
            NativeArray<CrowdAgentOutput> outputs = new NativeArray<CrowdAgentOutput>(2, Allocator.TempJob);
            NativeParallelMultiHashMap<int, int> spatialMap = new NativeParallelMultiHashMap<int, int>(8, Allocator.TempJob);

            positions[0] = new float3(0.0f, 0.0f, 0.5f);
            forwardDirections[0] = new float3(0.0f, 0.0f, 1.0f);
            radii[0] = 0.5f;
            poises[0] = 50.0f;

            positions[1] = new float3(0.0f, 0.0f, 0.0f);
            forwardDirections[1] = new float3(0.0f, 0.0f, 1.0f);
            radii[1] = 0.5f;
            poises[1] = 50.0f;

            spatialMap.Add(SpatialHashGrid.Hash(positions[0].xz, 2.0f), 0);
            spatialMap.Add(SpatialHashGrid.Hash(positions[1].xz, 2.0f), 1);

            EvaluateHordeCrowdStressJob job = new EvaluateHordeCrowdStressJob
            {
                positions = positions,
                desiredVelocities = desiredVelocities,
                forwardDirections = forwardDirections,
                radii = radii,
                basePoises = poises,
                spatialMap = spatialMap,
                cellSize = 2.0f,
                stumbleRearThreshold = 1.2f,
                crowdCrushThreshold = 2.0f,
                lateralInstabilityLimit = 1.5f,
                maxSurgeMultiplier = 1.45f,
                minCongestionMultiplier = 0.25f,
                separationWeight = 1.0f,
                outputs = outputs
            };

            job.Schedule(2, 1).Complete();

            Assert.Greater(outputs[0].rearPressure, 0.0f);
            Assert.Greater(outputs[0].speedMultiplier, 1.0f);

            positions.Dispose();
            desiredVelocities.Dispose();
            forwardDirections.Dispose();
            radii.Dispose();
            poises.Dispose();
            outputs.Dispose();
            spatialMap.Dispose();
        }

        [Test]
        public void EvaluateCrowdStress_TriggersStumble_WhenRearPressureExceedsThreshold()
        {
            NativeArray<float3> positions = new NativeArray<float3>(2, Allocator.TempJob);
            NativeArray<float3> desiredVelocities = new NativeArray<float3>(2, Allocator.TempJob);
            NativeArray<float3> forwardDirections = new NativeArray<float3>(2, Allocator.TempJob);
            NativeArray<float> radii = new NativeArray<float>(2, Allocator.TempJob);
            NativeArray<float> poises = new NativeArray<float>(2, Allocator.TempJob);
            NativeArray<CrowdAgentOutput> outputs = new NativeArray<CrowdAgentOutput>(2, Allocator.TempJob);
            NativeParallelMultiHashMap<int, int> spatialMap = new NativeParallelMultiHashMap<int, int>(8, Allocator.TempJob);

            positions[0] = new float3(0.0f, 0.0f, 0.2f);
            forwardDirections[0] = new float3(0.0f, 0.0f, 1.0f);
            radii[0] = 0.5f;
            poises[0] = 50.0f;

            positions[1] = new float3(0.0f, 0.0f, 0.0f);
            forwardDirections[1] = new float3(0.0f, 0.0f, 1.0f);
            radii[1] = 0.5f;
            poises[1] = 50.0f;

            spatialMap.Add(SpatialHashGrid.Hash(positions[0].xz, 2.0f), 0);
            spatialMap.Add(SpatialHashGrid.Hash(positions[1].xz, 2.0f), 1);

            EvaluateHordeCrowdStressJob job = new EvaluateHordeCrowdStressJob
            {
                positions = positions,
                desiredVelocities = desiredVelocities,
                forwardDirections = forwardDirections,
                radii = radii,
                basePoises = poises,
                spatialMap = spatialMap,
                cellSize = 2.0f,
                stumbleRearThreshold = 0.2f,
                crowdCrushThreshold = 2.0f,
                lateralInstabilityLimit = 1.5f,
                maxSurgeMultiplier = 1.45f,
                minCongestionMultiplier = 0.25f,
                separationWeight = 1.0f,
                outputs = outputs
            };

            job.Schedule(2, 1).Complete();

            Assert.IsTrue((outputs[0].stressType & CrowdStressType.FrontalStumble) != 0);

            positions.Dispose();
            desiredVelocities.Dispose();
            forwardDirections.Dispose();
            radii.Dispose();
            poises.Dispose();
            outputs.Dispose();
            spatialMap.Dispose();
        }
    }
}
