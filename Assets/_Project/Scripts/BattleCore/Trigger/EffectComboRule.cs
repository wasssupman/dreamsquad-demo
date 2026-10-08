using Somnia.Battle.BattleCore.Combat.Projectile;

namespace Somnia.Battle.BattleCore.Trigger
{
    /// <summary>
    /// 조합 검증의 답. 원점 사유 셋(unified-effect-layer 계약 5) + 비율형 수치 사유 둘(skill-data-table unit 3) + 배선 전 하나(unit 8) · append-only.
    /// </summary>
    public enum ComboVerdict : byte
    {
        Allowed = 0,
        /// <summary>원점을 못 낸다 — 그 사건이 효과가 요구하는 조준(대상 · 칸 · 남의 사건)을 싣지 않는다.</summary>
        NoOrigin = 1,
        /// <summary>효과가 그 원점 형을 못 받는다 — 원점은 나오지만 효과가 그 형(떠난 자리 · 부착 순간 · 어그로 못 드는 몸)을 못 쓴다.</summary>
        ShapeMismatch = 2,
        /// <summary>영영 안 터진다 — 붙는 순간 이미 지난 자기 사건이거나 그 숙주에게 사건 자체가 없다.</summary>
        NeverFires = 3,
        /// <summary>비율 기준이 없다 — 비율형 수치인데 스탯을 읽을 주인이 없다(주인 없는 시전 — 판 · 액티브 · 드림스톤 · 판 주기).</summary>
        NoRatioBasis = 4,
        /// <summary>그 효과 종류에 비율 칸이 없다(`tables.md` §9 — 비율은 피해 · 실드량에만).</summary>
        NoRatioField = 5,
        /// <summary>
        /// skill-data-table unit 8 — **배선 전**. 조합은 뜻이 있지만 그 숙주에 대한 배선이 아직 없다(설계 거절이 아니다 — `HasDetector` 의
        /// 「배선 전엔 닫아 둔다」 선례). 상시 효과의 「보유 시작 순간」은 오늘 **방어유닛에 붙는 카드**(부착 순간)만 편다 — 방어유닛 ·
        /// 적이 직접 들거나 적에게 붙는 카드는 unit 7(후속)이 배선한다. 조용히 반쪽만 도는 상태를 만들지 않으려고 굽기가 말하고 뺀다.
        /// </summary>
        NotWired = 6,
    }

    /// <summary>
    /// 조합 검증의 입력 — **출처(카드 · 유닛 능력 · 악몽)는 입력이 아니다**(계약 5). 숙주·부착 시점의 사실만 싣는다.
    /// </summary>
    public struct EffectCombo
    {
        public TriggerKind Trigger;
        /// <summary>누구의 사건을 듣나(저작 주체 축 — `Self` 기본 · 「남의 배치」 = `Any`).</summary>
        public BindingSubject Subject;
        public EffectKind Payload;
        /// <summary>효과가 탄(직접 탄 · 발사 명세 탄)을 드나. 거짓이면 `Binding` 은 읽지 않는다.</summary>
        public bool HasProjectile;
        /// <summary>그 탄 궤적의 결합(대상 · 칸 · 방향).</summary>
        public BindingClass Binding;
        /// <summary>발사 명세의 「한 발이 반경 안 전원에게」(`fanOutToAllCandidates`).</summary>
        public bool FanOut;
        /// <summary>숙주가 적인가 — 적에게는 배치·퇴근 사건이 없다.</summary>
        public bool HostIsEnemy;
        /// <summary>규칙이 **숙주가 놓인 뒤에** 붙는가(카드 부착). 그러면 숙주 자신의 배치는 이미 지났다.</summary>
        public bool BindsAfterPlacement;
        /// <summary>숙주가 어그로를 들 수 없다고 **굽는 시점에 안다**(가디언 아님). 숙주를 모르면 거짓 — 부착 판정 몫이다.</summary>
        public bool HostCannotHoldAggro;
        /// <summary>skill-data-table unit 3 — 효과의 수치 방식. 기본 `Flat` = 비율 규칙을 안 본다.</summary>
        public MagnitudeMode Magnitude;
        /// <summary>
        /// 규칙이 **주인 없이** 시전된다(판 시전 · 액티브 · 드림스톤 · 판 주기 — 사건 주체 `Match`). 비율형의 기준 스탯이 없다(계약 9).
        /// ⚠ 주인이 떠나는 사건(죽음 · 퇴근)은 여기 들지 않는다 — 감지자가 스탯을 스냅샷한다.
        /// </summary>
        public bool CastHasNoOwner;
    }

