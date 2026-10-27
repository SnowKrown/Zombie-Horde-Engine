using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UZE.Core;
using UZE.Core.Contracts;

namespace UZE.Tests.Runtime
{
    public class BaseContractTests
    {
        [Test]
        public void ZombieInstanceData_IsBlittable()
        {
            Assert.IsTrue(UnsafeUtility.IsBlittable<ZombieInstanceData>());
        }

        [Test]
        public void RootMotionData_IsBlittable()
        {
            Assert.IsTrue(UnsafeUtility.IsBlittable<RootMotionData>());
        }

        [Test]
        public void ZombieInstanceData_EqualityComparison_MatchesEntityId()
        {
            ZombieInstanceData first = new ZombieInstanceData { entityId = 42, currentHealth = 100.0f };
            ZombieInstanceData second = new ZombieInstanceData { entityId = 42, currentHealth = 50.0f };
            ZombieInstanceData third = new ZombieInstanceData { entityId = 99, currentHealth = 100.0f };

            Assert.AreEqual(first, second);
            Assert.AreNotEqual(first, third);
        }

        [Test]
        public void ZombieState_EnumByteSize_IsOneByte()
        {
            Assert.AreEqual(1, UnsafeUtility.SizeOf<ZombieState>());
        }

        [Test]
        public void StatusEffectType_EnumByteSize_IsOneByte()
        {
            Assert.AreEqual(1, UnsafeUtility.SizeOf<StatusEffectType>());
        }
    }
}
