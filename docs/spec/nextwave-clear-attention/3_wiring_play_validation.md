# 3 — 배선·Play 검증

## 목적

새 상태 판정과 UI 에셋/모션을 BattleScene에 완전 배선하고, 자동/강제/유출/최종 웨이브에서
클리어 강조 수명이 계약대로인지 실제 플레이로 확인한다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Tests/PlayMode/NextWaveClearAttentionSmokeTest.cs` (신규)
- `docs/spec/nextwave-clear-attention/README.md` (완료 상태)
- `docs/spec/nextwave-clear-attention/4_handoff_summary.md` (완료 시 신규)

## 구현

- UnityMCP로 `NextWaveDock`의 Bridge/Sprite/튜닝 참조를 배선하고 씬을 저장한다. 자동화 가능한
  씬 작업을 사용자 수작업으로 남기지 않는다.
- PlayMode smoke는 generated-wave battle에서 첫 웨이브를 호출한 뒤 다음 세 상태를 관찰한다:
  pending spawn이 남은 동안 false → 적이 남은 동안 false → 전부 제거되고 다음 웨이브가 남으면 true.
  `ForceNextWave()` 뒤 false 복귀도 확인한다.
- 계약 테스트는 정상 간격·강제 겹침·유출 제거를 각각 확인한다. 유출로 마지막 적이 사라졌지만
  패배 한도에 도달하지 않은 경우도 “필드 비움”으로 강조하며, 패배가 발생하면 즉시 숨는다.
- 마지막 웨이브 클리어는 버튼 강조가 아니라 기존 victory/tally로 이어져야 한다.
- 16:9와 20:9 Game View 캡처로 좌하단 SafeArea, 중앙 트레이, 우하단 각성 버튼과의 충돌을 본다.

## 완료 기준

- compile / EditMode 신규+wave 회귀 / PlayMode smoke green, Console error 0.
- 첫 웨이브 2초 리드인과 intra-wave spawn 사이에는 false.
- 일반 클리어에서 진입 한방 1회 + 반복 어필, 클릭/자동 호출 시 즉시 종료.
- 강제 호출 2회 이상 겹침은 모든 pending/필드 적이 사라진 뒤에만 강조.
- 골 유출 뒤 전투 계속이면 강조, 패배/타이머 종료/최종 승리면 강조 없음.
- 16:9·20:9에서 hit target과 라벨이 safe area 안에 있고 다른 하단 조작부와 겹치지 않는다.
- 자동 Play smoke와 시각 캡처 확인 뒤 각 작업 문서에 확인 일자·커밋을 기록하고 README 완료
  상태 및 handoff를 작성한다.

검증 2026-07-26 — 신규 smoke 1/1 pass. 전체 PlayMode 49/55 pass, 기존 독립 실패 6건은
`4_handoff_summary.md`에 기록 — commit `78978afe`.
