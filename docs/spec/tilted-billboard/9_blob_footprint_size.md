# 9 — 블롭 크기 = footprint 가로 타일 수

## 목적

블롭 지름이 전역 1타일 고정이라 2~3칸을 차지하는 유닛도 1타일 원을 깔고 있다.
크기를 **점유 폭**에서 파생시켜, 그림자가 «이 유닛이 몇 칸을 쓰는지»를 말하게 한다.

유닛별 크기 노브는 만들지 않는다(계약 6, 사용자 결정 2026-08-28) — 크기는 저작 취향이 아니라
footprint 의 함수다.

## 변경 대상

- `Assets/_Project/Scripts/Data/ISpineUnitVisualData.cs`
- `Assets/_Project/Scripts/Data/AttackUnitData.cs`
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs`
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs`

## 구현

### 폭의 출처

```csharp
// 점유 폭(가로 셀). 블롭 지름의 기준. 적/보스는 1(sim 이 1칸 점유).
int FootprintWidthCells { get; }
```

- `DefenderUnitData` → `Footprint.x` (기존 `footprintWidth`, 현재 저작값 1·2·3)
- `AttackUnitData` → `1`

**공용 인터페이스에 두는 이유**: 점유 폭은 디펜더 전용 개념이 아니다 — 보스가 2×2 를 쓰게 되는 날
`IDefenderSpineExtras` 에 넣어뒀다면 길이 막힌다. 그 함정은 무기 궤적에서 이미 한 번 겪었고
`ISpineUnitVisualData` 주석에 기록돼 있다. 적의 `1` 은 "범위 밖 더미"가 아니라 **참값**이다.

### 크기 산식

```
지름(월드) = FootprintWidthCells * BattleBridge.BlobShadowSize
```

전역 `BlobShadowSize`(현재 1.0)는 **배율**로 의미가 정리된다 — 필드 이름·타입·씬 값 불변.
1×1 유닛과 모든 적은 `1 * 1.0 = 1.0` 으로 **현행과 동일**(계약 9).

`QuadUnitView.Configure` 는 `visualData` 를 안 받으므로 `int footprintWidth` 파라미터를 추가하고,
`QuadUnitViewPool.TrySpawn` 까지 **관통**시킨다. **기본값을 두지 않는다** — 리뷰(2026-08-30)에서
기본값 1 이 디펜더 fallback(quad) 경로를 조용히 삼킨 것이 잡혔다. 호출처 3곳이 의도를 말한다:
디펜더 fallback 2곳(`BattleBridge.cs` 의 `!spineSpawned` 분기)은 `unitData.Footprint.x`,
적 1곳은 리터럴 `1`.

### 단위에 대한 주의 (리뷰 F3)

지름은 `footprintCells × BlobShadowSize` 로 **월드** 지름이 되는데, footprint 는 **셀** 단위다.
같은 feature 의 `BattleBridge.FootprintViewOffset` 은 같은 셀→월드 변환에서 `tileSize` 를 곱한다.
`tileSize ≠ 1` 이면 두 곳이 갈린다.

**지금 고치지 않는 이유**: 산식이 곱셈이라 전역 `blobShadowSize` 를 `tileSize` 로 두면
**모든 폭에 균일하게** 보정된다(리뷰의 "폭 3 은 전역 노브로 못 고친다"는 성립하지 않는다 —
`3 × 1.5 = 4.5` 가 그대로 나온다). 현재 `tileSize: 1` 이고 스테이지 저작 규약이
`previewTileSize == 런타임 tileSize` 를 하드 체크한다. 남는 것은 «전역 노브가 예술적 배율과
셀→월드 환산을 겸직한다»는 의미 혼탁뿐이라 후속 후보로 이관한다.

### 모양은 원형 유지

가로 폭 지름의 원을 깐다. 2×3 유닛에서 세로가 남는 것은 **수용**한다 —
footprint 를 덮는 타원/rect 는 README 후속 후보.

> unit 3 은 원래 `blobShadowFootprint(1.35, 0.95)` 로 **타원**을 저작했는데 이후 은퇴하고 원형만 남았다.
> 이 unit 은 타원을 되살리는 게 아니라 **지름의 출처**만 전역 상수 → footprint 폭으로 옮긴다.

## 완료 기준

- [x] Play 실측: Malphite(폭 3) 블롭 월드 지름 **3.00**, AntiAir(폭 1) **1.00**
      — ⚠ 이 실측은 `eb2386ad` **이전**(footprint 저작이 살아 있던 시점)이다. 그 커밋이
      **전 유닛 저작을 1×1 로 철회**했으므로(사용자 결정 2026-08-30) 현재 저작값으로는
      전부 지름 1.00 = 화면 변화 0 이다. 메커니즘은 그대로 살아 있어, `footprintWidth` 를
      다시 올리면 블롭이 **코드 0 줄로** 따라온다 — 그 철회 커밋이 multi-cell 시스템을
      «값만 1» 로 남긴 것과 같은 계약이다.
- [x] 회귀 가드: 폭 1 = 1.00 (= 전역 배율 1.0, 종전과 동일)
- [x] 코어 lane 2494 초록
- [x] `docs/reference/` 갱신 불요 — 블롭은 어느 참조 문서에도 정거장으로 등재돼 있지 않다

확인: 2026-08-30 사용자 Play 확인 통과 · 커밋 `06ab754a`
