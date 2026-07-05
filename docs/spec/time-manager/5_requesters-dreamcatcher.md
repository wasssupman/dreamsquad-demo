# 5 — 요청자 배선 + Dreamcatcher 마이그레이션

## 목적

TimeManager 의 실제 소비자를 연결한다: (1) 정지 UI, (2) D&D 슬로우모, (3) 기존 `DreamcatcherController` 의 `Time.timeScale=0` 정지를 lease 로 이관(critic MAJOR — 두 정지 권한 충돌/회귀 방지).

## 변경 대상

- 정지 UI: 정지 버튼/패널 컨트롤러 (구현 시 위치 확인)
- 드래그: 배치 D&D 시작/종료 지점 (drag preview 시스템)
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherController.cs`

## 구현

1. **정지 UI**:
   - 열기: `_pauseLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);`
   - 닫기: `_pauseLease.Dispose();`
   - 정지 패널 자체 애니메이션은 unscaled(트윈 SetUpdate(true)/WaitForSecondsRealtime) — Battle 도메인 아님.

2. **D&D 슬로우모**:
   - 드래그 시작: `_dragLease = TimeManager.Instance.Request(TimeDomain.Battle, 0.2f);` (priority 기본 0)
   - 드롭/취소: `_dragLease.Dispose();`
   - 드래그 유닛/프리뷰/코스트/카메라는 Interaction=1 → 손대지 않음.

3. **DreamcatcherController 마이그레이션** (critic MAJOR):
   - `:85–87` `Time.timeScale = 0f;` → `_dreamLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);`
   - `:98` `Time.timeScale = 1f;` → `_dreamLease.Dispose();`
   - **두 `Time.timeScale` write 삭제**. 프로젝트 전역 `Time.timeScale` writer 0 확인.

## 완료 기준

- [ ] 컴파일 통과.
- [ ] Play: 정지 버튼 → 전투 완전 정지 + 정지 UI 조작 가능. 닫기 → 재개.
- [ ] Play: 배치 드래그 중 전투 0.2x, 드래그 유닛/배치 정상속도. 드롭 → 1x 복귀.
- [ ] Play: 드래그 중 정지 열기 → 0(pri100 승), 닫기 → 0.2 복귀, 드롭 → 1.
- [ ] Play: Dreamcatcher 선택 화면 → 전투 정지(기존 동작 유지), 선택 후 재개.
- [ ] grep: 코드베이스 `Time.timeScale` write 0건.

## 주의

- 우선순위 규약: 정지·Dreamcatcher = 100, 드래그 = 0. 동시 활성 시 정지가 이김.
- lease 필드는 컨트롤러가 보관, 비활성/파괴 시 반드시 Dispose(leak 방지).
