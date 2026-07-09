using UnityEngine;

namespace Wassup.Data
{
    // aggro-targeting Unit 13 — 어그로 아이콘 표현 파라미터(SO). 하드코딩 금지:
    // 스프라이트/오프셋/크기/펄스/틴트를 전부 여기서. AggroIconView/Spawner 가 참조.
    [CreateAssetMenu(fileName = "AggroIconStyle", menuName = "Wassup/Aggro Icon Style")]
    public class AggroIconStyle : ScriptableObject
    {
        [Header("Sprite")]
        public Sprite icon;
        public Color tint = Color.white;

        [Header("Placement (world units)")]
        [Tooltip("적 발치(anchor) 기준 머리 위 Y 오프셋")]
        public float headYOffset = 1.5f;
        public float worldScale = 0.5f;

        [Header("Pulse (optional)")]
        public bool pulse = true;
        [Tooltip("스케일 진동 폭 (0 = 없음)")]
        public float pulseAmplitude = 0.12f;
        public float pulseSpeed = 3f;

        [Header("Render")]
        public int sortingOrder = 15000;

        public float SampleScale(float time)
        {
            if (!pulse || pulseAmplitude <= 0f) return worldScale;
            return worldScale * (1f + pulseAmplitude * Mathf.Sin(time * pulseSpeed));
        }
    }
}
