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

- [x] compile 오류 없음 (2026-07-02)
- [x] 신규 테스트 통과: DTO atk/heal 역직렬화 / skip-list(atk·heal·attackDamage 미복사, aggroAttackDamage 유지) / 투영 magnitude 갱신 / Damage 0개 skip+사유 / Damage 2개+ skip / heal 적용 / atk 생략 시 magnitude 유지 / attackDamage shim 경고
- [x] `UnitRosterInvariantTests` 전 asset 통과 (Damage≤1 / Heal≤1 / id 타입별 유일 — 교차타입 sniper 중복은 정상으로 확인)
- [x] end-to-end 왕복 (execute_code, 순 변화 0): archer atk=15 → `projected 1`, poison_caster atk → `skipped — no Damage output`, archer attackDamage → shim 경고. 실 Swagger 엔드포인트 왕복은 URL 미제공으로 잔류
- [x] 기존 스위트 회귀 없음 (444개, 기지 실패 1건 제외)

## 주의 (2026-07-02)

end-to-end 검증 중 `ApplyPayload`가 매칭 SO에 무조건 `SetDirty`+`SaveAssetIfDirty`를 호출하는 특성 때문에 `Defender_Archer.asset`·`Defender_PoisonCaster.asset`이 재직렬화됨(기본값 필드 라인 추가). 두 파일은 세션 시작 전부터 사용자 WIP 로 dirty 상태였고, 커밋에는 포함하지 않음. 실 asset 대상 검증 시 Save 부작용에 유의.
