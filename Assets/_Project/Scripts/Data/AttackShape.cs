using System;
using UnityEngine;

namespace Wassup.Data
{
    // directional-attack-shape unit 1 — 공격 판정 도형의 **저작** 형태. `DefenderUnitData`·`AttackUnitData` 공통.
    //
    // 스키마는 `AttackOutput` 형태다(kind + 이름 붙은 flat 필드, 형마다 파라미터 **하나**). 리포 선례 중
    // 실패한 두 가지를 피한다: `DcTriggerSlot.tileRange`(스칼라 하나가 13가지 뜻 겸직 — `SkillParams` 헤더가
    // 「못 읽게 만든 원인」으로 기록)와 `HazardShape`(사전 정의 도형 enum — `Square3x3` 과 `RadiusSquare(1)` 이
    // 같은 도형의 두 표현). `angleDeg`(도, 거리에 비례해 벌어짐)와 `width`(타일, 평행)는 둘 다 «세로 허용»이지만
    // 단위·기하가 달라 한 필드로 접으면 `tileRange` 의 재현이다.
    //
    // ⚠ 도형은 「얼마나 멀리」를 정하지 않는다 — 반경도 길이도 `attackRange` 가 소유한다. 여기 있는 것은
    //   「주 대상 쪽으로 얼마나 좁게」뿐이다. 방향 = 주 대상을 향한 실제 방향(rev 3). 캐릭터 좌/우 반전은 연출.
    // ⚠ `None` kind 가 없다 — `Circle/360` 이 항등원이라 `None` 은 같은 것의 두 번째 표현이다.
    //   bake 쪽(`AttackShapeBaked`)엔 `Omni = 0` 이 있는데 그건 `default(AttackState)` 가 안전해야 해서다.
    //   둘의 번호를 맞추려 들지 말 것.
    public enum AttackShapeKind : byte { Circle = 0, Rect = 1 }

    [Serializable]
    public struct AttackShape
    {
        public AttackShapeKind kind;

        [Tooltip("Circle 전용 — 주 대상 쪽 부채꼴 전체각(도). 360 = 전방위(오늘 동작). 0 = 미저작 → 360. " +
                 "180 초과 360 미만(reflex)은 bake 가 거절하고 360 으로 읽는다.")]
        [Range(15f, 360f)] public float angleDeg;

        [Tooltip("Rect 전용 — 주 대상 쪽 띠의 좌우 폭(타일). 길이는 attackRange.")]
        [Min(0f)] public float width;

        public static AttackShape Omni => new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 360f };
    }
}
