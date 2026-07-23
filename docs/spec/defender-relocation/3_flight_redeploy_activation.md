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

## 완료 기준

- [ ] 컴파일 클린
- [ ] 에디터 Play 전체 플로우: 홀드 → 이동모드(슬로모) → 탭/드래그 확정 → 슬로모 해제 → 비행(실시간)
      → 착지 팝 → 재전개 대기 → 전투 복귀. 시각적으로 tap-to-place 배치 비행과 구분되지 않는 품질
- [ ] 비행~재전개 동안: 공격 안 함 · 적 타겟팅에서 제외(피격 0) · 시너지 배율에서 빠짐 → 복귀 시 원상
- [ ] 재전개 중 슬로모(다른 배치 드래그)를 걸면 재전개 시계도 같이 느려짐
- [ ] 비행 중 매치 재시작/유닛 강제 제거 시 예외·고아 프리뷰·lease 누수 없음, 콘솔 클린
- [ ] 사용자 Play 확인 (이 unit 이 UX 전체의 수용 게이트)
