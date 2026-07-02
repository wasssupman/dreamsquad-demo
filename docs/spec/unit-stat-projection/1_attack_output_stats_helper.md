# 1. AttackOutputStats Helper

## 목적

"outputs에서 특정 kind의 유일 항목을 찾아 읽기/쓰기"를 단일 구현으로 제공한다. unit 2(UI 표기)와 unit 3(임포터 투영)이 같은 불변식을 공유하는 지점 — 도입 시점에 소비자 2곳이 확정돼 있어 선제 추상화가 아니다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackOutputStats.cs` (신규, static class, runtime asm — 런타임 UI가 사용하므로)
- `Assets/_Project/Tests/EditMode/AttackOutputStatsTests.cs` (신규)

## 구현

- `bool TryGetUniqueMagnitude(AttackOutput[] outputs, AttackOutputKind kind, out float magnitude)` — 해당 kind 항목이 정확히 1개일 때만 true. null/빈 배열/0개/2개+ 는 false.
- `bool TrySetUniqueMagnitude(AttackOutput[] outputs, AttackOutputKind kind, float value)` — 동일 조건에서만 그 항목의 magnitude 갱신 (struct 배열이므로 인덱스 직접 쓰기).
- 이 유닛에서는 아무도 호출하지 않음 → 동작 무변화. 인터페이스/상속 없음.

## 완료 기준

- [x] compile 오류 없음 (2026-07-02)
- [x] 단위 테스트 8종 통과: null / 빈 배열 / 1개 read / 1개 write / 2개+ 거부(read·write) / 0개 write 거부 / kind 필터
- [x] 기존 스위트 회귀 없음 (430개, 기지 실패 ObstaclePlacer 1건 제외 전부 통과)
