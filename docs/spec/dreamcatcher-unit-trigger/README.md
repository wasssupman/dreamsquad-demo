# Dreamcatcher Unit Trigger — 개별유닛 바인딩 + 트리거형 메커닉

> 상태: **완료 2026-07-09** (units 0~3 + handoff). 검증 질문 YES — 실전투 Play 확인.
>
> 설계 배경: `docs/plans/2026-07-08-dreamcatcher-unit-trigger-design.md` · 인계: `4_handoff_summary.md`

## 목표

기존 드림캐쳐(축 매칭 스탯% 패시브)와 다른 새 카드 부류의 토대를 만들고 첫 카드로 실증한다:

- **개별 유닛 바인딩** 카드 — 축이 아니라 유닛 1명에게 부착.
- **트리거형 메커닉** — 첫 조합: `AttackN(5)` × `ProjectileToTarget(20)` = "공격 5회마다 공격 대상에게 20 데미지 투사체" (가칭 콕콕 바늘).

## 검증 질문

> 유닛에 부착된 카드가 **그 유닛의 5회째 타격 판정마다** 공격 대상에게 별도 투사체를 발사해 20 데미지를 입히는가? 같은 카드 2장 부착 시 **독립 카운터**로 동작하는가? 기존 투사체/스탯 카드 경로는 **무회귀**인가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_definition_layer.md` | 계약 | 정의 계층 — `DcTriggerSpec`/`DcPayloadSpec`/`DcMechanic` + `DreamcatcherCard` 확장 (ECS 무참조, 컴파일만) |
| 1 | `1_ecs_slot_and_attach.md` | 계약+배선 | `DcTriggerSlot` buffer(Combat) + request 캐리어 엔티티 파괴 분기 + `BattleBridge` 부착/베이크 API |
| 2 | `2_attack_resolve_trigger.md` | arm+테스트 | AttackSystem RESOLVE 카운트/발동 arm + `DcTrigger.Tick` 순수함수 EditMode |
| 3 | `3_card_asset_play_validation.md` | 에셋+검증 | 콕콕 바늘 카드 SO + Play e2e (5회째 발사·20 데미지·독립 카운터) |
| 4 | `4_handoff_summary.md` | 인계 | 종료 요약 (완료 2026-07-09) |

## Feature-wide 계약 (load-bearing)

1. **2계층.** 정의 계층(`Scripts/Data/Dreamcatcher/DcMechanic.cs`)은 순수 데이터 + Unity 에셋 참조만 — **Entities/Battle 타입 참조 금지**. 해석 계층(BattleBridge 베이크 + Combat 실행)만 ECS 를 안다. 아키텍처 교체 시 번역자만 재작성.
2. **소유권 = Combat.** `DcTriggerSlot`(DynamicBuffer, defender 엔티티)은 Combat 소유. 카운터 쓰기 = `AttackSystem` **전용**(RESOLVE / 폭탄 발사 훅 / 캐스트 드레인 세 지점이며, host 하나는 그중 정확히 1곳만 탄다 — `dreamcatcher-attack-decoupling` 계약 2), 부착/제거 = BattleBridge(유일 창구, `_em` 직접 쓰기 허용 선례). 다른 맥락 쓰기 금지.
3. **카운팅 = 공격 1회 = 1카운트.** 멀티 output 이어도 1회. 지연 만료로 타겟이 사라져 RESOLVE 가 무산되면 카운트도 없다.
   - **RESOLVE 카운트는 이 spec 이후로도 그대로다.** `attack-decoupling` 은 이 규칙을 대체하지 않고, RESOLVE 에 **구조적으로 도달할 수 없는** host 에만 대체 사건 지점을 추가했다(폭탄 발사 성사 / 캐스트 성사). 카운트를 START 로 옮기면 응축된 일격 pre-scan 이 이미 증가한 카운터를 읽고, 처형타 게이트가 wind-up 이전 HP 를 보고, 지연 무산 규칙이 사라진다 — **옮기지 말 것**.
4. **독립 카운터.** 슬롯 1개 = 효과 인스턴스 1개. 부착 시 `instanceId` 발급(bridge 카운터, stackId 패턴). 같은 카드 2장 = 슬롯 2개 = 카운터 2개.
5. **페이로드 = 기존 프리미티브 재사용, 새 데미지 경로 금지.** `ProjectileToTarget` 은 기존 단일 투사체 라이프사이클(`ProjectileSpawnRequest → drain → Move → Impact`)에 `HomingToEntity × SingleSplash`, `damage=magnitude` 로 태운다. outputs 스냅샷 없음 = `ProjectileHitSystem` 의 **no-AttackOutputElement fallback 경로**(`ProjectileState.damage` 직접 적용)를 탄다 — 스킬 투사체 선례(projectile-trajectory-payload 계약 6).
6. **request 캐리어 엔티티.** `ProjectileSpawnRequest` 는 엔티티당 단일 컴포넌트라 5회째 프레임에 기본 공격 요청과 충돌 → dc 투사체는 `ecb.CreateEntity` 한 캐리어 엔티티에 request + `ProjectileRequestCarrier` 태그로 부착하고, drain 이 캐리어를 **파괴**한다(기존 경로는 RemoveComponent 유지, additive 분기). 신규 시스템/드레인/큐 0.
7. **magnitude 는 flat.** `damageMul` 등 공격자 스탯 모디파이어를 곱하지 않는다(카드 수치 = 예측 가능한 고정값). 스케일링 페이로드는 후속에서 별도 kind 로.
8. **직렬화 append-only.** `DreamcatcherCard`/enum 확장은 기존 카드 에셋의 직렬화 값을 보존하도록 필드/케이스를 끝에 추가(기존 파일 주석 선례 유지).
9. **바인딩 UX/회수 로직은 스코프 밖.** 이번 spec 의 부착은 `BattleBridge` 공개 API + 테스트 훅으로만 실증. 유닛 사망 시 카드 회수(재사용 가능 전환)는 `DrainDefenderDeathEvents` 가 seam — 별도 spec 에서.
10. **`ProjectileToTarget` 은 적을 타겟하는 host 에만 붙는다** (2026-07-27). 니들은 그 공격의 `bestTarget` 으로 날아가는데 `targetAllies` 유닛(힐러)의 `bestTarget` 은 **아군**이라 회복 대상을 때린다(캐리어는 outputs 스냅샷이 없어 진영 필터 없는 `ProjectileHitSystem` fallback 을 탄다).
    - **판정 위치가 이동했다**(`attack-decoupling` unit 1). 술어는 `DcApplicability.EvaluateMechanic` 한 곳이고 UI preflight 와 커밋 bake 가 그 함수를 공유한다. 여기 있던 `BattleBridge.TargetsEnemies` 전용 게이트는 그 수렴에 흡수됐다 — **철회가 아니라 이관**이며, 힐러 거절은 그대로 유효하고 `NeedsEnemyTargeting` 으로 보고된다.
    - **폭탄맨·해저드 캐스터는 이제 발동한다**(unit 3·4). 각자 사건 지점을 얻었고, host 가 대상을 안 주므로 `payload.tileRange` 반경으로 스스로 고른다(반경 0 이면 `NeedsFallbackRange` 로 부착 거절).

## 파이프라인 커버리지 (투사체 아키타입 대조)

`docs/reference/object-pipeline-map.md` §투사체 기준. 기존 정거장 전부 재사용:

| 정거장 | 이번 spec | 비고 |
|---|---|---|
| 데이터 SO | `DreamcatcherCard`(mechanic 블록) + 기존 `ProjectileData` 재사용 | 신규 SO 타입 없음 |
| 스폰 진입점 | `AttackSystem` RESOLVE dc arm → 캐리어 엔티티 request → 기존 `DrainProjectileSpawnRequests` | drain 에 캐리어 파괴 분기만 추가 |
| ECS 컴포넌트 (Combat) | 기존 + `DcTriggerSlot`(buffer) + `ProjectileRequestCarrier`(태그) | |
| 시뮬 시스템 | 기존 `ProjectileMoveSystem`/`ProjectileHitSystem` 그대로 | Homing×SingleSplash arm 재사용 |
| 이벤트 큐 | 기존 `ProjectileHitEventsSingleton`(히트 VFX) + `AttackOutputLogEventsSingleton`(로그) | 신규 채널 0 |
| View/Pool | 기존 `ProjectileViewPool` | 카드 전용 뷰 프리팹은 `ProjectileData` 에서 |
| 발사 SFX | N/A — 캐리어 엔티티는 `DefenderUnitTag` 가 없어 기존 발사음 미재생 | 카드 고유 SFX 는 후속 |

## 확장 비용 지도

새 메커닉의 확장 지점은 클래스 계층이 아니라 **enum 케이스 + arm + 훅** 이다. 비용 등급:

- **싸다 (enum + arm/훅 수준)**: ① 이산 사건 트리거("N회 공격/킬/피격/버튼/웨이브마다" — 발생 지점에 `DcTrigger.Tick` 훅; UI 등 Mono 세계 사건은 bridge 창구 → NativeQueue 주입으로 소유권·결정론 유지) ② 기존 프리미티브 4종(투사체/스탯/스택/해저드)의 파라미터화로 표현되는 페이로드.
- **한 번 지불 (국소적 신규 조각)**: ① 상태형 조건("HP 50% 이하인 동안" — 카운팅이 아닌 상태 감시 세만틱 → 새 순수함수 + 평가 지점) ② 조합 조건("A 그리고 B" — mechanic 당 트리거 1개 전제를 그때 재설계) ③ 프리미티브 밖 페이로드(소환/지형 변경 — 해당 효과의 파이프라인 신설이 본체, `object-pipeline-map` 대조) ④ 트리거×페이로드 컨텍스트 비호환(타겟 없는 트리거 × 타겟 요구 페이로드 — 해석 규칙을 spec 호환표로 결정하고 부착 가드에서 거절).

보증은 "모든 조건이 공짜"가 아니라 **변경이 항상 국소적이고, 새 세만틱 지불 여부가 명시적 결정으로 드러난다**(부착 가드 + 미지원 payload LogWarning)는 것.

## 후속 후보

- **공격 개조형 카드 부류 (c)** — 트리거 없는 상시 산출물 개조(튕김/관통 등) → `docs/spec/dreamcatcher-attack-mod-bounce/` 로 승격 (2026-07-09).
- **SelfTileAoe 페이로드** [S] · "주변 1타일 물결" — TileAoe payload, impact=자기 셀 락.
- **NextAttackModifier 페이로드 + charge 소모형 모디파이어 만료** [M] · "5회마다 다음 공격 2배" — 시간이 아니라 공격 N회로 꺼지는 StatModifierSlot 수명. 모디파이어 시스템 범용 확장.
- **추가 트리거 소스** [M] · Kill / Damaged / NextWave — 각 소스(EnemyKilled, DamageApplication, bridge)에서 슬롯 카운트 훅.
- **개별 유닛 바인딩·회수 UX + 레지스트리** [M] · 카드 선택→유닛 지정 UX, 사망 시 회수/재사용, MonoBehaviour 레지스트리(카드↔유닛↔instanceId).
- **카드 고유 발사/히트 SFX·VFX 채널** [S] · payload 스펙에 에셋 슬롯 확장.
- **설명 템플릿 렌더링** [S] · 기획 템플릿에 trigger/payload 수치 삽입해 카드 텍스트 생성.
- **스케일링 magnitude** [S] · damageMul 연동이 필요해지면 별도 payload 파라미터로.
- **DcPayloadSpec 형태 재평가** [S] · 두 번째 payload kind 추가 시 monolithic struct 유지 vs kind별 분리 판단 (지금은 YAGNI 로 단일 struct — projectile 필드는 ProjectileToTarget 전용).
- **같은 프레임 다중 발동 시각 stagger** [S] · 슬롯 2개가 같은 RESOLVE 에 동시 발동하면 동일 궤적 투사체 2발 겹침 — 기능상 정상(의도), 보기 싫으면 오프셋/지연 후속.
