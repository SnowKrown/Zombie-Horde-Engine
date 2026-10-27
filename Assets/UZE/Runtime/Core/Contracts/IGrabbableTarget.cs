using UnityEngine;

namespace UZE.Core.Contracts
{
    public interface IGrabbableTarget
    {
        bool CanBeGrabbed { get; }
        void OnGrabbed(Transform attackerTransform, Vector3 attachOffset);
        void OnReleased();
    }
}
