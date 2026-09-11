using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // directional-attack-shape unit 1 — 저작(`AttackShape`) → bake(`AttackShapeBaked`) 순수 변환.
    //
    // sim 은 각도를 모른다: 저작 각도는 여기서 **한 번** `(sin, cos)` 가 되고 폭은 반폭이 된다
    // (`SkillCone.cosSq` 선례 — 저작값 하나가 두 표현으로 갈리지 않는다).
    //
    // **폴백은 전부 Omni(= 오늘 동작)** — 도형은 게이트라 fail-open 이 안전하다. 반대로 접으면 저작 실수
    // 하나가 유닛을 무력화한다. `ok = false` 는 «정의역 밖»을 저작자에게 말하는 신호이고, 로그는
    // 호출부(브리지)가 찍는다 — 이 함수는 순수하다(제약 10).
    //
    // 정의역: 전체각 (0°, 180°] ∪ {360°}. 그 사이(reflex)는 두 반평면의 여집합이라 같은 dot 두 번으로
    // 되지만 플레이 가치가 360° 와 구분되지 않아 층을 안 만든다(제약 8). `SkillCone` bake 가 반각 ≥ 90 을
    // 거절하는 것과 같은 규율 — 「270° 를 조용히 다른 각으로 돌리지 않는다」.
    public static class AttackShapeBake
    {
        public static AttackShapeBaked From(in AttackShape authored, out bool ok)
        {
            ok = true;
            switch (authored.kind)
            {
                case AttackShapeKind.Rect:
                    return new AttackShapeBaked
                    {
                        kind = AttackShapeBaked.BandKind,
                        halfWidth = authored.width < 0f ? 0f : authored.width * 0.5f,
                    };

                default: // Circle
                {
                    float a = authored.angleDeg;
                    if (a <= 0f || a >= 360f) return AttackShapeBaked.Omni;   // 0 = 미저작 · 360 = 전방위
                    if (a > 180f) { ok = false; return AttackShapeBaked.Omni; } // reflex — 정의역 밖, fail-open
                    double half = a * 0.5 * (System.Math.PI / 180.0);
                    return new AttackShapeBaked
                    {
                        kind = AttackShapeBaked.SectorKind,
                        sinHalf = (float)System.Math.Sin(half),
                        cosHalf = (float)System.Math.Cos(half),
                    };
                }
            }
        }
    }
}
