# shield-guardian-defender — 실드셔틀 (실드 가디언)

> 상태: 스펙 작성 2026-07-21 · 구현 대기

## 목표

**실드(피해 흡수막)** 신규 메커니즘과 그 첫 소비 유닛 **실드셔틀**(id `shield_shuttle`)을 추가한다.
어그로 탱커(가디언 클래스)가 A초마다 공격범위 내 아군 C명에게 실드 B량을 부여해 "받는 피해를 미리 막아주는" 서포트 탱커 정체성을 만든다.

- 실드 = Health 차감 전에 먼저 소모되는 흡수 풀. TTL 없음(깨질 때까지 유지).
- **출처별 슬롯** (사용자 결정 2026-07-21): 같은 출처(캐스터 개체)의 재부여는 max(잔량, B)로 중첩 불가, **서로 다른 출처의 실드는 합산**(셔틀 a 100 + 셔틀 b 100 = 200). 유효 실드 = 슬롯 합.
- 필터 3종: `SELF`(자신만) / `ALL`(가까운 순 C개) / `MINHEALTH`(HP 비율 오름차순 C개). 전부 SO 데이터.
- 공격 채널 불변 — 실드는 공격과 독립된 두 번째 쿨다운(HazardCast 선례). 어그로(공격 명중 트리거)는 그대로 산다.

검증 질문: **"실드가 체력바에서 읽히고, 위험한 아군을 실제로 살리는 게 체감되는가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_shield_pool_absorb.md` | `ShieldPool`/`IncomingShield`(Units) + `ShieldMath.Absorb` 순수 흡수 + DamageApplication 훅 + EditMode |
| 1 | code | `1_shield_cast_system.md` | SO 필드(A/B/C/필터) + `ShieldCastState`/`ShieldCastSystem`(Effects) + 필터 선별 순수 함수 + EditMode |
| 2 | code | `2_overhead_shield_segment.md` | 체력바 실드 오버레이 세그먼트 (프레젠테이션 폴링 확장) |
| 3 | asset | `3_unit_asset_and_catalog.md` | `Defender_ShieldShuttle.asset` 저작 + 카탈로그 등록 + Play 검증 |
| 4 | code | `4_shield_granted_vfx.md` | 실드 부여 원샷 VFX (신규 채널 + VfxSpawner 슬롯 + 씬 배선) |

## Feature-wide 계약

