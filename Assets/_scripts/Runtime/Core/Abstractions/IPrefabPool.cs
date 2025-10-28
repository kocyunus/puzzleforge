using UnityEngine;

namespace Yunus.Game.Core
{
    // Tek bir prefaba baðlý, hafif "pool handle"
    public interface IPrefabPool
    {
        GameObject Spawn(Vector3 pos, Quaternion rot, Transform parent = null);
        GameObject SpawnImmediate(Vector3 pos, Quaternion rot, Transform parent = null);
        void Despawn(GameObject instance);
        void DespawnAll();
        (int available, int inUse, int total) Stats { get; }
    }
}
