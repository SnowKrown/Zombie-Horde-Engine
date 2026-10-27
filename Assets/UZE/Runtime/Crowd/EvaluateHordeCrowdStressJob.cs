using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UZE.Crowd
{
    [BurstCompile]
    public struct EvaluateHordeCrowdStressJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<float3> desiredVelocities;
        [ReadOnly] public NativeArray<float3> forwardDirections;
        [ReadOnly] public NativeArray<float> radii;
        [ReadOnly] public NativeArray<float> basePoises;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialMap;

        public float cellSize;
        public float stumbleRearThreshold;
        public float crowdCrushThreshold;
        public float lateralInstabilityLimit;
        public float maxSurgeMultiplier;
        public float minCongestionMultiplier;
        public float separationWeight;

        [WriteOnly] public NativeArray<CrowdAgentOutput> outputs;

        public void Execute(int index)
        {
            float3 myPos = positions[index];
            float3 forward = forwardDirections[index];
            float radius = radii[index];
            float poise = basePoises[index];

            EvaluateNeighbors(index, myPos, forward, radius, out float3 rawSeparation, out float rearPressure, out float frontResistance, out float3 lateralForce);

            float3 filteredSeparation = SpatialHashGrid.FilterSeparation(rawSeparation, forward);
            float speedMult = CalculateSpeedMultiplier(rearPressure, frontResistance);
            CrowdStressType stress = DetermineStressType(rearPressure, frontResistance, lateralForce, poise);

            float3 desired = desiredVelocities[index];
            float3 adjusted = (desired * speedMult) + (filteredSeparation * separationWeight);

            outputs[index] = new CrowdAgentOutput
            {
                adjustedVelocity = adjusted,
                filteredSeparation = filteredSeparation,
                speedMultiplier = speedMult,
                stressType = stress,
                rearPressure = rearPressure,
                frontResistance = frontResistance
            };
        }

        private void EvaluateNeighbors(
            int index,
            float3 myPos,
            float3 forward,
            float radius,
            out float3 rawSeparation,
            out float rearPressure,
            out float frontResistance,
            out float3 lateralForce)
        {
            rawSeparation = float3.zero;
            rearPressure = 0.0f;
            frontResistance = 0.0f;
            lateralForce = float3.zero;

            int2 centerCell = SpatialHashGrid.GetCellCoords(myPos.xz, cellSize);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cellHash = SpatialHashGrid.HashCell(centerCell + new int2(dx, dz));
                    ProcessCell(index, cellHash, myPos, forward, radius, ref rawSeparation, ref rearPressure, ref frontResistance, ref lateralForce);
                }
            }
        }

        private void ProcessCell(
            int index,
            int cellHash,
            float3 myPos,
            float3 forward,
            float radius,
            ref float3 rawSeparation,
            ref float rearPressure,
            ref float frontResistance,
            ref float3 lateralForce)
        {
            if (spatialMap.TryGetFirstValue(cellHash, out int otherIndex, out NativeParallelMultiHashMapIterator<int> it))
            {
                do
                {
                    if (otherIndex != index)
                        AccumulateNeighborEffects(index, otherIndex, myPos, forward, radius, ref rawSeparation, ref rearPressure, ref frontResistance, ref lateralForce);
                }
                while (spatialMap.TryGetNextValue(out otherIndex, ref it));
            }
        }

        private void AccumulateNeighborEffects(
            int myIndex,
            int otherIndex,
            float3 myPos,
            float3 forward,
            float myRadius,
            ref float3 rawSeparation,
            ref float rearPressure,
            ref float frontResistance,
            ref float3 lateralForce)
        {
            float3 otherPos = positions[otherIndex];
            float combinedRadius = myRadius + radii[otherIndex];
            float3 repulsion = SpatialHashGrid.CalculateRepulsion(myPos, otherPos, combinedRadius);
            rawSeparation += repulsion;

            float3 toMe = myPos - otherPos;
            toMe.y = 0.0f;
            float distSq = math.lengthsq(toMe);
            if (distSq >= combinedRadius * combinedRadius || distSq < 0.0001f)
                return;

            float3 toMeDir = math.normalize(toMe);
            float dotForward = math.dot(toMeDir, forward);
            float proximityWeight = 1.0f - (math.sqrt(distSq) / combinedRadius);

            if (dotForward > 0.05f)
                rearPressure += dotForward * proximityWeight;
            else if (dotForward < -0.05f)
                frontResistance += math.abs(dotForward) * proximityWeight;

            float3 right = new float3(-forward.z, 0.0f, forward.x);
            lateralForce += right * math.dot(toMeDir, right) * proximityWeight;
        }

        private float CalculateSpeedMultiplier(float rearPressure, float frontResistance)
        {
            float surge = math.clamp(1.0f + (rearPressure * 0.45f), 1.0f, maxSurgeMultiplier);
            float congestion = math.clamp(1.0f - (frontResistance * 0.75f), minCongestionMultiplier, 1.0f);
            return surge * congestion;
        }

        private CrowdStressType DetermineStressType(float rearPressure, float frontResistance, float3 lateralForce, float poise)
        {
            CrowdStressType type = CrowdStressType.None;
            float poiseMultiplier = math.max(poise / 50.0f, 0.2f);

            if (rearPressure > 0.1f)
                type |= CrowdStressType.Surging;
            if (frontResistance > 0.1f)
                type |= CrowdStressType.Congested;
            if (rearPressure > (stumbleRearThreshold * poiseMultiplier))
                type |= CrowdStressType.FrontalStumble;
            if ((rearPressure + frontResistance) > (crowdCrushThreshold * poiseMultiplier))
                type |= CrowdStressType.CrowdCrush;
            if (math.length(lateralForce) > (lateralInstabilityLimit * poiseMultiplier))
                type |= CrowdStressType.LateralInstability;

            return type;
        }
    }
}