1. **실드 상태 소유 = Units.** `ShieldSlot` 버퍼(출처별 슬롯, Health 옆) 쓰기는 `DamageApplicationSystem` 단독. 생산자(Effects)는 `IncomingShield` 버퍼에만 append — `IncomingHeal` 선례의 맥락 간 Buffer 통신. 흡수/병합 경로는 **신규 NativeQueue 채널 0**(버퍼 통신만). **부여 VFX(unit 4)만 신규 채널 1개** `ShieldGrantedEventsSingleton`(Effects→Bridge, 채널 20번째 — CLAUDE.md 갱신됨).
2. **흡수 계약**: `dmgTakenMul` 적용 **후** 실드 흡수(표시 데미지 = 흡수량). 소모는 **오래된 슬롯부터**(삽입 순, 결정론), 소진 슬롯은 제거. 산식은 순수 `ShieldMath`(슬롯 병합·흡수) — sim-critical, EditMode 필수(제약 10).
3. **완전 흡수 히트 = 피격 아님** (사용자 결정 2026-07-21). 흡수 후 `totalDamage` 값으로 기존 분기(wake-on-hit·가시갑옷 카운트·데미지 넘버·킬 귀속)를 전부 판정 — **기존 분기 조건 무변경**이 곧 이 계약의 구현. **인지된 귀결**: 실드 낀 유닛은 피격 카운트가 안 쌓여 가시 갑옷류 "맞아야 강해지는" 빌드와 상성이 나쁨 — 의도된 전략적 트레이드오프(기획 판단 2026-07-21).
4. **출처별 max · 교차 출처 합산 · TTL 없음** (사용자 결정 2026-07-21). 같은 출처 재부여 = 해당 슬롯 max(잔량, B) — 상한은 출처당 B 내장. 다른 출처 = 새 슬롯(합산). 만료 타이머 없음. **출처는 중첩 키일 뿐 수명 링크 아님** — 부여자가 죽어도 잔여 실드 유지(Entity 는 version 포함이라 재활용 id 와 충돌 없음).
5. **실드 범위 = `attackRange` 재사용.** 별도 range 필드 금지 (사용자 스펙 "자신의 공격범위 이내"). 베이크 시 `ShieldCastState` 에 attackRange 값 복사.
6. **타겟 선별**: 후보 = 범위 내 생존 아군 defender, **자신 포함**. `ALL`=거리 오름차순 C개 · `MINHEALTH`=**유효HP 비율 `(HP+실드합)/maxHP` 오름차순** C개 (실드 무시 정렬은 만충 대상 no-op 재부여 함정 — 기획 판단 2026-07-21) · `SELF`=C/범위 무시. 선별은 순수 함수 + EditMode.
7. **공격/어그로 채널 불변**: 실드 캐스트는 `HazardCastState` 선례의 독립 쿨다운. action-lock(Sleep/Stun)은 기존과 동일하게 공격/이동만 게이트 — 실드 캐스트는 HazardCast 와 같은 급으로 게이트 밖(일관성).
8. **표기 = 폴링**: `SyncMonoUnitViews` 의 기존 Health 폴링에 ShieldSlot 합산을 동승(이벤트 아님) → `UnitOverheadView` 에 실드 세그먼트를 **HP fill 끝에 이어붙임**(HP 60%+실드 20% = 바의 60~80% 구간 — HP 정보 가림 방지, 기획 판단 2026-07-21). 합>100% 는 **동적 정규화**(분모 = max(maxHP, HP+실드합)) — 풀피 유닛의 실드도 항상 표시(실드는 풀피에서도 유효한 게 힐과의 차별점). 바 폭 불변.
9. A/B/C/필터 포함 전 수치 SO (하드코딩 금지).

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_ShieldShuttle.asset` 신규 + `DefenderUnitData` 실드 필드 4종(A/B/C/필터) + **DefenderCatalog 등록**(unit 3) |
| 스폰 진입점 | 기존 `PlaceDefenderAs`→`CreateDefenderEntity`. `ShieldSlot`+`IncomingShield` 버퍼 전 defender 사전 부착(unit 0) + `ShieldCastState` 조건부 베이크(unit 1) |
| ECS 컴포넌트 | **신규 3**: `ShieldSlot`·`IncomingShield` 버퍼(Units) + `ShieldCastState`(Effects). Hazard/DeployedFacing/VolleyFireState N/A — 능력 비활성 |
| 시뮬 시스템 | **신규 1**: `ShieldCastSystem`(Effects). `DamageApplicationSystem` 흡수 훅(unit 0). AttackSystem 무변경 |
| 이벤트 큐 | 흡수/병합 = 채널 0(버퍼 통신). 부여 VFX(unit 4) = 신규 `ShieldGrantedEventsSingleton` 1개(Effects→Bridge) |
| View/Pool | 기존 `SpineUnitPool`(파츠 재조합). 체력바 실드 세그먼트 = `UnitOverheadView` 확장(unit 2) |
| 체력 표시 | `SyncMonoUnitViews` 폴링에 ShieldPool 동승(unit 2) — 기존 매 프레임 Health 폴링과 동형 |
| 씬 wiring | **N/A — 신규 SerializeField 없음.** 카탈로그 등록만 |

## 후속 후보

- ~~**실드 부여 순간 연출**~~ [완료 2026-07-21, unit 4] · `ShieldGrantedEventsSingleton` → `VfxSpawner.SpawnShieldGranted`(VFX_Fire_Green 단발화). 잔여: 전용 실드 아트(초록 화염은 placeholder, guid 스왑), 부여 SFX.
- **SELF/ALL 변종 유닛** [S] · 엔진은 완성 — SO 저작만으로 성립(필터 enum 데이터).
- **실드 TTL/붕괴 변종** [S/M] · 만료 타이머 도입 시 CcDecay 유사 시스템 필요 — 계약 4 변경이라 별도 결정.
- **Aura producer 이관** [M] · 백로그 `AuraApplySystem` 신설 시 ShieldCast 를 aura payload 로 흡수 검토(소비자 2개 시점).
- **적측 실드** [M] · `ShieldSlot` 버퍼를 적 스폰에도 부착 + 표기 확장. 현재는 defender 전용.
- **부여자 사망 시 실드 회수** [S] · 출처 키가 이미 있어 성립 가능 — 계약 4("수명 링크 아님") 변경이라 별도 결정.
- **흡수 데미지 넘버 변형** [S] · 흡수량을 회색/하늘색 폰트로 표시(현재는 미표시).
- **서스테인 인플레 밸런스 감시** [S] · 힐러+셔틀 동시 편성 시 저DPS 웨이브 탱커 불사 가능 — 규칙 결함 아닌 수치 문제(B/A vs 적 DPS). Play 밸런스 관찰 항목.
