# 0 — 기믹 요약문 생성기 (UnitKitSummary)

## 목적

상세 카드의 "설명문"을 **신규 SO 필드·콘텐츠 저작 없이** 기존 유닛 데이터에서 자동 조립한다. 클래스 + 공격 유형(원/근접/지원) + 특성(다중타격/방향/어그로/on-place/해저드)을 한국어 한 문장으로 투영하는 **순수 static 함수**. lore 문장은 후속.

`AttackOutputStats`와 동형 — `Wassup.Data`의 순수 프로젝션 헬퍼 + EditMode 테스트.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/UnitKitSummary.cs` (namespace `Wassup.Data`)
- 신규 `Assets/_Project/Tests/EditMode/UnitKitSummaryTests.cs`

## 구현

`string UnitKitSummary.Build(DefenderUnitData u)`:

- **null-safe**: `u == null` → `""`.
- **머리말** = `"{클래스} · {공격유형}"` (클래스 `None` 이면 접두 생략 → 공격유형만).
  - 클래스: Ranger 레인저 / Guardian 가디언 / Fighter 파이터 / Caster 캐스터 / Support 서포트 / None 없음.
  - 공격유형: `targetAllies` 면 outputs 에 Heal 있으면 "아군 치유형", 없으면 "아군 강화형". 아니면 `projectile != null` → "원거리형", null → "근접형".
- **특성절**(순서 고정, 콤마 결합):
  1. `attackTargetCount > 1` → "최대 N체 동시 타격"
  2. `directionalAttack` → `shotCount > 1` ? "지정 방향으로 N연발 사격" : "지정 방향 사격"
  3. `aggroCapacity > 0` → "최대 N체 도발 유지"
  4. `onPlaceEffect` (None 아니면): SlowPulse=주변 둔화 / BoostNearbyDefenders=주변 아군 강화 / BindNearby=주변 속박 / MeleeBurst=즉시 광역 폭발 / ForwardProjectile=전방 발사 / GainCost=코스트 획득 / ReduceSkillCooldown=스킬 쿨다운 감소 → 각 "배치 시/즉시 ~"
  5. `hazardCastEnabled` → "지속 해저드 설치"
- **결합**: 특성 0개면 `머리말 + "."`, 있으면 `머리말 + ". " + join(", ") + "."`.
- 클래스 라벨은 enum→한국어 매핑(하드코딩 수치 아님, 순수 프레젠테이션). 수치는 전부 SO 필드에서 읽는다.

예: 가디언 근접 + attackTargetCount 3 + aggro 3 + BoostNearbyDefenders → `"가디언 · 근접형. 최대 3체 동시 타격, 최대 3체 도발 유지, 배치 시 주변 아군 강화."`

## 완료 기준

- [x] `UnitKitSummaryTests` 전부 통과(EditMode 10/10, 1.1s): null / 근접·원거리 / role None 접두 생략 / 지원 힐·버프 분기 / 특성 순서·문구 / 방향 단·다연발 / 결정론.
- [x] 컴파일 클린(신규 .cs → scope=all refresh, cascading CS0246 없음).
- [x] 하드코딩 수치 0 — 모든 숫자는 `DefenderUnitData` 필드에서 파생.

> 완료 2026-07-18 · 커밋 `576df8da` (EditMode 10/10)
