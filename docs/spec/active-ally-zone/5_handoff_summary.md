# 5 — 인계 요약 (units 0~4 + 리뷰 2회 반영)

## Commit

- `2b8b3efd` — `feat(active-ally-zone): units 0~4 — 아군 버프 장판화 + 선택 중 액티브 허용`
- 선행: `e5cdb48a` (`active-dreamcatcher-tile-aim`, Play 육안 검증 대기)

## Implemented

- **아군 버프 = 시간제 장판.** `AllyBuffField`(Effects 캐리어, 셀 중심·stat·magnitude·remaining,
  `StackId = 3`) + `AllyBuffFieldSystem`(BattleSimGroup, `UpdateBefore(ModifierApplySystem)`, 매 프레임
  재발행) + `EffectTickSystem` 수명 루프. 선례는 `ZoneApplySystem`.
- **모든 적용은 `EffectSpawner.AllyBuffApplySec`(0.5초) 지속.** `skill.durationSec` 는 캐리어 수명 전용.
- **겹침은 stat 별 최댓값으로 접는다** — 승자를 chunk 순회 순서에 맡기지 않는다.
- bridge 는 스폰 + 로그 스냅샷만(`SpawnAllyBuffZone`). 구 `ApplyAllyBuff`/`CountDefendersInRange`/
  `SkillData.TargetsAllies`/아군 카운트 예고 전부 삭제 → 조준 상태줄이 액티브 6종 동일.
- **아군 0기여도 시전 성공** — 적 장판과 규칙 통일(구 0기 거절 폐기).
- **장판 바닥 점등**: `TilemapMapView` 전용 zone 타일맵 + **칸별 refcount**, 색은
  `allyZoneColor`(SerializeField). 등록부/refcount 는 `ClearAllyBuffZonePaint()` 한 곳에서 함께 반납.
- **선택 중 액티브 허용**: 차단 제거 → `SelectionReleasedForAim` 전용 이벤트(드래그 확정 시점) →
  `DcInspectController.ReleaseSelectionKeepHand()` — 선택·패널·리티클·**슬로모 lease** 해제, 손패 유지.
  줌은 피드 중단으로 자동 복귀(신규 카메라 API 없음).
- 캐리어를 `DestroyBattleEntities` 에 등록(매치 경계 누수 차단).

## Key Files

- `Assets/_Project/Scripts/Battle/Effects/AllyBuffField.cs` · `AllyBuffFieldSystem.cs`
- `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`(스폰 + `AllyBuffApplySec`) · `EffectTickSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnAllyBuffZone` / `PaintAllyBuffZone` /
  `DrainAllyBuffZoneVisuals` / `ClearAllyBuffZonePaint` / `DestroyBattleEntities`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — zone 타일맵 + refcount
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — `ReleaseSelectionKeepHand`
- `Assets/_Project/Tests/PlayMode/ActiveAllyZoneTest.cs` — 10 케이스

## Verified

- Unity 컴파일 에러 0.
- EditMode **1657건 중 실패 1** — `PresetCommitSemanticsTests.Delete_NonCommittedPreset_...`.
  이 spec 과 접점 없음(다른 세션이 편집 중인 프리셋 확정 로직).
- PlayMode `ActiveAllyZoneTest` **10/10**. `ActiveTileCastTest`·`PlacementAuraTest` 통과.
  `DragCancelZoneTest` 실패는 이 변경 **전에도 같은 메시지로 실패**했다(배치 트레이 소관).
- 리뷰 3회 반영: spec critic(REVISE — C1·C2·H1~H5·M1~M8) + code-reviewer(REQUEST CHANGES — H1·M1~M5·L1~L7)
  + ecs-reviewer(REQUEST CHANGES — H1·M1~M3·L1~L6).
- **Play 육안 검증 미완**(사용자 대기): `4_validation.md` Play e2e 1~6.

## Notes (되돌리면 안 되는 의도)

- **`AllyBuffApplySec` 는 `Maximum Allowed Timestep`(0.3333)보다 커야 한다.** 작으면 히칭 프레임
  한 번에 `StatModifierTickSystem` 이 갱신값을 넘어 깎아 슬롯이 사라지고 그 프레임만 base 스탯이 된다.
  정지·슬로모는 오히려 안전 — 위험 구간은 **정상 속도**다.
- **모디파이어 duration 은 `AllyBuffFieldSystem.Enqueue` 한 곳에서만 정해진다.** 호출부가
  `skill.durationSec` 를 넣으면 refresh 가 `max(old,new)` 라서 스냅샷 동작으로 조용히 회귀한다.
- **점등 등록부와 refcount 는 항상 같은 함수에서 반납한다**(`ClearAllyBuffZonePaint`). 떼어 놓으면
  stale 엔트리가 새 매치의 refcount 를 깎아 살아 있는 장판의 발자국이 꺼진다.
- `ReleaseSelectionKeepHand` 는 `CloseFromSelection()` 을 부르지 않는다 — 그 안의
  `CancelAllCardInteraction()` 이 방금 시작한 드래그를 취소한다. 반대로 `_slomoLease.Dispose()` 는
  필수다(누락 시 손패 닫힘 후 Battle 이 0.3× 고착).
- 선택 해제 트리거는 **드래그 시작**이다. press 로 옮기면 선택 중 탭 즉발 부착이 죽는다.
- 유닛별 하이라이트는 의도적으로 **넣지 않았다** — `SpineUnitView.SetHoverHighlight` 가 단일 슬롯
  래치라 조준 틴트와 공존이 불가능하다. 정식 경로는 StatusFx 채널(프리팹 필요) → 후속.

## Follow-up

- Play 육안 검증 후 각 unit "완료 기준" 에 확인 일자 + 해시 기재.
- **감속장이 아직 스냅샷**이다 — 원칙("안에 있는 대상이 영향을 받는다")의 마지막 예외. README 후속 후보.
- 장판 위 아군 하이라이트(StatusFx) · press 시점 범위 프리뷰 · 장판 겹침 시각 규칙.
- `TornadoField`/`PortalLink` 도 `DestroyBattleEntities` 누락 — 적 전용이라 실질 무해하지만 같은 구멍.
- 선행 spec 의 PlayMode 케이스 중 아군 카운트 예고 관련 그물은 **대체가 아니라 소멸**했다(예고 자체가
  삭제됐으므로 의도된 것). 사고가 아니라는 기록.
