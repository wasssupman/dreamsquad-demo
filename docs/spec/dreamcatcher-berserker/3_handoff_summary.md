# 3 — 인계 요약 (dreamcatcher-berserker)

## Commit

| 해시 | 내용 |
|---|---|
| `a4818537` | ⚠ **동반 커밋** — 병행 세션의 넉백 작업에 이 spec 의 **공격 arm** 이 함께 담겼다 |
| `e4afa642` | units 0~1 — 누적 병합 축 · 상한 환산 · bake · 경계/처치 arm |
| `db8b2bc8` | unit 2 — 광란 카드 · 문안 · 카탈로그 |

> ⚠ **`a4818537` 단독으로는 컴파일되지 않는다.** 그 커밋의 `AttackSystem` 이
> `magnitudeCap`·`StackCap` 을 부르는데 둘 다 `e4afa642` 에 있다. 되돌리지 않은 이유는
> 그 세션이 그 위에서 작업 중일 수 있고 병행 세션에서 남의 커밋을 고치는 게 더 위험해서다.
> `git log -- <이 spec 의 파일>` 로 이력을 좇으면 첫 커밋 제목이 남의 작업으로 나온다.
> **교훈: 병행 세션이 돌 때는 엔진 축을 먼저 커밋하고 arm 을 나중에 얹을 것.**

## Implemented

- **광란** — 공격할 때마다 공속 +8%가 쌓여 최대 10중첩(+80%). 마지막 공격에서 4초가
  지나면 중첩이 하나씩 빠지는 게 아니라 **통째로** 사라진다.
- **자기 버프 누적 축** — 버프를 합칠 때 상한이 있으면 덮어쓰기가 아니라 누적한다.
  이 엔진의 자기 버프는 여태 전부 덮어쓰기라 다시 걸어도 값이 안 자랐다.
- **「공격 N회 × 자기 버프」 개통** — 여태 붙어도 안 터지던 조합이다(부착 판정도 통과하고
  슬롯도 구워지는데 공격 지점에 갈래가 없어 경고만 남기고 카운트를 태웠다).
- **처치·경계 arm 도 상한을 싣는다** → 짱빠른·짱쎈버서커는 시트의 최대 중첩 칸만 적으면
  **코드 0줄로** 누적이 된다.

## Key Files

- `Battle/Effects/Modifiers/ModifierApplySystem.cs` — 누적의 **유일한** 지점(병합)
- `Battle/Effects/Modifiers/StatModifierApplyEvents.cs` — `magnitudeCap`(0 = 기존 동작)
- `Battle/Effects/Modifiers/ModifierAuthoring.cs` — `StackCap`(배율 −1 규약 위의 상한 환산)
- `Battle/Combat/AttackSystem.cs` — 공격 arm(**`a4818537` 에 있다**)
- `Battle/Combat/HealthThresholdSystem.cs` · `Battle/Units/DamageApplicationSystem.cs` — 경계·처치 arm
- `Bridge/BattleBridge.Dreamcatcher.cs` — `SelfStatBuff` bake(최대 중첩 + 거절)
- `UI/Dreamcatcher/DreamcatcherCardText.cs` — 중첩 문안
- 에셋: `Card_Frenzy`

## Verified

- EditMode **2504 중 2501 통과 · 0 실패 · 3 스킵**(스킵은 전부 기존 문서화된 무시 항목).
- 신규: 병합 누적 7건(누적·클램프·상한에서의 지속 갱신·전량 소멸·상한 0 덮어쓰기·회수 리셋) ·
  상한 환산 3건 · bake 3건 · 공격 arm 2건.
- 사용자 Play 확인 완료(2026-08-17).
- 푸시됨 — `9f9e9286..db8b2bc8` (GitHub `main`). **GitLab 미러는 안 했다.**

## Notes — 되돌리면 안 되는 것

1. **상한은 `magnitude` 만 막고 `remaining` 은 막지 않는다.** 최대 중첩에 도달해도 매
   발동이 지속을 갱신해야 한다. 같이 막으면 **가장 뜨거운 지점에서 버프가 스스로 꺼진다** —
   이게 스택 시스템(`StackModifierSlot`)을 못 쓴 이유와 정확히 같은 함정이다.
   그쪽은 임계 규칙이 «올라가는 길에만» 발화해서 최대 중첩에서 파생 버프가 만료된다.
2. **버프를 지우는 이벤트에 상한을 실으면 안 된다.** 이 엔진의 회수는 슬롯 삭제가 아니라
   **항등값 덮어쓰기**다(`RevokeDreamcatcherEffects`). 상한이 실리면
   `min(상한, 기존+항등) = 기존` 이 되어 **카드를 떼도 버프가 안 지워진다.** 지금 그 경로는
   상한을 안 싣지만 우연한 안전이라 회귀 핀이 걸려 있다.
3. **누적은 병합 한 곳에서만 한다.** arm 이 현재값을 읽어 더하는 형태로 바꾸지 말 것 —
   Effects 소유 버퍼를 Combat/Units 이 들여다보게 되고, 읽은 시점과 병합 시점이 벌어진다.
4. **상한 0 = 기존 덮어쓰기.** 기존 생산자(오라·시너지·존·스택 파생)는 전부 이 필드를 안
   실어 무변화다. 기본값을 바꾸면 게임 전체의 버프 규칙이 조용히 바뀐다.
5. **`origin` 은 공격 arm 에서 `Dreamcatcher` 다.** 경계 arm 의 `HealthThreshold` 를
   복사하면 상태FX 가 「빈사에서 켜졌다」로 읽는다.
6. **최대 중첩의 저작 자리는 `tileRange` 다.** `ApplyStackToTarget` 이 같은 칸을 같은 뜻으로
   쓰는 선례가 있고, **시트 DTO 에도 이미 있어** 저작 경로가 공짜로 열렸다.
7. **회귀 핀을 시트가 소유하는 카드 에셋에 걸지 않는다.** 꺼진 카드에 걸면 정리되는 날
   같이 죽고, 켜진 카드에 걸면 시트가 값을 바꿀 때마다 빨개진다. 코드에서 카드를 조립할 것.
8. **테스트 엔티티에 `ModifierStats` 를 빠뜨리지 말 것.** 지속 tick 시스템이 그 컴포넌트를
   가진 엔티티만 훑어서, 없으면 시간이 안 흐르고 「식는다」 계열 단언이 통째로 무의미해진다
   (이 spec 의 첫 테스트 실패가 그것이었다).

## Follow-up

- **GitLab 미러** — `git push gitlab main:refs/heads/master` 미실행.
- **시트에 `frenzy` 행** — 아직 없다(2026-08-17 읽기 전용 확인). 자동 push 로 생기면
  `visible` 이 1 인지 확인할 것 — 빈 칸은 해제가 아니라 「그대로 둠」이다.
- **짱빠른·짱쎈버서커 누적화** — 시트 최대 중첩 칸만 적으면 된다. 밸런스 결정.
- **피의 대가** — 버서커 컨셉 2안(미착수). 선결 3가지는 README 후속 후보 참조.
- **중첩 수 표시** — 지금 값은 슬롯의 float 이라 「몇 중첩인가」라는 정수가 어디에도 없다.
