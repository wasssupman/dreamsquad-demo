# 6b — 존 장판 · 길막 · 장 캐리어 · 효과 타일 (조각 C · 2/4)

> 6a 가 **효과의 슬롯**을 세웠다면 6b 는 **판 위에 깔려 효과를 뿜는 것들**을 세운다. 기믹의 셈판(픽업·사직서·열기·피로)은 6b2 다 — 둘을 합치면 2,696줄이라 한 커밋이 안 된다.

## 목적

판 위에 **효과를 가진 물건**이 선다. 이 unit 이 끝나면 존 장판이 밟은 적을 태우고 느리게 하고, 길막이 길을 막다 부서지고, 아군 버프 장이 안에 선 아군을 세게 하고, 효과 타일이 그 칸에 놓인 유닛을 바꾼다 — **그리고 퇴근시키면 되돌린다**(옛 전투가 못 하던 것).

「무엇이 언제 그것을 놓는가」는 unit 7 이다 — 이 unit 은 **물건과 그 규칙**을 세우고, 놓는 구멍(투사체 페이로드 · 디버그 커맨드)을 연다.

## 경계 — 6b 와 unit 7 의 분담

| 축 | 6b(이 unit) | unit 7 |
|---|---|---|
| 존 장판 | 개체·수명·멤버십 재판정·효과 부여 | 카드/스킬이 **어디에 까는가** |
| 길막 | 개체·체력 감소·부서짐 사건 | 부서질 때의 **폭발**(사망 seam) |
| 장 캐리어 | 개체·수명·아군 버프 재발행 | 회오리·포탈을 **까는 자** |
| 효과 타일 | 정의표·적용·**회수** | — (뽑기는 unit 4 완료 · 칸은 판 내내 남는다) |

⚠ 이 분담 때문에 **6b 끝에 회오리·포탈은 라이브에서 저절로 안 나타난다**(까는 자가 unit 7 이다). 빈 목록이 정상이고, 대신 **디버그 커맨드**로 전부 세울 수 있어야 한다(tools.md 6·7).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 존 해저드 | `BattleCore/World/Hazard.cs`(개체) · `BattleWorld.Hazards` 목록 + `SpawnHazard`/`DestroyHazard` · `Phases/FieldPrepPhase.cs` 끝(수명 → 멤버십 → 부여 — 옛 `HazardLifetimeSystem`(1) · `ZoneApplySystem`(5) 둘 다 **이동 앞**) |
| 길막 | `Phases/TickProjectilePhase.cs` 끝(체력 감소) — 개체는 **이미 있다**(`UnitKind.BlockingHazard`, `TickProjectilePhase.ResolveSpawnBlocker`) |
| 장 캐리어 | `World/FieldCarrier.cs` 확장(수명 · 아군 버프 재발행) · `TickProjectilePhase` 끝(캐리어 수명 — 옛 `EffectTickSystem.cs:20` 이 `[UpdateAfter(MovementSystem)]` 라 **이동 뒤**다) |
| 효과 타일 | `Match/EffectTileDef.cs` + `MatchDefinition.EffectTiles[]` · `Owners/PlacementService.cs`(배치 시 적용 · **퇴근·재배치 시 회수**) |
| 정의표 | `Match/HazardDef.cs`(모양·반경·수명·효과 배열) · `Match/BlockingHazardDef.cs` · canonicalize |
| 커맨드 | `Match/Command.cs` 에 `DebugSpawnHazard`·`DebugSpawnBlocker` |
| 사건 | `CoreEvent` **44~47**: `HazardSpawned`·`HazardDestroyed`·`FieldSpawned`·`FieldDespawned` · 트레이스 채널 **42~45**(사건 42·43 = 6a `DotCleared`·6a2 `ImbueChanged`, 트레이스 40·41 이 그 둘 — 그 다음 번호부터 append) |
| 테스트 | `Tests/EditModeCore/`: `HazardZoneTests`·`BlockingHazardTests`·`FieldCarrierTests`·`EffectTileTests` |

## 구현

