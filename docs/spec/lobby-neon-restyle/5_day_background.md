# 5 — 낮 배경 도입 (디졸브 시간대 전환 활성화)

## 목적

unit 2 는 낮 버전 아트가 없어 day/night 슬롯에 같은 밤 텍스처를 꽂아 디졸브를 사실상
no-op 으로 두었다. 낮 버전을 받아 실제 시간대 전환을 살린다.

## 변경 대상

- New: `Assets/_Project/Art/lobby_bg_neon_day.png` (1672×941, Sprite/Single)
- `Assets/_Project/Scenes/OutgameScene.unity` — `LobbyBackgroundDissolve.daySprite`,
  `LobbyBackgroundUnder` 의 Image sprite
- `Assets/Resources/SceneTransition.prefab` — 커버의 `daySprite`

## 구현

- **낮/밤 짝 검증**: 제공된 낮 아트가 현재 밤 아트와 같은 구도인지 먼저 대조했다
  (엣지 구조 상관 0.669 — 되돌린 2차 시안 후보와는 0.277 이라 짝이 명확히 갈렸다).
  구도가 다르면 디졸브가 "시간대 전환"이 아니라 "장면 교체"로 읽힌다.
- **뎁스맵은 1장 공유 유지**. 낮 아트로 따로 베이크해 기존(밤 기준) 맵과 대조: 상관 0.9997,
  평균차 0.59%. 항구 페어의 0.998/1.2% 선례와 동급이라 공유가 정당하다
  (`LobbyBackgroundParallax` 는 슬롯이 하나뿐이라 공유가 전제이기도 하다).
- 앞/뒤 레이어 sprite 는 런타임에 `LobbyBackgroundDissolve.Awake` 가 day/night 슬롯에서
  다시 채운다. 씬에 넣는 값은 에디트 모드 프리뷰용이지만 혼동을 막기 위해 맞춰둔다.
- `SceneTransition.prefab` 커버도 같이 — 커버는 "현재 로비 배경과 같은 그림" 전제로 불투명
  스냅을 숨긴다(unit 2 리뷰 major 와 같은 함정).

## 완료 기준

- Play 진입 시 밤 배경(`startNight: 1`), 콘솔 에러/워닝 0.
- 낮 스프라이트가 같은 파이프라인으로 정상 렌더 — `startNight` 를 임시로 꺼서 확인
  (`Assets/Screenshots/neon_lobby_day.png`), 확인 후 `1` 로 원복.
- 씬 diff 는 낮 슬롯 2곳만.

> 2026-07-31 완료. 위 3항목 확인. 낮 아트 임포트 시 spriteMode 가 또 Multiple 로 떨어져
> `.meta` 직접 수정으로 Single 고정(MCP `manage_asset` 의 알려진 함정).
> **미실시**: 캐릭터 터치로 실제 디졸브 전환 재생 확인 — 입력이 필요해 사용자 확인 대기.
