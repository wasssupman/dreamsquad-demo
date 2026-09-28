using UnityEngine;

namespace Wassup.Data
{
    /// <summary>
    /// skill-data-table unit 4 — **효과 한 줄 = 에셋 하나**(`tables.md` §2 · README 계약 1). 카드 · 방어유닛 · 적의 소유 줄(`BindingSpec`)이
    /// 이 에셋을 참조한다 — 소유자를 바꾸는 일이 복사가 아니라 참조 한 줄이다(U6). 수치가 다르면 다른 에셋(U13 — 병합하지 않는다 ·
    /// 소유자별 덮어쓰기 없음).
    ///
    /// `id` 는 서버 어휘다(계약 6) — 소문자 스네이크 · 표 안 유일 · 첫 공개 뒤 개명 금지(지울 때는 `deprecated`).
    /// </summary>
    [CreateAssetMenu(fileName = "Effect_", menuName = "Wassup/Effect", order = 21)]
    public class EffectData : ScriptableObject
    {
        [Tooltip("효과 id(서버 어휘) — ^[a-z][a-z0-9_]*$ · 표 안 유일 · 첫 공개 뒤 개명 금지.")]
        public string id;
        [Tooltip("폐기 — 소유 줄이 참조하면 굽기가 거절한다(삭제 대신).")]
        public bool deprecated;
        public EffectValues values;

        [Header("참조(종류별)")]
        public ProjectileData projectile;
        public ProjectilePatternData pattern;
        public HazardSO hazard;

        [Header("뷰 전용(시트 밖)")]
        [Tooltip("숙주에 상시 부착하는 루핑 오라 · `AreaDot` 은 빔. null = 무연출.")]
        public GameObject auraPrefab;
        [Tooltip("<= 0 = 1.")]
        public float auraScale;
        [Tooltip("문안 전용 참조(스택 임계 요약) — 런타임 임계는 정의표가 권위.")]
        public StackModifierSO stackModifier;
    }
}
