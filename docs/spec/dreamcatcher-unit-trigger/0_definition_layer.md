# 0 — 정의 계층 (아키텍처 비의존 메커닉 데이터)

## 목적

트리거형 드림캐쳐 메커닉을 **ECS 를 모르는 순수 데이터**로 표현한다. 카드 SO 가 이 데이터를 담고, 해석(베이크/실행)은 unit 1~2 의 몫.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs`
- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`

## 구현

`DcMechanic.cs` (namespace `Wassup.Data`):

```csharp
public enum DcTriggerKind { None, AttackN }          // Kill/Damaged/NextWave 는 후속 append
public enum DcPayloadKind { None, ProjectileToTarget } // SelfTileAoe/NextAttackModifier 는 후속 append

[Serializable] public struct DcTriggerSpec { public DcTriggerKind kind; public int period; }
[Serializable] public struct DcPayloadSpec
{
    public DcPayloadKind kind;
    public float magnitude;            // ProjectileToTarget: flat damage
    public ProjectileData projectile;  // 투사체 궤적/뷰 정의 (에셋 참조)
}
[Serializable] public struct DcMechanic { public DcTriggerSpec trigger; public DcPayloadSpec payload; }
```

`DreamcatcherCard` 확장 — **끝에 append** (기존 에셋 직렬화 보존, 파일 내 기존 주석 선례와 동일하게 사유 주석):

```csharp
public enum CardBinding { Axis, Unit }   // Axis = 기존 축 매칭, Unit = 개별 유닛 부착
// DreamcatcherCard 필드 추가 (art 뒤):
public CardBinding binding;              // 기본 Axis (=0, 기존 에셋 값 보존)
public DcMechanic[] mechanics;           // 보통 0~1개. binding=Unit 카드가 사용
```

계약:

- 이 파일들은 `Unity.Entities` / `Wassup.Battle.*` 를 참조하지 않는다 (using 포함).
- `effects[]`(스탯%) 와 `mechanics[]` 는 공존 가능하지만, 이번 spec 의 해석 경로는 `binding=Unit` 카드의 `mechanics` 만 소비한다. `binding=Unit` + `effects` 조합의 해석은 후속(현재 미정의 — `ApplyDreamcatcherCard` 는 Axis 카드 전용 유지).
- 설명 템플릿 렌더링은 후속 — 필드만 확보.
- `mechanics[]` 는 **베이크 타임 전용** 읽기 (부착 API 1회) — per-frame 순회 금지 (managed array GC). 코드 주석으로 명시.
- `projectile` 필드는 `ProjectileToTarget` 전용 — 두 번째 payload kind 추가 시 struct 분리 여부 재평가 (README 후속 후보).

## 완료 기준

- [x] 컴파일 통과 (신규 .cs 는 refresh scope=all — lessons 참조)
- [x] 기존 `DreamcatcherCard` 에셋 인스펙터에서 값 변동 없음 (binding=Axis, mechanics 비어 있음)
- [x] 정의 계층 파일에 ECS/Battle 참조 없음 (리뷰 육안)

완료 확인: 2026-07-08 — 컴파일 0 에러, 기존 카드 에셋 dirty 없음(직렬화 무변동), code-review(low) 지적 0건. 이 문서와 동일 커밋.