1. **존은 개체이고 멤버십은 스냅샷이 아니다.** 매 틱 재판정한다 — 들어온 적도 걸리고 나간 적은 풀린다. 판정은 **연속 원**이다(옛 셀 해시는 `distance-based-range` unit 19 가 이미 은퇴시켰다, 리포 grep 0건). **제약 13 — 존은 「자리에 떨어지는 것」**이라 원점 항이 **칸 반폭**이고 유닛의 몸은 안 붙는다: `SkillMath.ReachFromCell(dx, dz, 반경, 피해자 몸)`. 반경 매핑은 저작 모양이 정한다 — `SingleCell→0` · `Square3x3→1` · `RadiusSquare→max(1, r)`, 그리고 **음수 = 존 효과 없음**(센티널, 0 으로 바꾸면 한 칸 존이 전부 켜진다, F18).
2. **존의 「지속시간」은 위에 서 있는 동안 매 틱 갱신된다**(F17). 저작 0.2초는 **「나가면 0.2초 뒤 꺼진다」**는 뜻이지 총 지속이 아니다 — 총 지속으로 읽으면 장판이 즉시 꺼진다. 총 수명은 개체의 `Lifetime` 이 따로 든다.
3. **존의 감속은 군중 제어가 아니라 이동속도 모디파이어다.** 저작은 `CcKind.Slow` 토큰이지만 런타임은 6a 의 스탯 슬롯으로 간다: `StatKind.MoveSpeedMul` · `CombineOp.Multiplicative` · `origin = ModifierOrigin.Zone` · 지속 = `restDuration`(`ZoneApplySystem.cs:108`). 그래서 **장판을 나가면 `restDuration` 만큼 뒤에 원속도로 돌아온다** — 슬롯을 즉시 지우는 것이 아니다.
4. **존이 만드는 지속 피해의 출처는 언제나 「장판」이다**(F16). 저작하는 것은 `DotElement` 뿐이고 `DotOrigin` 은 코드가 `Zone` 으로 박는다 — 출처를 저작으로 열면 장판 화염과 스택 화염이 한 슬롯을 덮어 6a 구현 12 의 과피해가 재현된다.
5. **대상 통행층은 저작값이 아니라 런타임 스냅샷이다**(F15). 저작은 항상 0(= 필터 없음)이고 **까는 자가 덮어쓴다**. 저작 축으로 오해하면 장판이 아무에게도 안 먹는다.
6. **존의 진영 축을 연다**(F34 · **제약 8**). 옛 전투는 `Faction.EnemyUnit` 하드 게이트였고 CLAUDE.md 제약 8 이 그것을 **「축 없이 하드코딩해 둔 같은 실수의 반대편」**으로 명시 지목했다. `HazardEffect.targetFactions`(비트) 를 저작으로 열되 **라이브 저작은 오늘과 같은 값**(적만)이라 판은 안 바뀐다. 동시에 대상 자격은 6a 의 같은 술어를 지난다(`EffectEligibility` — 거점 면역 · **방패 걸린 마음의 광역 제외**, unit 4 이월).
7. **겹친 장판의 승자를 순회 순서에 맡기지 않는다**(F23). 같은 스탯에 여러 장판이 겹치면 **가장 강한 값으로 못박는다** — 만료가 슬롯 순서를 런타임에 뒤섞기 때문에 순서에 맡기면 승자가 무작위가 된다. 리스트로 바뀌어도 규율은 같다.
8. **아군 버프 장의 재발행 주기는 상수를 베끼지 않는다**(F36). 옛 `0.5초` 의 근거는 Unity `Maximum Allowed Timestep`(0.3333) 이었고 — 규칙은 **「재발행 지속 > 최대 틱 델타」** 다. 고정 틱 1/60 에서 그 근거가 바뀌므로 값을 **다시 산출**하고 정의표에 싣는다. 주기를 굳이 남기는 이유는 「나가면 곧 풀린다」의 여유 시간이 그 값이기 때문이다.
9. **드림캐쳐 오라와 아군 장판은 형이 다르다**(제약 13). **오라 = 몸에서 나오는 것**(부착된 숙주의 `HitRadius` 가 원점 항) · **아군 버프 장판 = 자리에 떨어지는 것**(칸 반폭). 둘을 한 자로 재면 부착 오라가 반 칸 좁아지거나 장판이 숙주 몸만큼 넓어진다. 도발도 몸형이다.
10. **길막의 문은 「부서짐」 하나다.** 시한 만료 경로는 은퇴했다. 체력 ÷ 초당 감소 = **아무도 안 때렸을 때의 수명(초)** 이 저작 감각이고(F11 — 주석에만 있던 것을 정의표 주석으로), 두 값을 따로 굴리면 수명이 통째로 달라진다. 폭발 피해는 저작했는데 폭발 탄이 미배선이면 옛 전투는 **0번 탄 비주얼이 한 프레임 번쩍였다**(경고만) — 새 코어는 `-1` 센티널 + `MatchDefinitionBuilder` loud 거절이다(F12). 실제 폭발 발사는 unit 7 의 사망 seam.
11. **효과 타일은 회수가 있다.** 6a 구현 4 의 슬롯 삭제가 F33 을 푼다 — 배치 시 `SlotTag.Tile` 로 걸고 **퇴근·재배치 시 그 키로 지운다**. 옛 전투는 회수 경로가 없어 「개체당 1회」로 봉인됐고, 재배치하면 새 칸 효과는 붙는데 옛 칸이 안 풀렸다. ⚠ **효과 타일 1회 마킹과 배치 스킬 1회 마킹은 절대 공유하지 않는다**(F19 — 재배치가 배치 스킬을 재무장시키기 때문). 같은 스탯 중복 저작은 마지막만 남는다(저작 규칙). 칸 목록의 주인은 **뽑는 자**(`PlacementService.ArmedEffectTiles`, unit 4 완료 · 판 내내 불변)이고 — 장부의 「새 주인 = `MapRuntime.EffectTiles`」는 unit 4 가 이미 다르게 답했으므로 그 3행을 **정정**한다(잔량은 안 준다, 이미 배정된 행이다).
12. **디버그는 커맨드로 넣는다.** 메뉴가 담당자를 직접 부르면 하네스·리플레이에서 같은 길을 안 탄다(tools.md 원칙). 도구 자체(메뉴 UI)는 6c.
13. **결정론.** 해저드·장 캐리어 목록 순회는 `SimEntityId` 오름차순.

