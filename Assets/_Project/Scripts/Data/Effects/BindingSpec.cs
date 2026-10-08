using System;

namespace Somnia.Battle.Data
{
    /// <summary>
    /// skill-data-table unit 4 — **소유 줄**(`tables.md` §4 `Skills` 한 줄): 언제(트리거 · 주체 · 게이트) → 효과 에셋. 카드 · 방어유닛 · 적이
    /// 같은 형식을 `bindings` 배열로 든다(계약 2 — 옛 저장처 넷 `mechanics` · `UnitSkillAbility` · `nightmareMechanics` · `ShieldCastAbility` 를 합친다).
    ///
    /// **수명은 칸이 아니다** — 소유자 종류에서 파생한다(카드 · 유닛 · 적 = 주인 수명 · 시전 = 발동 상한까지). 저작 손잡이는 `fireCap` 하나.
    /// </summary>
    [Serializable]
    public struct BindingSpec
    {
        public TriggerSpec trigger;
        [UnityEngine.Tooltip("발동 상한 — 0 = 무제한. 궁극기(UltimateLeap) = 1(「생존당 1회」). 시전(Cast) = 1.")]
        public int fireCap;
        public EffectData effect;
    }

    /// <summary>
    /// skill-data-table unit 4(U5) — 카드가 **붙을 수 있는 숙주 종류**(부여 게이트 — 계약 4). 굽기가 켜진 종류마다 조합 검증을 돌린다.
    /// 기본 = 방어유닛. 적 표식 카드(`BountyMark`) = 적만(옛 `HasBountyMark()` 파생을 값으로). append-only(에셋이 정수로 직렬화).
    /// </summary>
    [Flags]
    public enum HostKinds
    {
        None = 0,
        Defender = 1,
        Enemy = 2,
    }

    /// <summary>
    /// skill-data-table unit 4 — 소유 줄을 **옛 메커닉 모양으로 보는 창**(이전 뒤 과도기). 문안(`DreamcatcherCardText`) · 진단(`UnitKitSummary`) ·
    /// 굽기의 잎 검증 함수가 아직 옛 칸 이름(magnitude · tileRange · duration)으로 읽는다 — 그 읽기를 소유 줄 위로 옮기되 **값은 같게**
    /// (`EffectSlots` 표 하나 · 라이브 왕복 = 이전 dry-run 이 확인). 저장 형식이 아니다(에셋에 안 쓴다).
    /// ⚠ 소유자별 인코딩은 싣지 않는다 — 효과 값은 소유자 무관(예: 자기 버프 = %).
    /// </summary>
    public static class BindingSpecView
    {
        public static DcMechanic ToMechanic(in BindingSpec s)
        {
            var e = s.effect;
            if (e == null) return new DcMechanic { trigger = s.trigger };
            var p = EffectSlots.ToLegacy(in e.values);
            return new DcMechanic
            {
                trigger = s.trigger,
                payload = new DcPayloadSpec
                {
                    kind = p.Kind,
                    magnitude = p.Magnitude,
                    tileRange = p.TileRange,
                    duration = p.Duration,
                    projectile = e.projectile,
                    pattern = e.pattern,
                    hazard = e.hazard,
                    auraPrefab = e.auraPrefab,
                    auraScale = e.auraScale,
                    stackModifier = e.stackModifier,
                    ccKind = EffectSlots.CcToLegacy(e.values.ccKind),
                    stackKind = EffectSlots.StackToLegacy(e.values.stackKind),
                    buffStat = p.BuffStat,
                    slamDamage = p.SlamDamage,
                    slamTileRange = p.SlamTileRange,
                    tickIntervalSec = p.TickIntervalSec,
                    orbitCount = p.OrbitCount,
                    coneHalfAngleDeg = p.ConeHalfAngleDeg,
                    telegraph = p.Telegraph,
                },
            };
        }

        /// <summary>소유 줄 배열 전부(null = 빈 배열). 관리 배열을 새로 만든다 — 굽기 · UI 시점 전용, 매 프레임 금지.</summary>
        public static DcMechanic[] Of(BindingSpec[] bindings)
        {
            if (bindings == null || bindings.Length == 0) return Array.Empty<DcMechanic>();
            var r = new DcMechanic[bindings.Length];
            for (int i = 0; i < r.Length; i++) r[i] = ToMechanic(in bindings[i]);
            return r;
        }
    }
}
