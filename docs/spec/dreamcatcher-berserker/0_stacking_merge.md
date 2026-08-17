# 0 — 스탯 모디파이어 병합에 누적 축

## 목적

같은 출처가 같은 스탯 버프를 다시 걸면 지금은 **덮어쓴다**. 「다시 걸면 더해지되 상한에서
멈춘다」를 병합 규칙 자체에 넣는다. 생산자는 상한만 실어 보내고, 더하는 일은 버퍼를 소유한
Effects 가 한 곳에서 한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/StatModifierApplyEvents.cs`
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs`
- 신규: `Assets/_Project/Tests/EditMode/StackingModifierMergeTests.cs`

## 구현

**① 이벤트에 상한 칸 하나.** `StatModifierApplyEvent` 에 `magnitudeCap` 추가.
append 라 기존 생산자 전부 0 으로 역직렬화 = 기존 동작.

```
public float magnitudeCap; // >0 = 누적(상한까지 더함) · 0 = 덮어쓰기(기존)
```

**② 병합 분기 한 줄.** `ApplyStat` 의 **기존 슬롯 매치 분기**에서 `magnitude = ev.magnitude`
를 상한이 있을 때만 누적하도록 바꾼다.

```csharp
magnitude = ev.magnitudeCap > 0f
    ? math.min(ev.magnitudeCap, slot.magnitude + ev.magnitude)
    : ev.magnitude,
```

**⚠ 순수 함수로 빼지 않는다.** 실질 호출처가 이 분기 **하나**다 — 슬롯을 새로 만드는 두
경로는 현재값이 0 이고 상한은 항상 1회분 이상이라 `min` 이 아무 일도 하지 않는다. 한 줄짜리
자명한 산술을 호출처 하나뿐인데 빼는 것은 제약 10 이 명시한 과잉추상화다(스펙 리뷰 rev 1).
검증은 순수 함수가 아니라 **병합을 통해** 한다.

**⚠ `remaining` 은 손대지 않는다.** 지금의 `max(old, ev.duration)` 그대로다. 상한에 닿아도
지속은 계속 갱신돼야 한다 — 이걸 같이 막으면 최대 중첩에서 버프가 스스로 꺼진다
(README 「왜 스택 시스템을 안 쓰는가」의 바로 그 결함).

**⚠ 병합 키는 그대로다.** `(source, stat, op, stackId)` 4축 무변경. 누적은 *같은 키*의
재적용에만 일어나므로, 다른 출처끼리 섞이는 경로가 생기지 않는다.

**⚠ 지우기가 상한에 걸리면 안 된다.** 이 엔진의 회수는 삭제가 아니라 **항등값 덮어쓰기**다
(`BattleBridge.Dreamcatcher.cs` 의 `RevokeDreamcatcherEffects`, 188~215행). 그 이벤트가
상한을 실으면 `min(상한, 현재+0) = 현재` 가 되어 **버프가 안 지워진다.** 지금 그 경로는
상한을 안 싣지만(확인함) 우연한 안전이라 테스트로 고정한다.

## 완료 기준

- [ ] 컴파일 통과
- [ ] EditMode — 같은 키 3회 적용 시 magnitude 가 1·2·3배로 자란다
- [ ] EditMode — 상한에서 클램프된다 · **상한 도달 후에도 `remaining` 이 갱신된다**
- [ ] EditMode — 상한 0 이벤트는 예전처럼 덮어쓴다
- [ ] EditMode — **누적으로 자란 슬롯에 상한 0 + 항등 magnitude 를 보내면 항등으로 리셋된다**
      (회수 경로 회귀 핀 — 계약 4)
- [ ] 기존 EditMode 전량 통과 (모든 기존 생산자는 상한 0 = 무변화여야 한다)

> 확인 2026-08-17 — EditMode 2504 중 2501 통과 · 0 실패 · 3 스킵(기존 무시 항목). 커밋 `e4afa642`.
