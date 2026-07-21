# 2 — 체력바 실드 오버레이 세그먼트 (프레젠테이션)

## 목적

실드 잔량을 머리 위 체력바에서 읽히게 한다 — HP fill 위 하늘색 오버레이, 바 폭 불변.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncMonoUnitViews` 의 defender Health 폴링에 `ShieldSlot` 버퍼 동승(read-only, 슬롯 합산), 실드 비율 전달
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs` — 실드 세그먼트 Image 1개 추가 + `Show(...)` 실드 비율 파라미터

## 구현

- 실드 세그먼트는 **HP fill 끝에 이어붙임**(계약 8): HP 60% + 실드 20% → 바의 60~80% 구간이 하늘색.
- 합 > 100% 는 **동적 정규화**: 분모 `D = max(maxHP, HP+실드합)`, `hpShown = HP/D`, `shieldShown = 실드합/D`. 풀피+실드도 실드가 항상 보인다(클램프식은 풀피 실드를 숨겨 폐기 — 실드는 풀피에서도 유효한 게 힐과의 차별점). shield 0 이면 세그먼트 비활성 + 분모 maxHP 로 복원(기존 렌더 무변경).
- 색은 기존 스타일 번들(`UnitOverheadUiStyle`) 에 실드 색 필드 추가(하드코딩 금지) — 기본 하늘색/백색 계열.
- 이벤트/큐 신설 없음 — 기존 매 프레임 폴링 경로에 필드 하나 동승(계약 8).
- 적 유닛은 ShieldPool 미부착 → lookup 가드로 자연 스킵.

## 완료 기준

- [ ] compile 클린 + 기존 EditMode 그린.
- [ ] 시각 확인은 unit 3 Play 검증에서 통합 수행(실드 보유 시 세그먼트 표시 / 소진 시 소멸 / 재부여 시 복원).
