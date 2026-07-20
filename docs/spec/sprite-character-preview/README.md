# Sprite Character Preview

상태: **홀드 2026-07-20** (사용자 지시). 구현·커밋은 완료, Play 육안 확인 미완.

> 재개 전에 `attack.png`·`death.png` 를 온전한 파일로 다시 받아야 한다 —
> 현재 파일은 IDAT 중간에서 잘려 각각 29% · 41% 의 픽셀 데이터가 없다.
> 상세와 남은 작업은 `2_handoff_summary.md`. **지시 없이 재개하지 말 것.**

## 상위 목표

**스프라이트 시트 애니메이션으로 만든 캐릭터를 맵 위에 수동으로 올려놓고 눈으로 확인**할 수 있는
프리팹과 재생 컴포넌트를 만든다. 현재 모든 캐릭터는 Spine 기반이고, 그 대안이 실제로 볼 만한지
판단할 근거가 없다.

이 spec 은 **비주얼 확인용**이다. 게임 로직에 진입하지 않는다.

## 명시적 비목표

다음은 전부 이번 범위 **밖**이다 (2026-07-20 사용자 결정):

- `BattleBridge` / `SpineUnitPool` / `DefenderUnitData` 변경 — **한 줄도 건드리지 않는다**
- 전투 연동 (스폰·공격 이벤트·사망 이벤트·배치 코스트)
- 백엔드 선택 로직 (Spine ↔ 스프라이트 자동 전환)
- 파츠 스킨 조합 · 슬롯 틴트 · 캐스트 앵커 본 추적
- 좌우 반전 자동화 · 공격 애니 주기 압축
- 적/보스 · 로비 스쿼드 페이지 · 드래그 배치 프리뷰

프리팹은 사용자가 **씬에 직접 배치**해서 본다. 코드가 스폰하지 않는다.

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 컴포넌트 | `0_character_view.md` | `FlipbookCharacterView` — 상태별 플립북 재생 + EditMode 테스트 |
| 1 | 오소링 | `1_preview_prefab.md` | 인스펙터 상태 버튼 + 템플릿 프리팹 + 맵 배치 확인 |
| 2 | 인계 | `2_handoff_summary.md` | 구현 종료 요약 (커밋 해시 · 시트 오소링 함정) |

## Feature-wide 계약

- **기존 재생기를 고치지 않는다.** `SpriteFlipbookPlayer` / `SpriteFlipbookData` / `FlipbookMath` 는
  그대로 쓴다. 이 spec 이 얹는 것은 **상태 매핑과 전이**뿐이다.
  재생기 확장이 필요해 보이면 그건 이 spec 이 뭔가를 잘못 하고 있다는 신호다.
- **시트는 사용자가 만들어 넣는다.** placeholder 플립북을 만들지 않는다 (2026-07-20 사용자 결정).
  구현은 슬롯이 빈 상태로 완료되고, 시각 검증은 사용자가 시트를 넣고 신호할 때 수행한다.
- **상태는 5개 고정** — `Idle` · `Attack` · `Death` · `Deploy` · `Drag`.
  Spine 디펜더가 실제로 쓰는 상태 집합과 같다. walk 는 없다 (디펜더는 타일 고정이라
  `DefenderUnitData.SpineWalkAnimation => ""` 으로 하드코드돼 있다).
- **필수 3 + 선택 2.** `Idle`/`Attack`/`Death` 는 필수, `Deploy`/`Drag` 는 비면 `Idle` 로 폴백한다.
  Spine 쪽 `SpineUnitView.ResolveAnimation` 폴백 체인과 같은 정신이다.
- **루프 정책은 상태가 소유한다.** `Idle`/`Drag` 는 루프, `Attack`/`Death`/`Deploy` 는 원샷.
  데이터가 이를 위반하면 경고한다 (아래 함정 참조).
- **`Death` 는 마지막 프레임을 유지한다. GameObject 를 파괴하지 않는다.**
  게임에서는 사망 후 파괴가 맞지만, 이건 확인용 프리팹이라 재실험이 가능해야 한다.
- **클럭·정렬·수명은 재생기 계약을 그대로 승계한다.** `TimeManager` 도메인 시간을 쓰고,
  `sortingLayer`/`order` 는 프리팹이 authored 로 소유하며, 뷰는 자기 GameObject 를 만들지도 지우지도 않는다.

## 함정: 원샷 시트에 루프가 켜져 있으면 상태가 갇힌다

원샷 → `Idle` 복귀 판정은 `SpriteFlipbookPlayer.IsPlaying` 폴링이다. 루프 플립북은
`IsPlaying` 이 **영원히 참**이라 복귀가 일어나지 않는다. `Attack` 시트에 `loop` 가 켜져 있으면
유닛이 공격 애니에 영구 고착되고, 원인이 컴포넌트가 아니라 **에셋 체크박스**라 추적이 오래 걸린다.

