# 1 — `StackModifier_Fire` 신설 + 씬 배선

## 목적

화염 스택의 **임계 규칙**을 만든다. 현재 프로젝트에 `StackModifier_Fire` 는 **에셋 자체가
없어서**(`StatusFxKind.cs` 주석이 이 부재를 명시) `BattleBridge.GetStackThresholds(Fire)` 가
빈 배열을 돌려준다 — 화염 스택을 아무리 쌓아도 아무 일도 일어나지 않는다.

이 단위가 끝나면 **화염 5스택 = 화상** 이 성립하고, `StatusFxKind.Fire` 오라(프리팹까지 이미
배선됨)가 Stack origin 으로 **처음** 점등된다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/StackModifier_Fire.asset` (신규 — Bleed/Ice 와 같은 폴더)
- `Assets/_Project/Scenes/BattleScene.unity` — BattleBridge `stackModifierAuthoring` 배열
  (현재 3칸: Bleed·Ice·Fatigue → 4칸)

## 구현

`StackModifier_Bleed` 를 형제로 삼아 저작한다.

| 필드 | 값 | 비고 |
|---|---|---|
| `kind` | `1` (`Fire`) | `StackKind { None=0, Fire=1, Ice=2, Bleed=3, Poison=4 }` |
| `maxStack` | `5` | producer 의 `stackMaxStack` 과 **양쪽 명시**(계약 4) |
| `perAppDuration` | `3.0` | producer 의 output `duration` 과 일치. 킨들러 쿨다운 1.2보다 커야 함 |
| `policy` | `0` (`RefreshAll`) | |
| `thresholds[0].atStack` | `5` | |
| `thresholds[0].mode` | `1` (`Consume`) | 소진 후 재축적. ⚠ Edge 다중 임계 금지(계약 1) |
| `thresholds[0].derivedKind` | `0` (`ApplyDot`) | |
| `thresholds[0].magnitude` | `10` | `tickInterval > 0` 이므로 **틱당 피해**(DPS 아님) |
| `thresholds[0].tickInterval` | `0.5` | |
| `thresholds[0].duration` | `2.85` | **= (6−1)×0.5 + 0.35** → 6틱 · 1회분 60 |

`duration 3.0` 을 쓰면 안 된다 — 7번째 틱 자리가 만료와 정확히 겹쳐 6틱(60)과 7틱(70)
사이에서 프레임레이트에 따라 흔들린다(계약 3, Bleed 4.85 와 같은 이유).

씬 배선: `stackModifierAuthoring` 배열은 `BattleBridge` 가 `_stackThresholds` 딕셔너리를
채우는 유일한 소스다(`BattleBridge.cs:5875~5880`). 배열에 안 넣으면 에셋을 만들어도
**조용히 무효**다. 씬은 사용자 WIP 가 얹혀 있을 수 있으므로 이 필드 hunk 만 스테이징한다
(`feedback_parallel_session_commit_hygiene`).

## 완료 기준

- [ ] 인스펙터에서 `StackModifier_Fire` 필드 전량 확인(특히 `tickInterval 0.5` · `duration 2.85`)
- [ ] Play 진입 후 `BattleBridge.GetStackThresholds(StackKind.Fire).Length == 1` 확인
      (`execute_code` 정적 프로브 — 배틀 진입 불요)
- [ ] `DotElementMap.FromStack(StackKind.Fire) == DotElement.Fire` 는 기존 코드가 보장 —
      별도 작업 없음. 오라 점등은 unit 3 Play 검증에서 육안 확인
- [ ] 씬 diff 가 `stackModifierAuthoring` 1줄 추가뿐인지 확인(무관 dirty 혼입 없음)

## 확인

<!-- 확인 일자 + 커밋 해시 -->
