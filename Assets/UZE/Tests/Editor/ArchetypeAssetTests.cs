using NUnit.Framework;
using UnityEngine;
using UZE.Core;

namespace UZE.Tests.Editor
{
    public class ArchetypeAssetTests
    {
        [Test]
        public void ZombieArchetypeAsset_Instantiation_HasValidDefaultValues()
        {
            ZombieArchetypeAsset archetype = ScriptableObject.CreateInstance<ZombieArchetypeAsset>();

            Assert.IsNotNull(archetype);
            Assert.AreEqual("Walker", archetype.ArchetypeName);
            Assert.AreEqual(100.0f, archetype.MaxHealth);
            Assert.AreEqual(0.35f, archetype.AgentRadius);
            Assert.Greater(archetype.BaseMoveSpeed, 0.0f);
            Assert.Greater(archetype.BasePoise, 0.0f);

            Object.DestroyImmediate(archetype);
        }
    }
}
