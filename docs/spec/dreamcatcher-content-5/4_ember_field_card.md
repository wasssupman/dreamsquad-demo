# 4 — 잿불 카드 (`ember_field`)

> 적을 처치하면 그 자리에 불씨가 남아 밟는 적을 태운다.

## 목적

**카드가 장판을 깔 수 있게** 연다. 장판(해저드) 파이프라인 — 모양·수명·효과·틱·뷰·소멸 —
은 전량 이미 있고, 없는 것은 드림캐쳐에서 그 파이프라인으로 가는 길 하나뿐이다.

조합은 `OnKill × SpawnHazard`. **시체폭발(`corpse_burst`)이 이미 「죽은 자리」에서 터지는
선례**라 위치를 나르는 배선도 그 형태를 그대로 복제한다.

## 변경 대상

- `Battle/Units/EnemyKilledEvent`(구조체) — 해저드 스탬프 필드
- `Battle/Units/DamageApplicationSystem.cs` — 킬 처리에서 슬롯 RO 읽어 스탬프
- `Bridge/BattleBridge.cs` — 킬 드레인에서 `SpawnHazardWithVisual` 호출
- `Bridge/BattleBridge.Dreamcatcher.cs` — bake(해저드 SO 인덱스 등록)
- `Data/Hazards/Hazard_Ember.asset` · `Data/Dreamcatcher/Card_EmberField.asset` — **신규**
- `Data/Dreamcatcher/DreamcatcherCardCatalog.asset` — 등록

## 구현

**스탬프 → 드레인 형태를 그대로 따른다.** 시체폭발이 `hasKillBurst`/`burstDamage`/
`burstTileRange`/`burstDataIndex` 를 킬 이벤트에 실어 브리지가 터뜨리는 것과 동형으로,
`hasKillHazard`/`hazardDataIndex`/`hazardTargetLayers` 를 싣는다.

⚠ **해저드 스폰은 브리지 전용 행위다.** SO·머티리얼·뷰 프리팹이 필요하므로 sim 이 만들 수 없다
(분열 `SplitOnDeath` 이 인덱스 레지스트리·전용 큐를 전부 걷어낸 것과 같은 이유). 브리지는 이미
`SpawnHazardWithVisual(HazardSO, cell, targetTraversalLayers)` 를 갖고 있고, 킬 드레인은 죽은
위치를 이미 손에 들고 있다 — **신규 큐 0.**

> **기각한 대안**: 기존 `HazardSpawnRequestsSingleton` 에 sim 이 직접 enqueue(그러면 킬 이벤트
> 필드가 하나도 안 는다). 그 드레인이 `caster` 생존을 요구하는데 **여기서 caster 는 방금 죽은
> 적**이라 매번 걸러진다. 스탬프 쪽이 맞다.

**통행 층은 처치한 유닛의 사양을 「처치 시점에」 스탬프한다.** 안 실으면 0 인데, 0 은
**무제한 통과**라 지상만 때리는 유닛이 깐 불씨가 **그 유닛이 못 때리는 비행 적을 태운다**
(content-4 계약 3-1 이 궤도 화염구에서 겪은 그 구멍). 초판은 「브리지가 killer 에서 읽고, 이미
사라졌으면 0 폴백」이라고 썼는데 **그 폴백이 정확히 막으려던 구멍을 연다.**
killer 가 살아 있는 것이 보장되는 유일한 시점은 **킬 처리 프레임**이므로 거기서 같이 굽는다.
시체폭발이 「드레인 시점엔 슬롯을 못 읽는다」고 주석에 적어둔 것과 같은 이유다.

**bake** 는 `RegisterZoneHazardSO(payload.hazard)` 로 인덱스를 얻어 슬롯에 싣는다
(`projectileDataIndex` 와 같은 자리·같은 역할).

## 지속은 카드가 정하지 않는다

초판은 카드 `duration` 으로 지속을 덮으려 했다. **그 경로가 존재하지 않는다** — sim 수명과
뷰 수명이 둘 다 해저드 SO 의 값을 직접 읽고, 오버라이드 파라미터가 없다. 뚫으려면 Effects
맥락의 스폰 시그니처까지 바꿔야 하는데 **소비자는 이 카드 한 장뿐**이다(제약 8).

→ **지속도 장판의 성질로 두고 계약 9 에 포함한다.** 카드는 「어떤 불씨를」만 말한다.
다른 지속이 필요하면 해저드 SO 를 복제해 가른다(탄 SO 복제 관례와 같다).

## 저작(초기값)

| 값 | 자리 | 초기값 |
|---|---|---|
| 지속 | **해저드 SO `lifetime`** | 4초 |
| 모양/반경 | 해저드 SO `shape`/`radius` | 단일 셀 |
| 태우는 피해 | 해저드 SO `effects` | **1초마다 12** (= 초당 12, 최대 4틱) |
| 뷰 | 해저드 SO `visualPrefab` | 기존 화염 장판 재사용 |

## 겹침

같은 셀에서 연달아 처치하면 불씨가 겹친다. **기존 해저드 병합/수명 규칙을 그대로 따르고
이 spec 에서 새 규칙을 만들지 않는다** — 눈으로 보고 과하면 지속을 줄인다(에셋 값).

## 완료 기준

- [ ] Play: 적을 처치한 **그 자리**에 불씨가 생기고, 밟고 지나가는 적이 탄다
- [ ] 지속이 끝나면 사라진다(뷰도 같이) — 잔류 엔티티 0
- [ ] 부착 유닛이 죽거나 퇴근한 뒤에도 **이미 깔린 불씨는 자기 수명을 산다**
- [ ] **지상만 때리는 유닛의 불씨가 비행 적을 태우지 않는다** — killer 가 그 프레임에 죽어도
      (동귀어진) 통행 층이 0 으로 새지 않는다
- [ ] `hazard = null` 저작이 loud 경고 + 슬롯 미생성
- [ ] 시체폭발(`corpse_burst`) 무회귀 — 같은 킬 이벤트를 공유하므로 자동 테스트로 고정
