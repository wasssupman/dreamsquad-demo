# 11 — 인계 요약

## Commit

로컬 브랜치 **`heart-stress-axis`** 의 heart-stress 커밋 **18건** (`cc174aca` … `2e6165be`).
**`main` 에는 없다** — revert 커밋 `473b8c9a` 가 전량 제거했다(사용자 지시: 이 기능은 깃에 올리지 않는다).

되돌리기: `git revert --no-commit $(git log --format=%H --grep '^feat(heart-stress-axis)')`

## Implemented

- **스트레스 = 마음 체력의 표시 반전.** `StressMath.FromHealth` 하나가 그 반전을 소유한다.
  별도 리소스가 아니라 마음 `Health` 가 정본이다.
- **판이 끝나는 통로가 둘이 됐다** — 3분 만료 · **스트레스 100**(`EndMatch("stress_full")`).
  유저 제출은 허용하되 공식 절차로 세지 않는다. 호출부 정확히 **3곳**.
- **누수가 없다.** 첫 마음 파괴에 판이 끝나므로 유출 배수구가 열릴 프레임이 존재하지 않는다.
- **악몽 처치 → 마음 회복.** `awakeningReward × Deck.killHealPerAwakening`(라이브 10).
  잡몹 1킬 = 20 = `Enemy_Basic` 1타 상쇄.
- **본능이 마음의 방패다**(`CoreShielded`) — 살아있는 방어 본능이 하나라도 있으면 마음이
  타겟 후보에서 빠진다. 소비처 **6곳**(그 컴포넌트 주석의 표가 정본).
- **돌격형(Runner·Swift)이 일반 공격을 갖는다** — 가는 길에 싸우고 도발도 걸리지만 마음은
  못 때린다(마스크에 `DefenderCore` 없음 → 도달 시 산화 + `stabilityDamage` 직격).
- **연출 4채널**: 마음 프랍 붉은 틴트 · 심박(단계 계단 + lub-dub) · URP 포스트 비네트 ·
  **머리 위 차오르는 스트레스 바**(파랑→빨강 램프 + 상승 펀치 + 0 이면 미노출).
- **마음이 터지는 한 박자**(unit 10) — 붕괴 VFX·프랍 주저앉음을 배수구에서 떼어내 붕괴
  프레임에 직접 쏘고, **결과 화면만** 늦춘다(집계·서버 제출은 즉시).
- 밸런스: 마음 HP **1500**(본능 1000 × 1.5) · 회복 배율 10 · 골든 코퍼스 7종 재녹화.

## Key Files

- `Scripts/Core/StressMath.cs` — 스트레스 산식(순수)
- `Scripts/Presentation/HeartStressPulse.cs` — 심박·단계·펀치(순수)
- `Scripts/Battle/Units/CoreShielded.cs` — 방패 태그 + **소비처 6곳 표**
- `Scripts/Bridge/BattleBridge.cs` — `SyncGoalStability`(붕괴·방패 writer) ·
  `SyncGoalOverheadGauges`(마음 분기) · `EndMatch`/`HoldThenShowResult`/`PlayCoreBurst`
- `Scripts/Data/AttackDeck.cs` — `goalStabilityMax`(시계) · `killHealPerAwakening`(저울)
- `docs/reference/score-formula.md` · `ingame-flow.md` · `map-wave-balancing.md` — 정본 갱신분

## Verified

- 컴파일 0 에러 · **EditMode 2647 실행**
- ⚠ **실패 2건이 남아 있다** — `DreamcatcherCardAssetTextTests`(boomerang) ·
  `UnitKitCatalogTests`(bomb_man). 둘 다 `desc` 문안 구조 단언이고 해당 에셋을 마지막으로
  만진 커밋이 **시트 임포트 `92365780`** 다. heart-stress 와 무관하지만 **숨기지 않는다** —
  그 커밋은 main 에도 올라갔으므로 **main 도 이 2건이 빨갛다.**
- 골든 코퍼스 7종 `Verify` **전건 통과**
- Play 실측(하네스 고정 스텝): 바 `ratio=stress01` · fill 파랑→빨강 · 상승 펀치 1.13→1.06 ·
  스트레스 0 에서 뷰 0개 · 붕괴 프레임 phase=Tally(배율 0.30) → 박자 후 Result(배율 1.00) ·
  `complete` 는 지연 없음 · `StopBattle` 이 리스 반납

## Notes — 되돌리면 안 되는 것

- **`OpenBreachedCellsForLeak` 을 되살리지 말 것.** 안 부르는 것이 「누수 없음」의 실체다.
  연출만 필요하면 `PlayCoreBurst` 를 쓴다(unit 10 이 그 둘을 분리한 이유).
- **`EndMatch` 넷째 호출부를 만들지 말 것.** 그게 곧 패배 조건의 부활이다.
- **결과 리본에 종료 사유를 넣지 말 것** — `"결과"` 고정은 **의도된 결정**(사용자 2026-08-24).
- **마음 바에 전용 `SetUnit` 을 만들지 말 것.** 공용 경로를 타는 것이 unit 9 의 요점이다.
- **`goalStabilityMax` 와 `killHealPerAwakening` 을 같은 패스에서 함께 돌리지 말 것** —
  전자는 시계, 후자는 저울이고 HP 는 교환비를 못 바꾼다(같은 분모가 약분된다).
- **보드 3×3 잠식**과 **머리 위 «체력» 바**는 각각 반려·철회됐다. 되살리지 말 것.

## Follow-up

- **육안 확인 미완** — unit 9(바·색·펀치)와 unit 10(붕괴 박자)은 프로그램 실측만 했다.
- **소리 0줄.** 이 spec 밖으로 미뤘다 — 오디오 에셋 생성이 필요하다(ElevenLabs 파이프라인).
- ⚠ **시트가 `Swift.engageMovement` 를 0(Halt)으로 되돌렸다.** 그러면 Swift 가 첫 방어유닛
  앞에서 멈춰 마음까지 못 간다. **시트에서 1(Advance)로** 고쳐야 한다 — 에셋만 고치면
  다음 로비 임포트에 또 되돌아간다.
- ⚠ **밸런스 기준선이 stale 하다.** unit 5 실측은 배치 판이 3분 완주(최고 59)였는데,
  이후 시트 임포트·보너스 당기기·duel 경로 revert 가 들어와 같은 조건이 **139.5초에
  터졌다**. 「현재 밸런스 유지」 결정은 옛 숫자를 보고 내린 것이다.
- ⚠ **이 브랜치에 heart-stress 아닌 커밋 5건이 섞여 있다**(duel-route-tours revert ·
  sheet-import · bonus-wave-pull 3건). 앞의 둘은 main 으로 옮겼다(`798a7070`·`d3257286`).
  **`4ecf4429`(bonus-wave-pull)에는 unit 9 의 `BattleBridge.cs` 절반이 들어 있다** —
  경로 미명시 스테이징 사고다. main 으로 옮길 때 heart-stress 줄을 빼야 한다.
- **GitLab 미러 미확인** — 사내망이 아니라 접속이 안 됐다. main 의 revert 가 미러에 안 갔다.
- 남은 후속 후보(휴면 `PushGoalCrack` 정리 · 몽마의 계약 코스트 · `map-rework` 문구 정합)는
  README 「후속 후보」 참조.
