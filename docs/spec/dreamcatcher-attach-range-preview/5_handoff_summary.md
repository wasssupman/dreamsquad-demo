# 5 — Handoff Summary (2026-09-03 · 구현 완료 · 검증 마감 대기)

> 다음 세션의 일: **골든 조건 결정 → 재베이크 1회 → unit 4 육안 → README 「완료」 선언.** 계약은 README, 단위 계약은
> 번호 문서가 정본이다. 이 문서는 지도다.

## Commit

`cc56fb7d`(골든 eol=lf) → `9cd583b0`(0a sim) → `6ba61bec`(0b 표기) → `f74f814a`(1 라우팅·카탈로그) → `80c00663`(2 채널)
→ `240e2b04`(3 락온 연동) → `14c9d47e`(PlayMode 테스트) → `cd7a232e`(docs) → `abff12fd`(투트랙 리뷰 반영). 미푸시.

## Implemented

- 스킬 광역 사각 → 원 `N + 0.5 + 대상 몸`. `RangeMetric { AreaCircle = 0, Euclidean = 1, [Obsolete(error)] Chebyshev = 2 }`.
  사각 술어 본체 `SkillMath.BodyOverlapsSquare` 는 보존(사용자 결정 「기능은 남기고 비활성화」).
- 반폭 매핑은 `SkillMath.TryShapeHalfWidth` 하나 — 어댑터(`EcsSkillContext.Collect`)와 페이크(`TestSkillContext`)가 공유.
- `TilemapMapView.SetAreaRange(center, radius[, style])` 링 전용 경로. `squareShape` 삭제((2N+5)² 페인트 결함 동반 제거).
  액티브 조준·텔레그래프 = 조준 셀 중심 원 `N + 0.5`. 회오리·운석 VFX 반경은 브리지 소비처에서 `+0.5·tileSize`.
- `Core/Dreamcatcher/DcSkillRouting`(브리지 라우팅 본문 이동, bake 와 공유) + `DcRangeCatalog`(concrete → 도형·반경,
  fail-closed, 겸직 `tileRange` 는 kind 로 차단).
- `RangeDisplayOwner.AttachPreview` + `BattleBridge.SetAttachPreview/ClearAttachPreview`. 획득은 owner 가 None/AttachPreview
  일 때만(Placement·SkillTelegraph 양보), 생존 = `_defenderByTile` 등재, LateUpdate 추종(sim `LocalTransform` → 타일 좌표).
- `DreamcatcherCardDragSlot`: 유효 락온 전환 순간에만 arm/clear, `EndInteraction` 하드 클리어. 색은
  `DreamcatcherFocusConfig.attachRangeStyle`(SO), 조준은 `TileSetData.aimRingStyle`(SO).

## Key Files

`Skills/ISkillContext.cs`(enum) · `Skills/SkillMath.cs`(`TryShapeHalfWidth`) · `Battle/Skills/EcsSkillContext.cs:~440`
· `Core/TilemapMapView.cs`(`SetAreaRange`·`ApplyRingTint`) · `Bridge/BattleBridge.cs`(`SetAttachPreview` ~8160,
`PinCenteredRange`, LateUpdate ~3475) · `Core/Dreamcatcher/DcSkillRouting.cs`·`DcRangeCatalog.cs`
· `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs`(`BeginFocus`·`UpdateUnitHover`·`EndInteraction`)
· 테스트 `Tests/EditMode/AreaCircleMembershipTests`(손실표 핀 포함)·`DcRangeCatalogTests`·`DcSkillRoutingTests`,
`Tests/EditModeAssets/DcCardRangeInvariantTests`, `Tests/PlayMode/AttachRangePreviewTest`.

## Verified

- EditMode 코어+에셋 **2696건**, 실패 2건 = 선행(`boomerang`·`bomb_man` 문안, 시트 소관).
- PlayMode `AttachRangePreviewTest` 2건 통과(host 몸 중심·반경·Clear·비공간 무접촉·Placement 양보). 격리 A/B 로
  0a 귀속 실패는 `OnPlaceBindNearbyTest` 2건뿐이었고 픽스처를 원 계약으로 고쳐 통과.
- 골든 A/B(같은 세션·같은 조건): 0a 전/후 **8건 바이트 동일**. ⚠ N=1 카드만 증언 — N≥2 축소는 손실표 핀 테스트로.
- 투트랙 리뷰(code-reviewer + ecs-reviewer): CRITICAL 0, HIGH 3·MEDIUM 5 반영(`abff12fd`). 전문은 스크래치
  `review-track-a-code.md`·`review-track-b-ecs.md`(세션 한정).

