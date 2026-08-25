# skill-layer-migration — 모든 스킬을 레이어 위로

> 상태: **작성 완료 2026-08-25 · 미착수. 선행 = `docs/spec/skill-layer-foundation/` 전체.**
> 설계 근거: [`docs/plans/2026-08-24-skill-layer-unification-critic.md`](../../plans/2026-08-24-skill-layer-unification-critic.md)

## 상위 목표

**이 spec 이 끝나면 이 게임의 모든 스킬이 하나의 레이어 위에 있다.**
토대(`skill-layer-foundation`)가 지은 포트·디스패처·레지스트리 위로 현존 스킬 전량을 옮긴다.
옮기고 나면 legacy 어휘 3개(`DcPayloadKind` arm · `OnPlaceEffectType` · `SkillEffectType`)와
그 실행 arm 이 죽는다.

## 검증 질문

> **스킬을 하나 새로 만들 때 손대는 곳이 «concrete 1개 + 저작 SO 1개» 인가?**

오늘은 새 스킬 하나가 **switch 4곳**(부착 자격 · bake · arm · 문안)을 갱신하게 만든다.
그 넷이 하나로 접히는 것이 이 spec 의 성립 조건이다.

부수 질문: **보스 스킬과 방어유닛 배치 스킬이 같은 `ISkill` 목록에 섞여 있는가?**

## 이전 우주 (census ~75행)

| # | 가족 | 실측 | 문서 |
|---|---|---|---|
| 0 | 적 mechanics | **11행** (보스 10 + 드래곤 `AreaBreath` 1). 분열 2행 제외 — 아래 | [0](0_enemy_mechanics.md) |
| 1 | 방어유닛 규칙(`UnitSkillAbility`) | **5행/5에셋** — SkyStrike · Taunt · AreaShield · OnPlaceBlast · BombMan | [1](1_defender_rules.md) |
| 2 | 레거시 `OnPlaceEffectType` | **arm 9종/12에셋** | [2](2_legacy_onplace.md) |
| 3 | 드림캐쳐 카드 — 슬롯 arm | **26행/25장** | [3](3_cards_slot_arm.md) |
| 4 | 드림캐쳐 카드 — 즉발 · hand-op | 즉발 5행 + `RecallAttachedToFront` | [4](4_cards_immediate_handop.md) |
| 5 | 캐스트 계열 | **8에셋** — 하자드4 · 실드1 · 볼리2 · 폭탄1 | [5](5_casts.md) |
| 6 | 소환 (`SummonPatrolAbility`) | 1에셋 + 전용 시스템 3 | [6](6_summon.md) |
| 7 | **액티브 (`SkillData`)** | **6에셋 — 전부 라이브** | [7](7_actives.md) |
| 8 | 철거 | legacy enum · arm · flat 필드 · 화이트리스트 | [8](8_teardown.md) |
| 9 | 인계 | | 9_handoff_summary.md |

각 가족이 **독립 커밋**이다. 어디서 멈춰도 게임이 돈다 — 토대가 깐 베이크된 `skillId`
라우팅 축(0 = legacy)이 그것을 보장한다.

## Feature-wide 계약

1. **토대의 계약 12개를 그대로 상속한다.** 특히 계약 1(도메인은 ECS 를 모른다) ·
   3(쓰기는 소유 맥락 채널만) · 7(드레인 3지점) · 8(이벤트 = 값 스냅샷).
2. **가족 하나 = 커밋 하나 이상. 가족 중간에 멈추지 않는다.**
   한 가족을 옮기는 동안 그 가족의 legacy arm 은 살아 있고, 가족이 끝나면 같은 커밋에서 죽는다.
3. **특성화 그물이 가족마다 선행이다**(토대 계약 11 의 연장). 그물 없는 행은 옮기지 않는다.
4. **`DcTriggerKind`/`DcPayloadKind` 의 기존 값을 재번호·은퇴하지 않는다.**
   시트가 enum **값**으로 왕복한다(`Data/StatImport/DcSheetImportDto.cs`). append-only 유지.
5. **카드 authoring 과 시트는 무변경.** 카드 mechanics 는 시트가 덮는 유일한 경로
   (`DcSheetApplier.OverlayMechanics`)라 어댑터로 흡수한다. **이 spec 의 시트 손실은 0.**
6. **화이트리스트 철거는 해당 가족 이전이 끝난 뒤.** 먼저 열면 legacy enum 경로인 payload 와
   개방된 트리거×주체 조합이 공존하는 창이 생긴다.
7. **분열(`SplitOnDeath`)은 옮기지 않는다** — 이미 끝점의 성질을 옛 경로로 충족한다.
   아무 적 에셋에 payload 한 줄이면 코드 0줄이다. (「슬롯을 안 쓴다」는 현황 서술일 뿐
   구조적 불가가 아니다 — 디스패치가 managed 라 `SpawnUnitIntent` 로 표현은 가능하다.
   옮길 이유가 없어서 안 옮기는 것이다.)
8. **기믹 SO 4개는 범위 밖** — `Burnout`(불금) · `RedBull`(먹고 달리자) · `ClockOut`(사직서) ·
   `Onsen`(온천). 「스킬」 정의에서 정의 수준으로 제외했다(토대 README 결정 기록).
   ⚠ critic 은 앞 둘을 「과로」 하나로 묶었으나 **`BattleConfig` 활성화 단위로는 독립 2개**다(census 실측).
   이관 가능 판정분이 있어도 여기서 하지 않는다.
9. **공격 출력 수식자는 범위 밖** — 토대 README 결정 기록의 5종.
10. **골든/기준선은 이 spec 의 일이 아니다.** `battle-sim-extraction` 이 착수될 때 그쪽이 정한다.

