using Unity.Netcode.Components;
using UnityEngine;

namespace CockroachFantasia.Networking
{
    [DisallowMultipleComponent]
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;

        public void ConfigureForPlayerMotion()
        {
            SyncPositionX = true;
            SyncPositionY = true;
            SyncPositionZ = true;
            SyncRotAngleX = false;
            SyncRotAngleY = true;
            SyncRotAngleZ = false;
            SyncScaleX = false;
            SyncScaleY = false;
            SyncScaleZ = false;
            Interpolate = true;
            UseUnreliableDeltas = true;
            UseHalfFloatPrecision = true;
            UseQuaternionSynchronization = false;
            PositionThreshold = 0.01f;
            RotAngleThreshold = 0.5f;
        }
    }
}
