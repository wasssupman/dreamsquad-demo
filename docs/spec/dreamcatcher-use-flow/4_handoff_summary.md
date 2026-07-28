# 4 — Handoff Summary

## Commit

- `b6415456` unit 0 — 슬로모 press~release 재배치 + `slomoOnOpen` A/B 토글
- `05b3737b` unit 1 — 사용 후 손패 유지 + 재딜인 + 자동 닫힘
- `531182cb` unit 2 — 드래그 중 부착 불가 유닛 일괄 붉은 틴트
- `baaef266` unit 3 — 발동 신호 채널(`DcTriggerFiredEvents`, 23번째) + 임팩트
- `773ee691` fix unit 1 — 재딜인 잠금 누수 2건(마감 리뷰 M1·M2)
- 선행: `cb38591e` hand-drag-clearance unit 0 (조준 중 손패 하강 — held 신호 공급자)

## Implemented

- 열림 ≠ 슬로모: 카드를 잡은 press~release 동안만 0.3 (하강·lift 와 단일 `held` 신호 3중 결합)
- `slomoOnOpen` 토글로 구동작 Play 중 즉시 A/B 비교
- 사용 후 손패 유지 — entryId diff 로 잔류 카드 스프링 슬라이드 + 새 카드 1장 재딜인
  (비차단), 사용 가능 0장이면 자동 닫힘(재딜인 생략)
- 드래그 중 불가 유닛 붉은 틴트 스윕(시안 링=가능/붉은 몸=불가 대칭 문법)
- 발동 신호: AttackN 3지점(Combat) → 신규 큐 → 유닛 펀치+플래시+흡수 VFX + 아이콘 행
  펄스·시안 링(×1.6). OnShieldBreak 는 기존 채널 편승. 연발 = 코얼레스+0.25s 스로틀
- 동반 수정: `FlashWhite` 연발 stray-tint 가드(`SpineUnitView`)

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` — held 신호·TickSlomo·OnCardUsed·DealInSlot
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` — ApplyInvalidSweep/ClearInvalidSweep
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerFiredEvents.cs` + `AttackSystem.cs`(발화 3지점)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 채널 3점 세트·DrainDcTriggerFiredEvents·스로틀
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs`·`UnitOverheadUiLayer.cs`·`Data/UnitOverheadUiStyle.cs`

## Verified

- 리그 EditMode 1540/1543 (실패 1 = MobileBuild 사전 실패, clean HEAD 재현 무죄 판정) · CS 0
- 신규 채널이 리플렉션 규약 테스트 2건에 자동 편입 통과
- ecs-review APPROVE (unit 3 — lifecycle 3점 세트 CastEvents 대칭·발화 지점·경계 검증)
- 사용자 Play 확인 2026-07-29 "이상없음" (units 0~3 + clearance 통합)
- 마감 일반 리뷰 REQUEST CHANGES → **M1·M2(재딜인 잠금 누수) 수정 반영**, M3(문서 마감)은
  이 handoff/README 커밋이 해소. LOW 7건은 아래 Follow-up 으로 이관. 계약 대조는 전 항목 통과

## Notes (되돌리면 안 되는 것)

- `held = _focusIndex >= 0 || AnyInteractionActive()` 단일 신호 — OnBeginDrag 가 `_dragging`
  을 `SetFocus(-1)` 보다 먼저 세워 전환 무갭. 순서 뒤집으면 press→drag 1프레임 팝
- 재딜인은 `_redealSeq`(비게이트) — `_dealSeq` 로 옮기면 Transitioning 이 연속 사용을 막음
- OnCardUsed diff 는 **entryId 기준** — 위치 기준이면 시프트 카드가 전부 재딜인 오판
- 발동 임팩트에 카메라 킥·SFX 금지(연타 멀미/소음) · 스로틀은 월드 임팩트만(UI 펄스 제외)
- 진동갑주 등 **Units 맥락 발화는 이 채널 커버리지 밖** — unit 3 §A.5, 확장은 별도 결정

## Follow-up

- Units 계열 발동 신호 확장(OnDamagedN/HealthThreshold/OnKill/OnDeath/PeriodicTimer)
- 전용 벤더 파티클 시그니처(vfxSpawner 원샷, ShieldGranted 선례) — 아트 선택 필요
- 카드 정밀 귀속(instanceId↔entryId recall registry 후속 spec 과 병합)
- 슬로모 진입/이탈 보간(체감상 현재 불필요 판정, 재부상 시)
- README 후속 후보의 퀵슬롯 A/B/C 분석·드래그 스루 보류 건

마감 리뷰 LOW 이관 (실전 노출 낮음·시각 한정으로 후속 판단):

- `_redealSeq` 단일 핸들 — 한 사용에 새 카드 2장+ 이면 orphan(큐<손패 후반부에만 성립)
- 자동 닫힘 분기가 `RefreshUsability` 미호출 — 침강 0.3s 간 face 색이 옛 게이지 기준
- 포탈 2탭 대기 중 손패 하강 공백대(y 54~264) 탭이 dismiss catcher 에 물려 손패가 닫힘
- 포탈 입구를 하강 완료 전(<0.33s) 확정하면 잔여 하강분만큼 카드가 미끄러짐
  (`IsPointerFollowing` 이 `_dragging` 요구 — 2탭 대기는 미포함)
- 스윕 대상이 드래그 중 사망하면 사망 연출 동안 붉은 틴트 잔상(풀이 Dispose 라 누수는 없음)
- Begin 시점 화면 렉트가 없던 유닛은 락온해도 invalid 피드백 없음(카메라 이동 시에만 노출)
- `FlashWhite` 가드가 2중첩까지만 안전(3중첩은 스로틀 0.25s>0.14s 로 현재 도달 불가)
