using UnityEngine;

namespace Wassup.Data
{
    // dreamcatcher-awakening-hand unit 0 — tunable numbers for the awakening
    // currency + CR-style cycling hand. Designers adjust economy/UX pacing here
    // without code. Per-unit death rewards live on the unit SOs
    // (DefenderUnitData/AttackUnitData.awakeningReward), not here.
    [CreateAssetMenu(fileName = "AwakeningConfig", menuName = "Wassup/AwakeningConfig", order = 24)]
    public class AwakeningConfig : ScriptableObject
    {
        // dreamcatcher-sheet-sync unit 2 — DcConfig sheet-tab row key. Appended so
        // the existing asset keeps its serialized values (id backfilled to
        // "awakening_default" in the same commit).
        public string id;

        [Header("Gauge")]
        public int gaugeMax = 100;   // awakening cap; gains past this are lost
        // value at match start (reset every Placement entry). 20 = DcConfig 시트 기준값.
        // 카드 비용도 전부 20 이라 전투 시작에 정확히 1장을 낼 수 있고, 그래서 각성 힌트
        // A단계가 Battle 진입 즉시 발화한다 — 이 값이 20 미만이 되면 그 전제가 깨진다
        // (first-session-tutorial unit 16 "알려진 한계" 참조).
        public int gaugeStart = 20;

        // Per-CardType use cost. Serialized separately (not an array) so the
        // inspector reads clearly; CostFor maps by enum CASE, never by index —
        // CardType order is { Squad=0, Unit=1, Active=2 } which differs from the
        // spec's prose order (Unit 15 / Squad 30 / Active 20).
        [Header("Use Cost (per CardType)")]
        public int costSquad = 30;
        public int costUnit = 15;
        public int costActive = 20;

        [Header("Hand")]
        public int handSize = 5;              // hand = front N of the cycle queue
        public int maxAttachPerUnit = 3;      // Unit-type cards attached per defender

        [Header("Use UX")]
        // Battle-domain time scale while the hand is open (TimeManager lease).
        // Never 0 — the game must not pause (spec contract 8).
        // (confirmDelaySec removed 2026-07-10 — 오부착 방어 확정 지연 폐기, touchup 즉시 커밋.)
        public float slomoTimeScale = 0.3f;

        [Header("Bounty Mark")]
        // subconscious-curse-expansion unit 3 — 살찌운 제물 드롭 픽 반경(타일 단위,
        // 유클리드 xz). 기존 에셋은 필드 초기값으로 역직렬화(끝에 추가).
        public float enemyPickRadiusTiles = 1.5f;

        public int CostFor(CardType type)
        {
            switch (type)
            {
                case CardType.Squad: return costSquad;
                case CardType.Unit: return costUnit;
                case CardType.Active: return costActive;
                default: return costSquad;
            }
        }
    }
}
