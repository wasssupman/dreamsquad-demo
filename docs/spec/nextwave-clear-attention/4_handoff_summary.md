# 4 — Handoff Summary

## Commit

- `8431b891` — `feat(nextwave): add inter-wave clear readiness contract`
- `663ad01c` — `feat(nextwave): overhaul clear-ready CTA attention`
- `78978afe` — `test(nextwave): wire clear-attention play validation`
- `e45c8ebf` — `fix(nextwave): restore dock position and content padding`

## Implemented

- 호출된 모든 웨이브의 pending 적과 필드 `AttackUnitTag`를 합쳐 클리어를 판정한다.
- 킬·골 도달·강제 중첩 웨이브가 같은 emptiness 계약으로 수렴한다.
- 첫 호출 전, 리드인/분산 스폰 중, 최종 웨이브 뒤, legacy/종료 상태는 강조하지 않는다.
- `BattleBridge.NextWaveClearReady` 읽기 API만 UI에 노출한다.
- 작은 타이머 캡슐, 큰 청록 CTA, 코드형 이중 화살표, 골드 펄스 링을 배선했다.
- false→true 진입 hop/flash와 unscaled 반복 bounce/nudge/ring 연출을 구현했다.
- 클릭·자동 호출·비활성·전투 종료 시 모션과 ring을 원상복구한다.
- 개편 전 좌하단 `(40,40)`을 복원하고 Sprite 비율·라벨 safe padding을 고정했다.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Scenes/BattleScene.unity`
- `Assets/_Project/Tests/EditMode/NextWaveClearReadyTests.cs`
- `Assets/_Project/Tests/PlayMode/NextWaveClearAttentionSmokeTest.cs`
- `Assets/_Project/Art/UI/{NextWaveDockFrame,NextWaveButtonFace,NextWavePulseRing}.png`

## Verified

- Unity `6000.4.3f1`, Entities/Collections/Entities Graphics `6.4.0`.
- C# compile error 0, 테스트 종료 뒤 Console을 비우고 `CS` error 0 재확인.
- EditMode 전체: 1,360 total / 1,358 pass / 0 fail / 2 skip.
- unit 5 레이아웃 assertion 포함 targeted PlayMode smoke 재수행: 1/1 pass.
- 전체 PlayMode: 55 total / 49 pass / 6 fail. 신규 smoke는 전체 순서에서도 pass.
- 기존 실패 6건: Auth 중복 사용자, Dreamcatcher assertion, carry-in/scene 전역 상태.
- 교정 후 1920×1080 normal/clear 캡처에서 원점·비율·패딩 확인.
- ECS 리뷰: 신규 Component/Queue/System/구조 변경 없음, Bridge 경계 위반 없음.

## Notes

- 클리어는 개별 wave id가 아니라 “이미 호출된 합집합이 비었는가”가 계약이다.
- `CheckVictory()`와 강조가 `NoQueuedAttackersRemain()`을 공유하므로 따로 카운트하지 않는다.
- 사망 ECB 반영 순서 때문에 강조가 최대 한 프레임 늦을 수 있으나 먼저 켜지지는 않는다.
- 전체 PlayMode 실패 6건은 본 기능과 독립된 기존 전역 상태/외부 Auth 문제다.
- Dock 원점 `(40,40)`은 유지 계약이다. 트레이 충돌 회피를 이유로 위치를 옮기지 않는다.

## Follow-up

- **필수 종료 게이트**: Android 실기기에서 alpha halo, cutout SafeArea, 터치 hit target을 확인한다.
- Next Wave 튜토리얼·자동 진행·클리어 보상은 이 spec 범위 밖이다.
