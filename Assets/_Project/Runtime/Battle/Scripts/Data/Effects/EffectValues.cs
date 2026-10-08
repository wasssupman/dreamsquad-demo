using System;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.Data.Authoring;

namespace Somnia.Battle.Data
{
    /// <summary>
    /// skill-data-table unit 4 — **효과 한 줄의 값**(`tables.md` §2 `Effects` 의 스칼라 · enum 칸). 참조(탄 · 명세 · 장판)와 뷰 칸은
    /// `EffectData` SO 가 든다 — 이 struct 는 SO 없이 만들고 비교할 수 있는 순수 값이다(이전 변환 · 헤드리스 dry-run 이 쓴다).
    ///
    /// 옛 겸직 칸(`magnitude` · `tileRange` · `duration`)을 **뜻 이름 칸**으로 푼 것이다. 종류마다 어느 칸을 읽는지는
    /// `EffectSlots.Of` 한 표가 정한다(그 표가 옛 칸 ↔ 새 칸의 양방향 정본 — 굽기와 이전이 같은 표를 지난다).
    /// ⚠ 필드 추가는 뒤에만(append-only · 에셋이 이름으로 직렬화하지만 시트 열 순서가 이 순서를 따른다).
    /// </summary>
    [Serializable]
    public struct EffectValues
    {
        public EffectKind kind;

        // U7 — 수치 방식. `Flat`(기본) = 아래 칸 그대로 · `OwnerStatRatio` = 그 종류의 비율 칸(피해 · 실드량)을 시전 순간 소유자
        // `basisStat` 최종값 × `ratio` 로 채운다(코어 `EffectMagnitude`). 비율 칸은 비워 둔다.
        public MagnitudeMode magnitudeMode;
        public BasisStat basisStat;
        public float ratio;

        // U10 — 피해는 여기에만 있다(패턴 · 장판 DoT · 길막 폭발 · 착지 슬램 포함).
        public float damage;
        public float shield;
        public float percent;      // % (+30 = +30% · 음수 허용)
        public float mul;          // 배율(2 = ×2)
        public int count;
        public int radiusTiles;    // 광역 원 반경(+칸 반폭 · 대상 몸은 코어가 더한다 — 제약 13)
        public int rangeTiles;     // 사거리 · 재조준 · 조준 거리
        public float durationSec;
        public float flightSec;    // 발사 → 착탄(자리형 · 예고 시간)
        public float tickSec;      // 0 이면 `damage` = DPS(`AreaDot`)
        public int stackCap;
        public float speed;        // 넉백 · 당김 속도
        public float coneHalfDeg;  // (0, 90) — `AreaBreath`
        public int densityRadiusTiles;
        public int landingRingTiles;
        // ⚠ CC · 스택은 **스킬 어휘 번호**(`Data.Authoring.CcKind` = `SkillCcKind` · `StackKind` = `SkillStackKind`)다 — 옛 저작
        // `DcCcKind`(Stun 0 · Impulse 1 · Sleep 2) · `DcStackKind`(None 없음)와 번호가 다르다. 옮길 때는 `EffectSlots.CcFromLegacy` ·
        // `StackFromLegacy`(이전 스크립트)를 지난다.
        public CcKind ccKind;
        public StackKind stackKind;
        public CardBuffKind buffStat;
        public ShieldTargetFilter shieldFilter;
        public bool includesSelf;
        public bool telegraph;     // U1 착탄 예고
        // skill-data-table unit 8 — **수혜 대상은 효과의 뜻**(계약 12 · `shieldFilter` 선례): 진영 버프(`FactionStatBuff`)와 배치 오라
        // (`PlacementAura`)가 누구에게 거는가. 값 = 옛 카드 `axis` 와 같은 어휘(`CardTargetAxis` — 개명은 후속 후보). 시트 열 `ally_filter`.
        public CardTargetAxis allyFilter;
    }
}
