# unit-health-display — 체력 표기 설계 (브레인스토밍 결과)

> 얇은 설계 요약. 구현 상세는 `docs/spec/unit-health-display/` 참조.

## 목표

적/방어유닛 체력 상태를 **머리 위 상시 바 없이** 직관적으로 전달한다. 현재 ECS 헬스바(`HealthBarSystem`)는 tilemap 뷰 전환 때 렌더가 게이트되어 화면에 보이지 않는 죽은 코드 — 복원이 아니라 백지 설계.

## 핵심 프레임 — 정보 비대칭

- **적**: 수십 마리가 흐름. 개별 정밀 수치 무의미. "내 화력이 먹히는가" → **순간 피드백 + 몸 상태**.
- **방어유닛**: 소수·타일 고정·플레이어 자산. "지금 개입해야 하는가" → **지속 표시**.

규칙 한 줄: **"방어유닛은 타일이 말하고, 적은 맞는 순간만 말한다."**

## 채택 (조합 1 — 비대칭 설계)

| 대상 | 표기 | 방식 |
|---|---|---|
| 적 | 피격 시 마이크로바 | `DamageNumberEvent` 확장(entity+hpRatio) → 피격 순간만 등장, ~1초 후 페이드 |
| 적 | 저체력 틴트 | Spine `Skeleton.SetColor` 램프 (정상→창백→검붉음), BattleBridge 폴링 |
| 방어유닛 | 점유 타일 테두리 게이지 | perimeter-fill 셰이더, HP 비율 fill + 녹→황→적. full HP 숨김 |

시각 파라미터는 `HealthDisplayStyle` ScriptableObject 단일 소스.

## 기각/보류한 대안

- 상처 attachment swap·wounded idle — 캐릭터별 Spine 오소링 비용 폭발.
- 발밑 radial 링 전면 — 이동 유닛 다수의 바닥 클러터.
- 킬 포어캐스트 마크(IncomingDamage 기반 스컬) — 유력 후속 후보로 이관.
- 웨이브 압력 게이지·보스 상시 바 — 후속 후보.

## 아키텍처 요약

- ECS 경계 불변: HP 읽기는 BattleBridge 만 (`SyncMonoUnitViews` 폴링 read-only + `DamageNumberEvent` drain). 뷰/스포너는 ECS 미접근.
- 이벤트 확장은 소유 맥락(Units, `DamageApplicationSystem`) 내 쓰기 — 경계 위반 없음.
- 구 ECS 헬스바 3파일 + `CreateHealthBar` 호출 3곳 삭제.

## Spec

`docs/spec/unit-health-display/` — units 0~3 + handoff.
