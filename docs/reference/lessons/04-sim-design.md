# 전투 시뮬 설계 원칙

## 구조적 결정론 (seeded RNG 보다 index 기반)

전투 시뮬의 비주얼/배치 분산은 RNG(seeded 포함) 대신 **결정론 수열**을 쓴다. 목표 = 구조적 결정성(같은 입력 → byte-identical).

- **Why**: 비동기 토너먼트 리플레이/공정성. 사용자 명시 요구 "랜덤 있으면 안됨".
- **적용**: 분산/지터/선택은 index 기반 결정론으로. 예) 스폰 측면 분산을 `_spawnSpreadRng`(seeded) → `SpawnSpread.LaneFraction`(이산 N-레인 round-robin, 스폰 순번 % N)로 교체.
- **선호**: clever 한 저불일치(golden-ratio)보다 **단순·예측가능한 이산 N-레인 round-robin**(또렷한 N줄 대형 + 디버그 용이). seeded RNG 는 차선.

## 시간 제어는 TimeManager 만 — `Time.timeScale` 금지

시간 스케일 제어는 `Somnia.Battle.Core.TimeControl.TimeManager`(의도된 예외 싱글턴 — `CLAUDE.md` 「Unity 함정」)만 담당. 코드에서 `Time.timeScale` 은 **절대 write 안 함(항상 1)**.

- **Why**: 글로벌 `Time.timeScale` 은 너무 blunt — 전투만 멈추고 UI·드래그·카메라는 실시간으로 두려면 도메인 분리 필요.
- **사용**: 정지 = `TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority:100)`, 슬로우 = `Request(Battle, 0.2f)`. 반환 `TimeLease` 를 보관 후 Dispose(멱등)로 해제.
- **전투 도메인 스케일(지금)**: 전투 코어는 고정 틱 1/60 이고 프레임을 모른다. 슬로모·정지는 **틱 발행률**이다 — `BattleDriver` 가 매 프레임 `TimeManager.Instance.ScaleOf(TimeDomain.Battle)` 을 읽어 그만큼만 틱을 발행한다(`Runtime/Battle/Scripts/BattleCoreUnity/BattleDriver.cs`). 웨이브·타이머는 코어 담당자(`WaveScheduler`·`MatchClock`)가 틱으로 센다.
- (이력 — 옛 ECS 전투, unit 9 에서 제거) ECS 는 `BattleSimGroup` 위 `BattleScaledRateManager`(scale 0=skip, >0=scaled delta), 브리지가 `BattleTimeScale` singleton write + `_battleClock`(unscaledDeltaTime×scale)로 웨이브/타이머를 구동했다. 되돌리면 안 되던 것: 정리 루틴의 `DestroyEntitiesByType<BattleTimeScale>()`(빼면 orphan → 시간제어 무력화) · RateManager 로컬 `_elapsedTime` 누산(월드 elapsed 를 읽으면 정지 후 점프). 교훈 — **정지 후 재개에서 「누적 시간」을 어디서 읽는지가 점프를 만든다** — 은 유지.
- **부작용**: `Time.timeScale=0` 으로는 웨이브/타이머가 안 멈춘다(발행률이 `TimeManager` 에서 온다). 검증 목적 완전 동결은 `TimeManager.Request(Battle,0)`. (→ `01-unity-mcp-operation.md` 애니 검증.)

### 함정 — `TeardownCurrentBattle` 안에서 `?.` 를 쓰면 그 뒤가 통째로 죽는다

> (이력 — 옛 ECS 전투, unit 9 에서 제거) `TeardownCurrentBattle`·`BattleTimeScale`·`BattleBridge` 는 옛 전투의 것이다. **교훈(`OnDestroy` 계열에서 UnityEngine.Object 에 `?.` 금지 · 첫 예외부터 찾기)은 모든 MonoBehaviour 에 그대로 적용된다.**

**증상**: 무관해 보이는 테스트 여러 개가 `HasSingleton<BattleTimeScale>() found 2 instances` 로 무너진다.

**원인**: `TeardownCurrentBattle` 은 `OnDestroy` 에서도 불린다. 그 시점엔 씬의 다른 컴포넌트가 이미
파괴돼 있는데, **C# 의 null 조건 연산자 `?.` 는 Unity 의 fake-null 을 모른다** — 파괴된 UnityEngine.Object
는 `== null` 이 true 지만 C# 참조로는 non-null 이라 `?.` 가 short-circuit 하지 않고
`MissingReferenceException` 을 던진다. 그러면 **그 메서드가 거기서 중단돼 뒤에 있는
`DestroyEntitiesByType<BattleTimeScale>()`(및 나머지 정리)이 실행되지 않는다.** 싱글턴이 살아남고
다음 씬의 BattleBridge 가 하나 더 만들어 2개가 된다.

