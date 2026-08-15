# 2 — 관통 페이로드 재타격 쿨타임 (레인 A · 엔진)

## 목적

`PathHit` 은 지금 **피해자당 영구 1회**다. 도는 화염구는 한 바퀴마다 같은 적을 다시 지나가므로
그대로면 첫 바퀴에만 아프고 나머지 N초는 장식이 된다. 피해자별 **재타격 간격**을 축으로 연다.
기존 방향탄(샷건너·다연발)은 값 0 으로 **동작이 한 글자도 바뀌지 않아야 한다.**

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/PathHitRecord.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` (PathHit arm)
- `Assets/_Project/Tests/EditMode/` — 재타격 판정 순수 케이스

> `ProjectileData.rehitCooldownSec`(탄 SO) · `ProjectileState.rehitCooldownSec` **선언** ·
> 드레인의 SO→상태 복사는 **unit 0 이 이미 놓았다.** 이 unit 은 브리지도 SO 도 만지지 않는다
> (README 계약 P2). 여기서 하는 일은 **판정과 기록**뿐이다.

## 구현

### 1) 기록에 시간 축 추가

`PathHitRecord` 에 `public float nextHitAt;` 추가. 의미 = "이 피해자를 다시 때릴 수 있는
**투사체 자기 시계**(`state.elapsed`) 값".

⚠ **시계는 `state.elapsed` 를 쓴다.** `SystemAPI.Time.ElapsedTime` 이 아니다 — 궤도는 이미
`elapsed` 를 누적하고, Battle 도메인 시계(슬로우모/일시정지)를 그대로 따라가며, 리플레이
결정론이 투사체 안에서 닫힌다.

`Contains` 는 **남긴다**(쿨타임 0 경로가 그대로 쓴다). 재타격 판정은 별도 순수 헬퍼로:
```
public static bool CanHit(in DynamicBuffer<PathHitRecord> records, Entity victim,
                         float now, float cooldown, out int index)
```
- `cooldown <= 0` → 기록에 있으면 false (기존 동작)
- `cooldown > 0` → 기록에 없으면 true(index = -1), 있으면 `now >= nextHitAt`

### 2) 상태 필드

`ProjectileState.rehitCooldownSec` 는 unit 0 이 선언해 뒀다. 값은 탄 SO 에서 드레인이 채운다.
0 = 기존 1회 동작.

### 3) ⚠ ECB 로는 원소를 갱신할 수 없다 — 접근 방식부터 바꾼다

**이것이 이 unit 의 유일한 진짜 난관이다**(ECS 리뷰 H1). 지금 `pathHitRecordLookup` 은
`isReadOnly: true` 이고(`ProjectileHitSystem.cs:49`) 기록 추가는 `ecb.AppendToBuffer` 로 간다
(`:432`). **ECB 에는 "버퍼의 N번째 원소를 수정" 오퍼레이션이 없다** — `AppendToBuffer`(추가)와
`SetBuffer`(전체 교체)뿐이다. 그래서 "갱신"을 ECB 로 쓰려다 벽에 부딪히면 안 된다.

**선례가 바로 위에 있다**: bounce 가 outputs 버퍼를 in-place 로 감쇠시키려고
`outputLookup` 을 `isReadOnly: false` 로 잡는다(`:42`, 주석에 그 이유가 적혀 있다).
같은 형태로 간다:

1. `pathHitRecordLookup` 을 `isReadOnly: false` 로 변경 (주석의 "read-only: appends go
   through the ECB" 설명도 함께 갱신 — 그 문장이 더 이상 사실이 아니게 된다)
2. 기존 `ecb.AppendToBuffer(entity, new PathHitRecord{...})` → 직접 `.Add(...)`
3. 갱신은 `var recs = pathHitRecordLookup[entity]; recs[idx] = ...;`

`ProjectileHitSystem` 은 `IJobEntity` 가 아니라 **메인 스레드 `ISystem.OnUpdate`** 이고 기록
버퍼는 투사체별(순회 중인 엔티티 자신)이라 이중 접근이 아니다.

⚠ **무회귀 주의**: ECB append 는 플레이백까지 지연되므로, 지금은 **같은 프레임 안에서 방금 추가한
기록이 `Contains` 에 안 보인다.** 직접 쓰기로 바꾸면 즉시 보인다. 현재 방향탄 루프는 한 프레임에
피해자당 1회만 처리하므로 결과가 같아야 하지만, **그 동등성을 테스트로 고정**하고 넘어간다
(한 프레임에 여러 victim 을 스치는 관통탄이 회귀 표면이다).

### 4) Hit arm 분기 (`ProjectileHitSystem` PathHit)

- 판정을 `PathHitRecord.CanHit` 으로 교체.
- 때린 뒤: 기록이 없으면 추가(`nextHitAt = elapsed + cooldown`), 있으면 **그 원소를 갱신**한다.
  (버퍼가 무한히 자라면 안 된다 — 갱신이지 추가가 아니다.)
- **계약 3 — `rehitCooldownSec > 0` 이면 `pierceRemaining` 을 소모하지 않는다.**
  이 투사체의 유일한 종료 조건은 수명(`impactReached`)이다. 소모하면 화염구가 몇 명 스치고
  N초를 못 채운 채 사라진다.

### 4) 스폰 시 버퍼

PathHit 투사체에 `PathHitRecord` 버퍼가 붙는 것은 기존 브리지 드레인이 이미 한다 — 변경 없음.

## 완료 기준

- EditMode 신규:
  ① `cooldown = 0` 이면 두 번째 시도가 항상 거절(기존 동작 고정)
  ② `cooldown > 0` 이고 `now < nextHitAt` → 거절
  ③ `now >= nextHitAt` → 허용, 갱신 후 다음 창까지 다시 거절
  ④ 미기록 피해자는 쿨타임 유무와 무관하게 최초 1회 허용
- **무회귀 어서션**: 방향탄(`DirectionalLinear` × `PathHit`) 경로의 기존 EditMode/PlayMode green.
  샷건너·다연발의 관통 예산 소모가 그대로인 것을 테스트가 말해야 한다.
  **ECB→직접 쓰기 전환(§3)의 동등성**을 명시적으로 덮는 케이스 1건 — 한 프레임에 여러 victim 을
  스치는 관통탄이 전과 같은 수의 피해자를 때리고 같은 순서(front-most 우선)로 예산을 쓴다.
- 컴파일 확인까지. 커밋하지 않는다(계약 P3).

## 파일 소유 주의

`ProjectileHitSystem.cs` 의 **PathHit arm 과 그 lookup 선언 줄**만 만진다. 같은 파일의
SingleSplash/TileAoe/bounce arm 은 건드리지 않는다 — 회귀 표면이 그쪽으로 번지면 무회귀 증명이
이 unit 의 범위를 넘어간다.
