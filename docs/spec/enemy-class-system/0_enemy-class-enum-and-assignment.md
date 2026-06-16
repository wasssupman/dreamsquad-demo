# Unit 0 — Enemy 클래스 enum + 필드 + 분류 + defender Fighter 개명

## 목적

적 클래스 데이터 토대를 만든다. enum 정의, `AttackUnitData` 필드 추가, 기존 6종 분류, defender `Bruiser`→`Fighter` 개명. 행동 로직은 포함하지 않는다 (데이터만).

## 변경 대상

- (신규) `Assets/_Project/Scripts/Data/EnemyClass.cs`
- `Assets/_Project/Scripts/Data/AttackUnitData.cs`
- `Assets/_Project/Scripts/Data/DefenderClass.cs` (Bruiser→Fighter)
- `Assets/_Project/Data/Defenders/Defender_Bruiser.asset` (displayName)
- `Assets/_Project/Data/Enemies/Enemy_*.asset` (6종 enemyClass)

## 구현

### EnemyClass enum

```csharp
public enum EnemyClass { None, Tanker, Runner, Bruiser, Shooter }
```

값 고정: None=0, Tanker=1, Runner=2, Bruiser=3, Shooter=4. 에셋 직렬화가 정수에 의존하므로 순서를 바꾸지 않는다.

### AttackUnitData

`public EnemyClass enemyClass = EnemyClass.None;` 추가 (Header "Class").

### DefenderClass

`Bruiser` 멤버를 `Fighter` 로 개명. 값(3) 유지 → `role: 3` 에셋 영향 없음. 코드 참조처 없음(BattleBridge 는 Ranger/Guardian 만 사용).

### 에셋 enemyClass 값

| 에셋 | enemyClass |
|---|---|
| Enemy_Runner | 2 (Runner) |
| Enemy_Swift | 2 (Runner) |
| Enemy_Basic | 3 (Bruiser) |
| Enemy_Tanker | 1 (Tanker) |
| Enemy_Needler | 4 (Shooter) |
| Enemy_Rootcaster | 4 (Shooter) |

Defender_Bruiser.asset: `displayName: Bruiser` → `Fighter` (role:3, id:bruiser 유지).

## 완료 기준

- [x] Unity 컴파일 에러 없음 (`EnemyClass`, `DefenderClass.Fighter` 인식).
- [x] 6종 enemy 에셋이 의도대로 분류 (reflection 검증: Runner/Swift→Runner, Basic→Bruiser, Tanker→Tanker, Needler/Rootcaster→Shooter).
- [x] Defender_Bruiser displayName/role 이 "Fighter".
- [x] 적 SO 6종 + 머티리얼을 `Assets/_Project/Data/Enemies/` 로 이동 (GUID 보존, 참조 유지).

완료: 2026-06-17 / 커밋 해시 `<feature-commit>` (아래 docs 커밋에서 기재)
