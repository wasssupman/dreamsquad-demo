# 4 — 인계 요약 (units 0~3 + 리뷰 rev)

## Commit

- `feat(active-dreamcatcher-tile-aim): units 0~3 — 액티브 조준 통일 + 대상축 폐기` (해시는 커밋 후 기재)

## Implemented

- **대상축 폐기**: `SkillTargetType` enum + `SkillData.target` 필드 삭제. 아군 대상 판별은
  `SkillData.TargetsAllies`(파생), 포탈은 `SkillData.IsPortal`(파생) — 직렬화 무변경.
- **캐스트 창구 통합**: `CastSkillOnDefender` 삭제 → `CastSkillAtTile` 의 effect switch 가
  `PowerSurge`/`RapidFire` 를 `ApplyAllyBuff` 로 처리. 컨트롤러도 `CommitActiveDefender` 은퇴.
- **아군 광역**: `CollectAlliesInRange`(체비셰프 + `PendingDeployment` 제외)를 적용과 조준 예고
  (`CountDefendersInRange`)가 공유. 버퍼는 분리(`_allyApplyScratch` / `_allyCountScratch`).
- **아군 0기 = 무차감 거절**, 적 장판은 0기여도 성공(의도된 비대칭).
- **스킬 아군 버프 전용 슬롯** `SkillAllyBuffStackId = 3` — 배치 오라와 **합산**(사용자 결정).
- **조준 통일**: `AimMode` = None/Defender/**TileAim**/EnemyMark. 카드는 전 모드 손패 고정 +
  화살표. `IsPointerFollowing` 및 손패 하강 예외 삭제. 끝점은 타일 중심
  (`bridge.TryGetTileScreenCenter`, sim→view 변환은 bridge 소유).
- **보드 밖 엄격 판정**: `GridMath.WorldToCellUnclamped` + `bridge.TryScreenToCellStrict`.
- **포탈 2단계**: 릴리즈=입구 → 화살표 기점이 입구 타일로 이동 → `[입구,출구]` 동시 점등 →
  두 번째 탭 커밋. 입구==출구는 UI·`CastPortal` 양쪽에서 거절.
- 에셋: PowerSurge/RapidFire `range 1`, 6개 스킬에서 `target:` 키 제거, Active 카드 2장 문안 갱신,
  시트 익스포터의 `_target` 정보 열 제거.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CastSkillAtTile`(effect switch),
  `ApplyAllyBuff` / `CollectAlliesInRange` / `CountDefendersInRange`, `TryScreenToCellStrict`,
  `TryGetTileScreenCenter`, `SetSkillAimCells`, `SkillAllyBuffStackId`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — 조준 상태 머신 전체
- `Assets/_Project/Scripts/Battle/Movement/GridMath.cs` — `WorldToCellUnclamped`
- `Assets/_Project/Tests/PlayMode/ActiveTileCastTest.cs` — 4 케이스

## Verified

- dotnet build: Runtime / Tests.EditMode / Tests.PlayMode **오류 0**
- EditMode **1617건 실패 0** (신규 GridMath 2건 포함, 스킵 2 = 기존 Ignore)
- PlayMode `ActiveTileCastTest` **4/4** — 반경 내 전부 버프 / 반경 밖 불변 / 아군 0기 무차감 거절 /
  적 장판 0기 성공 / 오라 위 합산(증분 +1.0) / 퇴화 포탈 거절
- 투트랙 리뷰 반영 완료(code-reviewer H1·H2·M1~M4·L1·L3·L4 / ecs-reviewer H1·M3~M6·L7·L9).
  ECS 트랙 경계 판정은 통과 — 맥락 간 쓰기는 `StatModifierApplyEvents` 큐만, 조준 경로 0 할당.
- **Play 육안 검증 미완**(사용자 대기): 6종 조준·포탈 2단계·손패 하강 회귀·선택 중 Active 차단.

## Notes (되돌리면 안 되는 의도)

- `TryScreenToCell`(관대) / `TryScreenToCellStrict`(엄격) **분리 유지**. 부착·적 표식은 관대한
  판정이 맞고(손가락이 몸체를 대충 가리켜도 잡혀야 한다), 타일 캐스트만 보드 밖을 거절한다.
- `range 0` 은 `SetSkillAimRange` 로 칠 수 없다(`tileRange<=0` 조기 return + owner 만 전환).
  단일 셀 점등 경로를 유지할 것.
- 아군 카운트는 **매 프레임** 재계산(점등만 셀 게이트) — 조준 중 아군 사망/배치 완료로 예고가
  거짓이 되는 것을 막는다.
- `_view.Focus?.Confirm(...)` 의 `?.` 는 필수(pulse override 경로는 Focus 없이도 성립).
- PlayMode 테스트는 `damageMul` 절대값을 쓰지 말 것 — 시너지·오라가 같은 stat 을 쓴다. 속사
  (`attackSpeedMul`) 또는 **증분**으로 재라.

## Follow-up

- Play 육안 검증 후 각 unit 문서 "완료 기준" 에 확인 일자 + 해시 기재.
- 후속 후보(README): 범위 내 아군 초록 하이라이트 · 손가락 오클루전 오프셋 · 적/아군 프리뷰 색
  구분 · Active 전용 아트 · `PendingDeployment` 제외 테스트 커버.
- PlayMode 전체 스위트에 **기존 실패 13건**이 있다(인증 서버 duplicate key · Gift 페이즈 흐름 ·
  씬 전환 · 폴백 덱 제거로 stale 해진 `DreamcatcherDeckCarryInTest` · 배치 트레이
  `DragCancelZoneTest` · 스탯 베이스라인 드리프트 등). 모두 이 spec 이 손대지 않은 영역이지만
  **베이스라인 대조 실행은 하지 않았다** — 확정이 필요하면 이 커밋 이전 상태에서 같은 13건을 돌려
  비교할 것.
