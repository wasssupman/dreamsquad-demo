# placement-enemy-see-through

상태: **완료 2026-07-06** (units 0~6) · Play 시각 sign-off ✅ · two-track 리뷰 APPROVE (units 0~4, M1 반영; 5~6 은 wiring 미리뷰) · 컴파일 클린 · 튜닝: enemyDragDimAlpha=0.3 / fadeSpeed=8

## 목표

방어 유닛을 **드래그 배치하는 동안** 적 유닛을 반투명(see-through)으로 만들어,
적에게 가려져 있던 **뒤/아래 보드 타일이 비쳐 보이게** 한다. 드롭·취소 시 원복한다.

**검증 질문**: 드래그 중 적 유닛(Spine·Quad **둘 다**)이 반투명해져서 그 뒤에 가려져 있던
보드 타일(및 사거리/hover 하이라이트)이 보이는가? 드롭·취소·비활성 등 **모든 종료 경로**에서
불투명으로 원복되는가?

## 연결 문서

- 드래그 라이프사이클(BeginDrag/CleanupSession funnel): `docs/spec/defender-drag-drop-deployment/`
- 드래그 중 전투 슬로우모 선례(같은 트리거 지점): `docs/spec/time-manager/`
- 사거리 프리뷰(반투명해진 적 뒤로 드러날 하이라이트): `docs/spec/placement-attack-range-preview/`
- 그림자 2모드(실그림자 cast vs blob 스프라이트): `docs/spec/tilemap-real-shadows/`

