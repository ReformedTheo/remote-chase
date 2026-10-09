using UnityEngine;

namespace RemoteChase.Combat
{
    /// <summary>Algo que pode levar tiro.</summary>
    public interface IHittable
    {
        void TakeHit(float damage, Vector3 point);
    }
}
