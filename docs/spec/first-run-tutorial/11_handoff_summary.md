# 11 — Handoff Summary (units 8~10)

units 0~7 의 인계는 [7_handoff_summary.md](7_handoff_summary.md). 이 문서는 그 위에
얹은 **B3 재설계 · 레인 축 · 로비 배웅** 만 다룬다.

## Implemented

- **1웨이브를 뭉친 10마리 아래 레인 하나로.** `AuthoredSpawnGroup.laneIndex` 를 새로 열고
  (`-1` = 무지정 = 기존 라운드로빈) `FromPlanAsset` 이 런타임으로 넘긴다.
- **B3 가 3블록이 됐다** — 말파이트 배치/배치스킬 → **퇴근** → 샷건맨 배치/배치스킬.
  `RunPickAndPlace` 가 유닛·문구를 인자로 받아 두 번 돌고 사이에 `RunRetire` 가 들어간다.
- **기본 편성 교체** — 이쑤시개 → 샷건맨, 말파이트 1번 · 샷건맨 2번.
- **샷건맨 배치 스킬 damage 40 → 100**(`Pattern_Shotgunner_Blast`). 관통은 건드리지 않았다
  — 이미 `pierceCount: 99` 였고, 다중 피격을 막던 것은 관통이 아니라 데미지였다.
- **판을 마치고 로비로 오면 한 번 더 배웅**한다(`firstRunLobbyOutroDone`).
- 코드 리뷰 반영: 앱 잠김 2건 · 무한 대기 2건 수정.

## Key Files

- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` — `RunB3` / `RunRetire`
- `Assets/_Project/Scripts/Data/WavePlanAsset.cs` + `WavePatternGenerator.FromPlanAsset`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs` — `ActionRect`
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/LobbyTutorialStep.cs` — 인트로/아웃트로
- `Assets/_Project/Data/DefenderCatalog.asset` · `Data/Projectiles/Pattern_Shotgunner_Blast.asset`
- `.claude/skills/enemy-wave-integration/SKILL.md` — 「저작 플랜도 레인을 지정할 수 있다」

## Verified

- `dotnet build Wassup.Runtime.csproj` 오류 0 (7초 — 스킵된 빌드가 아니다)
- **사용자 Play 확인 2026-08-20** — 전 구간 통과
- 씬 배선: `primaryUnit`·`secondaryUnit`·`inspectPanel` 3건 BattleScene 에 기입

## Notes — 되돌리면 안 되는 것

1. **배치 대기 술어의 «아직 지불 가능한가».** 배치 스텝은 차단막만 내리고 정지는 유지하는데
   코스트는 Battle 도메인이라 회복되지 않는다. `OnPlaced` 의 유닛 확인과 겹치면 잔여
   코스트가 대상 밑으로 떨어지는 순간 **영영 만족 불가**가 되고, 매치 타이머도 멈춰 있어
   정리 경로조차 안 온다. 유닛 확인이 없던 시절엔 이 상태가 없었다 — **가드를 조이며
   생긴 구멍**이다.
2. **`ActionRect` 는 «누를 수 있는가» 까지 본다**(`_visible`·`interactable`). `Hide()` 는
   알파가 0.02 밑으로 갈 때까지 루트를 켜 둔 채 `blocksRaycasts` 만 끊는다 —
   `activeInHierarchy` 만 보면 못 누르는 rect 에 구멍을 뚫고 영원히 기다린다.
3. **퇴근 대기는 `_retired || ActionRect == null`.** grace 구간에 트레이 셀을 다시 탭하면
   그건 «닫기» 다.
4. **접근 대기는 B3a 만 소유한다.** 두 번째 블록에서 다시 돌면 조건이 이미 참이라 문구가
   0프레임 뜬다.
5. **퇴근 비행 관람은 정지를 푼다.** `DefenderRetireFlight` 는 Battle 도메인 델타로 도는
   1.6초 연출이라, 정지한 채 기다리면 유닛이 공중에 멈춘 채 뒤 스텝들을 통과한다.
6. **정지 해제 예산 ≈14.7초 < 2웨이브 첫 스폰 16.0초.** 앞 8.5초는 적 이동이 결정하므로
   앞 문구를 줄여도 총합이 안 준다 — 창을 새로 열면 **어디선가 같은 크기를 반납**한다.
7. **저작 플랜의 그룹에는 `laneIndex` 를 명시한다.** 키가 없을 때의 기본값에 기대면 전
   플랜이 레인 0 으로 고정되는 조용한 회귀가 난다.
8. **완료 기록은 세 블록 + B4 전부 완주.** 스킵한 판은 기록하지 않는다(계약 11) —
   대신 스킵할 때마다 콘솔에 이유를 남긴다.

## Follow-up

- **말파이트 `onPlaceMagnitude` 0 → 40** 이 병행 세션에서 올라왔다(desc 도 "스턴 3초 **+ 피해
  40**"). 확정이면 `PrimarySkillText`("주변 악몽을 3초간 기절시킵니다!")도 피해를 언급하도록
  고친다 — 문구가 유닛 에셋의 값을 주장하고 있다.
- **기존 계정 배웅 1회 노출** — `firstRunLobbyOutroDone` 에 마이그레이션이 없어, 이미
  온보딩을 끝낸 계정도 다음 로비에서 배웅을 한 번 본다. 락은 아니다. 제대로 겨누려면
  `schemaVersion` bump 가 필요한데(단순 로드 시 마이그레이션은 **정상 신규 계정의 배웅까지
  억제**한다) 값이 안 맞아 보류했다.
- **1웨이브 `durationSec` 15 → 20** — 예산 여유가 1.3초뿐이라 접근 대기가 실측에서 길어지면
  남은 카드. 지금은 채택하지 않았다(사용자 결정).
- **10마리 = 600HP vs 산탄 5×100** — 부채 가장자리는 남을 수 있다. 거슬리면 수량이 아니라
  각도 폭(±40°)이나 발 수를 본다.
