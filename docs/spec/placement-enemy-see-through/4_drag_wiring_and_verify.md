# 4 — DragController 배선 + Play 검증

**작업 구분**: wiring + 검증

## 목적

드래그 시작/종료에 dim 을 토글하고, 뒤 타일 가시성·모든 종료 원복을 Play 로 실증한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`

## 구현

- `BeginDrag(...)`: 세션 빌드 성공 직후(현재 `CleanupSession()` → `BuildSession` 이후)
  `bridge?.SetEnemiesDimmed(true);`
- `CleanupSession()`: 앞부분(슬로우모 lease 해제 옆)에서 `bridge?.SetEnemiesDimmed(false);`
  → 드롭 성공·거부·OnDisable·OnDestroy 모든 경로 원복(단일 funnel).
- 별도 상태 저장 없음(멱등). BeginDrag 가 내부에서 CleanupSession 을 먼저 부르므로
  off→on 순서가 되지만 최종 on 이라 무해.

## 완료 기준 (Play / MCP)

에디터 포커스 상태에서(비포커스면 프레임 정지):

- 드래그 시작 → 적(Spine·Quad 혼재 웨이브)이 반투명해지고 **가려졌던 보드 타일이 보인다**.
  스크린샷으로 육안 확인.
- 사거리/hover 하이라이트가 반투명해진 적 뒤로 드러난다.
- 그림자(blob 또는 실그림자)도 옅어져 바닥 얼룩이 남지 않는다.
- **드롭 성공**·**invalid 거부**·**드래그 취소** 각각에서 적이 즉시 불투명 원복.
- 드래그 중 새로 스폰된 적도 반투명으로 등장.
- 디펜더/배경/프랍은 불투명 유지.
- 겹친 적·프랍 뒤 케이스에서 정렬 붕괴 없음(낮은 리스크, 육안 확인).

## 검증 산출물

- 드래그 중/후 게임뷰 스크린샷 2장(before opaque / during see-through) 첨부로 회귀 기준 고정.
