# 3. Seed JSON + 시트 입력 가이드

## 목적

현재 SO 값들을 계약 형태의 JSON으로 추출해 보존하고(`3_seed_unit_stats.json`), 기획파트가 구글 시트에 최초 입력할 때 따를 규약을 확정한다. 코드 변경 없음 (docs + data only).

## 변경 대상

- `docs/spec/unit-stat-spreadsheet-schema/3_seed_unit_stats.json` (신규) — 16 defenders + 9 enemies 현재 값 스냅샷
- 이 문서 (시트 입력 규약)

## 구현 (시트 규약)

### 탭 구성

- 탭 이름 = API `sheetName` 파라미터: **`Defenders`** / **`Enemies`** (`GET /demo/google/sheet/{sheetName}`)
- 1행 = 컬럼 헤더. **헤더는 계약 키 이름 그대로** (`displayName`, `attackCooldown` …) — API가 헤더를 JSON 키로 그대로 내려주고, Unity 매퍼가 이름 매칭 리플렉션이므로 헤더가 곧 계약이다.
- 2행부터 유닛 1행씩. 시드 JSON의 배열 순서대로 입력하면 된다.

### 컬럼 목록

- **Defenders**: `id, displayName, role, rarity, health, attackRange, attackCooldown, hitDelaySec, deployDelaySec, attackTargetCount, cost, aggroCapacity, aggroRange, atk, heal`
- **Enemies**: `id, displayName, enemyClass, attackMethod, targetMode, engageMovement, targetPriorityClass, targetClassMask, health, moveSpeed, attackRange, attackCooldown, attackTargetCount, hitDelaySec, aggroAttackDamage, aggroAttackCooldown, aggroAttackRange, atk`

### 셀 값 규약

- **`id`**: SO 매칭키 (슬러그, 예 `archer`, `basic`). 변경 금지 — 바꾸면 해당 행 전체가 unmatched 로 무시된다.
- **enum 셀**: C# 멤버명 문자열, 대소문자 무관 (`Guardian` = `GUARDIAN`). 허용값:
  - `role`/`targetPriorityClass`: None/Ranger/Guardian/Fighter/Caster/Support
  - `rarity`: Common/Rare/Epic/Ego
  - `enemyClass`: None/Tanker/Runner/Bruiser/Shooter
  - `attackMethod`: None/Melee/Projectile · `targetMode`: None/Nearest/FocusUntilDead · `engageMovement`: Halt/Advance/Pulse
- **`targetClassMask`**: 콤마 구분 문자열. `Everything` = 전체, `None` = 아무도 타겟 안 함, `Ranger,Guardian` = 부분. 빈 셀은 (다른 컬럼과 동일하게) "기존 값 유지"이므로 None 은 반드시 `None` 으로 명시한다. `Everything`/`None` 과 개별 클래스명 혼용 금지.
- **빈 셀** = JSON 키 생략 = import 시 기존 SO 값 유지 (부분 갱신). 0 을 넣으면 0 으로 덮어쓴다 — 다른 의미.
- **`atk`/`heal` 빈칸이 정상인 유닛**: 캐스터 4종(blocking/fire/ice/poison_caster — 데미지가 hazard 경로, 시트 스코프 밖)과 runner/swift(aggroAttackDamage 로만 공격). `heal` 은 healer 만 값이 있다.
- **`_` 접두 컬럼은 import 계약 밖**: 기획 편의용 파생/메모 컬럼(예 `_dps`, `_memo`)은 `_` 접두로 만든다. JSON 에 포함돼 내려와도 임포터 DTO 에 없는 키는 무시된다 (규약으로 보장).
- **행동 enum 주의**: Enemies 탭의 `attackMethod`/`targetMode`/`engageMovement`/`targetClassMask` 는 밸런스 수치가 아니라 AI 행동 정의다. 수치 밸런싱 중 임의 변경 금지 (엔지니어 협의 항목). 시트에서 색상 등으로 구분 권장.

## 완료 기준

- [x] `3_seed_unit_stats.json` 이 25 유닛 전수를 담고, 값이 asset 원본과 일치 (2026-07-06, enum 매핑 8종 전수 + 유닛 표본 교차 검증 + unit 5 export 동치 대조)
- [x] 사용자가 시드를 시트에 입력 완료 → unit 4 실 API 왕복으로 확인 (2026-07-06): 시트 데이터 = 시드 25유닛 전 필드 동치, SO 값 변경 0. 잔여: Enemies 헤더 `name`/`type` → `displayName`/`enemyClass` 정정 필요 (unit 4 문서 참조)
