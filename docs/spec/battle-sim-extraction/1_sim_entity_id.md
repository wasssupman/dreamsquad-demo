# 1 — SimEntityId: 매치 내 stable ID 도입

## 목적

`Entity.Index/Version`이 ① 타겟팅 동률 tiebreak(`NearestTargeting`·`FrontmostTargeting`·`LowestHealthTargeting`·`AggroTargeting`)과 ② **발사 패턴 RNG seed**(`AttackSystem`: `math.hash(int2(attackerEntity.Index, fireCountBase))`)에 직접 쓰인다. Entity 번호는 할당 순서 산물이라 신 sim(M1)에서 재현 불가 — 골든(unit 4) 생성 **전에** 매치 내 비재사용 `SimEntityId`(spawnOrdinal)로 축을 통일해야 A/B parity가 성립한다. 커맨드·이벤트·스냅샷·뷰 키의 유일 축이 될 ID이기도 하다.

**의도된 행동 변경**: 동률 해소 결과와 랜덤 탄막 시퀀스가 현재와 달라질 수 있다(규칙은 동일, 동률 승자·난수열만). 골든은 이 unit 이후를 기준선으로 삼는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/SimEntityId.cs` — `IComponentData { int value }` (매치 시작 0부터 스폰 순 발급, 재사용 없음)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `AttachSimEntityId` 헬퍼(단일 카운터) + 스폰 경로 부착: 적·방어·순찰·투사체·존해저드·차단해저드·장애물. 카운터 리셋은 **`BeginPlacement`**(`_dcInstanceCounter` 와 같은 매치 경계 — 배치 페이즈가 defender 를 먼저 낳으므로 StartBattle 리셋은 오답). **ECS 내부 ECB 생성에는 부착하지 않는다**(2026-08-04 전수 조사 정정) — 전부 1프레임 staging carrier(브리지 드레인이 파괴, 투사체 실체는 `SpawnProjectile` 에서 발급)이고, 영속 예외인 Pickup/사직서·필드 캐리어 3종(버프장판·토네이도·포탈)은 타겟팅 무관이라 미부여. unit 4 트레이스 축 설계 시 필요해지면 그때 승격(카운터의 ECS 싱글턴화 필요).
- `Combat/NearestTargeting.cs` · `Combat/FrontmostTargeting.cs` · `Combat/LowestHealthTargeting.cs` — tiebreak 축을 `entityIndex/Version` 2필드 → `simId` 1필드로 교체
- `Combat/AggroTargeting.cs` — **tiebreak 신설**(교체 아님 — 2026-08-04 구현 중 정정: 이 유틸엔 Entity tiebreak 자체가 없었고 등거리 동률을 후보 배열 순서 = 청크 스냅샷 순서가 결정하고 있었다). `AggroCandidate.simId` 추가 + 등거리 시 simId 낮은 쪽
- `Combat/ThreatTable.cs` — `Leader` 의 `attacker.Index` 동률을 병렬 `simIds` 배열로 교체(alive 배열과 같은 caller-해소 패턴. 현재 런타임 소비자 0 — boss-jjangssen unit 4 가 blink 정책 교체 — 이라 테스트만 갱신)
- `Combat/AttackSystem.cs` — `PatternShotRandomizer.Apply` seed를 `SimEntityId` 기반으로
- `Effects/HazardCastSystem.cs` — 최근접 타겟 선택(라인 83-88)에 tiebreak **신설**(현재 부재 — 동률 지점 중 유일한 무-tiebreak 타겟팅)

## 구현

발급은 Bridge 스폰 지점에서만(단일 카운터). `Entity.Index` 사용처는 위 목록이 전부인지 `grep -rn "\.Index" Battle/`로 전수 확인 후 교체. EditMode 타겟팅 테스트의 기대값을 새 tiebreak 축으로 갱신.

**허용 예외 목록 (grep 전수 확인 2026-08-04)**:
1. `SimEntityId.Resolve` 폴백(`e.Index`) — Bridge 미경유 조립 월드(테스트 rig) 전용. 라이브 스폰은 전부 부착.
2. 뷰/디버그: `Presentation/QuadUnitViewPool.cs`·`SpineUnitPool.cs`(GO 이름 접미사), `BattleBridge.cs` 해저드 런타임 로깅(`target_index`).
3. `BattleBridge.TryPickNearestEnemy` 의 거리 동률 `Entity.Index` — **입력 측**(스킬 캐스트 대상 선택 = 커맨드 구성)이라 sim 결정론 무관. M1 커맨드화 때 재검토.

## 완료 기준

- compile + EditMode 타겟팅/랭킹 테스트 통과(기대값 갱신 포함).
- 같은 배치 시나리오 2회 Play에서 타겟 선택·탄막 로그 동일(스폰 순서가 같으므로 ordinal 동일).
- sim 로직의 `Entity.Index/Version` 직접 사용 잔존 0건(허용 예외 목록 명시).

> 진행 기록 2026-08-04: 구현 완료 — `SimEntityId` + Bridge 카운터/부착 7경로, 랭킹 3종 축 교체,
> Aggro·HazardCast 동률 **신설**, 발사 RNG seed 교체, ThreatTable simIds 화. **EditMode 전체
> 1,859건 중 실패 0**(1,857 통과/기존 skip 2) — unit 1 관련 26건 green, `AggroTargetingTests` 신설.
> grep 전수: Entity.Index 잔존 = Resolve 폴백 1곳(예외 ①). 잔여 = 같은 배치 2회 Play 로그 동일
> 확인(unit 0 smoke 와 함께 — unit 2 하네스가 이 확인의 상위 호환이기도 하다).
