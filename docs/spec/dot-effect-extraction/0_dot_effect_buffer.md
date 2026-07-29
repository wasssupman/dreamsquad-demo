# 0 — `DotEffect` 버퍼 추출

## 목적

지속 피해를 `CcEffect` 에서 떼어내 자기 버퍼·자기 채널·자기 병합 규칙을 갖게 한다. 이 단위가
CRITICAL 과피해를 끝낸다 — 병합이 `(origin, element)` 별로 분리되므로 출혈과 화염 장판이 공존한다.

**원자적이어야 한다.** 쓰는 곳과 읽는 곳이 갈리는 중간 상태는 게임이 깨진다.

## 변경 대상

신규:

- `Assets/_Project/Scripts/Battle/Effects/DotEffect.cs` — `DotOrigin`·`DotElement` enum · `DotEffect` 버퍼 ·
  `DotElementMap.FromStack` 순수 매핑
- `Assets/_Project/Scripts/Battle/Effects/DotEffectMerge.cs` — (origin, element) 별 병합
- `Assets/_Project/Scripts/Battle/Effects/DotApplyEvents.cs` — `DotApplyEvent` ·
  `DotApplyEventsSingleton` (26번째 채널)

이관:

- `CcEffect.cs` — `CcKind.DoT` 는 **저작 토큰으로 남기고** 런타임 생성 경로에서 제거
- `DotApplySystem.cs` — `DotEffect` 버퍼를 읽도록. 슬롯마다 독립 지급
- `CcDecaySystem.cs` — DoT 몫은 `DotEffect` 감쇠로 분리
- `CcApplySystem.cs` — DoT 라우팅 제거
- producer 3곳 — `StackModifierTickSystem.cs:99` · `ZoneApplySystem.cs:62` · `BattleBridge.cs:3894`
- `BattleBridge.cs` — 채널 생성/드레인/해제, `_stackAuraLatch` 블록의 DoT 판정은 **unit 1** 에서
- `Assets/_Project/Data/Hazards/Hazard_{Fire,Poison}_{1x1,3x3}.asset` — element 저작
- `CLAUDE.md` — 채널 목록 25 → 26

## 구현

```csharp
public enum DotOrigin  : byte { Unspecified = 0, Stack = 1, Zone = 2, OnPlace = 3 }
public enum DotElement : byte { None = 0, Bleed = 1, Fire = 2, Ice = 3, Poison = 4 }

public struct DotEffect : IBufferElementData
{
    public DotOrigin origin;    // 슬롯을 가르는 기준 — 어느 파이프라인이 만들었나
    public DotElement element;  // 화면에 보이는 그림 — 무슨 원소인가
    public float scalar;        // tickInterval>0 이면 틱당 피해 / 0 이면 DPS
    public float tickInterval;
    public float tickTimer;     // 슬롯 지속 상태 — 병합 시 보존
    public float remainingTime;
}
```

축이 둘인 이유와 겸직 금지는 README 계약 2번. 병합은 `(origin, element)` 로 매칭한다 —
`CcEffectMerge` 의 `tickTimer` 보존·주기 환산·add 경로 첫 틱 즉발 규약을 그대로 가져오되,
**kind 분기가 사라진다**(도메인이 하나라서).

라우팅:

- **스택 파생** — `DotElementMap.FromStack(kind)`. 기믹 스택(Fatigue 등)은 `None`
- **해저드** — `ZoneApplySystem` 이 `effect.kind == CcKind.DoT` 를 보고 새 채널로 보낸다.
  `Slow` 가 이미 같은 형태로 `StatModifier` 로 빠지고 있으므로 **분기 하나가 늘 뿐**이다
- **`DotNearby`** — element `None` 유지. 버스터즈가 유일 producer 라 충돌 상대가 없다(제약 8)

`HazardEffect` 에 `element` 저작 필드를 추가한다. `[Serializable]` 구조체라 append-safe 하고 기존
에셋은 0(None)으로 로드된다 → `Hazard_Fire_*` = Fire, `Hazard_Poison_*` = Poison 을 명시 저작.
**`Hazard_Fire_3x3` 은 `tickInterval` 필드 자체가 없다**(그 필드 이전 저작 = 연속 도트) — 키 기준
삽입이 필요하다.

⚠ `Stun`·`Sleep`·`Impulse` 경로는 **한 줄도 건드리지 않는다.**

## 완료 기준

- [x] compile 통과, `CcEffect` 를 만들면서 `CcKind.DoT` 를 넣는 **런타임 코드가 0건**(grep)
- [x] EditMode `DotApplySystemTests` — `DotEffect` 기준으로 이관하고 **7/7 green 유지**
      (연속/이산 분기, 틱 수, 피해량이 이전과 동일)
- [x] EditMode `DotEffectMergeTests`(신규, 6건) — 같은 키는 병합(`remainingTime = max`, `tickTimer`
      보존, 주기 환산) / **키가 다르면 슬롯 분리** / 같은 element 라도 origin 이 다르면 분리 /
      element 없는 `OnPlace` 끼리는 병합
- [x] PlayMode `DotCoexistenceTest`(신규, `7093222e`) — 전용 큐 → `DotApplySystem` 드레인 → 틱 →
      감쇠의 실제 사슬을 태운다. 출혈(Stack·Bleed) 위에 화염(Zone·Fire)을 얹어 두 슬롯이 각자
      `scalar`·`tickInterval` 을 유지하고, 화염 갱신이 끊기면 **화염만** 만료되며 그 뒤에도 출혈이
      자기 요율로 계속 도는지 단언한다 — "장판을 나가도 장판 요율로 탄다"는 과피해의 e2e 가드
- [x] PlayMode 55 통과 / 13 실패 = HEAD 베이스라인과 동일(13건 전부 사전 실패)
- [ ] ~~PlayMode 기존 스택·오라·CC 스위트~~ (특히 `BleedAuraOutlastsStackSlotTest` — unit 1
      전까지 오라 경로는 `CcEffect` 를 보므로, DoT 가 빠지면 **깨진다. 이 단위에서 같이 옮긴다**)
- [x] 리그 실측(2026-07-29): 출혈 1회분 = **10틱 · 총 50**. 프로브 로그는 9틱/45 로 읽히는데
      이는 폭발 감지 프레임에서 HP 기준선을 재설정하느라 **즉발 첫 틱을 계산에서 뺀** 계측
      아티팩트다. 같은 로그에서 화염을 큐에 넣은 프레임에 곧바로 10 이 들어간 것이 add 경로
      첫 틱 즉발의 라이브 증거이고, 틱 간격은 정확히 0.5초로 유지된다
- [x] 리그 실측(2026-07-29): 출혈 중인 대상이 화염 장판 위에 있는 동안 **출혈 scalar 가 5.00 을
      유지**하고(이관 전이라면 10 으로 덮임) 화염 0.25s/10 · 출혈 0.5s/5 가 동시에 틱한다.
      장판을 벗어나면 **화염 슬롯만** 자기 지속(0.2s)으로 사라지고 출혈 잔여는 정상 감소 —
      과피해(~194)와 "장판 밖에서 장판 요율로 타는" 증상이 모두 사라졌다
- [x] `CLAUDE.md` 채널 목록에 `DotApplyEventsSingleton` 추가(26개 — 병행 세션이 보스 도약 채널을 먼저 추가했다)
