using Unity.Entities;
using UnityEngine;

namespace Wassup.Battle.Effects
{
    public class BlockingHazardPresenter : MonoBehaviour
    {
        public Entity Entity { get; private set; }

        public void Bind(Entity entity)
        {
            Entity = entity;
        }

        public void OnDestroyed(GameObject vfxPrefab)
        {
            if (vfxPrefab != null)
                Instantiate(vfxPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
