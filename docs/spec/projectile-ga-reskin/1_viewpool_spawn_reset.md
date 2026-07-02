# ViewPool as-is 가드 (streak + native colors)

**작업 구분**: 1

## 목적

GA VFX 를 ViewPool 에서 데모와 같은 외형으로 재생하기 위한 두 가드를 넣는다.

1. **streak 제거**: 풀 재사용 시 view 가 **이전 사망 위치→새 스폰 위치로 1프레임 순간이동**하며 world-space 파티클/TrailRenderer 가 줄(streak)을 긋는 문제. GA 는 강한 world-space 궤적 + Trail×3 이라 이 가드 없이는 as-is 가 깨진다.
2. **native colors 보존**: ViewPool `ApplyMpb` 가 매 스폰 `_Color`/`_EmissionColor` 를 데이터 tint(기본 흰색)로 덮어쓴다. GA 머티리얼은 `_Color` 가 HDR 밝기(예: 4.24)·`_EmissionColor` 가 authored 유색이라, 흰색 덮어쓰기로 밝기/색이 죽는다. `ProjectileData.preserveVfxColors` 로 recolor 를 건너뛰어 프리팹 고유 색을 그대로 쓴다.

## 배경 (현재 동작)

`ProjectileViewPool.Spawn` 은 `SetActive(true)` 후 scale/rotation/MPB 만 세팅하고 **position 은 세팅하지 않는다** — 위치는 다음 `SyncTransforms` 프레임에야 잡힌다. 풀에서 꺼낸 뷰는 직전 투사체가 죽은 위치에 활성화된다. 또한 spawn 시 Trail/Particle 리셋이 없다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (`Spawn`/`PlayHit`/`PlayCast`, `ResetVfx`/`ComputeRootParticles` 신설)
- Modify: `Assets/_Project/Scripts/Data/ProjectileData.cs` (`preserveVfxColors` 필드)

## 구현

- `Spawn`: `view.SetActive(true)` 직후, 첫 SyncTransforms 전에
  - `view.transform.position = ToView(initialPosition)` 즉시 세팅(`initialPosition` 파라미터 이미 존재).
  - `ResetVfx(view)` 호출.
- `ResetVfx(GameObject view)` 헬퍼:
  - 캐시된 `TrailRenderer[]` 각각 `Clear()`.
  - 캐시된 `ParticleSystem[]` 각각 `Clear(true)` 후 `Play(true)` (또는 `Simulate(0,true,true)` 후 `Play`). world/local 무관하게 잔상 제거 + 신선 재생.
- **캐시**: `ViewRendererCache` 에 `trails`/`rootParticles`(top-level PS만) 1회 캐시. 핫패스 GetComponentsInChildren 금지. `ComputeRootParticles` 탐색은 프리팹 내부로 한정(viewRoot 위 풀 계층 제외).
- **루트 PS만 Play(true)**: 자식/서브에미터로 cascade 되게 해 authored 트리거 관계 보존. 가정: 루트 PS 는 스폰 시 재생돼야 하는 시스템.
- `PlayHit`/`PlayCast`: 위치/회전 세팅 후 `ResetVfx` 호출 → 풀 재사용 시 파티클 신선 재생.

### native colors (preserveVfxColors)

- `ProjectileData.preserveVfxColors`(기본 false = 기존 recolor 유지). true 면 `Spawn` 이 `ApplyMpb`(tint/emission/texture) 를 **건너뛴다**. 프리팹 머티리얼 고유색 사용.
- RNG draw 수는 preserveVfxColors 무관하게 동일 소비 → 시각 결정성 유지(GA 는 textureVariants 비어 SelectTexture 가 RNG 미소비).
- 기존 투사체(Arrow/Bolt/CannonBall 등)는 false 라 동작 불변.

## 완료 기준

- GA Arrow(유닛 0 산출 프리팹)를 임시 배선한 뒤 같은 타워에서 연사 → 발사 간 **streak 줄 없음**, 각 투사체가 타워에서 정상 출발.
- 풀 반환→재사용 후에도 첫 프레임 위치 정확(이전 사망 위치 잔류 없음).
- `preserveVfxColors=true` 인 GA 투사체가 프리팹 고유 밝기/색으로 렌더(흰색 wash 없음). 기존 투사체(false)는 렌더 불변.
- `_active`/`_pool` 카운트 누수 없음(N회 발사 후 카운트 안정).
- `read_console` Error/Warning 0. (Play 검증, 에디터 포커스 필요. 시각 확정은 유닛 2 A/B 에서.)

리뷰: code-review APPROVE-WITH-NITS(2026-07-03) — 조상 탐색 viewRoot 한정 + playOnAwake 가정 주석 반영. streak 로직 caller 불변식까지 검증.
