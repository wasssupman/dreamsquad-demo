using UnityEngine;

namespace Wassup.Data
{
    // defender-ability-assets unit 0 — 유닛 고유능력 서브에셋의 base. DefenderUnitData 의
    // 능력별 flat 필드 산발을 대체한다(유닛은 abilities 리스트 하나만 보유).
    // 정의 계층은 아키텍처 무지(DcMechanic 계약 승계) — Unity.Entities/ECS 타입 참조 금지.
    // 해석(ECS 컴포넌트 bake)은 BattleBridge.CreateDefenderEntity 번역자 단독(spec 계약 3).
    public abstract class DefenderAbilityData : ScriptableObject
    {
        // 시트 매칭키(슬러그, 에셋명 스네이크 컨벤션). 능력종별 탭 싱크는 후속 unit —
        // unit-stat-spreadsheet-schema 의 id 매칭 규약 승계(spec 계약 5).
        public string id;

        // 이 능력이 배치 시 공격방향 지정(조준 페이즈)을 요구하는가.
        // 구 DefenderUnitData.directionalAttack flag 의 대체(spec 계약 4) —
        // capability 는 능력이 선언하고, UX 소비처는 유닛 헬퍼만 읽는다.
        public virtual bool RequiresFacing => false;
    }
}
