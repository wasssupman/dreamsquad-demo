# damage-number-visual-upgrade

> 상태: **진행중 2026-07-07** — units 0·1 구현·커밋 완료(`3ba0f57`, `831c923`). units 2·3 은 **코덱스 에셋 대기**(하프톤 텍스처·스파크 파티클, `assets-codex-request.md`).

## 상위 목표

데미지 숫자를 참고 이미지("Sword x Staff") 수준의 **겹치지 않는 그리드 배열 + 임팩트 있는 강렬한 룩**으로 끌어올린다. 현재는 고정 오프셋 + 단조로운 흰→빨강 단색이라 다단 히트/AoE 때 완전 중첩되고 밋밋하며, 앵커가 낮아 유닛 몸통 중하단에 깔려 가려진다.

기존 `damage-number-popup` (완료 2026-06-05) 의 파이프라인(ECS `DamageNumberEventsSingleton` → `BattleBridge.DrainDamageNumberEvents` → `DamageNumberSpawner.Spawn` → 풀링 월드 TMP)은 **그대로 유지**하고, 배치·머티리얼·모션 레이어만 교체/확장한다.

## 결정된 방향 (브레인스토밍 2026-07-07)

- **색 소스**: 마그니튜드 기반 유지 + 팔레트 재설계. **ECS 이벤트 payload 변경 없음.**
- **렌더 방식**: 머티리얼 중심. 하프톤/글로우/그라데이션/아웃라인은 TMP 머티리얼·셰이더에, 스파크는 스폰 클러스터당 공용 파티클 1개로 최소화(모바일 드로우콜 안전).

## 작업 단위

| # | 문서 | 작업 | 코덱스 에셋 의존 |
|---|---|---|---|
| 0 | `0_placement-grid-stagger.md` | 머리 위 앵커 + 결정론적 점유 격자 겹침 방지 배치 | 없음 (즉시) |
| 1 | `1_palette-gradient-motion.md` | 마그니튜드 팔레트 재설계 + 정점 그라데이션 + 모션 강화 | 없음 (즉시) |
| 2 | `2_material-impact-look.md` | 하프톤 페이스 텍스처 + 글로우 + 흰 아웃라인 | 하프톤 텍스처 |
| 3 | `3_spark-hook.md` | 클러스터당 공용 스파크 삽입점 (선택/후행) | 스파크 파티클 |
| — | `assets-codex-request.md` | 코덱스 에셋 요청서 (별도, 비-작업단위) | — |

**0·1 은 코덱스 의존 0** — 즉시 구현·검증 가능. 그 사이 코덱스에 에셋 요청 → 도착하면 2·3. 병목 없음.

## Feature-wide 계약

- **ECS 경계 불변**: `DamageNumberEvent` / 싱글턴 채널 / enqueue(`DamageApplicationSystem`) 는 건드리지 않는다. 이 업그레이드는 100% MonoBehaviour 프레젠테이션 계층(`Assets/_Project/Scripts/Presentation/`) 작업이다.
- **좌표계 (critic 반영)**: `BoardSpace.ToView` 는 **sim-Y 를 버린다**(x,z만). ① 머리 앵커는 sim-Y 가 아니라 **ToView 이후 view 공간에서** 올린다. ② 점유 격자는 tilted 보드 world X/Y 가 아니라 **카메라 빌보드 축(camera.right/up) 투영**으로 짠다(pitch 무관 화면 정렬). ③ `ToView` 는 스포너 1곳에서만 적용 — `View.Play` 는 view-space 위치를 그대로 받는다(이중 변환 금지).
- **시간축 (critic 반영)**: 글로벌 `Time.timeScale` 은 **1 고정** → `Time.deltaTime` 은 TimeManager 정지에 안 멈춘다. 애니메이션 델타는 **`TimeManager.DeltaTime(TimeDomain.Battle)`** 경유(현 `DamageNumberView` 의 raw `Time.deltaTime` 은 정지-미반응 기존 버그, unit 1 에서 교정). `Time.timeScale` 은 건드리지 않는다.
- **결정론**: 배치·모션 지터는 seeded RNG·frame-count(시간 소스) 금지. 스포너 **monotonic 스폰 카운터 index** 로만 결정(나선 tie-break·셰이크 방향·미세 회전). 프로젝트 구조적 결정론 원칙과 동일 결.
- **하드코딩 금지**: 팔레트·셀 크기·모션 강도 등 튜닝값은 `DamageNumberStyle` 직렬화 필드(스포너 인스펙터 노출). 머티리얼 파라미터는 `.mat` 에셋.
- **모바일 우선**: 숫자마다 파티클/오브젝트를 붙이지 않는다. 임팩트 파티클은 클러스터당 1개.
- **풀링 무결성**: 점유 셀은 뷰가 풀에 반납될 때 반드시 해제(반납 콜백 경유). 셀 누수 금지.

## 후속 후보 (현 스코프 밖)

- **스킬/속성 색** — `DamageNumberEvent` 에 속성/색 id 추가해 공격 종류가 색 결정. ECS payload 변경 동반. [M]
- **크리티컬 구분** — 크리 히트에 별도 스케일/색/외곽선. crit 플래그 payload 필요. [S]
- **힐/디펜더 피격 숫자** — 힐 숫자(초록 +), 방어 유닛 피격 숫자. 별도 이벤트 채널. [M]
- **DoT 누적 합산** — 같은 적의 도트 데미지를 한 숫자로 합산 tick. [S]
- **`DamageNumberStyle` SO 승격** — 다중 프리셋 필요 시 `[Serializable]` → `ScriptableObject`. 현재는 단일 스타일이라 불필요. [S]
- **트리밍 커스텀 모바일 셰이더** — unit 2 의 비-모바일 Distance Field 셰이더가 실기 프로파일에서 무거우면 face-tex+glow 만 남긴 경량 셰이더 저작. [M]
- **유닛별 정밀 머리 앵커** — unit 0 은 단일 고정 view 오프셋. 적/디펜더/보스 키 차이를 뷰 bounds/height 해석으로 정밀 앵커링(entity→view 매핑 경유). 대형 유닛 정합용. [S]
