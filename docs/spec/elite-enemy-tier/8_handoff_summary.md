# 8 — 인계 요약 (elite-enemy-tier)

> 상태: **완료 2026-08-13**. 최신 계약은 [README](README.md) 와 번호 문서가 우선한다.
> 이 문서는 다음 작업자가 커밋과 위험 지점을 빨리 찾기 위한 지도다.

## Commit

| 해시 | 제목 |
|---|---|
| `b59f7cfa`·`814f0ed3` | spec 신설 + 리뷰 반영(과설계 1건 철회) |
| `1588a997` | unit 0 — `EnemyTier` 신설 + `BossTag` 유도 분리 |
| `bb74b476` | unit 5 — 분열(`SplitOnDeath`) + 적 엔티티 조립 분해 |
| `a4e8f26f`·`12923018` | unit 6 — 슬라임 에셋 + 2단계 분열 + 보상 불변식 |
| `ceb36396` | 투트랙 리뷰 반영 — 셀 이탈 버그 + 거짓 보증 테스트 |
| `a1025d45` | unit 3 — 적에게 `AttackN` 개방 |
| `903936d0` | unit 4+7 — 화염 브레스(콘 페이로드 + 에셋) |
| `c3f29e3d`→`ef78f937` | 드래곤 스케일·facing·브레스 VFX 재작성 4연속 |
| `b7750a4b` | **연출 소유권을 `VfxSpawner` 로 이관**(사용자 지적) |
| `feda9054` | `targetFactions` 저작 제거 — 신규 4종이 방어 본능을 못 때리던 구멍 |
| `da2261ae` | 리팩토링 — 손수 복제한 두 자리를 정본으로 |
| `66004836` | 밸런스 상향(사용자 튜닝) |
| `60339fa7` | 콘 세 술어 커버리지 |

## Implemented

- **등급 축** — `EnemyTier{Normal,Elite,Boss}`. 보스 특권(`BossTag` = CC·어그로 면역 + 등장 경보 + `ThreatEntry`)이 `nightmareMechanics` 유무가 아니라 **tier 에서만** 나온다. 그래서 «메커닉을 가진 비보스» = 엘리트가 성립한다.
- **분열** — `OnDeath × SplitOnDeath`. 슬롯도 이벤트 필드도 sim 변경도 없이 **브리지 킬 드레인이 SO 를 직독**한다. 2단계(1→2→4)까지 출하, 사슬 예산은 `SplitChain`(깊이 8 · 총 32)이 bake 에서 검증.
- **콘 브레스** — `AttackN(3) × AreaBreath`. 즉발이고 대상 상한이 없다. 판정은 `TileAoe.IsInCone`(제곱 비교, `normalize` 없음), 저작 도→`cosSq` 변환은 **bake 1회**.
- **드래곤** — Air 통행층 + `flightLift`, 화염 스택(Kindler 와 `StackModifier_Fire` 공유), 브레스.
- **연출** — 원샷 VFX 는 `VfxSpawner.SpawnAreaBreath` 가 소유(프리팹 슬롯·스폰·정렬·수명). 브리지는 뷰 앵커만 풀어서 위임.
- **테스트 하네스** — `BattleBridgeTestAccess` 가 e2e 의 리플렉션 스폰을 한 자리로.

## Key Files

- `Data/EnemyTier.cs` · `Data/SplitChain.cs` · `Battle/Combat/TileAoe.cs`
- `Bridge/BattleBridge.cs` — `CreateEnemyEntity` / `SpawnSplitChildren` / `BakeNightmareMechanics` / `ConeCosSq`
- `Battle/Combat/AttackSystem.cs` — RESOLVE arm 의 `AreaBreath` 분기 + `ApplyConeBreath`
- `Presentation/VfxSpawner.cs` `SpawnAreaBreath` · `BoardSortOrder.AreaBreathOrder`
- `Tests/EditMode/ConeBreathPredicateTests.cs` · `Tests/PlayMode/BattleBridgeTestAccess.cs`

## Verified

EditMode **2,347 / 실패 4** — 전부 미개편 맵(Coil·Twin·Spiral·Zig)의 폭 계약이며 이 spec 과 무관하다(`map-rework` 착수 신호). PlayMode 분열 사슬·드래곤·Kindler 화염 **5/5**. 콘솔 에러 0. 사용자 Play 확인 2026-08-13.

## Notes — 되돌리면 안 되는 것

1. **`targetFactions` 를 적 에셋에 굽지 말 것.** 0(미저작)이어야 기본값 변경을 따라간다. 기존 적 복제로 신규 적을 만들면 `13` 이 묻어오고 **방어 본능을 못 때린다** — 이 spec 의 신규 4종이 정확히 그렇게 태어났다(`feda9054`). 가드 = `AuthoredTargetMaskTests`.
2. **콘 순회의 세 술어를 지우지 말 것.** `AttackSystem` 후보 배열은 **전 진영 통합 풀**이다(초판 스펙이 반대로 적었다). 진영·통행층·자기 제외가 없으면 드래곤이 동료와 적 마음을 태운다. `ConeBreathPredicateTests` 가 고정.
3. **분열 자식은 부모의 «셀 중심» 에서 오프셋한다.** 연속 좌표 기준이면 `MovementCellTrim` 허용치와 겹쳐 자식이 인접 셀에 태어나고, 그 셀이 골이면 «처치했는데 유출» 이 된다(리뷰 H1).
4. **`BakeNightmareMechanics` 의 보스 부착은 `AddBuffer<DcTriggerSlot>` **앞**에 있어야 한다** — 그 핸들이 "마지막 AddBuffer" 전제로 캐시된다.
5. **브레스 반각 ≥ 90 은 bake 가 거절한다.** `cos²θ = cos²(180−θ)` 라 조용히 (180−각) 콘이 된다.
6. **`StackModifier_Fire` 는 Kindler 와 공유한다.** `_stackThresholds` 가 StackKind 당 규칙 한 벌이라 드래곤만 분리할 수 없다. 화염 수치를 만지면 Kindler 도 같이 움직인다(`66004836` 에서 4→10 동반 상향, 사용자 승인).
7. **연출 소유권은 `VfxSpawner`.** 브리지에 프리팹 슬롯을 되돌리지 말 것 — `DragonBreathE2ETest.BreathVfxPrefab_IsWiredOnVfxSpawner` 가 회귀를 잡는다. 루프형 벤더 프리팹의 단발화는 `ConfigureOneShot` 이 하고 **공유 에셋은 건드리지 않는다**.

## Follow-up

- ~~라이브 덱 풀 등록~~ — **완료**. `wave-concept-blocks` unit 7(`2712aa01`)이 라이브 덱 7종 전부에 넣었다(이 spec 종료 직후).
- ~~밸런스 실전 검증~~ — **완료 2026-08-13**. `66004836` 의 수치로 확정. 다만 이 값들의 회귀는 **테스트가 잡지 못한다**(단언이 전부 상대·구조형 — 의도된 것).
- ~~브레스 지속 콘 전환~~ — **하지 않기로 함**(사용자 결정 2026-08-13). 비용 조사 결과는 백로그 항목에 남겼다 — 재론 시 다시 파헤치지 말 것.
- 나머지 후보는 `docs/spec/README.md` 의 **Follow-up Backlog → 엘리트 등급 적** 그룹으로 이관했다.

**이 spec 은 여기서 끝난다.** 남은 항목은 전부 다른 spec 이나 후속 판단의 몫이다.
