# 1 — ECS 슬롯 + 부착 경로 (해석 계층: 번역자)

## 목적

정의 계층 카드를 defender 엔티티의 unmanaged 슬롯으로 **베이크해 부착**하는 경로와, dc 투사체가 기본 공격 요청과 충돌하지 않는 **request 캐리어** 기반을 만든다. 발동(카운트/발사)은 unit 2.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs`
- 신규: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileRequestCarrier.cs` (태그)
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 부착 API + `DrainProjectileSpawnRequests` 캐리어 분기

## 구현

`DcTriggerSlot` (Combat 소유 buffer element):

```csharp
public struct DcTriggerSlot : IBufferElementData
{
    public int instanceId;               // 효과 인스턴스 식별 (독립 카운터/후속 회수용). int = _dcStackCounter 선례.
                                         // stackId 와 별개 네임스페이스 — 어떤 downstream 에서도 stackId 와 비교 금지.
    public Wassup.Data.DcTriggerKind trigger;
    public ushort period;                // AttackN 의 N
    public ushort counter;               // 소유 쓰기: AttackSystem RESOLVE 만
    public Wassup.Data.DcPayloadKind payload;
    public float magnitude;              // flat damage
    public int projectileDataIndex;      // GetOrCreateProjectileDataIndex 베이크
    public float speed, hitThreshold, visualScale; // ProjectileData 에서 베이크 (AttackSystem 은 managed SO 접근 불가)
}
```

`ProjectileRequestCarrier`: 빈 `IComponentData` 태그. `DrainProjectileSpawnRequests` 루프에서 `SpawnProjectile` 호출 직후 **기존 RemoveComponent 라인들보다 먼저** `HasComponent<ProjectileRequestCarrier>` 를 검사 — 캐리어면 `_em.DestroyEntity` 후 `continue` (파괴 예정 엔티티에 불필요한 RemoveComponent 구조 변경을 얹지 않는다). 기존 shooter-attached 경로는 무변경. 캐리어는 `DefenderUnitTag`/outputs 버퍼가 없으므로 발사 SFX 미재생 + outputs 스냅샷 없음(damage-only) — 의도된 동작.

BattleBridge 부착 API:

```csharp
public bool ApplyDreamcatcherCardToUnit(Entity defender, Wassup.Data.DreamcatcherCard card)
```

- 가드: `binding != Unit` / mechanics 비어 있음 → false(무로그), ECS 미준비·entity 미존재·**비-defender(DefenderUnitTag 부재)** → LogWarning + false (계약 2 를 API 가 런타임 강제 — 리뷰 반영 2026-07-08). ProjectileToTarget 의 `magnitude <= 0` mechanic 은 warn + skip.
- mechanic 마다: `instanceId = _dcInstanceCounter++`(신규 **int** 카운터, `BeginPlacement` 에서 리셋), `ProjectileData` → `GetOrCreateProjectileDataIndex` + speed/hitThreshold/visualScale 베이크(참고: `_projectileDataByIndex` 는 BeginPlacement 에서 리셋되지 않는 session-lifetime 레지스트리 — 베이크된 index 는 매치를 넘어 유효), defender 의 `DynamicBuffer<DcTriggerSlot>` 에 append (버퍼 없으면 `_em.AddBuffer` — 즉시, ModifierApplySystem 의 em.AddBuffer 선례).
- 유효 mechanic 이 1개 이상 부착되면 true. `trigger/payload == None` 또는 `projectile == null`(ProjectileToTarget) mechanic 은 skip + `Debug.LogWarning`.
- 레지스트리(카드↔유닛↔instanceId, 회수)는 후속 spec — 지금은 부착만.
- teardown: 슬롯은 defender 엔티티에 붙으므로 기존 엔티티 파괴 경로에 자동 포함. 캐리어 엔티티는 `ProjectileRequestCarrier` 로 `DestroyEntitiesByType` teardown 목록에 추가.

## 완료 기준

- [x] 컴파일 통과 (refresh scope=all)
- [x] 기존 투사체 경로 무회귀: 기존 PlayMode smoke 통과 (원거리 유닛 발사→히트 정상)
- [x] execute_code 로 배치 유닛에 부착 → `DcTriggerSlot` 버퍼 길이/필드 값 reflection 확인 (같은 카드 2회 부착 = 슬롯 2개, instanceId 상이)

완료 확인: 2026-07-08 — 컴파일 클린. Play 부착 검증(슬롯 2개·instanceId 0/1·베이크 값 정확·Axis/비-defender/magnitude0 거절). 무회귀는 stash 베이스라인 비교로 확정(EditMode 561·PlayMode 14 실패 목록이 HEAD 와 동일 = diff 기인 회귀 0; 사전실패 4건은 무관 — SkyFallTests 불일치·ProjectileVisualSmoke Grid 오염·Auth 타임아웃·ObstaclePlacer 기지). 8앵글 code-review 3건 반영(비-defender 거절 = 계약 2 런타임 강제, magnitude≤0 skip, 무로그 가드에 LogWarning). 이 문서와 동일 커밋.
