# 4 — Handoff Summary

## Commit

- `9acd4e37` — Redesign awakening HUD as resource button
- `20eec6af` — Rework awakening button as casual burst gauge
- `72619a57` — 젤리 face 호흡+간헐 광택 상시 affordance (unit 5)
- 사용자 마감 확인: 2026-07-18 · unit 5 Play 확인: 2026-07-19

## Implemented

- 각성치를 우하단의 큰 숫자 중심 캐주얼 버스트 버튼으로 표시한다.
- 버튼 중앙 액체 충전면과 `현재값 /100`으로 0~100 자원을 함께 전달한다.
- 꿈·별·밤 등 서사 상징 없이 네이비·보라·청록·노랑 젤리 문법을 사용한다.
- 값 획득 시 숫자 punch, `+N`, 젤리 squash를 재생한다.
- 100 최초 도달 burst와 준비 상태의 느린 bounce를 구분한다.
- 버튼 탭은 기존 `Toggled → DreamcatcherHandView` 경로를 유지한다.
- Placement에서는 각성을 숨기고 전투 시작 버튼을 우하단에 유지한다.
- Battle에서는 NextWave 좌하단, 각성 버튼 우하단으로 코너 소유권을 분리한다.
- Battle 무입력 시 숫자는 고정한 채 젤리 face만 3초대 호흡·간헐 광택으로 살아 있다
  (unit 5). 반응 연출 중에는 ambient 가 쉬고 끝나면 자동 재개한다.

## Key Files

- `Assets/_Project/Scripts/UI/Dreamcatcher/AwakeningGaugeView.cs`
- `Assets/_Project/Art/UI/AwakeningBurstFrame.png`
- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Scripts/UI/PlacementPhaseView.cs`
- `docs/spec/awakening-hud-resource-button/README.md`

## Verified

- Unity 6000.4.3f1 Play, 1920×1080 Placement/Battle 레이아웃을 확인했다.
- 각성값 30과 100에서 숫자·액체 충전면·MAX 상태를 확인했다.
- 손패 open/close 구독 경로와 선택 톤을 확인했다.
- Unity 컴파일 및 Console error 0을 확인했다.
- 2026-07-18 사용자 마감 확인을 받았다.

## Notes

- 이 기능은 프레젠테이션 전용이다. 각성 경제·카드 비용·ECS 경로는 변경하지 않는다.
- Sprite 미할당 시 절차적 원형 플레이트 폴백을 유지한다.
- 100 미만에서도 손패를 열 수 있는 기존 제품 동작을 유지한다.
- 구조 변경이 없어 `docs/reference/object-pipeline-map.md` 갱신 대상이 아니다.

## Follow-up

- Android cutout/gesture 영역 확인은 `mobile-ui-safe-area`의 실기 QA에서 수행한다.
- 각성 경제와 카드별 소비량 밸런스는 별도 spec으로 다룬다.
