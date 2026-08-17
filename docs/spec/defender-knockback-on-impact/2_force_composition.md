# 2 — 넉백 소비를 한 곳으로 (변위 합성 통일)

## 목적

「멈춘 적은 안 밀린다」를 없앤다. unit 1 이 넉백을 **만드는** 쪽을 고쳤다면, 이 unit 은
**쓰는** 쪽을 고친다.

## 증상과 원인

사용자: *「넉백머신이 제자리 공격 중인 킨들러를 못 민다」*.

넉백 이벤트는 정상 생성되고 `CcEffect` 버퍼에도 정상 진입한다. 끊기는 곳은 **소비**다 —
impulse 를 실제로 쓰는 코드가 `MovementSystem` 에 한 줄뿐인데(`CcKind.Impulse` 전수 조사:
생산자 4 · 소비자 1), 자기주도 이동을 하지 않는 상태들이 **그 줄에 닿기 전에 `continue`** 한다.
못 쓴 impulse 는 `CcDecaySystem`(`[UpdateAfter(MovementSystem)]`)이 소비 여부와 무관하게
만료시켜 조용히 증발한다.

근본은 **복붙**이다. 「변위 적용」(clamp → 충돌 해소 → Position 쓰기)이 7곳에 복사돼 있었고
**각 복사본이 서로 다른 힘 부분집합만** 알았다. 넉백이 나중에 추가되며 메인 한 곳만 갱신됐다.

| 사이트 | 상황 | 수정 전 self / pull / impulse |
|---|---|---|
| Standoff | 도발 대치 | ✗ / ✗ / ✗ |
| Chasing + locked | 추격 중 기절 | ✗ / ✗ / ✗ |
| Chasing | 가디언 추격 | ✓ / ✗ / ✗ |
| Engaging + Halt | **제자리 공격 — 적 18종의 기본값** | ✗ / ✓ / ✗ |
| Patrol dir==0 | 순찰 아군 대기 | ✗ / ✓ / ✗ |
| 고립 셀 | 갈 길 없음 | ✗ / ✓ / ✗ |
| Main | 마칭 | ✓ / ✓ / ✓ |

생산자와 무관한 결함이라 **Impulse 를 쓰는 전부**가 같이 죽어 있었다 — 근접·발사시점 넉백,
훑는 탄 넉백, 드림캐쳐 「밀치기」 카드.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`
- `Assets/_Project/Tests/EditMode/MovementImpulseAcrossStatesTests.cs` (신규)

## 구현

**힘은 두 종류다.** `self`(자기주도 이동 — flow / chase / patrol)와 `external`(외력 — 넉백 + 당김).
**「멈춤」은 `self = 0` 이지 「변위 계산을 건너뜀」이 아니다.**

1. `ComposeMove(current, self, external, tileSize, radius, in nav)` — 순수 static. 7곳이 공유.
   제약 10 판정: 호출처 7 + sim-critical 이동 → 추출 대상.
2. 넉백 합성을 **분기 앞으로 hoist**. `CcEffect` 버퍼만 읽어 의존성이 없다.
3. 7개 사이트가 각자 자기 항만 넣어 `ComposeMove` 를 호출.

### 하지 않은 것과 그 이유

⚠ **당김(pull)은 hoist 하지 않는다.** 당김은 `cell` 에 의존하고 그 값은 **포탈 텔레포트 이후**여야
정확한데, Standoff/Chasing 은 포탈 **전에** 이탈한다. 올리면 텔레포트된 적의 당김 방향이 틀어진다.
→ 「외력을 분기 앞에서 한 번 계산」은 넉백에만 성립한다.

⚠ **`speedMul` 도 같이 올리지 않는다** — flowStep 전용이라 외력과 무관하다.

⚠ **추격에 당김 추가는 범위 밖**(후속 후보). 추격은 포탈을 건너뛰는 의미를 갖고 있어
재배치하면 의미가 바뀐다.

⚠ **`AgentSeparationSystem` 은 안 건드린다.** 별도 시스템으로 같은 프레임 뒤에 위치를 또 쓴다.

## 알려진 상호작용 (수정 안 함)

- 새로 밀리는 사이트의 유닛은 `holdingGround = 1` 을 유지한다(외력은 이 값을 안 내림 — 의도).
  `AgentSeparationSystem` 이 그 유닛의 전진 성분 분리를 거부해 일시적 뭉침이 가능하다.
  기존 당김에도 있던 상호작용이다.
- 보스는 Impulse 면역(`CcActionLock.IsBossImmune`) → 보스전 무영향.
- 순찰 아군에 넉백은 실전 no-op — 오늘 Impulse 생산자 4곳이 전부 적을 겨눈다.

## 밸런스 결과 (사용자 고지됨)

- **넉백머신이 적을 전진시킬 수 있다.** 제자리 공격 중이던 적이 사거리 밖으로 밀리면
  Engaging→Marching 전이로 골을 향해 다시 걷는다.
- **도발된 적이 진동할 수 있다** — 밀림 → 이탈 → 복귀 → 밀림.
- 밀치기 카드가 교전 중인 적에게 갑자기 먹기 시작한다(기존 콘텐츠 버프).

## 완료 기준

- [x] **재현 먼저** — 6개 상태에서 「Impulse 가 버퍼에 있는데 위치가 안 변한다」가 빨간불
      (Standoff · Chasing · Chasing+Stun · Engaging+Halt ×2 · Patrol. Marching 대조군 2개는 초록)
- [x] 수정 후 8/8 초록
- [x] EditMode 코어 **2352 통과 / 0 실패** — 이동·분리·경로 회귀 0
- [x] PlayMode 이동 계열(`WaypointRoutingLiveTest`) **8/8 통과**
- [x] Play 육안 — 교전 중 적이 밀리는지 · 도발된 적 진동 · **코너 근처 톱니**(이 파일의 과거
      회귀 유형이라 정적 검증으로는 답이 안 난다)

확인: 2026-08-17 · 사용자 Play 확인 · 커밋 `3a9237fb`

## 사각지대 메모

`MovementCompositionTests` 픽스처는 `EnemyAiState` 가 없어 **항상 Marching** 이다. 그래서 7개
사이트 중 6개가 어느 테스트에도 안 걸려 있었고, 이 결함이 전 테스트 초록인 채로 살아 있었다.
`MovementImpulseAcrossStatesTests` 가 그 구멍을 메운다 — **상태별 소비 커버리지**가 존재 이유다.
