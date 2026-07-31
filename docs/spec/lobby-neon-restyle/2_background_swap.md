# 2 — 배경 슬롯 스왑 (네온 시티)

## 목적

로비 배경을 항구 아트에서 네온 시티(사용자 제공 밤 버전)로 교체한다.
**코드 무변경** — `LobbyBackgroundDissolve`/`LobbyBackgroundParallax` 의 인스펙터
슬롯만 바꾼다.

## 변경 대상

- `Assets/_Project/Scenes/OutgameScene.unity` — `LobbyBackground`(디졸브 앞) /
  `LobbyBackgroundUnder`(뒤) 의 컴포넌트 참조 교체
- `Assets/Resources/SceneTransition.prefab` — 씬 전환 커버의 day/night 슬롯 + 커버 Image.
  **커버는 "현재 로비 배경과 같은 그림"이라는 전제로 불투명 스냅을 숨긴다**
  (`SceneTransition.cs:189~196` 주석) — 로비 배경을 바꾸면 이 프리팹도 반드시 함께 스왑.

## 구현

1. `LobbyBackgroundDissolve`: `daySprite`·`nightSprite` **둘 다** `lobby_bg_neon_night`
   스프라이트로 교체(낮 버전 도착 전까지 디졸브 no-op — 전환 트리거·파면 연출은 살아있고
   시간대만 안 바뀜). `startNight=true` 유지.
2. 앞/뒤 Image 의 초기 sprite 도 네온 밤으로 교체 (`LobbyBackgroundUnder.Image.sprite` 포함).
3. `LobbyBackgroundParallax.depthMap` → `lobby_bg_neon_depth` 교체.
   `LobbyBackgroundDissolve` 는 파랄락스가 같은 뎁스맵을 밀어주므로 별도 작업 없음.
4. 튜닝 SO(`DepthParallaxSettings`)·앰비언트 값 불변.

## 완료 기준

- Play 진입: 네온 시티 배경 표시, 콘솔 에러 0.
- 로비 캐릭터 터치 리액션 → 디졸브 파면 연출이 에러 없이 재생(시간대 변화는 없음이 정상).
- 캐릭터 키링 스와이프 중 배경 패럴랙스 틸트 동작(스와이프 방향으로 미세 기울임).
- 이 커밋 revert 시 항구 배경(낮/밤 디졸브 포함) 완전 복원.
- 낮 버전 수급 시: `daySprite` 슬롯만 교체하면 전환 부활 (후속, 이 unit 범위 밖).

> 2026-07-31 구현 완료 — 커밋 `f8f0c89f` (참조 5개만: 앞/뒤 Image sprite, day/night 슬롯, depthMap).
> Play 스크린샷으로 네온 배경 렌더 확인(`screenshot-20260731-123955.png`).
> 미실시: 캐릭터 터치 디졸브 파면·키링 스와이프 패럴랙스 실기 확인 — 사용자 확인 대기.
>
> **리뷰 반영 (major)**: `SceneTransition.prefab` 커버 참조 3개가 항구 배경으로 남아
> START 진입 시 커버 스냅에서 항구 배경이 노출되는 회귀 발견 → 네온 밤으로 스왑 (unit 2 rev).
