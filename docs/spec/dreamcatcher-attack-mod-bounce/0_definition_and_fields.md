# 0 — 정의 계층 (c) 부류 + 투사체 bounce 필드

## 목적

공격 개조형 카드를 순수 데이터로 표현하고, 투사체 파이프라인에 bounce 파라미터 슬롯을 additive 로 뚫는다. 이 unit 은 전부 미사용 상태로 컴파일만 통과하면 된다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — 개조형 정의 추가 (같은 파일, 같은 계약)
- 수정: `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `attackMods[]` append
- 수정: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` — bounce 필드
- 수정: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs` (실제 파일 위치 확인) — bounce 필드

## 구현

`DcMechanic.cs` 에 추가 (ECS 무참조 계약 유지):

```csharp
public enum DcAttackModKind { None, ProjectileBounce } // 후속 kind 는 append

[Serializable] public struct DcAttackModSpec
{
    public DcAttackModKind kind;
    public int count;          // ProjectileBounce: 튕김 횟수
    public int tileRange;      // 재타겟 검색 반경 (Chebyshev 타일)
    public float damageMul;    // 튕김당 감쇠 (1 = 무감쇠)
}
```

`DreamcatcherCard`: `public DcAttackModSpec[] attackMods;` — 맨 뒤 append (직렬화 보존, bake-time 전용 주석).

`ProjectileSpawnRequest` / `ProjectileState`: `public int bounceRemaining; public int bounceTileRange; public float bounceDamageMul;` — 기본값 0/0/0 = 기존 경로 무영향 (bounceRemaining 0 이면 어떤 분기도 타지 않음).

## 완료 기준

- [x] 컴파일 통과 (refresh scope=all)
- [x] 기존 카드/투사체 에셋·경로 무변동 (필드 append-only, 기본값 0)
- [x] 정의 계층에 ECS 참조 없음

완료 확인: 2026-07-09 — 컴파일 클린, EditMode 582 통과(머지 후). append-only 필드라 기존 에셋 직렬화 무변동. code-review(low) 지적 0건. 이 문서와 동일 커밋.