## 파이프라인 커버리지

`object-pipeline-map.md` 전면 재작성은 unit 8 이고, 여기서는 **이 unit 이 여는 정거장**만 적는다. 뷰 열은 6c 가 채운다.

| 정거장 | 존 해저드 | 길막 | 장 캐리어 | 효과 타일 |
|---|---|---|---|---|
| 저작 | `HazardSO`(무변) | `BlockingHazardSO`(무변) | 카드·스킬 SO | `EffectTileData`(무변) |
| 정의표 | `HazardDef` | `BlockingHazardDef` | `HazardDef` 공유 | `EffectTileDef` |
| 생성 | `World.SpawnHazard` → `HazardSpawned` | `TickProjectilePhase`(기존) → `UnitSpawned` | `FieldSpawned` | N/A — 칸이라 개체가 없다 |
| 매 프레임 | 수명 → 멤버십 → 부여(이동 앞) | 체력 감소 | 수명(이동 뒤) · 아군 버프 재발행 | N/A — 배치·퇴근 엣지에서만 |
| 소멸 | `HazardDestroyed` | `UnitDestroyed`(기존) | `FieldDespawned` | 슬롯 회수 |
| 뷰 | 6c | 6c(오버헤드 게이지 포함) | 6c(생산자는 unit 7) | 5b 오버레이 + 6c |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| **캐스터 4기 + 캐스트 기계** | 제거 확정(계약 9 · rev 3 §5). `HazardCastState/System/Kind` · `CastEvents` · `SkillSeam.Cast` · `HazardCastAbility` · `CastHazardSkill`. 그래서 **「자는 캐스터가 계속 시전한다」(F32)의 판정 자체가 사라진다** | 제거 |
| `HazardSingleton.cellToEffects` 멀티해시 · `HazardCellsBuffer` · 길막 **시한 만료** | 앞의 둘은 `distance-based-range` unit 19 가 이미 은퇴시켰고(리포 grep 0건) 판정이 연속 원이라 규칙을 증언하지 않는다. 시한 만료는 문을 「부서짐」 하나로 좁히면서 은퇴했다 | 제거(선행) |
| 존의 `Faction.EnemyUnit` **하드 게이트** · `explodeProjectile` 미배선 **fail-silent** | 앞은 데이터 축으로(구현 6, 라이브 저작이 같아 판은 안 바뀐다), 뒤는 `-1` 센티널 + 빌더 loud 거절로 승격 | 완료 · 제약 8 · F12 |
| **감속장 스냅샷**(`ApplySlowField`) | 「안에 있는 대상이 영향을 받는다」 원칙의 **마지막 예외**다. 예외를 옮길지 없앨지는 생산자(액티브 카드)가 오는 unit 7 에서 정한다 — 여기서 정하면 소비처 없는 결정이 된다 | 보류 · unit 7 · F35 |
| 회오리·포탈을 **까는 자** · 길막 **부서질 때의 폭발** | 「경계」 표의 unit 7 열. rev 3 이 바인딩으로 환원했고, 여기서 또 세우면 unit 7 이 그것을 걷어내야 한다 | 보류 · unit 7 |
| `MapRuntime.EffectTiles`(장부의 제안 주인) | unit 4 가 뽑기를 `PlacementService` 에 두면서 다르게 답했다. **칸 목록의 주인은 뽑는 자**이고 맵은 그 칸을 모른다 | 완료(장부 정정) |
| 존 효과 `Impulse`(넉백 토큰) | 옛 `ZoneApplySystem` 은 벡터 `0` 으로 실었다 — **방향이 없는 넉백**이라 밀지 않는다(C8 「방향을 모르는 대상은 밀리지 않는다」). 라이브 장판 저작 9종 중 이 토큰을 쓰는 것이 없다. 방향 있는 장판 넉백이 필요해지면 저작에 방향 축부터 연다 | 제거 · C8 |
| 길막 폭발 탄의 **탄 표 편입** | 폭발 탄은 공격 표 밖이라 줄 번호가 없다(`ExplodeProjectileDefIndex = -1`). 발사도 편입도 사망 seam 이 여는 unit 7 의 것 — 여기서 편입하면 소비처 없는 줄이 선다 | 보류 · unit 7 |
| 아군 버프 장의 재발행 여유 **0.5초** | 근거(엔진 최대 프레임 델타)가 고정 틱에서 사라졌다(F36). 틱 3개(0.05초)로 재산출 — 「나가면 곧 풀린다」의 체감이 바뀌지만 생산자가 unit 7 이라 라이브 영향 0. 체감을 되돌리려면 저작 필드(`FieldCarrier.RefreshSeconds`)다 | 변경 · F36 |

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 어디 | 출처 |
|---|---|---|
| **효과 타일 개수가 정의표에 안 실렸다** — `EffectTileCount` 를 아무 빌더도 안 채워 라이브 3칸이 새 코어에서 0 이 됐다 | `BoardEffectDefinitionBuilder.FillEffectTiles`(시즌 맵 테마의 `effectTiles`·`effectTileCount`, 스테이지 `suppressEffectTiles` 존중) ← `BattleDriver` 가 `SeasonRuntime.Active.mapTheme` 을 넘긴다 · `BoardEffectAuthoringTests.효과_타일_개수와_종류가_시즌_맵_테마에서_실린다` | 2026-09-24 드리프트 감사(리드 배정) |
| 효과 타일 **종류 배정**이 없었다(칸만 뽑고 무엇인지 몰랐다) | `EffectTileSelect.AssignKinds`(옛 `seed ^ 0x7EFFEC7` 칸마다 난수 그대로) · `PlacementService.EffectTileKindAt` | 옛 `BattleBridge.cs:1500-1508` |
| 장 목록을 아무나 고칠 수 있었다(`List<FieldCarrier>` 공개) — 장만 소멸 사건 없이 사라질 수 있었다 | `BattleWorld.Fields` → `IReadOnlyList` + `SpawnField`/`DespawnField` 두 문(계약 7) · `MovementRulesTests` 두 곳이 문을 지나게 고쳤다 | 계약 7 |
| 길막이 **자기 칸 하나**만 막았다(3×3 바위도 한 칸) · 몸 반경이 빌더 상수 0.5 | 정의 줄이 있으면 `SpanRadius` 만큼 `BlockRect`(unit 2 경로) · 몸 = 막는 칸의 내접원(`StructureSize.BodyRadius`) | 옛 `EffectSpawner.SpawnBlockingHazard`(반경 1 샘플) |
| **효과 타일을 놓는 순간 칸이 사라졌고 footprint 전 칸으로 판정했다** — unit 4 가 「개체당 1회」를 「칸 1회 소비」로 오독 | 칸은 판 시작에 뽑고 **판 내내 불변** · 판정은 **앵커(대표 칸)** 하나(`PlacementService.ArmTileFor` → 활성화 엣지 적용 → 퇴근 회수) — 그래서 퇴근 뒤 같은 칸에 놓은 유닛이 다시 받는다 · `EffectTileTests.증상_퇴근_뒤_같은_칸에_다시_놓으면_다시_받는다` · `증상_앵커가_아닌_칸의_타일은_안_받는다` · `MatchPlacementTests.효과_타일_칸은_판_내내_남는다` | 옛 `BattleBridge.cs:297·1492·9078`(칸 제거 경로 0) · `:9126-9131`(개체당 가드) · `:7867`(대표 칸, defender-footprint unit 1) |
| 탄 착탄 길막이 **자리 검증 없이** 섰다(골 칸·막힌 칸·방어유닛 위) | `BlockerSpawn.TrySpawn` 한 문 — 탄·디버그가 같이 지난다 | 옛 `ValidateCellsForBlockingHazard` |

