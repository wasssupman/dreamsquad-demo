# 2 — 배경 슬롯 스왑 (네온 시티)

## 목적

로비 배경을 항구 아트에서 네온 시티(사용자 제공 밤 버전)로 교체한다.
**코드 무변경** — `LobbyBackgroundDissolve`/`LobbyBackgroundParallax` 의 인스펙터
슬롯만 바꾼다.

## 변경 대상

- `Assets/_Project/Scenes/OutgameScene.unity` — `LobbyBackground`(디졸브 앞) /
  `LobbyBackgroundUnder`(뒤) 의 컴포넌트 참조 교체

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