**규칙**: `TeardownCurrentBattle`/`OnDestroy` 계열에서 UnityEngine.Object 를 부를 땐 반드시
`if (x != null) x.Foo();` (Unity 오버로드 `==` 가 fake-null 을 처리). 그 메서드의 기존 줄들이 전부
그 형태인 것이 우연이 아니다. `?.` 는 **순수 C# 객체에만** 쓴다.

**진단법**: 여러 테스트가 한꺼번에 무너지면 **신규 테스트를 먼저 빼고 돌려본다.** 그래도 실패하면
테스트가 아니라 프로덕션 변경이 원인이다. 그 다음 콘솔에서 **첫 예외**(여기서는 InvalidOperationException
이 아니라 그 앞의 MissingReferenceException)를 찾는다 — 뒤에 쏟아지는 것은 전부 파생이다.

**출처**: defender-clock-out 코드리뷰 반영 중 실측(2026-08-15).

## Bursted ISystem 에서 순수 함수를 부를 때 — 함정 둘

> (이력 — 옛 ECS 전투, unit 9 에서 제거) Bursted `ISystem` 과 `ComponentLookup` 은 이제 저장소에 없다(전투 코어는 Burst 를 쓰지 않는다). 아래 심볼(`HazardCastSystem`·`AttackSystem`·`EnemyAiStateSystem`·`PatrolFieldSystem`)은 옛 전투의 것이다. 교훈 — **「무관해 보이는 대량 실패」는 콘솔의 첫 에러(여기선 Burst BC1055)부터 본다 · 같은 파일에서 된다고 여기서도 된다고 가정하지 않는다** — 는 유지한다. Burst 를 다시 들일 일이 생기면 이 절이 그대로 지도다.

전투 심의 순수 계산을 별 asmdef(`Somnia.Battle.Skills`)로 빼면 두 번 넘어진다. **증상이 둘 다
「그 함수와 무관해 보이는 대량 실패」**라서 원인에 도달하는 데 시간이 든다.

### ① 대상 asmdef 이 `Unity.Burst` 를 참조하지 않으면 Burst 가 본체를 못 찾는다

```
Burst error BC1055: Unable to resolve the definition of the method
  `Somnia.Battle.Skills.SkillMath.InBodyReach(float, float, float, float)`
```

**호출하는 쪽이 아니라 정의된 쪽 asmdef 에 `Unity.Burst` 참조가 필요하다.** 없으면 Burst 가
그 어셈블리를 로드하지 않아 메서드를 해석하지 못한다. (당시엔 `noEngineReferences: true` 를
유지해도 됐다 — Burst 는 엔진 어셈블리가 아니라 패키지다. 6.6 전환 뒤 코어 asmdef 는
`noEngineReferences: false` 다 — `Unity.Mathematics` 가 엔진 모듈이 됐기 때문. `unity-6-6-upgrade`.)

⚠ **연쇄 증상에 속지 말 것.** BC1055 는 컴파일을 막지 않고, 실패는 **런타임에** 그 시스템이
무너지는 모습으로 나온다. 실측에서는 EditMode 25건 이상이
`ObjectDisposedException: EntityTypeHandle ... invalidated by a structural change` 와
「공격이 0건」으로 동시에 빨개졌다 — 전부 BC1055 하나 때문이었고, 그 직전에 추가한
`ComponentLookup` 이 범인처럼 보였다. **콘솔에서 Burst 에러를 먼저 확인한다.**

### ② `SystemAPI.GetComponentLookup` 지역 변수가 어떤 시스템에서는 NRE 를 낸다

```
NullReferenceException ... compiled with Burst, which has limited exception support
  #3 Wassup.Battle.Effects.HazardCastSystem.OnUpdate
```

초기화 안 된 lookup 포인터다. **같은 파일의 다른 `SystemAPI.GetComponentLookup` 이 멀쩡히
도는 것이 함정** — 「저 형태가 되니까 여기도 되겠지」로 되돌리게 된다. 실측에서 차이는
그 타입이 **같은 시스템의 쿼리 `.WithAll<>` 에도 쓰인다**는 점 하나였지만 **원인은 확정하지
못했다**(재현은 확실하다).

**해법 = Entities 정본 형태.** `SystemAPI.GetComponentLookup` 은 그 위에 얹힌 소스 생성기
설탕이므로, 아래가 축약이 아니라 원형이다:

```csharp
private ComponentLookup<Foo> _fooLookup;                  // 시스템 필드
public void OnCreate(ref SystemState state)
    => _fooLookup = state.GetComponentLookup<Foo>(isReadOnly: true);
public void OnUpdate(ref SystemState state)
{
    _fooLookup.Update(ref state);                          // 소비처보다 앞, early-return 앞
    ...
}
```

