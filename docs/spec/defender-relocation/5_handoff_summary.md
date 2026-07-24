# 5 — Handoff Summary

## Commit

- `b4002268` docs — spec 작성 (README + units 0~4)
- `4b0cc3a2` unit 0 — Bridge relocate API + 판정 순수함수 + 테스트
- `c59a394d` unit 1 — 홀드 제스처 & 이동모드 컨트롤러 (+씬 배선)
- `9069df45` unit 2 — 이동모드 배치 세션 (탭/드래그 커밋)
- `4ec95a97` unit 3 — 비행·재전개·활성화
- (unit 4) DcInspect 경합 정리 + 테스트 견고화 — 이 문서와 같은 커밋

## Implemented

- Battle 중 배치 유닛 1초 홀드 → 이동모드(슬로모 0.2×·하이라이트·인스펙트 줌) → 탭 or 드래그로
  목적지 지정 → 확정 시 슬로모 해제 → 실뷰가 베지어 아치로 비행(실시간) → 착지 → 재전개(Battle
  시계) → 전투 복귀
- 비용 = 재전개 시간 단독: 코스트 0 · 배치쿨다운/on-place/컷신/`PlacementCommitted` 미발화
- 이탈 구간 = `PendingDeployment` 재사용(비타겟·비무장·시너지 제외), 점유/`DefenderTile` 은 확정
  프레임 원자 스왑, `LocalTransform` 은 착지 프레임
- 취소: 본인 탭/보드 밖 릴리즈/타임아웃/대상 사망/트레이 세션 충돌. 남용 방지 = 진입 쿨다운
- 짧은 탭 = 기존 DcInspect(다운 즉시) 그대로, 이동모드 중 인스펙트 자동 양보

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — relocate API·seam 전부(본체 접촉 +5줄)
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 홀드/이동모드/배치/비행 상태 머신
- `Assets/_Project/Scripts/Data/RelocationSettings.cs` + `Data/Config/RelocationSettings.asset` — 노브 전부
- `Assets/_Project/Tests/PlayMode/Relocation*.cs` (3) + `Tests/EditMode/RelocationCheckTests.cs`
- 씬: `DefenderRelocationController` GO(참조 5) + `DcInspect.relocationController`

## Verified

- EditMode 7/7 + PlayMode relocation 스위트 4/4 (에디터 실행, 커밋마다 재실행)
- 컨트롤러 상태 머신은 입력-독립 `Step` 을 테스트가 reflection 구동(원격/unfocused 에디터 제약 우회)
- **사용자 Play 시각 확인(UX 수용 게이트) 미완** — 원격 세션. 절차: Battle 중 유닛 1초 홀드 →
  다른 타일 탭/드래그 → 비행·재전개 체감 확인 후 `3_...md` 완료 기준 체크

## Notes (되돌리면 안 되는 의도)

- 활성화에 on-place 스킵 플래그를 만들지 않았다 — `_onPlaceTriggeredEntities` 가드가 이미
  exactly-once. 플래그를 추가하면 이중 방어가 아니라 의미 중복.
- 비행 중 뷰는 `SetRelocationViewOverride` 가 유일 경로(sim 은 착지까지 옛 위치). 오버라이드를
  안 지우고 코루틴이 죽으면 뷰가 허공에 얼므로, 모든 중단 경로가 Clear 또는 즉시형 완결을 지난다.
- 슬로모는 확정 시 해제(착지 아님) — 비행이 실시간인 것이 재전개 비용의 시각화라는 설계 의도.
- 시너지 계약 검증은 총합 `damageMul` 이 아니라 origin=Synergy 슬롯 직독(랜덤 기믹 오염 방지).
- 라이브 씬은 `enableAdjacencySynergy=0` — 시너지 재계산 호출은 유지되나 현재 no-op.

## Review (2026-07-24, 투트랙)

code-reviewer + ecs-reviewer 병렬. **양측 동일 HIGH 1건으로 수렴** → 수정 완료.

- **HIGH(수정됨)**: 단일 `_flightGen`/`_activeFlightEntity` 가 동시 비행 2개 표현 불가 → 연속 재배치
  시 앞 유닛 영구 pending 고아화. 픽스 = `TryBeginHold` 에 `if (_activeFlightEntity != Entity.Null) return;`
  단일 세션 가드 + 회귀 테스트(`SecondRelocationBlockedWhileFirstInFlight_FirstStillActivates`).
- **LOW(수정됨)**: `_relocationViewOverride` 를 `BeginPlacement` 리셋에 co-locate.
- **MEDIUM(후속)**: 탭↔홀드 전이 겹침 → README 후속 후보.
- ECS 경계 5개 제약 전부 PASS, Entity recycle/뷰 오버라이드 고아 clean, `dotnet build` 0 에러.
- 사용자 4개 질문(재사용/분기/피격X/슬로모X): 정상 경로 전부 IMPLEMENTED 확인.

## Follow-up

- README "후속 후보" 참조 (재조준 · Placement 페이즈 재배치 · 풀 상태화면 · 어그로 chase 재계산 ·
  이동 가능 타일 프리하이라이트 · 재전개 연출 고도화 · effect-tile×relocate 정책)
- 사용자 Play 확인 후: 노브 튜닝(`RelocationSettings` — 홀드 1s/쿨다운 3s/타임아웃 8s/재전개 1.5s/
  비행 0.35s+0.04/u) 및 홀드 진행 표시(현 틴트 페이드) 체감 점검
