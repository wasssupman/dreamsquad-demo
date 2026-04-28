# Tests

**작업 구분**: 8

## 목적

배리에이션 인프라의 결정성과 hit 채널의 lifecycle 을 EditMode + PlayMode smoke 로 회귀 보호한다.

## 변경 대상

- New: `Assets/_Project/Tests/EditMode/ProjectileVariationTests.cs`
- New: `Assets/_Project/Tests/PlayMode/ProjectileVisualSmokeTest.cs`

## EditMode 테스트

대상은 view 풀에서 추출 가능한 순수 함수만 — `ApplyHueShift`, jitter 결정성, `TextureSelectMode` 인덱스 산출.

```csharp
[Test]
public void HueShift_ZeroPreservesColor() { /* tintColor 그대로 */ }

[Test]
public void HueShift_WrapsAroundOne() { /* hue=0.95 + shift=0.1 → 0.05 */ }

[Test]
public void RandomSelect_DeterministicWithSeed() {
    var rng1 = new System.Random(42);
    var rng2 = new System.Random(42);
    for (int i = 0; i < 100; i++) {
        Assert.AreEqual(rng1.Next(3), rng2.Next(3));
    }
}

[Test]
public void SequentialSelect_WrapsAroundLength() { /* counter 0..7 → idx 0,1,2,0,1,2,0,1 */ }

[Test]
public void ScaleJitter_ZeroProducesIdentity() { /* scaleMul == 1 정확히 */ }
```

## PlayMode smoke

```csharp
[UnityTest]
public IEnumerator Projectile_FullLifecycle() {
    // BattleScene 로드, Cannon 1대 배치, 적 1체 spawn, 충돌까지 대기.
    // 검증:
    // - 발사 후 view pool 의 _active 가 1개 증가
    // - 충돌 후 _active 가 0 으로 복귀
    // - hit prefab 이 한 번 재생되고 lifetime 후 풀로 반환
    // - 이벤트 큐가 enqueue/dequeue 카운트 일치
}
```

PlayMode 진입이 어려우면 `BattleBridge` 를 직접 인스턴스화하지 않고 `ProjectileViewPool` 단독 테스트로 축소:

```csharp
[UnityTest]
public IEnumerator HitPlayback_ReturnsToPool() {
    var pool = TestHelpers.MakeProjectileViewPool();
    var prefab = TestHelpers.MakeDummyParticlePrefab(lifetime: 0.2f);
    pool.PlayHit(prefab, float3.zero);
    yield return new WaitForSeconds(0.4f);
    Assert.AreEqual(0, pool.ActiveCount);
}
```

## 완료 기준

- EditMode 5개 테스트 그린.
- PlayMode 1개 그린 (또는 축소 버전 그린).
- 기존 `DraftSessionTests` 등 EditMode 슈트 회귀 없음.
- read_console Error/Warning 0.

확인 2026-04-28 / 커밋: (pending)
