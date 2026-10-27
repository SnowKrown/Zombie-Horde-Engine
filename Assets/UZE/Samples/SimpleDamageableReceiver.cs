using UnityEngine;
using UZE.Core.Contracts;

namespace UZE.Samples
{
    public class SimpleDamageableReceiver : MonoBehaviour, IDamageableTarget, IStatusEffectReceiver, IGrabbableTarget
    {
        [SerializeField] private float currentHealth = 100.0f;
        [SerializeField] private bool canBeGrabbed = true;

        public bool CanBeGrabbed => canBeGrabbed;
        public float CurrentHealth => currentHealth;

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
        {
            currentHealth -= amount;
            if (currentHealth < 0.0f)
                currentHealth = 0.0f;
        }

        public void ApplyStatusEffect(StatusEffectType type, float duration, float magnitude)
        {
            if (type == StatusEffectType.None)
                return;

            Debug.Log($"[UZE Sample] Status effect received: {type} (duration: {duration}s, magnitude: {magnitude})");
        }

        public void OnGrabbed(Transform attackerTransform, Vector3 attachOffset)
        {
            canBeGrabbed = false;
        }

        public void OnReleased()
        {
            canBeGrabbed = true;
        }
    }
}
