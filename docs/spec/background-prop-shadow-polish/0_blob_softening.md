# 0 — 블롭 그림자 소프트닝

## 목적

근경 프랍 발밑 블롭이 또렷한 어두운 타원으로 보인다. 더 부드럽고 확산된 접지 그림자로 다듬는다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity` — `BattleBridge` 컴포넌트 serialized 필드:
  - `blobShadowColor` (현재 black α=0.45)
  - `blobShadowFootprint` (현재 (1.35, 0.95))
  - `blobShadowSize` (현재 1.0)

## 구현

- 코드 변경 없음. `BattleBridge` serialized 값만 조정 → `Awake/OnValidate` 가 static 미러(`BlobShadowColor` 등)로 반영.
- 시작값(육안 튜닝 출발점):
  - alpha 0.45 → **0.30** (덜 어둡게, 확산감)
  - footprint (1.35, 0.95) → **(1.55, 1.10)** (살짝 넓혀 penumbra 느낌)
- 캐릭터 블롭(모바일 폴백)도 같은 static 을 공유하므로 동시 적용됨 — 의도된 통일.

## 완료 기준

- Play → 배틀 빌드 → 게임뷰 스크린샷. 근경 프랍 그림자가 또렷한 흑점이 아니라 부드러운 접지 그림자로 보인다.
- 사용자 육안 통과. 통과 시 확인 일자 + 커밋 해시 추가.

확인: 2026-06-29 사용자 육안 통과 · 커밋 ee10b86 (α 0.45→0.30, footprint (1.35,0.95)→(1.55,1.10)). 스크린샷 `Assets/Screenshots/prop_shadow_v1_draft.png`.
