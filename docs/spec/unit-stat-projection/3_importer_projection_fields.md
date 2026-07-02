# 3. Importer Projection Fields

## 목적

시트 `atk`/`heal`이 outputs의 유일 Damage/Heal 항목 magnitude로 투영되도록 임포터를 확장한다. 전환기 silent no-op 방지용 deprecation shim 포함.

## 변경 대상

- `Assets/_Project/Editor/UnitStatImport/UnitStatImportDto.cs`
- `Assets/_Project/Editor/UnitStatImport/UnitStatFieldMapper.cs`
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs`
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitStatImportTests.cs`
- `Assets/_Project/Tests/EditMode/UnitStatImport/UnitRosterInvariantTests.cs` (신규)

## 구현

- DTO: `float? atk` (Defender/Enemy 양쪽), `float? heal` (Defender만). 기존 `float? attackDamage`는 **shim으로 잔류** — 수신(non-null) 시 값 미적용 + "attackDamage는 atk로 개명됨, 값 미적용" 결과 로그 경고.
- 매퍼 skip-list: `{"id"}` → `{"id", "atk", "heal", "attackDamage"}` 상수(`static readonly HashSet<string>`)로. **`aggroAttackDamage`는 skip-list에 넣지 않는다** — live 필드, 리플렉션 매핑 유지 (`*AttackDamage` 패턴 일반화 금지).
- `ApplyPayload`: 리플렉션 복사 후 `AttackOutputStats.TrySetUniqueMagnitude`로 투영. 실패 시 사유(0개/2개+)를 결과 로그에 명시. 결과 요약에 `projected`/`skipped(사유)` 카운트 추가.
- `UnitRosterInvariantTests` (신규): `Data/{Defenders,Enemies}` 전 asset 스캔 — ① Damage 항목 ≤1 ② Heal 항목 ≤1 ③ id 비어있지 않음 + 중복 없음. 실패 메시지는 재협상 프롬프트 문구 포함 (README 계약 참조).

## 완료 기준

- [ ] compile 오류 없음
- [ ] 신규 테스트 6+ 통과: atk 적용(magnitude 갱신) / Damage 0개 skip+사유 / (합성) Damage 2개+ skip / heal 적용 / atk 생략 시 기존 magnitude 유지 / attackDamage 수신 시 미적용+경고
- [ ] `UnitRosterInvariantTests` 전 asset 통과
- [ ] 수동: 로컬 JSON(또는 실 엔드포인트)으로 Archer `atk` 왕복 — outputs magnitude 갱신 + 카드 DMG 연동 확인. 실 엔드포인트 미제공 시 잔류 항목으로 기록
- [ ] 기존 스위트 회귀 없음