    // unified-effect-layer unit 5 — **저작 조합 검증 한 함수**(H5). 카드 빌더 · 유닛/악몽 빌더가 같은 (트리거 × 주체 × 효과 × 탄 결합)에
    // 같은 답을 받는다. 옛 「출처 관례」 거절(카드만의 OnPlace 불가 · 발사 명세는 주기만 · 칸 탄 불가 · 유닛만의 실드 반경 블랙리스트 …)은
    // 이 셋 중 하나로 설명되지 않으면 **풀렸다**(`census.md` 표 3 · 라이브 거절 0).
    //
    // ⚠ 여기는 **조합**만 본다. 값 가드(크기 0 · 반경 0 · 탄 없음)와 게이트 배선(`SkillRouting.GateComboSupported`) ·
    //    라우팅 유무(`SkillRouting.Resolve`)는 각자의 자리다. 트리거 없음(부착 즉시)은 사건이 아니라서 감지자가 없다 → `NeverFires`
    //    이고, 부착 즉시 어휘는 카드 빌더의 그 갈래가 따로 굽는다. 예외 = 상시 효과 4종(⓪' — 트리거 없음과만 짝 · unit 8).
    public static class EffectComboRule
    {
        public static ComboVerdict Check(in EffectCombo c)
        {
            // ⓪ 시전(skill-data-table unit 4) — 액티브 카드는 사건이 아니라 **플레이어 입력**이다(감지자 없음). 시전 ⇔ 액티브 효과,
            //    그리고 주인 없는 시전이라 비율형의 기준 스탯이 없다(계약 9). 숙주 사실(진영 · 부착 시점 · 가디언)은 보지 않는다.
            if (c.Trigger == TriggerKind.Cast || SkillRouting.IsActiveCast(c.Payload))
            {
                if (c.Trigger != TriggerKind.Cast || !SkillRouting.IsActiveCast(c.Payload)) return ComboVerdict.ShapeMismatch;
                return c.Magnitude == MagnitudeMode.OwnerStatRatio ? ComboVerdict.NoRatioBasis : ComboVerdict.Allowed;
            }
            // ⓪' 상시 효과(skill-data-table unit 8 — 계약 11) — 트리거 없음(보유 시작 순간부터 계속) ⇔ 상시 효과 4종. 사건이 아니라 감지자가
            //    없고(`HasDetector(None)` = 거짓) 빌더가 기존 코어 모양으로 편다. 「보유 시작 순간」은 오늘 **방어유닛 숙주에 붙는 카드**
            //    (부착 = 숙주가 놓인 뒤)만 배선됐다 — 그 밖의 숙주(방어유닛 · 적이 직접 들거나 적 숙주 카드)는 배선 전(unit 7 후속).
            //    숙주 사실만 본다(출처는 입력이 아니다 — 계약 5). 비율 칸은 없다(`tables.md` §9).
            if (SkillRouting.IsAlwaysOn(c.Payload))
            {
                if (c.Trigger != TriggerKind.None) return ComboVerdict.ShapeMismatch;
                if (c.Subject == BindingSubject.Any) return ComboVerdict.NoOrigin;
                if (c.HostIsEnemy || !c.BindsAfterPlacement) return ComboVerdict.NotWired;
                return c.Magnitude == MagnitudeMode.OwnerStatRatio ? ComboVerdict.NoRatioField : ComboVerdict.Allowed;
            }
            // ⓪'' 부착 즉시 전용 효과(자기 버프 후 사망 · 꿈 고치 · 현상금 표식 · 배치 오라 — 트리거 없음 = **부착 순간**) × 타고난 숙주(방어유닛 ·
            //     적이 직접 든다 — 부착이 없다) = 배선 전(설계 거절이 아니다 · 「보유 시작 순간」 배선은 unit 7 후속). 숙주 사실만 본다(⓪' 와 같다).
            if (c.Trigger == TriggerKind.None && c.Subject == BindingSubject.Self && !c.BindsAfterPlacement
                && (SkillRouting.OnlyValidWithNoTrigger(c.Payload) || c.Payload == EffectKind.PlacementAura))
                return ComboVerdict.NotWired;
            // ① 그 숙주에게 그 사건이 없다(적 × 배치·퇴근 · 트리거 없음) — 감지자 표가 정본이다.
            //    ⚠ 주체 `Any` 의 사건 주인은 숙주가 아니라 **남**(배치된 방어유닛)이다 — 숙주 종류로 감지자를 묻지 않는다
            //    (skill-data-table unit 2 — 적 숙주의 「남의 배치」를 영영 안 터진다고 오판하던 결함. 코어는 터진다).
            bool eventOwnerIsEnemy = c.Subject != BindingSubject.Any && c.HostIsEnemy;
            if (!SkillRouting.HasDetector(c.Trigger, eventOwnerIsEnemy)) return ComboVerdict.NeverFires;
            // ② 자기 배치는 붙기 전에 지났다 — 남의 배치(`Any`)는 앞으로 온다.
            if (c.Trigger == TriggerKind.OnPlace && c.Subject == BindingSubject.Self && c.BindsAfterPlacement)
                return ComboVerdict.NeverFires;
            // ③ 「남의 사건」은 배치만 듣는다 — 다른 사건은 남의 것을 싣지 않는다.
            if (c.Subject == BindingSubject.Any && c.Trigger != TriggerKind.OnPlace) return ComboVerdict.NoOrigin;
            // ④ 부착 즉시 전용 효과 — 그 원점(부착 순간)은 사건이 못 낸다.
            if (SkillRouting.OnlyValidWithNoTrigger(c.Payload)) return ComboVerdict.ShapeMismatch;
            // ⑤ 주인이 떠나는 사건(죽음 · 퇴근)은 **자리**만 남긴다 — 살아 있는 발동 주체를 쓰는 효과는 못 받는다.
            if (SubjectLeaves(c.Trigger) && !UsesOnlySite(c.Payload)) return ComboVerdict.ShapeMismatch;
            // ⑥ 「반경 안 전원」은 대상 결합 탄만 — 칸 결합은 조준이 둘이고 방향 결합은 대상이 없다.
            if (c.FanOut && (!c.HasProjectile || c.Binding != BindingClass.Entity)) return ComboVerdict.NoOrigin;
            // ⑦ 도발은 어그로를 드는 몸에서 나온다 — 그 몸이 아니면 효과가 받을 원점이 아니다.
            if (c.Payload == EffectKind.AreaTaunt && c.HostCannotHoldAggro) return ComboVerdict.ShapeMismatch;
            // ⑧ 비율형 수치(skill-data-table unit 3 · `tables.md` §9) — 비율 칸이 있는 종류만 · 기준 스탯을 읽을 주인이 있을 때만.
            //    「남의 배치」(`Any`)는 기준 = **규칙 소유자(숙주)** 의 스탯이다(U17) — 발사 자리 · 킬 귀속만 놓인 유닛(U3)이라 허용한다.
            if (c.Magnitude == MagnitudeMode.OwnerStatRatio)
            {
                if (!EffectMagnitude.AcceptsRatio(c.Payload)) return ComboVerdict.NoRatioField;
                if (c.CastHasNoOwner) return ComboVerdict.NoRatioBasis;
            }
            return ComboVerdict.Allowed;
        }

