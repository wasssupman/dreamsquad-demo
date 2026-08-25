# 8 — Handoff Summary (2026-08-19)

## Commit

브랜치 `feature/map-diorama-stage` (main 팁 기반, behind 0). 주요 커밋:
`7c7bf005`(설계+spec) → `27d383b5`(critic 반영) → `c05d1993`(unit 0) → `d8b82ed1`(unit 1+2 코드) → `6e60af32`(unit 2 에셋/씬) → `1caffef2`(unit 3) → `d41b5c11`+`a60bf3c5`(unit 4) → US-004b/5 커밋들 → unit 7(68파일 은퇴) + 아키텍트 정정.

## Implemented

- 디오라마 스테이지 파이프라인 전면 교체: `MapStage`+마커 저작 → `MapStageScanner`/`DioramaMapBuilder`(순수) → `GeneratedMap` 무변경 합성(열림=Walk/차단=Deco, placeMask 직접 조립)
- 브리지 스테이지 경로: 폴백 리니어·시드 커빙 은퇴, 연결성 실패 = 하드 실패, `AlignGridTo` 단일 grid writer, 스테이지 수명 = `TeardownGeneratedMap`
- 바닥 페인팅/절차 프랍 은퇴 · 오버레이 7채널 존치 · `BoardSortOrder` 폭 종속 stride + 대역 4000
- 골 균열/붕괴/앵커 = 마커 뷰 훅(브리지 등록부), 튜토리얼 브리지 앵커 교체
- e2e 스테이지 이관: `MapSlot` 포트, KayKit 스테이지 9종(픽스처·파일럿 포함), 덱/플랜 짝 승계, 이격 배치 정규화
- 구 파이프라인 68파일 은퇴 (후계 매핑 기록)
- unit 9 (`07ebea0c`): `BonusSpawnMarker` → `GeneratedMap.bonusSpawns` 투영, 규칙은 main `BonusSpawnAuthoringRules` 재사용. Duel(6,1)/(6,6)·DuelClassic(10,3)/(10,8) 저작 — main 의 보너스 웨이브가 스테이지에서 살아난다

## Verified

- EditMode 두 lane **2397 그린** · PlayMode 스모크 Passed · PlayMode 전체 148 pass / 18 잔존(분류 기록) · 아키텍트 **APPROVED**(정정 6건 반영)

## Notes (되돌리면 안 되는 의도)

- 계약 11: 공성·본능·적 마음·Env **비가용** — 병합 = StructureMarker 후속까지 공성 기능 부재 (사용자 결정 필요, 아키텍트 지적)
- `grid.transform` writer 는 `AlignGridTo` 하나 — Initialize **앞** 호출 순서 불변
- BattleScene 커밋에 타 spec 발 재직렬화 churn 포함(NextWaveDock orphan 키 정규화 — 무해, 기록됨)
- Ralph 러너 2종(`RalphTestRunner`/`RalphEditorTasks`)은 검증 채널로 잔존 — 삭제 판단은 사용자

## Merge (2026-08-21)

`e6129466` 에서 origin/main(87커밋)을 병합. 충돌 5 + 수선 2(TryGetGoalViewAnchor 재지향 · 카메라 상한 ≤23×12 를 StagePoolBuildabilityTests 로 이식). 검증: 컴파일 0 에러 · EditMode 2396 중 1 실패(malphite 텍스트 폭 — main 상속, 바이트 동일 실증) · PlayMode 168 중 13 실패 **전수 분류: 머지 유발 0** — 기존 US-007 잔존 9 · main 상속 1(DragPlacementReach: ResolveFocusAndTarget 3→2인자인데 main 이 테스트 미수정) · 순서 의존 2(단독 green) · 환경 누수 1(`dev_forceMapIndex` PlayerPrefs 잔존 → 비-pin 테스트가 dev 맵에서 돎, 키 제거 후 green). 사용자 BattleScene 실험(FluidBackdrop off + Hello 배치)과 ProjectSettings 는 stash 보관.

2차 병합 `4d780e25` (main +18: instinct-wreck·spawn-point-visual·카메라 셰이크 등). 충돌 1
(BoardSortOrder — 우리 RowStride·대역 4000 + main 잔해 상수 병기). 검증: dotnet 0 에러 ·
EditMode 2413 중 1 실패(malphite — 동일 main 상속) · 스모크 Passed · 풀 PlayMode 168 중
16 실패 = 기존 US-007 잔존 9 + main 상속 1(DragPlacementReach) + 순서 의존 6(전부 단독
green 실증 — suite 구성이 바뀌면 오염 패턴이 이동) + MovementIntegrity 는 오버라이드 제거로
해소 유지. **머지 유발 0.** 포탈 스폰 프랍·본능 잔해는 structures 휴면이라 스테이지 경로
비활성(StructureMarker 후속에서 활성화).

