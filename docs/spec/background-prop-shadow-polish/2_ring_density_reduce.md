# 2 — 원경 프랍 밀도 하향

## 목적

원경(외곽 링) 프랍이 빽빽하다(침엽수림 의도, ~230개). 밀도를 낮춰 더 성기고 시야가 트인 배경으로 만든다.

## 변경 대상

- `Assets/_Project/Map/Theme/forest/forest.asset`:
  - `ringPropDensity` (현재 0.55)
  - (선택) `ringPropFalloffPerCell` (현재 0.04) — 바깥일수록 더 빨리 성기게 하려면 상향.

## 구현

- `ringPropDensity` 0.55 → **0.35** 시작값 (≈36% 감소). 시드 결정적이라 동일 시드 비교 가능.
- 가장자리만 더 트고 싶으면 `ringPropFalloffPerCell` 을 0.04 → 0.06 추가 조정(선택).
- `mobilePropBudgetScale`(0.5)은 모바일 전용 — 본 변경은 데스크톱 기준 밀도를 직접 낮춘다.

## 완료 기준

- Play → 외곽 링 원경 프랍이 눈에 띄게 성겨졌으나 보드를 감싸는 자연 경계는 유지. unit 1 블롭과 함께 자연스럽다.
- 사용자 육안 통과. 통과 시 확인 일자 + 커밋 해시 추가.

확인: 2026-06-29 사용자 육안 통과 (ringPropDensity 0.55→0.35, 링 프랍 ~193→~120).
