using UnityEngine;

namespace Wassup.Presentation
{
    public class HazardVisualLifetime : MonoBehaviour
    {
        [SerializeField] private float remainingLife = 5f;

        public void Init(float lifetime)
        {
            remainingLife = lifetime;
        }

        private void Update()
        {
            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f)
                Destroy(gameObject);
        }
    }
}
