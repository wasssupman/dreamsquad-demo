# 1 — 회오리 연출 (적에게 «유닛별 공격 VFX» 를 개방)

## 목적

「돈다」를 화면에 만든다. **연출은 지속, 판정은 연타**(README 계약 6) — 회전 pulse 는 이산
사건인데 플레이어는 «계속 돌고 있다» 로 읽어야 한다.

이 단위의 실체는 **발명이 아니라 대칭 맞추기**다. 방어유닛 SO 는 이미 유닛별 공격 VFX 를
갖는다(`DefenderUnitData.attackVfxPrefab`·`attackVfxScale`·`attackVfxFacesTarget`·
`attackVfxEulerOffset`). 적 SO(`AttackUnitData`)에는 그 필드가 **하나도 없다.**

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `attackVfxPrefab` + `attackVfxScalePerTile`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DrainUnitAttackVisualEvents` 에 적 분기
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — 회오리 스폰
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 밴드 1개
- `Assets/_Project/VFX/` — 회오리 프리팹

## 구현

**신규 이벤트 채널 0 · 신규 이벤트 필드 0.** 기존 `UnitAttackVisualEvent` 가 매 공격 START 에
나가고 `attacker` 와 `attackAnimPeriod`(= 이번 공격의 **실발사 주기**)를 이미 싣는다.
브레스처럼 두 번째 이벤트를 만들 필요가 없다 — 회오리는 «그 공격 자체» 이기 때문이다.

**드레인 지점.** `DrainUnitAttackVisualEvents` 는 `NotifyAttack` 뒤에
`FindDefenderData(...) == null → continue` 로 **적을 걸러낸다.** 적 분기는 그 `continue`
**앞**에 놓고 `_enemyTypeByEntity` 로 SO 를 직독한다 — 슬라임 분열이 킬 드레인에서 SO 를 직독한
것과 같은 수법이다.

★**어느 적이 회오리를 갖는지는 «프리팹 유무» 가 결정한다.** id·이름 분기 금지이며
`attackTargetCount > 1` 로도 판정하지 않는다 — 그러면 Basic·Tanker·짱쎈까지 회오리가 생긴다.
빔 유닛 판정(*"빔 유닛인가는 SO 의 프리팹 유무가 결정한다(id/kind 분기 없음)"*)과 같은 규율.

**중심**은 공격자 자신이다(대상이 아니다). 뷰 좌표는 브레스와 같이
`ResolveBeamViewPos(evt.attacker, ...)` 로 푼다.

**크기는 타일당 스케일**(`attackVfxScalePerTile` × `attackRange`)이다. 반경이 튜닝 knob 이라
고정 스케일로 저작하면 반경을 바꿀 때 연출이 조용히 어긋난다. `VfxSpawner` 의
`areaBreathScalePerTile` 이 같은 벤 자리에서 나온 같은 관례다.

**지속감 = 수명 이어붙이기.** 수명을 `attackAnimPeriod` 보다 길게(≈1.3배) 두면 끊김 없이
이어지고 최대 2개만 겹친다. 공격이 멈추면 자연 소멸한다 — on/off 상태도, sim 상태 읽기도,
신규 컴포넌트도 없다(README 계약 1). 루프형 프리팹의 단발화는 `ConfigureOneShot` 이 하고
**공유 에셋은 건드리지 않는다.**

> 겹침으로 밝기가 튀거나 깜빡이면 escalation 은 `StatusFxSpawner`(유닛당 인스턴스 1개 보장,
> 수면 Zz·스턴 별이 탄다). 대가는 `StatusFxKind` append + 레지스트리 + 「교전 중인가」 sim
> 폴링이고, 무엇보다 **회오리는 상태가 아니라 능력**이라 그 계층의 의미와 어긋난다. 싼 쪽 먼저.

**회전.** 스파인은 **돌리지 않는다** — 벤더 리그에 회전 애니가 없어 억지로 돌리면 발이 하늘로
간다. ⚠ 이펙트를 돌릴 때 **뷰 루트의 `transform.rotation` 을 쓰지 말 것**: `Billboard` 가
*"틸트/페이싱 회전의 유일 소유자"* 로 매 `LateUpdate` 에 덮어쓴다. 자식에서 돌려야 빌보드가
세운 평면을 상속한다(무기 궤적 리그가 자식이어야 하는 것과 같은 이유).

**모양 주의 2건**
- 판정은 **Chebyshev(사각)** 이다(`AttackSystem` 의 `tileDistAoE > tileRange` 게이트) —
  딱 떨어지는 **원 경계선을 그리지 말 것**
- **번아웃 먹구름과 달라야 한다**(README 계약 8). 번아웃은 방어유닛 *상태*, 이건 적 *공격*이다

## 완료 기준

- [ ] Unity 컴파일 에러 0 · 콘솔 에러 0
- [ ] 프리팹 미할당이면 **무동작**(경고 없이 조용히 넘어가는 것이 옳다 — 적 17종 전부가 미할당이고 그게 정상이다). 이 점만 브레스의 «빈 슬롯 경고» 관례와 다르며 이유를 코드에 남긴다
- [ ] EditMode 전량 — 신규 실패 0
- [ ] **Play 육안**(unit 2 이후): 회오리가 끊겨 보이지 않는다 · 밝기가 튀지 않는다 · 몸을 가리지 않는다 · 번아웃 먹구름과 헷갈리지 않는다 · 다른 적 16종에 회오리가 생기지 않는다
