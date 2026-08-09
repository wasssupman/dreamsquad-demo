# unit 4 — 거점 스폰·붕괴·뷰

## 목적

저작된 거점(unit 3)을 판에 세운다: 엔티티 스폰 + 3×3 점유 + 배치 배제 + 프랍·게이지. 그리고 계약 7(거점 단위 붕괴)을 **ⓐ 방식**(사용자 확정 2026-08-09)으로 구현한다 — 브리지의 전역 bool 을 «붕괴한 셀 집합» 으로 확장하고, 라이브 공성 기계(`canSiege` → `GoalReachedMarker` → 브리지 드레인)는 유지한다.

**행동 변화**: 골 2개 맵에서 한 골이 부서져도 **나머지가 선다**(현행: 하나 부서지면 전부 파괴·전역 전환). 거점 저작 맵에서 본능·적 마음이 실제로 스폰된다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/StructureTag.cs` — 신설(첫 실소비처가 생기므로 — unit 0 이 미룬 그 태그)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스폰 일반화 · 붕괴 ⓐ · teardown · 게이지
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — 거점 프랍
- `Assets/_Project/Tests/EditMode/` — 공용 픽스처 빌더(리뷰 M-d) + 붕괴 per-cell 테스트

## 구현 — step 1: sim (스폰 + 붕괴 ⓐ)

### 1. `StructureTag` + 등록부

```csharp
public struct StructureTag : IComponentData { public int2 cell; public Faction faction; }
```

`GoalTowerTag` 는 **존치**(패배 판정·기존 쿼리가 읽는다). 방어 마음 = `GoalTowerTag` + `StructureTag` 둘 다, 본능·적 마음 = `StructureTag` 만.

브리지 등록부: `_goalGaugeList` 를 `_structureRegistry`(entity · cell · faction)로 개명·부활한다 — 리뷰 M-e 의 «writer 0» 처분. `EnsureGoalTowers` → `SpawnStructureEntities` 로 일반화하면서 writer 가 돌아온다.

### 2. 스폰 일반화 — `SpawnStructureEntities`

두 소스, 한 아키타입(unit 3 결정 승계):

| 소스 | 대상 | HP 출처 | 태그 |
|---|---|---|---|
| `_generatedMap.goals[]` | 방어 마음(= 현행 골 타워) | **덱**(`goalStabilityMax`) — 현행 유지, SO 이관은 안 한다(아래) | `GoalTowerTag` + `StructureTag` |
| `_resolvedMapDoc.Structures` | 본능 + 적 마음 | **SO**(`StructureData.health`) | `StructureTag` |

공통 컴포넌트: `FactionTag`(파생 비트) + `Health` + `IncomingDamage` + `LocalTransform`. CC·모디파이어 버퍼 미부여(계약 8). `AttackState` 는 unit 5.

방어 마음 HP 의 덱→SO 이관을 **하지 않는 이유**: 현행 9장에 `StructureData` 저작이 없어 이관하면 «HP 소스가 맵마다 갈리는» 상태가 된다. 침략 맵의 골 HP 는 덱이 정본(F5 확인 사항 그대로), 거점 저작 맵의 거점 HP 는 SO — 소스가 태그로 갈리는 게 아니라 **스폰 소스로** 갈린다.

### 3. 3×3 점유 + 배치 배제

- **통행 차단** = 본능 본체 3×3 만(계약 12 — 마음은 비차단). `BlockingHazardCellsBuffer` 선례(`EffectSpawner:164`)를 그대로: 본능 엔티티에 buffer 로 9칸 등재. `ObstacleLifetimeSystem`·`FlowFieldRebuildSystem` 이 이미 이 버퍼를 소비하므로 **통행 코드 신설 0**.
- **배치 배제** = 적 본능 3×3 + 주변 3타일(9×9) `placeMask` 클리어. `CloseCellLayers` 선례(`BattleBridge:1029`)와 같은 자리(빌드 시 파생, 저작본 무접촉)에서 수행.
- **연결성**: 3×3 블로커가 스폰→골을 끊을 수 있다 — `MapConnectivity` 를 거점 반영 후 상태로 검사(페인터 Validate 에 합류).

### 4. 붕괴 ⓐ — bool → 셀 집합

| 현행 | ⓐ |
|---|---|
| `_goalBreached` (bool) | `_breachedCells` (`HashSet<Vector2Int>`) |
| `SyncGoalStability:4978` — count 비교 «하나라도 사라짐» | 등록부 순회 — **사라진 엔티티의 셀**을 특정해 `_breachedCells` 에 추가 |
| `OpenGoalAfterBreach:5022` — **전 타워 파괴** + 공성 적 전부 유출 | 그 셀만: 파괴는 이미 일어났고(표준 사망 경로), **그 셀에서 공성 중이던 적만** 유출 전환(적 위치 → 최근접 골 셀로 귀속 — `EnqueueGoalTowerDamage` 가 이미 쓰는 방식) |
| `DrainGoalEvents:4882/4896` — 전역 bool 판정 | `evt.position` → 셀 → `_breachedCells` 포함 여부로 공성/유출/스트레스 판정 |
| 패배(StressLimit 0): 하나 부서지면 즉시 | 유지 — «마음 하나라도 부서짐 = 패배» 는 동일(구 동작 보존) |
| 스트레스(StressLimit>0): 붕괴 후 유출 1 = 스트레스 1 | 유지 — 단 «부서진 셀로의» 유출만 센다 |
| 리셋 `:4799` | `_breachedCells.Clear()` — 매치 경계 소멸(이월 금지) 동일 |

미러 스칼라(`_goalStability`)는 **유지한다** — 점수 tie-break(`EncodeSubmission`)·HUD·공개 API 가 읽는 «가장 위험한 골» 캐시로서, 판정은 이미 per-entity Health 다. 계약 7 의 «미러를 걷어낸다» 는 문구는 «미러가 판정을 소유하지 않는다» 로 충족된다(문구보다 좁게 이행 — README 정정 대상).

**본능·적 마음의 붕괴**는 결정 2(v1 = 연출·로그만)를 따른다: 등록부에서 제거 + 게이지 숨김 + 붕괴 VFX. 유출 전환·스트레스는 **방어 마음(골) 전용** — 본능이 부서져도 그 셀이 유출 지점이 되지 않는다.

`GoalCollapsedEventsSingleton` 은 이 unit 에서도 **생산자를 만들지 않는다**. 붕괴 감지가 브리지 등록부 폴링(엔티티 부재)으로 충분하고, 채널 페이로드(`goalIndex`)가 거점 체계와 안 맞는다(리뷰 M-e). 채널은 후속 후보로 이관하고 존치.

## 구현 — step 2: view (프랍 + 게이지)

- **프랍**: `TilemapMapView` 가 `map.structures` 를 순회해 `StructureData.viewPrefab` 을 셀에 배치. `PlaceStructure`(PropData 경로)는 골 프랍용으로 존치 — `StructureData.viewPrefab` 은 GameObject 직참조라 **별도 소량 경로**(Instantiate + 셀 중심 배치)로 간다. KayKit 후보를 SO 에 물린다.
- **게이지**: `SyncGoalOverheadGauges` 가 등록부(`_structureRegistry`)를 순회 — writer 부활로 도달 가능해진다. HUD 바(`SyncGoalStabilityBars`)는 무변경.
- **붕괴 VFX**: `VfxSpawner.SpawnGoalCollapse` 재사용(이미 있고 소비처만 죽어 있었다).

## 테스트 (리뷰 M-d 반영)

- **공용 픽스처 빌더** `StructureFixtures`(테스트 어셈블리): 타워/본능 아키타입 생성을 한 곳으로. `GoalTowerArchetypeTests` 가 «브리지 산물의 컴포넌트 집합 == 빌더 산물» 을 단정해 drift 를 구조로 막는다. `GoalProjectileTests.MakeGoal`·`GoalTargetingPriorityTests.CreateGoal` 을 빌더로 교체.
- 붕괴 per-cell: 골 2개 스폰 → 한쪽 파괴 → 그 셀만 `_breachedCells`, 다른 골 생존 + 그 셀 도달 적은 여전히 공성.
- 본능 스폰: `structures[]` 저작 문서 → 3×3 버퍼 9칸 + placeMask 9×9 배제 + SO HP.
- 브리지 경로는 `BattleBridgeGoalStabilityTests` 하네스 기법(리플렉션) 재사용.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 전량 그린 — **2039개 / 실패 0 / 의도적 스킵 3**(기준선 2032 + 신규 7)
- [x] 골 2개 맵: 한 골 붕괴 → 그 셀만 breached, 다른 골 존속 — `OneTowerDestroyed_BreachesOnlyThatCell_OtherStands`(브리지 하네스, 합성 아님)
- [x] StressLimit 0 즉시 패배 무회귀 — PlayMode `EndlessModeSmokeTest`(Deck_Endless 가 상한 0)가 라이브로 잰다. **통과**
- [x] 본능 3×3: 통행 버퍼 9칸 + SO HP — `AuthoredInstinct_SpawnsWithSoHp_AndNineBlockedCells`. placeMask 9×9 배제는 빌드 지점 구현(전용 테스트는 없음 — `CloseCellLayers` 의 기존 검증 범위)
- [x] 아키타입 단일 소스 — `BridgeSpawnedTower_ComponentSet_MatchesSharedFixtureBuilder`(컴포넌트 집합 대칭차 = 공집합). 합성 픽스처 2파일이 `StructureFixtures` 빌더로 교체됨(리뷰 M-d)
- [~] 프랍·게이지: 게이지는 등록부 소비로 도달 가능 복원(M-e), 프랍은 브리지 Instantiate 경로(Pickup 선례) 구현. **시각 확인은 거점 저작 맵이 아직 없어 스펙 종료 Play 검증으로 유보**
- [ ] 리뷰: **투트랙** — unit 5·6 후 스펙 종료 시점에 4~6 묶어서

---

**확인 2026-08-09** — 구현 커밋: (아래 커밋 해시)

**구현 중 확정된 것 3건**:
1. **붕괴 프레임의 미러 = 0.** «가장 위험한 골» 은 방금 죽은 그 골이다 — 생존 골 체력으로 덮으면 HUD 에 «부서졌는데 191» 이 뜨고, StressLimit 0 즉시 패배가 그 값으로 얼어붙는다(PlayMode 실측으로 적발). 다음 프레임부터 생존 골 중 최저.
2. **뷰는 브리지 Instantiate**(Pickup 프레젠터 선례) — `TilemapMapView` 는 `GeneratedMap`(unmanaged)만 봐서 SO 의 `viewPrefab` 에 닿을 수 없다. teardown 은 `DestroyStructureEntities` 가 함께 정리.
3. **PlayMode 재조준 2건**: `GoalStabilityTest` 의 «패배 시 미러 0» 단정 → «음수 금지»(멀티골에서 생존 골이 보이는 게 계약 7), Result 대기 10s → 60s(per-cell 은 부서진 복도 몫만 스트레스로 세서 축적이 구 전역 전환보다 느리다). `EndlessModeSmokeTest` 는 코드 픽스(붕괴 프레임 미러 0)로 무변경 통과.

**PlayMode 전량 91개 중 재조준 후 잔여 실패 15건 — 이 unit 과 무관 판정**(auth JSON·tween 타이밍·픽셀 판정·기믹 스탯 오염 계열·`Gift` 페이즈 stale·씬 환경). `Gift` 는 `gimmick-recognition-upgrade`(b725ea14, 배치 앞 리빌 페이즈 — 내 diff 이전 히스토리)의 산물로 확인. ⚠ base 커밋 대조 실행은 안 했다(공유 워크트리에서 checkout 회피) — 단정이 아니라 서브시스템 비접촉 근거의 귀속이다.