## 파이프라인 커버리지

**N/A** — 이전되는 arm 이 만드는 오브젝트(투사체 · 장판 · 소환물 · 존 캐리어)는 전부 기존
파이프라인을 그대로 탄다. 신규 플레이 오브젝트도 생성→렌더 경로 변경도 없다.

## 알려진 잔여 리스크 (투트랙 리뷰 2026-08-25)

- ~~`ExecutedCount` 가 「하나라도」만 증언한다~~ → **해소.** seam 별 카운터
  (`ExecutedCountOf(SkillSeam)`)와 경계 seam 전용 그물을 넣었다. 짱쎈놈은 경계 슬롯만
  넷 들어(자폭·도약×2·궁극기) 주기 슬롯이 없으므로, 그 단언이 경계 seam 만 본다는 것이
  **구조적으로 보장**된다.
  ⚠ **공격 seam(`SkillSeam.Attack`)은 아직 증인이 없다** — 거기로 가는 payload 가 아직
  없어서다. `AttackN` 계열을 이전하는 unit 이 그 그물을 같이 깐다.
- **이중 경로가 상존한다.** `SelfTileAoe`·`AreaSleep`·`GrantShield` 는 보스=concrete /
  카드·실드파열=legacy arm 이 **동시에 라이브**다. unit 8 철거 전까지 이 payload 들의 동작을
  고치려면 **반드시 양쪽에** 해야 한다. 파리티 테스트는 `SkillMath` 층만 고정하고 arm 로직
  층은 특성화 그물에 의존한다.
- ⚠ **unit 8 전제 — 카드 bake 는 `skillId` 를 거의 굽지 않는다.** 지금 카드 경로가 여는 것은
  `EmitProjectilePattern` 하나뿐이고(그 arm 이 이미 은퇴해서 열 수밖에 없었다), 나머지는
  전부 `LegacyArmId` 로 굽힌다. **그 상태로 arm 을 철거하면 라이브 카드 8장이 조용히 죽는다**
  (`SelfTileAoe` 7 · `AreaSleep` 1 — 2026-08-25 에셋 실측). 철거 전에 카드 bake 를 전면
  개방하는 unit 이 반드시 앞서야 한다.
- ⚠ **`SimEntityId` 없는 시전자 = 스킬 레이어 전면 침묵.** 어댑터의 핸들 역변환이 그 값으로
  풀을 스캔하므로, 없으면 시전자가 자기 자신도 못 찾고 모든 질의가 빈손이 된다. 감지도 되고
  concrete 도 불리기 때문에 `ExecutedCount` 로도 안 보인다. `BuildCaster` 가 loud warn 을
  내지만, **스폰 지점의 ID 발급 범위가 스킬 보유 아키타입을 전부 덮는지**는 별도 확인이 필요하다.
- **`order-capture.md` 에 디스패처 3계가 미등재**다(`battle-sim-extraction`). 다음 재캡처 때
  포함해야 「생산자 위치」 박제가 실제와 맞는다.
- **후보 상한 64 는 「가까운 64」가 아니라 「풀 순서 선착 64」**다(legacy `AuraPulse` 는 무상한).
  반경 안 후보가 64를 넘는 판이 실제로 생기면 그때 잘림 규칙을 계약으로 정한다.

## 어댑터가 직접 쓸 수 있는 버퍼 (2026-08-26 — ECS 리뷰 요구)

어댑터가 `_em.GetBuffer` 로 **직접 쓰는** 버퍼 목록이 늘고 있다
(`IncomingShield` → `PatternSlot` → `EmitterInstance`). 기준을 여기 박는다 —
안 그러면 unit 5~8 쯤에서 「이 한 줄만 예외로」가 반복된다.

**허용 조건 셋을 전부 만족할 때만** 어댑터가 직접 쓴다:

1. **그 버퍼의 소유 맥락이 `BattleSimGroup` 안에 있고**, 쓰는 seam 이 그 맥락의
   프레임 창 안이다(= 소비자보다 앞). 「어댑터가 유일한 번역자」 계약의 뜻이
   「도메인 대신 **소유 맥락 채널에** 쓴다」이기 때문이다.
2. **구조 변경이 아니다.** `DynamicBuffer.Add`/요소 대입까지다. 엔티티 생성·파괴·
   컴포넌트 추가는 ECB 로 간다(호스트가 재생한다).
3. **원자성이 그 자리에서 닫힌다** — 여러 버퍼를 걸쳐 쓴다면 그 사이에 실패할 수
   있는 연산이 없어야 한다(`EmitPattern` 의 카운터 전진 + 인스턴스 추가가 선례).

셋 중 하나라도 못 지키면 **큐 싱크**를 만든다(`Bind*Sink`). 그게 맥락 간 통신의
기본형이고, 직접 쓰기는 「같은 프레임 창 안의 같은 맥락」이라는 예외다.

## 후속 후보

- **무거운 arm** — 전 유닛 순회 Burst 코드 내부에 발동 지점이 있는 것들. 별도 seam 설계 필요.
- **카드 authoring 의 SO 이전** — 어댑터 은퇴. 시트 연동 재설계와 함께.
- **`ISkill` 요구 플래그로 `DcApplicability` 판정기 단순화.**
- **(시전자, 스킬) → 고유 연출 매핑** · **스킬 툴팁 노출** · **호스트당 슬롯 스케줄러**
  (발동 중재 콘텐츠가 실재할 때).
- **채널 접힘** — `ShieldBreakEventsSingleton`·`MeteorBarrageRequestsSingleton` 이
  `SkillFiredEvent` 로 접히는지 판정.
