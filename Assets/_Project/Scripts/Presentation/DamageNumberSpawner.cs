using UnityEngine;

namespace Wassup.Presentation
{
    // Prefab-only floating damage-number layer, mirroring VfxSpawner. BattleBridge
    // holds a SerializeField reference and calls Spawn() when draining
    // DamageNumberEvents. Pools popups to avoid GC spikes under heavy fire.
    public class DamageNumberSpawner : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private GameObject popupPrefab;
        [SerializeField] private DamageNumberStyle style = new DamageNumberStyle();

        [Header("Placement")]
        [Tooltip("적 발치 position 에서 머리 위로 올릴 월드 높이")]
        [SerializeField] private float headYOffset = 0.6f;
        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera billboardCamera;

        private DamageNumberPool _pool;

        private void Awake()
        {
            style.EnsureDefaults();
            if (popupPrefab != null)
                _pool = new DamageNumberPool(popupPrefab, transform);
        }

        private void OnValidate()
        {
            style?.EnsureDefaults();
        }

        public void Spawn(Vector3 worldPos, float amount)
        {
            if (popupPrefab == null)
            {
                Debug.LogError("[DamageNumberSpawner] popupPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogError("[DamageNumberSpawner] 빌보드 카메라를 찾을 수 없습니다 (billboardCamera/Camera.main 모두 null).");
                return;
            }
            if (_pool == null) _pool = new DamageNumberPool(popupPrefab, transform);

            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
            var pos = new Vector3(worldPos.x, worldPos.y + headYOffset, worldPos.z);
            var view = _pool.Get();
            if (view == null)
            {
                Debug.LogError("[DamageNumberSpawner] popupPrefab 에 DamageNumberView 가 없습니다.");
                return;
            }
            view.Play(shown, pos, cam, style, _pool.Return);
        }
    }
}
