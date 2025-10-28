using UnityEngine;

namespace Yunus.Game.Core
{
    public interface IPrefabPooler : IService, ITickable
    {
        // Prefab için (yoksa) havuz oluþturur, varsa mevcut havuzu kullanýr; handle döndürür.
        IPrefabPool CreatePool(GameObject prefab, int prewarmCount = 0);
    }
}
