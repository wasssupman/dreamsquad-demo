# Phase 8 VFX 리뷰

## 요약

1. 4종 VFX(Placement/Meteor/Tornado/Portal) 구현이 PHASE8.md §12 스펙과 정확히 대응하며, ECS 이벤트 채널(MeteorBurstEventsSingleton)도 기존 GoalReached/DefenderDeath 패턴을 따른다.
2. **`OnDestroy`에서 `_meteorBurstQueue` 미해제** — 네이티브 메모리 누수 버그가 1건 있고, `VfxSpawner._particleMaterial` 미해제도 동일 범주.
3. 시각 품질은 프로토타입 기준 양호하나, Tornado/Portal의 `orbitalY` 속도가 높아 저사양 기기에서 시각적 노이즈가 될 수 있으며, 파티클 수 튜닝 여지가 있다.

---

## A. 시각 품질

- **[Medium]** Placement ring 파티클 40개 / 수명 0.8s / radial 2.5 — 탑다운 1920x1080 기준 충분히 읽힌다. 다만 `startSize=0.15f`가 타일 크기(1.0) 대비 작아 밀도가 낮을 수 있다. **권고**: `startSize`를 `0.2f`로 올려보고, `sizeOverLifetime` 시작값을 `0.5f`로 조정하면 초반 가시성이 올라간다.

- **[Low]** Meteor burst 80개 파티클 / 0.9s — 폭발감이 충분하다. 색상 그라데이션(노랑→주황→갈색)이 잘 설계되어 있다. `gravityModifier=0.4f`로 자연스러운 낙하가 있다. 개선 여지: `startSpeed` 상한 6f가 `radiusWorld` 무관하게 고정이므로, 큰 반경일수록 파티클이 범위를 벗어날 수 있다. **권고**: `startSpeed` 상한을 `Mathf.Min(6f, radiusWorld * 3f)` 정도로 비례시키면 반경별 일관성이 생긴다.

- **[Medium]** Tornado swirl `orbitalY=6f` — 초당 6라디안 회전으로 매우 빠르다. 탑다운 시점에서 개별 파티클이 구분 안 되고 뭉개질 수 있다. **권고**: `orbitalY`를 `3f~4f`로 낮추고 `rateOverTime`을 `35f`로 줄이면 회전 궤적이 읽히면서도 토네이도 느낌이 유지된다.

- **[Low]** Portal swirl `orbitalY=5f` — Tornado와 유사한 빠른 회전. 보라(portal) vs 하늘색(tornado) 팔레트 구분은 명확하다. 동시 시전 시 혼동 우려 낮음. 다만 Portal beam(LineRenderer)의 `startWidth=0.12f`가 거리 대비 얇아서 탑다운에서 안 보일 수 있다. **권고**: `startWidth`/`endWidth`를 `0.18f~0.25f`로 올리거나, 색상 알파를 `0.8f`로 높여 가시성 확보.

- **[Low]** 4종 팔레트 구분: 시안(placement) / 주황(meteor) / 하늘색(tornado) / 보라(portal) — 색상환 상 4방향 분산이 잘 되어 있다. 겹침 시 혼동 위험 낮음.

---

## B. 성능

- **[Medium]** `ParticleMaterial` lazy init에서 `renderer.material = ParticleMaterial` 사용 (`VfxSpawner.cs:87,137,188,263`). Unity는 `renderer.material` 접근 시 내부적으로 Material을 복제(instantiate)한다. 4종 VFX 모두 동일 Material 인스턴스를 공유하려는 의도이나, `.material` setter가 매번 복제본을 만들어 GC 압박이 발생한다. **권고**: `renderer.sharedMaterial = ParticleMaterial`로 변경. ParticleSystemRenderer에서 `sharedMaterial`은 인스턴스 복제 없이 직접 할당한다.

- **[Low]** GameObject 생성/파괴 빈도 — Placement는 배치 시 1회, Meteor는 경고 만료 시 1회, Tornado/Portal은 스킬 시전 시 1회. 프로토타입 기준 풀링 불필요. 다만 Meteor burst가 동시 다발(5+ 동시 경고)이면 80파티클 × 5 = 400 동시 파티클 + 5 GameObject 생성이 발생한다. **권고**: 현 단계에서는 허용 범위. 이후 `ParticleSystem.Play()`+풀링 전환 시점은 프로파일링으로 판단.

- **[Low]** `new Material(shader)` cleanup — `VfxSpawner._particleMaterial`이 `OnDestroy`에서 해제되지 않는다 (아래 E 섹션 참조).

- **[Low]** Portal의 `root.transform.position = Vector3.zero` (`VfxSpawner.cs:201`) — 부모 root가 원점에 있고 자식 swirl이 worldPosition으로 배치된다. 문제는 없으나 root 위치를 entry와 exit의 중점으로 설정하면 씬 하이어라키 디버깅이 편하다.

---

## C. 코드 품질

