using UnityEngine;

namespace UZE.Core
{
    [CreateAssetMenu(fileName = "NewZombieArchetype", menuName = "UZE/Zombie Archetype", order = 1)]
    public class ZombieArchetypeAsset : ScriptableObject
    {
        [Header("General Attributes")]
        [SerializeField] private string archetypeName = "Walker";
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private float agentRadius = 0.35f;

        [Header("Locomotion")]
        [SerializeField] private float baseMoveSpeed = 3.5f;
        [SerializeField] private float maxSurgeMultiplier = 1.45f;
        [SerializeField] private float minCongestionMultiplier = 0.25f;

        [Header("Crowd Dynamics and Poise")]
        [SerializeField] private float basePoise = 50.0f;
        [SerializeField] private float stumbleRearThreshold = 1.2f;
        [SerializeField] private float crowdCrushThreshold = 2.0f;
        [SerializeField] private float lateralInstabilityLimit = 1.5f;

        [Header("Perception")]
        [SerializeField] private float sightRange = 25.0f;
        [SerializeField] private float fieldOfViewAngle = 110.0f;
        [SerializeField] private float hearingThreshold = 0.8f;

        [Header("GPU Animation Data")]
        [SerializeField] private Texture2D boneMatrixTexture;
        [SerializeField] private Mesh lod0Mesh;
        [SerializeField] private Mesh lod1Mesh;

        public string ArchetypeName => archetypeName;
        public float MaxHealth => maxHealth;
        public float AgentRadius => agentRadius;
        public float BaseMoveSpeed => baseMoveSpeed;
        public float MaxSurgeMultiplier => maxSurgeMultiplier;
        public float MinCongestionMultiplier => minCongestionMultiplier;
        public float BasePoise => basePoise;
        public float StumbleRearThreshold => stumbleRearThreshold;
        public float CrowdCrushThreshold => crowdCrushThreshold;
        public float LateralInstabilityLimit => lateralInstabilityLimit;
        public float SightRange => sightRange;
        public float FieldOfViewAngle => fieldOfViewAngle;
        public float HearingThreshold => hearingThreshold;
        public Texture2D BoneMatrixTexture => boneMatrixTexture;
        public Mesh Lod0Mesh => lod0Mesh;
        public Mesh Lod1Mesh => lod1Mesh;
    }
}