## Notes (되돌리면 안 되는 것)

- **골든은 커밋하지 않았다 — 조건 드리프트.** 현 세션 베이크의 `configHash` 가 HEAD(`e1c11669`)와 다르다(코드 무관,
  A/B 동일이 증거). 시트 임포트는 로비 로그인(`LoginAutoImport → AllRuntimeRefresher`)에서만 돌아 **BattleScene
  직접 Play 는 디스크 SO 값**을 쓴다. `MapTest` 에는 브리지가 없고 dev 맵 오버라이드 키도 없었다.
- `ProjectileHitEvent.radiusWorld` 는 **트레이스 기록값** — sim 에서 바꾸지 말 것(뷰에서 +0.5). 골든이 움직인다.
- `RangeMetric` 값 0 = 원. 인자 누락·`default` 가 은퇴한 사각으로 조용히 가지 않게 하는 핀(`RangeMetric_DefaultValue_IsAreaCircle`).
- 채움 펄스는 노브만 있고 **기본 off** — 사거리 채움 펄스는 사용자 요청으로 제거된 이력.
- `RangeRingStyle.default` 는 보이지 않는 링 — 호출부는 항상 SO 값을 넘긴다(`focusCfg == null` 이면 그리지 않는다).
- PlayMode 선행 실패 15건(`AbilityAreaShieldTest`·`AbilityBombManBarrelTest`·`ActiveAllyZoneTest`·`BossThresholdSelfAoeTest`·
  `DefenderRetireTest.Retire_WithOnRetireCard…`·`DreamcatcherKillThresholdTest`×2·`OnPlaceBoostNearbyTest`·
  `OnPlaceDotNearbyTest`·`OnPlaceMeleeBurstTest`×2·`OnPlaceSkyStrikeTest`×2·`OnPlaceTauntNearbyTest.TauntedEnemy…`·
  `PatrolDefenderPlayTest`)은 0a 이전 코드에서도 동일 실패 — 이 spec 무관(부모 spec 의 2×2 몸 반경 이후 미측정 추정).
- 도구: MCP 브리지가 안 붙으면 EditorPref `MCPForUnity.AutoStartOnLoad`(이 세션에서 켰다). `.omc/ralph/test_request.json`
  파일 프로토콜은 포커스·MCP 없이 EditMode/PlayMode(그룹 정규식) 실행이 가능하다.

## Follow-up (닫기 전 필수)

1. **골든 정본 조건 결정** — A) BattleScene 직접 Play(디스크 SO, 재현 가능) / B) 로그인 후 전투 진입(시트 값, 실제 조건).
   결정 후 Play 중 `Wassup/Battle/Sim Harness/Regenerate Golden Corpus` 1회 → `Verify` 2회 일치 → **골든만 담은 커밋**
   (`docs/spec/battle-sim-extraction/golden-corpus.md` 동반). 기대: A 면 스크래치 `golden_post0a/` 와 바이트 동일.
2. **unit 4 육안·실기기** (`4_play_verification.md` A~F): dim 아래 엄지가 host 위에서도 궁지폭발 채움이 1초 안에 읽히는가
   (손가락 사진 1장) · 라임/시안 구분 · 액티브 조준 원 + 발동 결과 일치 · 회오리·운석 VFX 크기 = 조준 링 · 배치 드래그 중
   락온 시 배치 링 유지 · Subway 맵에서 2×2 host 중심(셀 경계 교점). 부족하면 `attachRangeStyle`·`aimRingStyle` 튠, 그래도
   부족하면 후속 F3(dim 완화/면제).
3. 확인 후 README 상태 라인 「완료 YYYY-MM-DD」 + 번호 문서 완료 기준 체크.

## Follow-up (닫은 뒤 · 별도 커밋)

- `docs/spec/README.md` 「PlayMode 사전 실패」 절에 위 15건 등재 · Follow-up Backlog 에 이 spec 후속 후보(F1~F6) 이관.
- 리뷰 LOW: world→tile 변환 인라인 2곳 헬퍼화 · `ResolveCard` 경고를 카드당 1회로 · 전 concrete Id 를 카탈로그가 명시
  분류하는지 잡는 테스트(새 area concrete 가 조용히 None 이 되는 것 감지).
- `SkillIdForMechanic/SkillIdForPayload/SkillIdForCardPayload` 래퍼 3개(호출처 10곳) 를 `DcSkillRouting` 직접 호출로.
