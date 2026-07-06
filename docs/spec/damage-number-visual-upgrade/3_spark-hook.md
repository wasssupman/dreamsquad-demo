# 3 · 임팩트 스파크 훅 (선택 / 후행)

## 목적

참고 이미지의 타격 지점 **스파크/파편 파티클**을 얹어 임팩트를 마감한다. 모바일 드로우콜을 지키기 위해 **숫자마다가 아니라 스폰 클러스터당 공용 파티클 1개**만 재생한다. 선택 기능 — 0·1·2 완료 후, 코덱스 파티클 도착 시 진행.

## 의존

- **코덱스 스파크 파티클** (`assets-codex-request.md` 항목 2). VFX 저작/통합은 `unity-vfx-authoring` + `unity-vfx-integration` 스킬 경로.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/DamageNumberSpawner.cs` — 클러스터 판정 + 파티클 재생 삽입점
- (VFX) 스파크 파티클 프리팹 (코덱스 `_SKELETON` → 통합)
- 필요 시 `VfxSpawner` 재사용 검토(기존 위치 VFX 스폰 패턴)

## 구현

### 배칭 계약 (critic 반영)

- `BattleBridge.DrainDamageNumberEvents` 는 한 프레임에 큐를 **전량 드레인**하며 `Spawn` 을 즉시 per-event 호출한다. `Spawn` 은 stateless 라 호출 시점엔 "같은 프레임 미래 스폰의 중심"을 알 수 없다. → **버퍼링 계약을 명시**한다:
  - `Spawn` 은 이번 프레임 스폰 위치를 스포너 내부 리스트에 **누적**만 한다(스파크 관점).
  - **`LateUpdate`** 에서 누적 리스트를 근접 클러스터로 묶고 클러스터당 대표 위치(중심)에서 스파크 1회 재생 후 리스트 clear. (드레인이 LateUpdate 전에 큐를 비우므로 프레임 내 전체 스폰이 모여 있음 — 순서 보장.)
  - 대안(단순): 첫 스폰 즉시 재생 + 시간·거리 창 내 후속 스폰 억제(중심 계산 없이). 둘 중 하나를 구현에서 확정.

### 파티클

- 파티클은 **풀링/원샷**(기존 VFX 스폰 패턴 준수), additive, 짧은 수명(직렬화 필드, placeholder ~0.3s). 재생 후 자동 반납.
- 재생 여부·클러스터 시간창·거리 임계·스케일은 직렬화 필드. 대형 히트에서만 재생하는 임계값도 옵션.
- ECS 경계 불변 — 순수 프레젠테이션. 새 이벤트 채널 만들지 않는다(스포너 내부 배칭).

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play: 다수 데미지가 동시에 뜰 때 타격 지점에 스파크가 클러스터당 1회 튄다 — 숫자 수만큼 파티클이 폭증하지 않는다(스크린샷/프로파일 확인).
- 실기기 스모크: 대규모 AoE 에서 파티클로 인한 프레임 급락 없음.
- 스파크 미도착/비활성 시에도 0·1·2 룩이 온전히 동작(이 unit 은 순수 가산 레이어).

---

- **검증 2026-07-07**: 파티클 `DamageNumberSpark_SKELETON.prefab`(Shuriken, MaxParticles 24, burst 16, loop=false, playOnAwake=false, Billboard, URP/Particles/Unlit additive `DamageNumberSpark.mat`, sortOrder 32000) — VFX 저작 스킬 규약 준수(_SKELETON 접미사 유지, 사용자 폴리시 전까지). 오프스크린 Simulate 렌더로 방사형 스파크 버스트 확인(16 파티클, additive 정상). 스포너 배칭: `Spawn` 이 프레임 위치 누적 → `LateUpdate` 가 `sparkClusterRadius` 로 그리디 클러스터 → 클러스터당 1회 재생 + 풀 재활용(`IsAlive` 회수). ECS 무접근·새 채널 0. 빈 슬롯 경고(OnValidate + 1회) 구현. sparkPrefab 을 BattleScene 스포너에 배선·저장(clean diff). compile 0 err. **combat 중 실제 발화·클러스터 육안은 Play 최종 확인 대기.**
