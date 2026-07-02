# 0. Projection Contract

## 목적

기획 시트 필드 → outputs 투영 규칙과 JSON 스키마 v2 delta를 확정한다. docs only.

## 변경 대상

- 본 문서 (계약의 SoT)
- `docs/spec/unit-stat-spreadsheet-schema/README.md` — v2 포인터 1줄 추가

## 구현 (계약 정의)

### 투영 필드

| JSON 키 | 대상 | 투영 목적지 | 규칙 |
|---|---|---|---|
| `atk` | defenders[], enemies[] | outputs에서 `kind==Damage`인 유일 항목의 `magnitude` | 해당 항목 정확히 1개일 때만 갱신. 0개/2개+ → skip + 로그 사유 |
| `heal` | defenders[] | outputs에서 `kind==Heal`인 유일 항목의 `magnitude` | 동일 규칙 |

- 투영은 임포터의 리플렉션 필드 복사 **이후** 별도 단계로 수행. `atk`/`heal`은 SO에 동명 필드가 없으므로 리플렉션 skip-list에 등재.
- **`aggroAttackDamage`는 투영 대상이 아님** — live 스칼라이므로 기존 리플렉션 매핑 유지. skip-list에 포함 금지.

### JSON 스키마 v2 delta (v1 = `unit-stat-spreadsheet-schema/0_json_schema_contract.md`)

- **추가**: `atk` (number, defenders+enemies), `heal` (number, defenders)
- **삭제**: `attackDamage` 컬럼 — 시트에서 제거. 단 DTO에는 **deprecation shim**으로 1릴리스 잔류: 수신 시 값 미적용 + "attackDamage는 atk로 개명됨, 값 미적용" 경고를 결과 로그에 출력 (silent no-op 방지)
- 시트 컬럼 개명 절차: ① Unity 측 unit 3 배포 → ② 기획 시트에 atk/heal 컬럼 추가 → ③ attackDamage 컬럼 삭제 → ④ shim 경고 무발생 확인 후 shim 제거 (후속 커밋)
- 나머지 v1 계약(부분 갱신, id 매칭, enum 규칙)은 무변경

### 로스터 투영 가능성 (2026-07-02 기준 24 asset 전수)

| 분류 | 수 | 유닛 | atk 투영 |
|---|---|---|---|
| Damage 정확히 1개 | 17/24 | defenders 10 (Archer, Guardian, Bastion, Bruiser, Cannon, Marksman, Piercer, Ranger, Scout, Sniper) + enemies 7 (Basic, Debuffer, Needler, Rootcaster, Sniper, Tanker, Vanguard) | 적용 |
| Damage 0개 | 7/24 | defenders 5 (BlockingCaster, FireCaster, IceCaster, PoisonCaster — outputs 빈 배열, hazard 경유 / Healer — Heal만) + enemies 2 (Runner, Swift) | skip + 경고 |
| Damage 2개+ | 0/24 | 없음 | (불변식으로 금지) |
| Heal 정확히 1개 | 1/24 | Healer | heal 적용 |

## 완료 기준

- [x] 컬럼명 `atk` 확정, `heal` 포함 확정 — 사용자 확인 2026-07-02 (ralplan 승인 + 진행 지시)
- [x] 투영 규칙(exactly-1, skip+경고) 확정 — ralplan 합의 (Critic APPROVE)
- [x] v1 spec README에 v2 포인터 반영 (2026-07-02)