- **[Medium]** 4개 Spawn 메서드(PlacementRing/MeteorBurst/Tornado/CreatePortalSwirl) 모두 동일 보일러플레이트를 반복한다: `new GameObject` → `AddComponent<ParticleSystem>` → main/emission/shape/colorOverLifetime/renderer 설정 → `Destroy`. 공통 helper `CreateParticleGO(string name, Vector3 pos)` → `(GameObject, ParticleSystem, ParticleSystemRenderer)` 튜플 반환으로 15줄 정도 줄일 수 있다. **권고**: 즉시 리팩터링보다는 5번째 이펙트 추가 시점에 추출. 현재 4종은 각각 미묘하게 달라서 과도한 추상화 위험.

- **[Medium]** 하드코딩 수치 중 SerializeField 노출 후보 — 색상 4종은 이미 `[SerializeField]`로 노출되어 있어 좋다. 그러나 **파티클 수**(40/80/50 rateOverTime/35 rateOverTime), **수명**(0.8/0.9/0.8/0.7), **속도**(radial 2.5/startSpeed 3~6/orbitalY 6/5)는 코드 내 리터럴이다. **권고**: 당장은 유지하되, 튜닝 필요 시 `[Header("Placement")] [SerializeField] int placementParticleCount = 40;` 식으로 점진 노출. 스펙 §12.6이 "duration/color는 필드 공개" 요구이므로 duration도 노출 후보.

- **[Low]** `MinMaxCurve` 사용 혼재 — `startLifetime`에 `constant` 접근(`VfxSpawner.cs:90`), `startSpeed`/`startLifetime`에 `constantMax` 접근(`VfxSpawner.cs:140,191`). 단일값 `MinMaxCurve`는 `.constant`가 올바르고, 범위값은 `.constantMax`가 올바르다. 현재 사용이 정확하다 — 혼재가 아닌 올바른 사용. 문제 없음.

- **[Low]** `Destroy(go, ...)` safety margin — PlacementRing `+0.1f`, MeteorBurst `+0.1f`, Tornado `+0.2f`, Portal `+1f`. Portal만 `+1f`로 크게 다르다. Portal은 swirl 2개 + LineRenderer이므로 여유가 필요하나 `+1f`는 다소 과하다. **권고**: Portal의 swirl `startLifetime=0.7f`이므로 root destroy를 `durationSec + 0.7f + 0.2f`로 명시적 계산하면 의도가 명확해진다.

---

## D. ECS 브리지 정합성

- **[Low]** `MeteorBurstEventsSingleton` 라이프사이클 — `EnsureQueriesAndQueues`에서 생성(`BattleBridge.cs:293-296`), `TeardownCurrentBattle`에서 entity 파괴 + queue dispose(`BattleBridge.cs:199-206`). GoalReachedEventsSingleton(`BattleBridge.cs:281-284`, `191-193`), DefenderDeathEventsSingleton(`BattleBridge.cs:287-290`, `195-197`)과 완전히 동일한 3단계 패턴(create queue → create singleton entity → teardown에서 entity 파괴 + queue dispose). 일관성 문제 없음.

- **[Medium]** `MeteorResolutionSystem.OnUpdate` 내 `GetSingletonRW` 호출 위치(`MeteorResolutionSystem.cs:67`). `hasBurstSingleton` 플래그를 ForEach 루프 밖에서 한 번 계산(`42줄`)하지만, `GetSingletonRW`는 루프 내부에서 매 iteration마다 호출된다. 여러 MeteorPending이 같은 프레임에 해결되면 N회 호출된다. **권고**: `GetSingletonRW`를 루프 밖으로 hoist하여 한 번만 호출. `if (hasBurstSingleton)` 블록을 루프 전에 resolve하고 `ref`로 보관:
  ```
  // 루프 전
  RefRW<MeteorBurstEventsSingleton> burstRef = default;
  if (hasBurstSingleton)
      burstRef = _burstEventsQuery.GetSingletonRW<MeteorBurstEventsSingleton>();
  // 루프 내
  if (hasBurstSingleton)
      burstRef.ValueRW.queue.Enqueue(...);
  ```

- **[Low]** `[BurstCompile]` 유지 여부 — `NativeQueue.Enqueue`는 Burst 호환이므로 `[BurstCompile]`이 깨지지 않는다. `GetSingletonRW`도 unmanaged 경로. 문제 없음.

---

## E. 버그 후보

- **[High]** `OnDestroy`에서 `_meteorBurstQueue` 미해제 (`BattleBridge.cs:1094-1104`). `_goalEventQueue`와 `_defenderDeathQueue`는 해제하지만 `_meteorBurstQueue`가 빠져있다. `TeardownCurrentBattle`에서는 해제하므로 Restart 경로는 안전하지만, **에디터 Play→Stop 또는 씬 전환 시 `OnDestroy`만 호출되면 네이티브 메모리 누수**가 발생한다. Unity 콘솔에 `A Native Collection has not been disposed` 경고가 뜰 것이다. **수정**: `OnDestroy`에 `if (_meteorBurstQueue.IsCreated) _meteorBurstQueue.Dispose();` 추가 (1103줄 뒤).