3차 병합 `277121d8` (main +61: bonus-wave-pull·heart-stress-axis·battle-sim-extraction M0·duel-route-tours(자체 철회) 등). 충돌 7 — 은퇴 파이프라인 위의 main 수정 6건 삭제 유지 + TilemapMapView 의 heart-stress 골 틴트를 `GoalMarker.SetStressTint` 로 이식(렌더러 캐시 승계) · 브리지 `SetGoalStressTint`/`MarkGoalCollapsed` 2곳 마커 재지향. **남는 기능 격차 = `GeneratedMap.bonusSpawns` 스테이지 저작 부재 → unit 9 제안**(스테이지 맵에서 보너스 버튼 미등장, 크래시 없음). dotnet 4어셈블리 0 에러(Ralph csproj 를 글롭 기반으로 전환 — 이후 병합에서 파일 주입 불필요). Unity 검증: 컴파일 0 에러 · EditMode 2506 통과 / 3 실패 — malphite(main 상속) · `LegacyTraceV0Tests.StoredCorpus`(**CRLF 환경**: 골든 코퍼스가 index LF·작업본 CRLF 로 체크아웃되고 파서가 `lines[0]=="LTV0"` 엄격 비교 — main 몫: trim 또는 `.gitattributes eol=lf`) · `DreamcatcherCardAssetTextTests`(boomerang — 테스트·에셋 main 과 바이트 동일 = 상속) · 스모크 Passed. 머지 유발 0.

## US-007 결론 (2026-08-25)

unit 9 트리 풀 PlayMode 178 중 162 통과 / 16 실패. 실패 전수 원인 규명 — **머지·unit 9 유발 0**:
- **+1.2% 오염의 정체 = 장착 드림스톤 반입.** GameManager 테스트 모드 경로(직접 BattleScene 진입, `GameManager.cs` ~L475)가
  `StartSquadMatch` 를 미러해 `profileSO.profile.CommittedSquad()` 의 스톤을 `SetDreamstones` 로 반입한다(dreamstone-loadout
  unit 3 — 제품상 의도). 이 머신의 커밋 스쿼드 = 1.2% 스톤 4개(Stone_016/032/048/064) → 스톤 0 을 가정한 테스트에
  ×1.012 가 붙는다(PlacementAura 1.012 · DreamcatcherEffect 0.87→0.859 · AttachRequirement 1.10→1.112). profile.json 을
  비켜도 재현 — `profileSO` 가 에디터 메모리에 프로필을 들고 있어서다. 예전의 «순서 의존»은 프로필/로그인 상태가 런 중
  언제 채워지느냐의 차이였다. **수선 위치 = 테스트 하네스(main 몫)**: PlayMode 셋업에서 `SetDreamstones(빈)` 또는
  프로필 픽스처 주입.
- Auth E2E = dev 서버 닉네임 중복(서버 상태) · DeckInfoPreset = 그 하류 NRE · DragPlacementReach = main 시그니처 미갱신 상속 ·
  나머지(DragCancelZone·DropDismount·FlyingEnemy·SceneTransition·SlimeSplit·Tween·ExecutionStrike 0.43)는 병합 전 18건에
  있던 기존 항목 — ExecutionStrike 는 프로필 반입(스쿼드 디펜더/스톤) 의심.
- `dev_forceMapIndex` PlayerPrefs 가 다시 생겨 있었다(값 0 = 라이브 Duel 이라 이번엔 무해). 스테퍼 사용 후 OFF 습관 필요.

## Follow-up

- **사용자**: 육안 검증 축 5종(spec 5) · OutgameScene dev 패널 `pool` 수동 배선+저장 · 공성 부재 병합 판단 · push 승인
- **US-007(병합 게이트)**: 순서 의존 10(기제 미확인) · SceneTransition(브랜치 씬 편집 의심) · 환경 의심 4 · FlyingEnemy/SlimeSplit/Tween — main 기준선 대조
- spec 후속 후보: 접근 C · 물 영역 · LOS · 웨이브 재밸런스 · 라이브 맵 재저작
