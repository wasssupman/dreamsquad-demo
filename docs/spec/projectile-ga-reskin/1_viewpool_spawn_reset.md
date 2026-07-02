# ViewPool Spawn Reset (streak 제거)

**작업 구분**: 1

## 목적

풀 재사용 시 view GameObject 가 **이전 사망 위치에서 새 스폰 위치로 1프레임 순간이동**하면서, world-space 파티클과 TrailRenderer 가 그 사이에 줄(streak)을 긋는 문제를 제거한다. GA 프리팹은 강한 world-space 궤적 + Trail×3 이라 이 가드 없이는 as-is 외형이 깨진다.

## 배경 (현재 동작)

`ProjectileViewPool.Spawn` 은 `SetActive(true)` 후 scale/rotation/MPB 만 세팅하고 **position 은 세팅하지 않는다** — 위치는 다음 `SyncTransforms` 프레임에야 잡힌다. 풀에서 꺼낸 뷰는 직전 투사체가 죽은 위치에 활성화된다. 또한 spawn 시 Trail/Particle 리셋이 없다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` (`Spawn`, 필요 시 `PlayHit`/`PlayCast`)

## 구현

- `Spawn`: `view.SetActive(true)` 직후, 첫 SyncTransforms 전에
  - `view.transform.position = ToView(initialPosition)` 즉시 세팅(`initialPosition` 파라미터 이미 존재).
  - `ResetVfx(view)` 호출.
- `ResetVfx(GameObject view)` 헬퍼:
  - 캐시된 `TrailRenderer[]` 각각 `Clear()`.
  - 캐시된 `ParticleSystem[]` 각각 `Clear(true)` 후 `Play(true)` (또는 `Simulate(0,true,true)` 후 `Play`). world/local 무관하게 잔상 제거 + 신선 재생.
- **캐시**: 기존 `ViewRendererCache` 패턴을 따라 view 별 `TrailRenderer[]`/`ParticleSystem[]` 를 1회 `GetComponentsInChildren(true)` 로 캐시. 핫패스 스폰마다 재탐색 금지(GC 회피 — projectile-visual-upgrade rev1 원칙 유지).
- `PlayHit`/`PlayCast`: 풀 재사용 경로면 동일하게 위치 세팅 후 particle replay. 매번 fresh Instantiate 라면 무해(스킵 가능) — 기존 코드 확인 후 최소 변경.

## 완료 기준

- GA Arrow(유닛 0 산출 프리팹)를 임시 배선한 뒤 같은 타워에서 연사 → 발사 간 **streak 줄 없음**, 각 투사체가 타워에서 정상 출발.
- 풀 반환→재사용 후에도 첫 프레임 위치 정확(이전 사망 위치 잔류 없음).
- `_active`/`_pool` 카운트 누수 없음(N회 발사 후 카운트 안정).
- `read_console` Error/Warning 0. (Play 검증, 에디터 포커스 필요.)
