using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-attach-lockon — 부착 조준 유효성 preflight 의 순수 판정.
    // "이 Unit 카드가 이 유닛에 '기여'하는가"(= ApplyDreamcatcherCardToUnit 이 -1 이
    // 아닌가)를, **유닛-종속 게이트만** 미러해 plain 값으로 판정한다.
    //
    // ApplyDreamcatcherCardToUnit(BattleBridge.Dreamcatcher.cs)의 유닛-종속 skip 은
    // 딱 셋 — ProjectileBounce→ProjectileRef(투사체) / FrontmostTarget·HeavyStrike→
    // 데미지 output / 이중 LethalTimer·DreamCocoon(상태). 나머지 guard(magnitude·
    // duration·projectile-null·mapping)는 **카드 데이터 검증**이라 어느 유닛에서든 같은
    // 결과 → 여기선 유닛 종속 조건만 본다(authored-valid 전제). 그래서 UI 답이 '유닛별로'
    // 정확하다(같은 카드가 궁수엔 가능·가디언엔 불가).
    //
    // ★ 동기화 계약: 새 **유닛-클래스 게이트** kind 를 apply 에 추가하면 여기도 갱신 +
    //   DreamcatcherAttachEvalTests 케이스 추가. 데이터-검증 guard 추가는 무관.
    public static class DreamcatcherAttachEval
    {
        public static bool WouldApply(DreamcatcherCard card,
            bool hasProjectile, bool hasDamageOutput, bool hasLethalTimer, bool hasDreamCocoon)
        {
            if (card == null) return false;
            // Squad = 축-집합 버프(host 무제약, unit 9) → host 종속 거부 없음. Active 는 이 경로 밖.
            if (card.type == CardType.Squad) return true;
            if (card.type != CardType.Unit) return false;

            bool hasMech = card.mechanics != null && card.mechanics.Length > 0;
            bool hasMods = card.attackMods != null && card.attackMods.Length > 0;
            if (!hasMech && !hasMods) return false;

            if (hasMech)
            {
                // 이중 상태 거부 = 카드 '전체' 거부(apply preflight, return -1).
                for (int i = 0; i < card.mechanics.Length; i++)
                {
                    var pk = card.mechanics[i].payload.kind;
                    if (hasLethalTimer && pk == DcPayloadKind.SelfBuffLethal) return false;
                    if (hasDreamCocoon && pk == DcPayloadKind.DreamCocoon) return false;
                }
                // 하나라도 이 유닛에 먹는 mechanic 이 있으면 기여.
                for (int i = 0; i < card.mechanics.Length; i++)
                {
                    var pk = card.mechanics[i].payload.kind;
                    if (pk == DcPayloadKind.None) continue;
                    if (pk == DcPayloadKind.HeavyStrike) { if (hasDamageOutput) return true; continue; }
                    return true; // 그 외 mechanic 은 유닛 클래스 무관(데미지/투사체 불요)
                }
            }

            if (hasMods)
            {
                for (int i = 0; i < card.attackMods.Length; i++)
                {
                    var am = card.attackMods[i];
                    if (am.kind == DcAttackModKind.None || am.damageMul <= 0f) continue;
                    if (am.kind == DcAttackModKind.ProjectileBounce)
                    {
                        if (am.count > 0 && hasProjectile) return true; // 통통구슬 — 투사체 유닛만
                        continue;
                    }
                    if (am.kind == DcAttackModKind.FrontmostTarget)
                    {
                        if (hasDamageOutput) return true; // 끝을 보는 눈 — 데미지 output 필요
                        continue;
                    }
                    return true; // 그 외 mod(damageMul>0)는 유닛 클래스 무관
                }
            }

            return false;
        }
    }
}
