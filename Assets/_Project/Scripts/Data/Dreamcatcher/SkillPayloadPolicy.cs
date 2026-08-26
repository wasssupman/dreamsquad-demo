namespace Wassup.Data
{
    // skill-layer-migration unit 8 — **「이 payload 는 스킬인가」의 단일 정본.**
    //
    // 이 술어가 필요해진 이유: 이전이 끝나면서 `skillId == 0` 의 뜻이 뒤집혔다.
    // 이전 중에는 「아직 arm 이 처리한다」라 안전했는데, arm 이 철거된 지금은
    // **「아무도 처리 안 한다」**다 — 슬롯은 구워지고 트리거는 발화하고 그 다음에
    // 아무 일도 안 일어난다. arm 과 함께 「미처리 payload」 경고도 사라져 **로그조차
    // 없다.** 실제로 `OnPlace × NextAttackDoubleFire`(배치하면 다음 공격 2연발)가
    // 그렇게 죽어 있었고 EditMode 는 전부 초록이었다.
    //
    // 그래서 bake 가 판정한다: **스킬인데 라우팅이 없으면 거절하고 짖는다.**
    // 침묵보다 거절이 낫다는 이 레포의 규율 그대로다.
    public static class SkillPayloadPolicy
    {
        // 스킬이 **아닌** payload. 각각 이유가 다르므로 뭉뚱그리지 말 것 —
        // 다음 후보를 잘못 분류하게 된다.
        public static bool IsSkill(DcPayloadKind kind)
        {
            switch (kind)
            {
                case DcPayloadKind.None:
                    return false;   // 센티넬
                case DcPayloadKind.PlacementAura:
                    // **발동 규칙**이다(시제). 지금 실행이 아니라 앞으로 일어날 배치에
                    // 적용될 규칙을 등록한다 — 등록·조회·해지 세 시점이라 영수증이 필요하고,
                    // 그것이 포트의 결함이 아니라 범주가 다르다는 신호다.
                    return false;
                case DcPayloadKind.HeavyStrike:
                    // **그 공격의 성질**이다(자기참조). 자기를 부른 사건 자체를 바꾸는데,
                    // 스킬 seam 은 정의상 공격 해결 뒤라 늦다.
                    return false;
                case DcPayloadKind.SplitOnDeath:
                    // 슬롯을 안 쓴다 — 브리지 킬 드레인이 SO 를 직독한다.
                    // ⚠ 시제상으로는 스킬이다. 어휘 밖인 이유가 **배선이 다른 길**이라서지
                    // 범주가 달라서가 아니다(위 둘과 섞지 말 것).
                    return false;
                case DcPayloadKind.RecallAttachedToFront:
                    return false;   // 손패 UI 동작. 심이 아니다.
                case DcPayloadKind.AreaBarrage:
                    return false;   // arm 철거됨 — 발사 명세로 이관(브리지가 거절 사유를 남긴다)
                case DcPayloadKind.SelfWarmupBuff:
                    return false;   // 죽은 값. warmup 개념이 Sleep 으로 승격되며 은퇴
                default:
                    return true;
            }
        }

        // 부착 즉시(trigger=None) 전용 payload. 다른 트리거에서 라우팅이 없는 것이
        // **정상**이다 — 저작이 그리로 가지 않는다.
        public static bool IsAttachOnly(DcPayloadKind kind)
            => kind == DcPayloadKind.SelfBuffLethal
            || kind == DcPayloadKind.DreamCocoon
            || kind == DcPayloadKind.BountyMark;
    }
}