- **[Medium]** `VfxSpawner._particleMaterial` 미해제. `new Material(shader)`로 생성한 Material은 `Destroy` 호출 없이 GC에 의존하면 에디터에서 "%.0 material leaked" 경고가 뜬다. **수정**: `VfxSpawner`에 `OnDestroy()` 추가:
  ```csharp
  private void OnDestroy()
  {
      if (_particleMaterial != null) Destroy(_particleMaterial);
  }
  ```

- **[Medium]** `SpawnPortal` LineRenderer에 `Particles/Unlit` 셰이더 사용 (`VfxSpawner.cs:215`). `ParticleMaterial`을 그대로 할당하는데, Particles/Unlit 셰이더는 파티클 전용 vertex stream을 기대한다. LineRenderer는 `TEXCOORD0`만 제공하므로 **셰이더 키워드 불일치로 렌더링이 깨질 수 있다**(분홍색 또는 투명). **권고**: LineRenderer 전용 Material을 별도 생성하되 `Universal Render Pipeline/Unlit` 셰이더를 사용:
  ```csharp
  private Material _lineMaterial;
  // lazy init with Shader.Find("Universal Render Pipeline/Unlit")
  ```

- **[Medium]** `Shader.Find` 빌드 시 스트리핑 위험 (`VfxSpawner.cs:31-33`). URP 빌드에서 `Shader.Find("Universal Render Pipeline/Particles/Unlit")`는 해당 셰이더가 프로젝트 내 어떤 Material에서도 참조되지 않으면 **빌드에서 스트리핑**될 수 있다. 3단 폴백이 있지만 최악의 경우 3개 모두 스트리핑되면 `NullReferenceException`. **권고**: `ProjectSettings > Graphics > Always Included Shaders`에 `Universal Render Pipeline/Particles/Unlit` 추가, 또는 `Resources` 폴더에 더미 Material을 하나 두어 참조 보장.

- **[Low]** `CreatePortalSwirl`에서 `Destroy` 미호출 (`VfxSpawner.cs:223-265`). swirl GameObject는 parent(root)에 `SetParent`되어 있으므로 root가 `Destroy(root, durationSec + 1f)`로 파괴될 때 함께 파괴된다. 그러나 **swirl ParticleSystem의 duration이 끝나도 emission이 0이 아닌 rateOverTime으로 설정되어 있어** `main.loop = false`임에도 duration 이후 방출이 멈추고 잔여 파티클만 수명까지 표시된다. root destroy 타이머(`durationSec + 1f`)가 `startLifetime(0.7f)` 이상이므로 파티클이 잘려 보이지는 않는다. 안전.

---

## 우선순위 Top 5 개선점

| 순위 | 파일:줄 | 심각도 | 요약 |
|------|---------|--------|------|
| 1 | `BattleBridge.cs:1094-1104` | **High** | `OnDestroy`에 `_meteorBurstQueue.Dispose()` 누락 — 네이티브 메모리 누수 |
| 2 | `VfxSpawner.cs:215` | **Medium** | LineRenderer에 Particles/Unlit 셰이더 → 렌더링 깨짐 가능. 별도 URP/Unlit Material 사용 |
| 3 | `VfxSpawner.cs:31-33` | **Medium** | `Shader.Find` 빌드 스트리핑 대비 — Always Included Shaders 등록 |
| 4 | `VfxSpawner.cs` 전체 | **Medium** | `_particleMaterial` OnDestroy 미해제 — Material 누수 |
| 5 | `MeteorResolutionSystem.cs:67` | **Medium** | `GetSingletonRW` 루프 내 반복 호출 → 루프 밖 hoist |

---

## F. 후속 Phase 제안 (스코프 외)

- **Projectile hit 스파크**: Shuriken one-shot burst (10~15 파티클). `ProjectileHitSystem` 해결 시점에 동일 NativeQueue 패턴으로 BattleBridge drain. VfxSpawner에 `SpawnHitSpark(Vector3, Color)` 추가.

- **Enemy dissolve**: Shader Graph `Dissolve` 서브그래프 — `_DissolveAmount` float 프로퍼티를 0→1 트윈. `UnitLifecycleSystem`에서 Health 0 감지 시 dissolve 시작, 완료 후 entity 파괴. Shuriken보다 Shader Graph가 적합.

- **Synergy glow**: 배치 유닛 주변에 상시 발광 링. Shuriken looping emission(rateOverTime 5~10) + `SynergyBuff` 존재 여부에 따라 enable/disable. VfxSpawner보다는 SpineDefenderPool 쪽에서 관리하는 것이 자연스러움.

- **Slow/Buff aura**: 필드 이펙트. Tornado swirl과 유사한 Shuriken looping이지만 색상만 다름(파랑=slow, 노랑=buff). `EffectSpawner.ApplySlow` 호출 시점에 VfxSpawner 연동.

- **SO 기반 VFX 프로파일**: 파티클 수/수명/색상/속도를 `ScriptableObject`에 묶어 Inspector 튜닝 + 유닛별 프리셋 교체 가능하게. 5종 이상 이펙트가 쌓이면 도입 가치 높음. 현재 4종에서는 과도한 추상화.
