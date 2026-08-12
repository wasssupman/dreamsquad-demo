# 0 — 티어 축 신설 · `BossTag` 유도 분리

## 목적

적을 **일반 / 엘리트 / 보스** 로 가르고, **보스 특권의 출처를 «메커니즘 유무» 에서 «티어» 로
옮긴다.** 이 단위가 없으면 메커니즘을 하나 준 엘리트가 자동으로 보스가 되어 CC·어그로 면역을
얻는다 — 이후 전부가 이 분리를 전제한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/EnemyTier.cs` (신규)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `tier` 필드 (**append-only**)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeNightmareMechanics`
- `Assets/_Project/Data/Enemies/Enemy_Boss_{Nightmare,Jjangssen,Mamemo}.asset` — `tier: 2`

## 구현

```csharp
// EnemyTier.cs — 값 순서가 직렬화 계약이다(int). append-only.
public enum EnemyTier { Normal = 0, Elite = 1, Boss = 2 }
```

`AttackUnitData` 에 **맨 뒤로** 추가한다(직렬화 back-compat — 기존 17개 에셋은 폴백 0 = Normal).

```csharp
[Header("Tier")]
[Tooltip("일반/엘리트/보스. BossTag·위협테이블·등장경보는 Boss 에서만 나온다. " +
         "엘리트는 메커니즘을 갖되 보스 특권은 받지 않는다.")]
public EnemyTier tier = EnemyTier.Normal;
```

`BakeNightmareMechanics` 를 두 갈래로 가른다 — **메커니즘 bake 는 티어 무관**, 보스 부속물만
`tier == Boss` 로 좁힌다:

```
if (mechanics 없음) return;                      // 현행 유지
if (unitType.tier == EnemyTier.Boss)             // ← 신규 게이트
{
    AddComponent<BossTag>();
    _bossWarning?.Show();
    AddBuffer<ThreatEntry>();
}
// 이하 PatternSlot / EmitterInstance / DcTriggerSlot bake 는 그대로 (엘리트도 받는다)
```

**순서를 지킬 것**: `BossTag`/`ThreatEntry` 부착이 `DcTriggerSlot` 의 `AddBuffer` 보다 **앞**이어야
한다. 현행 주석이 경고하는 대로 `slots` 는 «마지막 `AddBuffer`» 라는 전제로 핸들을 캐시한다 —
그 뒤에 구조 변경을 하나라도 넣으면 핸들이 죽는다.

### 하지 않는 것

- **`nightmareMechanics` 필드를 rename 하지 않는다.** 라이브 보스 3종이 이 YAML 키를 들고 있고,
  `AttackDeck.bossUnit` 이 rename 으로 조용히 사라졌던 계보(`boss-jjangssen` 계약 1)를 반복하지
  않는다. 이름이 이제 좁아졌다는 사실은 주석으로 남긴다.
- **`killScore`/`stabilityDamage` 와의 정합성 검사(`OnValidate`)를 넣지 않는다.** README 티어 축
  계약 마지막 항목 — `Enemy_Tanker` 가 일반 티어이면서 엘리트 값 대역이라 정상 콘텐츠에서
  발화한다.
- **`EnemyClass`(Tanker/Runner/Bruiser/Shooter)와 섞지 않는다.** 그쪽은 «역할» 축이고 이쪽은
  «등급» 축이다. 엘리트 슬라임은 `tier=Elite` + `enemyClass=Bruiser` 로 둘 다 갖는다.

## 완료 기준

- [ ] compile 통과 (`dotnet build` 또는 Unity 콘솔 에러 0)
- [ ] EditMode 전체 통과 — 신규 실패 0
- [ ] `DcTrigger.EnemyTriggerArmed` 를 고정하는 기존 EditMode 테스트가 **그대로 통과**한다
      (이 단위는 화이트리스트를 건드리지 않는다)
- [ ] 신규 EditMode: `tier=Elite` + 메커니즘 1개인 `AttackUnitData` 를 bake 하면
      **`DcTriggerSlot` 은 생기고 `BossTag`·`ThreatEntry` 는 안 생긴다**
- [ ] 신규 EditMode: `tier=Boss` 는 셋 다 생긴다
- [ ] Play 무회귀 — 보스 웨이브에서 보스경보가 **여전히** 뜨고, 보스가 CC 에 걸리지 않는다
      (`tier: 2` 저작 누락이면 여기서 잡힌다)
- [ ] 보스 아닌 적 14종은 저작 변경 0 으로 현행 그대로 (폴백 `Normal`)
