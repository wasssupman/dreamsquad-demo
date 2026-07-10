# 1 — 보스 위협 테이블 (텔레포트 타겟 소스)

## 목적

보스가 "자신에게 가장 많은 데미지를 입힌 방어유닛"을 알 수 있게, **보스 전용** 위협 누적 버퍼를 정의한다. 텔레포트(§3)의 타겟 소스이자, 향후 위협 기반 타겟팅의 토대.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/ThreatTable.cs` (버퍼 + 조회 순수함수)
- 신규 채널: `ThreatHitEventsSingleton` (Combat→Combat 위협 귀속, AggroHitEvents 패턴)
- `ProjectileSpawnRequest`/`ProjectileState` — `owner`(발사 defender) 필드 추가 (원거리 귀속)

## 구현

### 버퍼 (보스 전용)

```csharp
public struct ThreatEntry : IBufferElementData
{
    public Entity attacker;      // 방어유닛
    public float cumulativeDamage;
}
```

- **보스 엔티티에만** 부착(일반 적 제외 — 계약 5). 부착은 보스 스폰 베이크(유닛 5)에서.
- 소유 = Combat. 누적 쓰기 = 데미지 귀속 지점. 조회(RO) = 텔레포트 arm.

### 위협 리더 조회 (순수함수, EditMode 대상)

```csharp
// 최대 cumulativeDamage 의 attacker 반환. 빈 테이블/전멸 시 Entity.Null.
static Entity ThreatLeader(DynamicBuffer<ThreatEntry> table, ...aliveCheck)
```

- **alive 정의**: attacker = 조회 시점 `LocalTransform` 컴포넌트가 존재하는 엔티티. 같은 프레임 파괴로 없으면 제외(→ 폴백). 이 정의를 §3 텔레포트 조회와 공유(MED-6 경합 해소).
- 결정론: 동점이면 안정 순서(entity index) 우선.

### ⚠ 핵심 seam — 공격자 귀속 (load-bearing)

**현재 `IncomingDamage { float amount }` 는 공격자 신원을 담지 않는다** (`IncomingDamage.cs:10`). 위협 누적은 "누가 때렸나"가 필수라 귀속 경로가 없으면 성립 불가. 두 방향:

- **(A) 보스 전용 위협 채널** *(권장)* — `AggroHitEventsSingleton` 패턴 대칭. Combat 데미지 생산자가 **피해 대상이 보스일 때만** `ThreatHitEvent { boss, attacker, amount }` 를 추가 enqueue. 드레인이 보스 `ThreatTable` 에 누적. 범용 `IncomingDamage` 무변경 → **회귀 0**. 생산자는 공격자를 아는 지점에서만 발화.
- **(B) `IncomingDamage.source` 확장** — 범용 채널에 `Entity source` 추가. 모든 append 지점(`AttackSystem:502`, `ProjectileHitSystem:108/147/177/265`) + 드레인 수정. 회귀 표면 큼. 보스만 필요한 지금은 과함.

→ **(A) 채택 + 원거리 귀속** (사용자 결정 2026-07-10 — 렌즈 A HIGH-2 반영):
- 근접(`AttackSystem:502`) — 공격자 엔티티가 쿼리 내에서 알려짐 → 즉시 귀속.
- 투사체(`ProjectileHitSystem`) — **현재 `ProjectileState` 에 shooter 필드 없음**(`target` 만 존재). → `ProjectileSpawnRequest`/`ProjectileState` 에 `owner`(발사 defender) 필드 추가, 스폰 지점(`AttackSystem` 발사 arm — 기본 공격·dc-trigger 캐리어 발사 모두, 콕콕바늘류 카드 투사체도 부착 defender 로 귀속)에서 채우고, 히트/스플래시/TileAoe 착탄 시 대상이 보스면 `ThreatHitEvent{boss, owner, amount}` enqueue. bridge 캐스트 스킬(Meteor 등 Active 카드)은 owner 미설정(=Null) 유지.
- 근접+원거리 모두 누적 → 텔레포트가 진짜 "가장 많은 데미지" 리더를 조준. **폴백(§3)은 상시 경로가 아니라 진짜 엣지**(보스 무피해로 위협 0, 또는 리더 사망)로 격하.

> **회귀 격리**: 범용 `IncomingDamage` 는 무변경. `owner` 필드는 추가만(기본 `Entity.Null`), `ThreatHitEvent` enqueue 는 **대상이 보스일 때만** — defender 피격 경로 무영향.

- **enqueue 가드(N2)**: `owner != Entity.Null && owner is defender` 일 때만 enqueue. 플레이어 Meteor(owner=Null)가 보스를 때려도 쓰레기 threat 엔트리 안 쌓임. (alive 조회가 Null 을 배제해 blink 는 이미 안전하나, 버퍼 위생을 위해 진입에서 차단.)

## 완료 기준

- [x] `ThreatLeader` 순수함수 EditMode 테스트(누적·동점 결정론·죽은 attacker(LocalTransform 부재) 제외·빈 테이블 Null).
- [ ] `owner` 필드가 근접+원거리 양쪽 데미지를 보스 위협에 귀속(원거리 defender 만으로도 리더 성립) — Play 확인.
- [ ] 위협 채널 (A) 가 보스 대상 히트에만 발화(defender 피격은 무영향) — 회귀 0 확인.

확인 2026-07-10 — 코드/EditMode 완료(621 중 619 그린, ThreatTableTests 7종). Play 항목 2건은 보스 베이크 부재로 unit 6 e2e 이연. code-review(medium) 반영 3(TryCredit 단일화·근접 게이트 hoist·CLAUDE.md 채널 16)/기각 3. 커밋 `507dc1e5`.
