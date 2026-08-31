using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Presentation
{
    public static class BoardSortOrder
    {
        public const int CharacterOffset = 1;
        // 투사체 VFX: 유닛 order(Compute 최대 = 보드높이×행간격+폭, 아래 참조) 보다 확실히 위,
        // 데미지 숫자(32000)·UI 아래. 근접 시 적 스프라이트에 가려지지 않게.
        // map-diorama-stage unit 3 — 1000→4000: 행 간격이 폭 종속(max(10, w+2))이 되면서
        // playArea 상한(48×48 ⇒ 최대 ≈ 2448)이 구 대역(1000)을 넘는다. BoardSortOrderTests 가
        // «Compute 최대 < ProjectileOffset» 를 상한 값으로 고정한다.
        public const int ProjectileOffset = 4000;
        // Compute 가 지원하는 playArea 상한(셀). 이를 넘는 스테이지는 대역 재설계가 필요하다.
        public const int MaxGridSide = 48;
        // tilted-billboard unit 3 — 블롭 그림자: 바닥 타일맵(ground −20 / overlay −10) 위, 캐릭터(양수) 아래.
        public const int ShadowOrder = -5;
        // unit-health-display unit 2 — 적 피격 마이크로바: 캐릭터·투사체(1000) 위, 데미지 숫자(32000) 아래.
        // bg = 이 값, fill = +1.
        public const int HitBarOrder = 16000;
        // unit-health-display unit 3 — 방어유닛 타일 테두리 게이지: 바닥 데칼(그림자 −5 위, 캐릭터 양수 아래).
        public const int TileGaugeOrder = -4;
        // placement-drag-preview-polish — 드래그 프리뷰 실루엣: 배치 중 배경 프랍/유닛/투사체 위로.
        // 프랍(prop.sortingOrder + Compute)·유닛(Compute+1)·투사체(+1000) 위, UI(Canvas) 아래.
        public const int DragPreviewOrder = 20000;
        // placement-cell-snap unit 4 — 배치 확정 팝: 상승한 overlay 하이라이트(10002) 위, 드래그 프리뷰(20000) 아래.
        public const int PlacementCommitPopOrder = 12000;
        // distance-based-range unit 5 — 공격 사거리 **링**(윤곽). 바닥 대역이다.
        //
        // ⚠ **유닛 위로 올리지 않는다**(사용자 결정 2026-08-31). 세계에 그린 도형이 스프라이트를
        // 관통하면 **UI 오버레이로 읽혀 물성이 깨진다.** 밀집 전투에서 선이 유닛에 끊기는 것은
        // 감수하고, **끊김은 채움(옅은 라임)이 흡수한다** — 정렬로 피하지 않는다.
        // 값: 범위 타일(−12)·고스트(−11)·overlay(−10) 위 = 채움 위에 선이 얹힌다.
        // 그림자(−5)·타일 게이지(−4)·유닛(양수) 아래 = 유닛이 선을 가린다(의도).
        public const int RangeRingOrder = -8;

        // placement-cell-snap unit 7 rev — 끈적 액체 하이라이트: 바닥 타일 하이라이트 위, 확정 팝(12000) 아래.
        // 팝은 확정 순간의 이완이라 액체보다 앞에 터져야 한다.
        public const int PlacementLiquidOrder = 11000;

        // defender-directional-volley unit 9 — 방향 지정 화살표. **보드에 그려진 것**이므로
        // "보드 레이어 < 유닛 레이어" 규칙을 따른다(스폰 예고 라인과 같은 판단):
        // 범위 타일(−12) 바로 위, overlay(−10)·그림자(−5)·유닛(양수) 아래 = 유닛이 화살표를 가린다.
        // 이전 값 11500 은 유닛 위로 떠서 조준 중 유닛을 덮었다(사용자 지적 2026-07-28, unit 5).
        public const int AimArrowOrder = -11;

        // spawn-point-alert unit 1(rev) — 스폰 예고 라인은 **바닥에 그려진 것**이다.
        // "보드 레이어 < 유닛 레이어" 규칙(TilemapMapView)에 따라 음수 대역에 둔다:
        // overlay 타일맵(−10) 위 · 블롭 그림자(−5)/타일 게이지(−4)/유닛(양수) 아래.
        // 레이어 4개(광휘/스트릭/코어/링)가 +0~+3 을 쓰므로 −9 부터 −6 까지 정확히 채운다.
        public const int SpawnAlertOrder = -9;

        // beam-ranger-defender unit 1 — 지속 빔. 벤더 프리팹은 order 0~2 로 들어오는데 그대로
        // 두면 **유닛(Compute = 수백대) 뒤에 깔린다**: 바닥(−20/−10) 위라 빈 땅 구간만 보이고
        // 유닛에 가린 구간은 끊겨 보이며, 적이 여럿이면 대부분 적 뒤에 숨어 "빔이 1개만" 처럼
        // 보인다(사용자 제보 2건의 실제 원인). 투사체(+1000) 위 · 피격바(16000) 아래에 둔다.
        // 프리팹 내부의 상대 순서(0/1/2)는 이 값에 **더해서** 보존한다.
        public const int BeamOrder = 15000;

        // spine-weapon-trail unit 0 — 무기 궤적 리본. 빔(15000) 위 · 피격바(16000) 아래.
        // 궤적은 공격자와 대상 **앞**에 떠야 "벤 자국"으로 읽힌다 — 유닛(Compute = 수백대) 뒤로
        // 가면 몸통에 잘려 궤적이 아니라 배경 반짝임이 된다(빔과 같은 증상).
        // ⚠ 이 상수는 **대역 문서이자 대조 기준**이고, 런타임에 실제로 적용되는 값은
        // `HS_SwordTrailPreset.materialLayers[].sortingOrder` 다. HS_SwordMeshTrail 이 매
        // LateUpdate 끝에 ApplyRendererSettings() 로 renderer.sortingOrder 를 프리셋 값으로
        // 되쓰기 때문에 외부에서 런타임에 써봐야 한 프레임 뒤 덮인다. 값을 바꿀 땐 둘을 같이 바꾼다.
        public const int WeaponTrailOrder = 15500;

        // elite-enemy-tier unit 4 — 화염 브레스 원샷. 빔(15000)과 **같은 이유·같은 증상**이다:
        // VFXPACK_FIRE_WALLCOEUR 프리팹이 order 0~2 로 들어와서 그대로 두면 유닛(Compute =
        // 수백대) 뒤에 깔리고, 「드래곤 발밑에 눌린 불」로 보인다(사용자 제보 2026-08-13).
        // 브레스는 시전자 **앞**에 떠야 전방 분사로 읽힌다. 투사체(+1000) 위 · 빔(15000) 아래.
        // 프리팹 내부의 상대 순서는 이 값에 **더해서** 보존한다(빔과 같은 규약).
        public const int AreaBreathOrder = 14000;

        // elite-whirlpot unit 1 — 유닛별 공격 광역 VFX(팽이 회오리). 브레스와 **반대 대역**이다.
        // 브레스는 시전자 «앞» 으로 뿜는 것이라 유닛 위여야 하지만, 회오리는 시전자를 «감싸는»
        // 것이라 유닛 위에 두면 몸을 덮어 무엇이 도는지 안 보인다. 그래서 "보드 레이어 <
        // 유닛 레이어" 규칙(스폰 예고 라인·조준 화살표와 같은 판단)에 따라 음수 대역이다:
        // 그림자(−5)·타일 게이지(−4) 위 · 유닛(양수) 아래.
        // 프리팹 내부의 상대 순서(0~2)는 이 값에 **더해서** 보존하므로 −3~−1 을 쓴다.
        public const int UnitAttackAoeOrder = -3;

        // instinct-wreck unit 1 — 부서진 거점에서 오르는 연기(버스트 + 잔불).
        // 벤더(VFXPACK_FIRE_WALLCOEUR)는 order 0~2 로 오는데 그대로 두면 유닛(Compute =
        // 수백대) 뒤로 가는 게 아니라 **바닥 타일(−20/−10) 위 · 유닛 아래** 라는 애매한 자리에
        // 걸린다. 잔해는 «이미 끝난 배경 사건» 이라 회오리(UnitAttackAoeOrder)와 **같은 판단**
        // — "보드 레이어 < 유닛 레이어" 규칙을 따라 음수 대역에 둔다. 그림자(−5)·타일
        // 게이지(−4) 위 · 유닛(양수) 아래. 회오리 대역(−3~−1)과 겹치지만 둘 다 유닛 아래라
        // 무해하고, `VFX_Smoke` 는 서브 파티클 없는 단일 ParticleSystem 이라 값 하나면 된다.
        // ⚠ 실제 적용값은 **프리팹의 `ParticleSystemRenderer.sortingOrder`** 다 — 잔해 VFX 는
        // 프랍 자식으로 저작되므로 코드가 런타임에 쓰지 않는다. 이 상수는 대역 문서이자
        // 대조 기준이고, 값을 바꿀 땐 둘을 같이 바꾼다(WeaponTrailOrder 와 같은 규약).
        public const int StructureWreckOrder = -2;

        // map-diorama-stage unit 3 — 행 간격을 상수 10 에서 폭 종속으로. 간격 < 폭이면 뒷줄
        // 오른쪽 유닛이 앞줄 왼쪽 유닛을 덮는다(폭 13~30 맵에서 실측된 기존 결함). +2 여유는
        // CharacterOffset(+1) 류의 행내 오프셋이 다음 행과 겹치지 않게 하는 완충.
        public static int RowStride(int2 gridSize) => math.max(10, gridSize.x + 2);

        public static int Compute(int2 gridSize, int cellX, int cellY, int offset = 0)
            => (gridSize.y - cellY) * RowStride(gridSize) + cellX + offset;

        public static int ComputeFromWorld(int2 gridSize, Vector3 world, float tileSize, int offset = 0)
        {
            float safeTileSize = Mathf.Max(0.0001f, tileSize);
            int cellX = Mathf.RoundToInt(world.x / safeTileSize);
            int cellY = Mathf.RoundToInt(world.z / safeTileSize);
            return Compute(gridSize, cellX, cellY, offset);
        }
    }
}
