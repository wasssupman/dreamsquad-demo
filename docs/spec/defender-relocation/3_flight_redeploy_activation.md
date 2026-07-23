# 3 — 비행 연출 · 재전개 · 활성화

## 목적

확정 후: 실제 유닛 뷰를 숨기고 프리뷰가 from→to 로 비행(실시간) → 착지 → 재전개 대기 → 전투 복귀.
"이동한 유닛의 DPS 공백이 눈에 보인다"가 이 unit 의 감각 목표.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (연출 코루틴)
- `Assets/_Project/Scripts/Presentation/` (SpineUnitPool/SpineUnitView — entity 뷰 숨김/재표시 seam 확인,
  없으면 최소 추가)

## 구현

1. **비행 프리뷰**: 기존 키링 프리뷰 빌더(`TryBuildKeyringPreview` — tap-to-place 가 지목한 추출 후보)와
   3차 베지어 비행 궤적(`RunSimulatedDrag` 의 `KeyringSim.CubicBezier` + `OutCubic`)을 재사용.
   출발 = from 셀 발 위치, 도착 = `GridCellToViewCenter(to)` (tap-to-place unit 6 "도착 기준" 계약 동일).
   비행 시간 = 기존 공식(기준 × clamp(화면거리 비율)) 재사용, `DragSwaySettings` 소스 그대로.
2. **실뷰 숨김/재표시**: 확정 프레임에 해당 entity 의 Spine 뷰 숨김 → 착지 프레임에 재표시.
   `SyncMonoUnitViews` 가 매 프레임 sync 하므로 뷰 오브젝트 비활성이 아니라 풀 쪽 suppress seam 으로
   (stale-scene 함정 회피 — 구현 시 SpineUnitPool 구조 확인 후 최소 침습 선택).
3. **비행 중 게임은 실시간**: 슬로모는 unit 2 커밋 시 이미 해제됨. 비행 중 재홀드/트레이 조작은
   이 유닛에 한해 무시(`PendingDeployment` 가 붙어 있어 unit 0 검증이 자동 거부 — 이중 방어).
4. **착지**: `FinishDefenderRelocation(to, entity)` (LocalTransform 갱신) + 실뷰 재표시 + 기존 착지 타일 팝 재사용.
5. **재전개**: 착지 후 `RelocationSettings.redeploySeconds` 대기(코루틴, **Battle 도메인 시계** —
   `TimeManager.DeltaTime(TimeDomain.Battle)` 누적. 슬로모/일시정지에 정직: placement-cooldown 계약 2 와 동일 근거)
   → `ActivateDeployedDefender(to, entity, facing: zero, triggerOnPlace: false)`.
   재전개 표현은 신규 배치 대기(`PendingDeployment`)와 동일 — 전용 연출은 후속 후보.
6. **중단 방어**: 비행/재전개 코루틴은 매치 teardown·유닛 사망(`_defenderByTile` 소실) 시 안전 중단.
   세대 토큰 패턴(`_sessionGen` 캡처) 준용.

## 구현 노트 (구현서와 달라진 점)

- **실뷰 숨김+프리뷰 → 실뷰 직접 비행**: 키링 프리뷰를 새로 짓지 않고, Bridge 에 뷰 위치
  오버라이드 seam(`SetRelocationViewOverride`)을 추가해 `SyncMonoUnitViews` defender 피드가
  비행 좌표를 대신 쓰게 했다(공유 파일 +3줄). 실제 유닛이 그대로 날아 정체성 보존 + 좌표계
  지식이 Bridge 안에 유지 — 구현서의 "최소 침습 선택" 지시의 귀결.
- **비행 시간 공식**: `DragSwaySettings` 스크린 공식은 키링 전용이라 재사용하지 않고,
  `RelocationSettings` 의 sim 거리 기반 노브(base + perUnit×dist, clamp)로 대체. 궤적은
  `KeyringSim.CubicBezier` + OutCubic 재사용(계획대로).
- **중단 방어**: 세대 토큰(`_flightGen`) + 바인딩 무결 검사. 컨트롤러 비활성 시 진행 중 비행을
  **즉시형으로 완결**(pending 고착 방지 — 단순 중단이 아님).

## 완료 기준

- [x] 컴파일 클린
- [x] 전체 플로우(홀드→이동모드→탭/드래그 확정→슬로모 해제→비행→착지→재전개→전투 복귀) —
      PlayMode `RelocationPlacementSessionTest`: 커밋 직후 pending(비행 중), 활성화 후 sim 위치
      착지 이동 확인, 2회 연속 비행도 완결
- [x] 비행~재전개 동안 비타겟·비무장·시너지 제외 — `PendingDeployment` 재사용(unit 0 스모크가
      타겟 제외·시너지 제외를 검증)
- [x] 재전개 시계 = Battle 도메인 (`TimeManager.DeltaTime(Battle)` 누적 — 코드 경로,
      placement-cooldown 계약 2 와 동일 구조)
- [x] 중단 방어: 세대 토큰+바인딩 검사(매치 재시작 = 바인딩 소실 → 안전 중단), OnDisable 즉시형
      완결 — 코드 경로. 콘솔 클린(테스트 실행 기준)
- [ ] **사용자 Play 확인 (UX 수용 게이트)** — 원격 세션이라 보류. 확인 방법: 에디터 Play →
      Battle 중 배치 유닛 1초 홀드 → 다른 타일 탭/드래그 → 유닛이 아치를 그리며 날아가 착지 후
      잠시 뒤 전투 복귀하는지, 비행 감각(속도/아치)과 재전개 길이 체감

2026-07-24 자동 검증 통과 (PlayMode relocation 스위트 4/4, 에디터 실행). 사용자 시각 확인만 남음.
