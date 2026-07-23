# 2 — 이동모드 배치 세션 (탭 / 드래그)

## 목적

이동모드에서 목적지를 두 방식으로 지정한다: **타일 탭**(tap-to-place) 또는 **프레스 드래그**.
유효 판정·피드백은 기존 파이프라인을 재사용하고, 커밋만 unit 0 의 relocate API 로 분기한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (unit 1 에 이어서)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (최소 접촉 — 공유 파일,
  재사용 seam 노출만: 키링 프리뷰 빌드/hover 는 가능하면 public seam 호출로)

## 구현

1. **제스처 연속성** (README 설계): 이동모드 진입 시 손가락 상태로 분기 —
   - **홀드 유지한 채 이동 임계 초과** → 즉시 드래그 추적 시작(프레스 드래그).
   - **릴리즈** → relocate-armed 유지, 이후 보드 탭 대기(탭투플레이스).
2. **목적지 판정**: `bridge.TryScreenToCell` 단일 소스. 유효 = unit 0 의 `RelocationCheck` 와 동일 규칙
   (사전 검증은 `CanRelocate...` 계열 read-only 로). 무효 셀 = `bridge.FlashPlacementReject(cell)` 재사용 +
   이동모드 유지. **본인 타일(from) 탭 = 취소**(README 계약 11).
3. **드래그 프리뷰**: 기존 hover 하이라이트·타일 팝 재사용. 프리뷰 비주얼은 unit 3 의 비행 프리뷰와 공용
   (이 unit 에서는 하이라이트만으로도 완료 가능 — 프리뷰 승격은 unit 3).
4. **커밋 시퀀스** (확정 프레임): `TryBeginDefenderRelocation(from, to, ...)` 성공 시 →
   슬로모 해제 + 하이라이트/카메라 복귀(unit 1 의 종료 루틴 공유) → unit 3 연출 핸드오프
   (unit 3 전이라면 임시로 Finish+Activate 즉시 호출 = 즉시형).
   실패 시 reject 피드백 + 이동모드 유지.
5. **스킵 셋 확인** (README 계약 1·4·8): 이 경로는 `TryBeginDefenderDeployment`·코스트·컷신·
   `PlacementCommitted`(배치 쿨다운)·on-place 를 지나지 않는다. 방향 유닛도 `_aimController.Begin` 을
   타지 않는다(facing 보존, 계약 3).
6. **세션 배타**: relocate 세션 중 트레이 드래그/탭 arm 이 시작되면 relocate 를 취소(단일 세션 원칙 —
   기존 `_sessionGen` 하이재킹 방지 계약과 동일 결).

## 구현 노트 (구현서와 달라진 점)

- **탭/드래그 단일 모델**: 이동모드 중 목적지 지정은 "press 추적 → 릴리즈 지점 해석" 하나로 통합 —
  탭과 드래그가 같은 경로(드래그는 스카우트 hover 만 추가로 따라옴). 홀드 승계 press 는 임계 전
  릴리즈 = 탭 대기 전환(커밋 아님), 임계 초과 = 드래그 승격(릴리즈 커밋).
- **보드 밖 릴리즈 = 취소** (탈출 제스처). 무효 셀 = reject + 유지, 본인 = 취소(계약 11).
- **커밋 꼬리는 임시 즉시형**(`Finish`+`Activate` 즉시) — unit 3 이 비행 코루틴으로 대체.
- 드래그 프리뷰(키링 승격)는 계획대로 unit 3 로 — 이 unit 은 hover 스카우트까지.

## 완료 기준

- [x] 컴파일 클린
- [x] 홀드→릴리즈→타일 탭 이동 성립(즉시형), 홀드→그대로 끌기(임계 초과)→릴리즈 로도 성립 —
      PlayMode `RelocationPlacementSessionTest` (Step reflection 구동)
- [x] 무효 셀(점유) reject + 이동모드 유지, 본인 타일 탭 = 취소(슬로모 해제 확인) — 테스트 검증.
      보드 밖 릴리즈 = 취소 (코드 경로)
- [x] 이동 후: 코스트 불변(테스트 검증) · 배치 쿨다운/on-place/컷신/`PlacementCommitted` 는
      relocate API 가 호출 자체를 안 함(unit 0 계약·코드 경로) · 방향 유지(unit 0 스모크가 커버)
- [x] relocate 중 트레이 조작 시 안전 취소 — unit 1 의 sessionConflict 가드(코드 경로,
      시각 확인은 unit 3 사용자 Play 게이트)

2026-07-24 자동 검증 통과 (PlayMode 4/4 — 신규 1 + 기존 relocation 3 회귀 없음, 에디터 실행).
