# 4 — 오버스캔 + 씬 배선 + Play 검증/튜닝

## 목적

UV 시프트가 가장자리를 드러내지 않게 오버스캔하고, 씬을 배선해 실제 체감을 확정한다.

## 변경 대상

- Modify(scene): `Assets/_Project/Scenes/OutgameScene.unity` — 배경 앞/뒤 Image 오버스캔 +
  `LobbyBackgroundParallax` 컴포넌트 추가/할당
- New(asset): `Assets/_Project/Data/LobbyParallaxSettings.asset` (`DepthParallaxSettings` 인스턴스)

## 구현

- **오버스캔 (하드)**: 배경 **앞·뒤 Image 둘 다** 동일하게 ~1.05배 확대(RectTransform scale 또는
  앵커 마진 음수). 여유가 진폭보다 커야 한다: peak UV 시프트 = `amplitude × 0.5`(중심 피벗) 이므로
  amplitude 0.02 → 1% → 오버스캔 5% 면 충분. **앞/뒤 오버스캔이 다르면 전환 중 어긋난다.**
- **Settings 에셋**: `_Persp`=0, `_HiStrength`=0, `amplitude` 0.015 시작, `depthCenter` 0.5,
  스프링은 컷신과 동일 계열(`tiltDamping` 임계감쇠 유지 — 낮으면 배경이 출렁인다).
- **씬 배선**: `LobbyBackgroundParallax` 에 dissolve(앞)/underImage(뒤)/depthMap/settings 할당.
  ⚠️ **씬 저장 = 미저장 WIP 베이크 함정**([[feedback_scene_save_bakes_wip]]) — 저장 전 씬 diff 로
  무엇이 함께 박히는지 확인. 필요하면 스냅샷 격리 절차.
- **튜닝**: amplitude / ambientAmplitude / dragGain / 스프링. 배경은 컷신보다 **더 보수적**으로
  (전체화면이라 과하면 멀미난다). 참고 권장: UV 시프트 0.005~0.02.

## 완료 기준

- **가장자리**: 최대 틸트에서도 화면 4변에 캔버스/빈 공간·늘어진 픽셀이 안 보임(오버스캔 충분).
- **난간/가로등**: 최대 틸트에서 늘어짐·찢어짐 없음 (이 spec 의 검증 질문).
- **디졸브 무회귀**: 낮/밤 전환이 기존과 동일(패럴랙스 on/off 양쪽에서).
- **배틀 컷신 무회귀**: `.cginc` 리팩터 후에도 컷신 패럴랙스 정상(unit 0 에서 이미 검증, 최종 재확인).
- 사용자 Play 체감 승인 + Android 실기기 프레임 확인.
- 통과 시: README 상태 완료 + `5_handoff_summary.md` 작성 → **main 머지**.
  실패 시: 브랜치 폐기(`git checkout main`) — 이 spec 의 격리 목적.