⚠ **세 번 밟았다.** `HazardCastSystem`(unit 1) → `AttackSystem`·`EnemyAiStateSystem`(unit 4a) →
`PatrolFieldSystem`(2026-09-01). 세 번째는 **이 문서를 쓴 뒤**였다 — 읽어도 그 순간 안 떠오른다.
그래서 규칙을 이렇게 굳힌다: **Bursted `ISystem` 에 `ComponentLookup` 을 새로 추가할 때는
`SystemAPI` 지역 변수 형태를 아예 쓰지 않는다.** 되는 경우가 있어도 쓰지 않는다 —
「되는지 확인하고 안 되면 바꾼다」는 매번 EditMode 전건이 빨개진 뒤에야 알게 된다.

출처: `distance-based-range` unit 4a · `HazardCastSystem`(unit 1) · `PatrolFieldSystem`.

## 런타임이 다르면 float 이 다르다 — Unity Mono 는 확장 정밀도로 평가한다

`battle-core-rebuild` 조각 A(2026-09-23): 같은 커밋·같은 시나리오가 헤드리스 dotnet(.NET 9)
에서는 골든과 일치하고 Unity EditMode 에서는 `kill_race_3min` 이 9,887틱에서 갈렸다.
틱마다 상태를 **비트 단위**로 찍어 보니 첫 갈림은 **301틱**(적의 첫 이동 방향 `normalize`
결과 1~2 ulp). 설계 입력으로 확정: `float a = 1 + 2⁻¹²; a*a - 1` 이 .NET 9 는 `2⁻¹¹`,
Unity Mono 는 `2⁻¹¹ + 2⁻²⁴`. 덧셈 없는 `q⁸` 곱 연쇄도 1 ulp 갈린다 — FMA 가 아니라
**중간값을 double 로 들고 가는** 평가다(C# 스펙이 허용하는 「더 높은 정밀도」). IL2CPP(clang)
는 또 다르다.

- **결정론은 같은 런타임 안의 계약**이다. 런타임 간 비트 동일을 약속하지 말고, 그걸 위해
  식마다 `(float)` 캐스트를 박지도 말 것(코드가 흉해지고 IL2CPP 에서 다시 깨진다).
- **골든은 게임이 실제로 도는 런타임(Unity)에서 굽고 대조한다.** 헤드리스 dotnet lane 은
  컴파일 + 규칙 테스트만(`[Category("Golden")]` 제외, csproj `VSTestTestCaseFilter`).
- 트레이스 헤더의 float 은 `"R"` 로 쓰지 말 것 — 런타임마다 자릿수가 다르다(.NET 9 최단 왕복
  vs Mono 9자리). `G9` 는 같다.
- 「같은데 갈린다」를 잡는 계측은 **비트 다이제스트**다. 1e-3 양자화 해시(`StateHash`)는
  이벤트 임계를 넘기 전까지 수천 틱을 초록으로 보여 준다.

출처: `docs/spec/battle-core-rebuild/README.md` 계약 5 · `CoreGoldenTests` 주석.

## 정의표 빌더의 매핑 누락은 조용히 죽는다 — 필드마다 핀 테스트

battle-core-rebuild unit 5·6(2026-09-23~24): 유닛 배치 저작 7칸(코스트 등)이 정의표로 안 옮겨져 **배치가 공짜**였고, 발사 명세 선정 규칙 enum 번호가 저작 쪽과 어긋나 **12 중 11 이 오독**됐다. 둘 다 컴파일·기존 테스트가 초록이었다 — 빠진 필드는 기본값 0 으로, 어긋난 enum 은 다른 유효값으로 읽힌다.

- **처방**: 저작 enum 을 코어가 미러하면 **번호 핀 테스트**(두 enum 의 이름↔값 대조)를 같은 커밋에 둔다. 정의표 필드를 추가하면 **빌더 매핑 테스트**(저작값 ≠ 기본값 → 정의표에 그 값)를 같이 쓴다. 모르는 enum 값은 기본값으로 떨어뜨리지 말고 빌드에서 거절한다.

## 「동률 결정론」 선례를 기하 순서 규칙에 쓰지 말 것

동거리 후보의 순서를 `SimEntityId` 로 정하는 것은 **결정론을 위한 동률 해소**다(옛 엔티티 순서는 비결정이었다). battle-core-rebuild 9c(2026-09-25)에서 이 선례를 「관통탄이 한 틱에 가로지른 적을 어떤 순서로 치나」에 적용해 옛 규칙(가까운 적부터)을 버렸다가 되돌렸다(`484e950b4`).

- **판별**: 옛 코드 주석이나 spec 이 순서를 **규칙으로** 말하면(「진행 방향 앞부터」) 그것이 정본이다. 동률 해소는 규칙이 순서를 정하지 않는 **남은 동률**에만 쓴다.
