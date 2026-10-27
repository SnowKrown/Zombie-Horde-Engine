using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UZE.Crowd
{
    [BurstCompile]
    public struct BuildSpatialHashJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> positions;
        public float cellSize;
        public NativeParallelMultiHashMap<int, int>.ParallelWriter spatialMap;

        public void Execute(int index)
        {
            float3 pos = positions[index];
            int hash = SpatialHashGrid.Hash(pos.xz, cellSize);
            spatialMap.Add(hash, index);
        }
    }
}
