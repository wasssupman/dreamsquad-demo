# 0 — UI/UX 계약과 critic 반영

## 목적

각성 버튼을 장식성 HUD가 아니라 전투 자원→드림캐쳐 덱 행동의 단일 인터랙션으로 읽히게
한다. 구현 전에 정보 위계와 하단 코너 소유권을 고정하고, critic 리뷰의 실제 결함만 계약에
반영한다.

## 변경 대상

- `docs/spec/awakening-hud-resource-button/README.md`
- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Scripts/UI/{NextWaveDock,PlacementPhaseView}.cs`

## 구현

- 정보 위계: **현재 수치 > 충전 비율 > 드림캐쳐 행동 > 장식**.
- 중앙 숫자는 1920×1080 기준 64~76px, 고대비 흰색+진한 보라 TMP SDF 외곽선을 사용한다.
- 전체 터치 영역은 244×244 reference px, 표시 오브는 220×220px이다. 투명 장식 외곽이
  아니라 원형 본체 전체가 Button hit target이다.
- 0에서도 버튼은 열려 손패를 확인할 수 있다. 충전량은 interactable 여부가 아니라 에너지
  밝기와 링 채움으로 표현한다.
- 값 0에서는 halo를 끄고 프레임을 70~80% 톤으로 낮춘다. Placement에서는 버튼 자체를 숨겨
  `전투 시작`과 시선 경쟁하지 않는다.
- 게이지 획득 시 숫자 punch와 `+N` 플로트를 사용한다. `Pulse()`는 손패 open 인과 피드백에만
  사용한다. 드림캐쳐 사용/손패 토글의
  기존 슬로모·전이 가드는 변경하지 않는다.
- Placement는 우하단 `전투 시작`만 노출한다. Battle은 좌하단 `NextWaveDock`, 우하단 각성으로
  분리하며 중앙 Defender/Hand tray 경계와 겹치지 않아야 한다.
- critic 결과는 BLOCKER/HIGH를 본 작업에서 반영한다. 취향·미래 확장은 README 후속 후보로
  이동한다.

## 완료 기준

- critic 리뷰 1회 결과가 문서 또는 코드에 반영되어 있다.
- 16:9 캡처에서 숫자가 장식보다 먼저 읽히고 버튼 의미가 보인다.
- 페이즈별 코너 액션/중앙 트레이 간 겹침이 없다.