`sprite-flipbook-player` spec 의 handoff 가 이미 경고한 지점이다
(`IsLooping` 이 그래서 존재한다). 여기서는 2겹으로 막는다:

- `OnValidate` — 상태별 루프 정책 위반을 **에셋 이름과 함께** 경고
- 런타임 `Play` — 원샷 상태에 루프 데이터가 들어오면 에러 로그

## 파이프라인 커버리지

플레이 오브젝트를 신설하지 않는다 — 사용자가 손으로 배치하는 **확인용 프리팹**이다.
`docs/reference/object-pipeline-map.md` 갱신 대상이 아니다.

| 정거장 | 이 spec | 비고 |
|---|---|---|
| 데이터 소스 | `SpriteFlipbookData` × 5 (기존 SO) | 신규 SO 를 만들지 않는다 |
| 트리거 | 인스펙터 버튼 (수동) | 게임 이벤트 연결 없음 — 비목표 |
| ECS | N/A | 시뮬을 읽지도 쓰지도 않는다 |
| View | `Presentation/FlipbookCharacterView.cs` | 풀링 없음 — 씬 배치 인스턴스 |
| 정렬 | 프리팹의 `SpriteRenderer` authored | 뷰는 안 건드린다 |
| 씬 wiring | N/A | 사용자가 직접 배치 |

## 후속 후보 (현 spec 범위 밖)

- **실제 전투 연동** · 스프라이트 캐릭터를 게임에 넣기로 하면 별도 spec. 사전 조사는 아래 참조.
- **적/보스 스프라이트 백엔드** · 디펜더가 먼저 검증된 뒤.
- **드래그 배치 프리뷰** · `DefenderDragPlacementController` 가 `SkeletonAnimation` 을 직접 만든다.
  스프라이트 유닛은 지금 캡슐 폴백이 뜬다.
- **로비 스쿼드 캐릭터 페이지** · `SquadUnitDetailView` 가 `SkeletonGraphic`(UGUI) 을 쓴다.

### 전투 연동을 하게 되면 — 사전 조사 결과 (2026-07-20)

다시 파헤치지 않도록 남긴다. **`BattleBridge` 는 거의 안 고쳐도 된다.**

- 전 코드베이스에서 `SpineUnitView` 를 **타입으로 명시한 호출부는 단 1곳** —
  `BattleBridge.cs:2811 TryGetUnitView(Entity, out SpineUnitView)`. 나머지 13개 seam 은
  전부 `spineUnitPool.TryGet(entity, out var view)` 형태라 인터페이스로 바꿔도 소스가 안 바뀐다.
- 따라서 경로는 `SpineUnitView` 의 **현재 public 표면을 그대로** `IUnitView` 로 추출 →
  `SpineUnitPool` 의 딕셔너리를 `Dictionary<Entity, IUnitView>` 로 교체 → 스폰 게이트 3-way 분기.
  새 메서드를 발명할 필요가 없다 (계약이 이미 실전 검증된 상태).
- `QuadUnitView`(정지 쿼드 폴백)는 `PlayAttack`/`Kill`/`ResolveCastAnchor` 가 없다.
  인터페이스에 넣으려면 no-op 를 채워야 하는데, 그건 폴백을 백엔드로 승격시키는 별개 결정이다.
- **스프라이트로 재현 불가한 Spine 의존 3가지**: 캐스트 앵커 본 추적
  (→ `castAnchorLocalOffset` 정적 폴백으로 다운그레이드 가능), 파츠 스킨 조합
  (`SpineCombinedSkinCache` — 원리적으로 불가), 슬롯별 틴트 (불가).
- **페이싱 부호 규약이 정반대다.** Spine 은 `Skeleton.ScaleX = +1` 이 **왼쪽**을 본다
  (`SkeletonFlipXModifier` 가 데이터 레벨에서 정규화). 스프라이트는 `flipX` 기준이라
  두 백엔드를 오가며 디버깅할 때 가장 헷갈릴 지점이다.
- **스크린 렉트는 프레임마다 흔들린다.** `SpriteRenderer.bounds` 를 매 프레임 쓰면
  드림캐쳐 픽킹/리티클이 떨린다. 기준 프레임 바운즈를 1회 캐시하는 게 맞다.
- `ISpineUnitVisualData.SpineVisualScale`/`SpineVisualOffset` 은 값 자체가 백엔드 중립인데
  이름만 `Spine*` 이다. 전면 rename 은 `DefenderUnitData`·`AttackUnitData`·validator 까지 번진다.