        /// <summary>경고 문안의 사유 한 줄.</summary>
        public static string Describe(ComboVerdict v)
        {
            switch (v)
            {
                case ComboVerdict.NoOrigin: return "원점을 못 낸다";
                case ComboVerdict.ShapeMismatch: return "효과가 그 원점 형을 못 받는다";
                case ComboVerdict.NeverFires: return "붙는 순간 이미 지난 자기 사건이거나 사건이 없다(영영 안 터짐)";
                case ComboVerdict.NoRatioBasis: return "비율 기준이 없다(주인 없는 시전)";
                case ComboVerdict.NoRatioField: return "그 효과에는 비율 칸이 없다(피해 · 실드량만)";
                case ComboVerdict.NotWired: return "배선 전 — 트리거 없음(보유 시작 순간) 효과는 카드 부착에만 배선됐다(방어유닛 · 적이 직접 들면 unit 7 후속)";
                default: return "허용";
            }
        }

        /// <summary>발화 시점에 주인이 판에 없는 사건 — 원점은 감지자 스냅샷(자리 · 몸)뿐이다.</summary>
        public static bool SubjectLeaves(TriggerKind trigger)
            => trigger == TriggerKind.OnDeath || trigger == TriggerKind.OnRetire;

        // 떠난 자리만으로 일하는 효과 — 자리 폭발(`DeathSiteBlastSkill`) · 자리 장판(`DeathSiteHazardSkill`) · 분열(7d) · 인수인계(손패 선언).
        private static bool UsesOnlySite(EffectKind payload)
            => payload == EffectKind.SelfTileAoe
            || payload == EffectKind.SpawnHazard
            || payload == EffectKind.SplitOnDeath
            || payload == EffectKind.RecallAttachedToFront;
    }
}
