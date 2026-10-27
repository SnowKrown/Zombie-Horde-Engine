using System;
using Unity.Mathematics;

namespace UZE.Core
{
    public struct ZombieInstanceData : IEquatable<ZombieInstanceData>
    {
        public int entityId;
        public ZombieState state;
        public float3 position;
        public float3 forward;
        public float currentSpeed;
        public float currentHealth;
        public float maxHealth;
        public float poise;
        public int currentPolygonIndex;
        public int targetPolygonIndex;
        public float stateTimer;

        public bool Equals(ZombieInstanceData other)
        {
            return entityId == other.entityId;
        }

        public override bool Equals(object obj)
        {
            return obj is ZombieInstanceData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return entityId;
        }
    }
}
