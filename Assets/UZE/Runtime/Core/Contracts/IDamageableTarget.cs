using UnityEngine;

namespace UZE.Core.Contracts
{
    public interface IDamageableTarget
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
    }
}
