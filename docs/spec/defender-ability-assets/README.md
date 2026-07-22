# defender-ability-assets — 유닛 고유능력 서브에셋 재구조화

> 상태: **완료 2026-07-22** (units 0~2 · compile 0 · EditMode 1193/0/2 · 사용자 Play 확인). 인계는 `3_handoff_summary.md`.

## 목표

`DefenderUnitData` 에 능력별 flat 필드가 산발 누적되는 구조(현재 ~35필드, 유닛당 실사용 0~9개)를
**능력 서브에셋 SO** 로 재구조화한다. 유닛 신설 시 고유능력이 늘어도 `DefenderUnitData` 와
스프레드시트의 기존 탭/컬럼이 오염되지 않게 한다 (사용자 결정 2026-07-22, B안).

- `DefenderAbilityData`(추상 base) + 능력종별 구체 SO. 유닛은 `abilities` 리스트 하나만 보유.
- 시트 관점: 능력종별 탭(키 = ability `id`) — 컬럼은 그 능력의 의미명 필드만, 행은 그 능력을
  쓰는 에셋만. **죽은 컬럼 0**. (탭 싱크 구현은 후속 unit — 이번엔 id 필드 등 계약만 예약)
- `zoneHazard→HazardSO` 서브에셋 참조 선례의 일반화. DcMechanic 의 "정의 계층 아키텍처 무지" 계약 승계.
- **이번 범위 = 캐스트 4종**(volley·hazard·shield·bomb, 25필드). 온히트/배치 라이더 3그룹
  (knockback·sleepOnHit·onPlacePush/Effect, 10필드)은 후속 unit.

검증 질문: **"기존 7유닛(머신거너·캐스터4·실드셔틀·폭탄맨)이 재구조화 전과 동일하게 동작하고, 새 고유능력 유닛이 DefenderUnitData 무변경으로 추가 가능한가?"**

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code (additive) | `0_ability_so_types.md` | base + 구체 4종 SO + `abilities` 리스트 + 헬퍼. 기존 필드 무변경 |
| 1 | asset (data only) | `1_ability_asset_authoring.md` | Ability 에셋 7개 저작 + 유닛 에셋 배선. 코드는 아직 flat 읽음 — 동작 불변 |
| 2 | code (cut-over) | `2_bake_cutover.md` | bake/aim/UI/desc 소비 7파일 → abilities 경로 + flat 25필드 삭제 + 테스트 갱신 + Play 검증 |

## Feature-wide 계약

1. **능력 데이터 소유 = 능력 SO.** `DefenderUnitData` 는 `List<DefenderAbilityData> abilities` 만. 같은 구체 타입 중복 부착 금지(`GetAbility<T>` = 첫 매치, 저작 규율).
2. **정의 계층은 아키텍처 무지**(DcMechanic 계약 승계): `Unity.Entities`/ECS 컴포넌트·시스템 타입 참조 금지. authoring 데이터 타입은 허용 — `Wassup.Data`(HazardSO·ShieldTargetFilter)와, `DefenderUnitData` 가 이미 참조하던 `Wassup.Battle.Effects` 의 authoring SO/enum(BlockingHazardSO·HazardCastKind — 네임스페이스만 Battle 이고 ECS 아님, 기존 예외 승계).
3. **번역은 BattleBridge 단독**: `CreateDefenderEntity` 가 abilities 순회 → 기존 ECS 컴포넌트(`VolleyFireState`/`HazardCastState`/`ShieldCastState`/`BombLauncherState`) bake. **ECS 컴포넌트/시스템/시뮬 무변경** — 이 spec 은 authoring 데이터 재편일 뿐.
4. **capability 는 능력이 선언**: `virtual bool RequiresFacing`(volley·bomb=true) 이 `directionalAttack` flag 를 대체. 조준 UX 소비처(배치 컨트롤러·튜토리얼·셀렉터·aim guide)는 유닛 헬퍼(`DefenderUnitData.RequiresFacing`/`GetAbility<T>`)만 읽는다.
5. **시트 계약 예약**: `DefenderAbilityData.id` = 시트 매칭키(슬러그). 필드명 = 시트 헤더(기존 DTO 규약 승계). 탭 = 능력종별(`AbilityVolley`/`AbilityHazard`/`AbilityShield`/`AbilityBomb`) — 임포터/익스포터 구현은 후속 unit.
6. **마이그레이션 등가성**: cut-over 전후 bake 결과 동일. 증명 = 기존 EditMode 통합 테스트(volley 등)를 abilities 경로로 갱신해 green + 7유닛 Play 스모크.
7. 상속 2단계(SO→base→구체) 준수. 구체 4종 실존이 추상화 근거(제약 8 충족 — 투기 아님).

## 파이프라인 커버리지 (Defender 아키타입 대조)

신규 플레이 오브젝트 없음 — **데이터 SO 정거장만 재편**.

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `DefenderUnitData` flat 능력필드 25개 → `Data/Abilities/` 서브에셋. 카탈로그 무변경 |
| 스폰 진입점 | `CreateDefenderEntity` 번역자만 abilities 순회로 재작성 — 진입 API 무변경 |
| ECS 컴포넌트 | **N/A — 무변경** (bake 결과 동일이 계약 6) |
| 시뮬 시스템 | **N/A — 무변경** |
| 이벤트 큐 | **N/A — 무변경** |
| View/Pool | **N/A — 무변경** |
| 씬 wiring | **N/A — 신규 SerializeField 없음** |

feature 종료 시 `object-pipeline-map.md` Defender 행의 데이터 SO 앵커에 ability 서브에셋 추가.

## 후속 후보

- **라이더 3그룹 이관** [M] · knockback(2)·sleepOnHit(1)·onPlacePush/Effect(7) → 능력 SO. `DefenderCcData` bake 와 `ApplyOnPlaceEffect` 소비처. 캐스트 4종 안정화 후.
- **시트 탭 싱크** [M] · 능력종별 탭 임포터/익스포터(`unit-stat-spreadsheet-schema` DTO 패턴, DcMechanics 탭 선례). Defenders 탭에 `_abilities` 정보 컬럼.
- **적(AttackUnitData) 동형 재구조화** [M] · 적 고유능력이 늘어나면 같은 패턴 적용.
- **능력 에셋 공유 변종** [S] · 같은 ability 에셋을 여러 유닛이 참조(수치 공유 변종). 구조는 이미 지원 — 저작 컨벤션만.
- **효과 트리거 통합과의 관계** · 파킹된 `2026-07-15-effect-trigger-unification-design.md`(DcMechanic 도메인 중립화)와 별개 축 — 이 spec 은 defender authoring 데이터 재편이고 트리거 엔진을 만들지 않는다. 통합 착수 시 ability SO 가 그 rule 의 데이터 홈이 될 수 있음.