## 작업 단위 목록

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_blob_shadow_alpha.md` | 토대 | `BlobShadow.SetDimAlpha(factor)` — blob 그림자도 함께 페이드 |
| 1 | `1_quad_transparency.md` | view | `QuadUnitView.SetDimmed` — cutout↔transparent 블렌드 전환 + 알파 + 그림자 |
| 2 | `2_spine_transparency.md` | view | `SpineUnitView.SetDimmed` — `skeleton.A` + 그림자 (블렌드 전환 없음) |
| 3 | `3_bridge_dim_state.md` | bridge | `SetEnemiesDimmed` + unscaled 페이드 lerp + 적 sync 루프 합성 |
| 4 | `4_drag_wiring_and_verify.md` | wiring | DragController 배선 + Play 검증(뒤 타일 가시성) |
| 5 | `5_drag_unit_opaque.md` | wiring | 드래그 유닛(프리뷰)은 불투명 유지 + 최상단 소팅(적만 투명) |
| 6 | `6_highlight_above_units.md` | wiring | 드래그 중 배치 하이라이트(range/hover)를 적 위로 임시 상승 |

의존: `0 → {1, 2} → 3 → 4`. (1·2 는 0 완료 후 서로 독립.) `5`·`6` 은 드래그 프리뷰/소팅만 건드려 독립.

## Feature-wide 계약

- **대상은 적만.** `AttackUnitTag` 엔티티. `SyncMonoUnitViews` 의 **적 루프**(`_aliveAttackersQuery`)만
  `SetDimmed` 를 호출한다. 디펜더는 **별도 루프**(`_defenderByTile`)로 sync 되며 dim 하지 않는다.
  → `QuadUnitView`/`SpineUnitView` 가 공용 클래스여도 디펜더는 불투명 유지.
- **적은 Spine·Quad 혼합.** `entry.unitType` 에 Spine 에셋 있으면 `SpineUnitView`("SpineEnemy"),
  없으면 `QuadUnitView` 폴백. **두 뷰 다** 반투명 처리한다. (제3 경로 없음.)
- **Quad(cutout) 은 블렌드 전환 필요.** 적 quad 머티리얼은 `URP/Unlit` + `_ALPHATEST_ON`(cutout,
  AlphaTest 큐). 알파만 낮추면 픽셀이 잘릴 뿐 안 비친다 → dim 동안 **transparent 블렌드**
  (SrcAlpha/OneMinusSrcAlpha, ZWrite off, renderQueue Transparent, `_ALPHATEST_ON` off)로 런타임 전환,
  원복 시 cutout 복원.
- **Spine 은 블렌드 전환 불필요.** 적 Spine 머티리얼은 `_StraightAlphaInput:0`(**PMA**)로 이미
  transparent 큐 → `skeleton.A = alpha` 한 줄로 페이드. `skel.R/G/B`(health tint)와 독립.
- **health tint 와 안 싸운다.** `SyncMonoUnitViews` 가 매 프레임 `SetHealthTint` 로 색을 쓴다.
  Quad = `_BaseColor.a` 에 dim 알파를 접어 넣고, Spine = `skel.A`(RGB 독립)로 유지. `_dying` 존중.
- **그림자도 페이드.** blob 모드 = blob 스프라이트 알파↓(`BlobShadow.SetDimAlpha`), 실그림자 모드 =
  적 renderer `shadowCastingMode` Off. 원복 시 복구. (유닛만 투명하고 바닥에 그림자 얼룩이 남는 것 방지.)
- **상태 소유·페이드는 BattleBridge.** `_enemyDimActive`(bool) + `_enemyDimAlpha`(현재값, 1↔target lerp).
  공개 API `SetEnemiesDimmed(bool active)`. 페이드는 `Update()` 에서 **`Time.unscaledDeltaTime`**
  (드래그 중 전투 슬로우모라 scaled 쓰면 페이드도 느려짐; sway 도 unscaled).
- **트리거는 DragController.** `BeginDrag` → `SetEnemiesDimmed(true)`, `CleanupSession` → `(false)`.
  CleanupSession 은 드롭·거부·OnDisable·OnDestroy 를 모두 경유하는 **단일 funnel** → 모든 종료 원복.
- **매 프레임 재적용으로 자동 커버.** 적 루프가 매 프레임 `SetDimmed` 호출 → 드래그 중 새로 스폰된
  적도 즉시 dim, `Configure` 머티리얼 리빌드(재스폰) 후에도 다음 프레임 재적용. (단 unit 1: `Configure`
  가 `_transparentApplied`·`_dimAlpha` 를 리셋해 cutout 기준으로 되돌려야 재적용이 정확.)
- **튜닝은 serialized(하드코딩 금지).** 목표 알파·페이드 속도는 BattleBridge SerializeField.
- **순수 프레젠테이션.** 새 ECS 컴포넌트/NativeQueue/맥락 0개. 채널 14개 불변. 시뮬/게임플레이 영향 0.
- **정렬/깊이 리스크는 낮음, Play 검증.** transparent 큐(3000) 전환은 불투명·cutout(바닥·프랍·디펜더는
  depth write)에 depth-test 로 앞뒤 정상. 적끼리는 기존 sortingOrder. 겹친 적·프랍 뒤 케이스만 육안 확인.

## 후속 후보 (현 스코프 밖)

- **블로킹 하자드 반투명** — `_blockingHazardVisualMap`(벽/장애물)도 타일을 가리지만 "적 유닛"이 아님.
  필요하면 같은 `SetDimmed` 패턴으로 별도 unit.
- **드래그 프리뷰/사거리 자체 강조** — 적을 물린 뒤 프리뷰를 더 강조(글로우 등)하는 건 별도 취향 작업.
- **투명도 곡선/이징** — 현재 선형 MoveTowards. 감성 튜닝 필요 시 AnimationCurve 화.
- **스크린스페이스 리빌(스텐실/후처리)** — 대안 접근. occluder 타입 무관 통합(Spine/Quad/하자드/프랍을
  한 패스로 — 뷰 클래스별 개별 처리 불필요). URP `ScriptableRendererFeature`(실루엣→스텐실 write +
  stencil-test clip) + `URP/Unlit`·Spine PMA 셰이더 변종 + **Android perf 스파이크** 필요. 실루엣이
  사람 모양·공중 위치라 타일 리빌엔 오히려 불리 — 스타일리시 x-ray 연출/타입 통합이 목적일 때만. 별도 design.