## 완료 기준

- [x] **EditMode 코어 lane 초록** + 새 테스트 4묶음: `HazardZoneTests`(칸 반폭 자 · 모양→반경 3매핑 · 음수 = 효과 없음 · `restDuration` 은 나간 뒤부터 · **감속이 스탯 슬롯으로 간다** · 진영 축이 오늘 저작으로 옛 결과와 같다) · `BlockingHazardTests`(체력÷감소 = 무간섭 수명 · 문은 부서짐 하나) · `FieldCarrierTests`(겹치면 가장 강한 값 · 재발행 주기 > 틱 델타 · 수명은 이동 뒤에 깎인다) · `EffectTileTests`(배치 적용 · **퇴근 회수** · 두 마킹 비공유).
- [x] **증상 단언 2건**: ⑴ 존 장판 위에 선 적의 체력이 **초당 저작값만큼** 준다(나가면 멈춘다) ⑵ 효과 타일 칸에 놓은 유닛의 공격력이 **오르고, 퇴근시키면 돌아온다**.
- [x] 디버그 커맨드 2종이 헤드리스 하네스에서 동작(`CommandSchedule` 로 예약 → 개체가 선다). **메뉴 UI 없이** 커맨드만으로 검증된다.
- [x] **틱 phase 수 무변** — 새 단계를 안 만들었고, 존은 `FieldPrepPhase` 끝 · 캐리어·길막은 `TickProjectilePhase` 끝이다(각각 옛 캡처 위치 1·5 와 27 을 따른다). ⚠ 존·아군 장은 `FieldPrepPhase` 안에서 **지속 피해 틱 앞**이다 — 옛 `ZoneApplySystem`(5)이 `DotApplySystem`(16)보다 앞이라 장판이 건 지속 피해는 같은 틱에 첫 지급이 났다(F7). 문자 그대로 「끝」에 두면 한 틱 밀린다.
- [x] `ledgers/rules.md` **F11·F12 · F15~F19 · F23 · F32~F36** 이 코드 포인터로 매핑.
- [x] `ledgers/bridge-methods.md` 미정 **51 → 46**: `RegisterBlockingHazardSO/1` · `RegisterZoneHazardSO/1` · `RecordHazardSpawn/2` · `RecordBlockingHazard/4` · `RecordBlockingHazardDestroyed/2` 가 닫힌다. ⚠ 효과 타일 3행(`AddEffectTile`·`ApplyEffectTileIfAny`·`ApplyEffectTileOnce`)은 **이미 배정된 행의 주인 정정**이라 잔량을 줄이지 않는다.
- [ ] **골든 체크박스를 여기서 들지 않는다** — 정의표가 6b2 에서 한 번 더 바뀐다. 재굽기는 6b2.
- [ ] `core-reviewer` APPROVE — **매니저 0**(`HazardManager` 없음 · 해저드는 `BattleWorld` 의 목록이고 규칙은 phase 가 든다) · **값 하드코딩 0** · 제약 13 진입점만 호출(인라인 거리 계산 0건, grep 으로 확인).

확인 2026-09-24 — 헤드리스·Unity EditMode 코어·Assets·PlayMode 코어 lane(수치는 커밋 보고). 골든 무변(재굽기 없음). 커밋 해시는 리드 재검증 뒤 기록.
