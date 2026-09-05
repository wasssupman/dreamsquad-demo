# 2 — 감지 판정 시스템

## 목적

매 프레임 「이 적이 지금 방어유닛을 발견했나」를 정하고, 그 답과 **발견한 대상**을 기록한다.
이동은 unit 3 이 읽고, 연출은 unit 5 가 읽는다. 이 unit 은 판정만 소유한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DetectedTarget.cs` (신규)
- `Assets/_Project/Scripts/Battle/Combat/DetectionSystem.cs` (신규)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DetectedTarget` 스폰 시 부착
- `Assets/_Project/Tests/EditMode/…/DetectionSystemTests.cs` (신규)

## 구현

**컴포넌트** — Combat 소유, `DetectionSystem` 이 유일한 writer:

```csharp
public struct DetectedTarget : IComponentData
{
    public Entity target;   // 발견한 방어유닛. 로그·트레이스 전용(계약 6) — 이동은 읽지 않는다
    public byte hunting;    // 1 = 이번 프레임 감지 성립. Movement 가 읽는 유일한 값
}
```

**스폰 시 무조건 부착**한다(`DetectionRange` 를 가진 적에 한해). 값만 매 프레임 write —
핫패스에서 구조 변경을 하지 않는다(`enemy-hunter-targeting` 계약 6 의 교훈).

**시스템** — `[UpdateInGroup(BattleSimGroup)] [UpdateBefore(typeof(MovementSystem))]`.
`DefenderFieldSystem` 과 순서 관계는 없다(그쪽은 필드를 굽고 이쪽은 후보를 스캔한다).

프레임 절차:

0. **`AttackState` 가 없는 적은 `hunting = 0`**(fail-closed). `EnemyAiState` 는 무조건 구워지지만
   `AttackState` 는 `if (wantsAttack)` 안이고, `attackMethod` 를 켜고 `outputs` 를 비우면
   **경고 한 줄만 찍고 walk-only 로 구워진다**(`BattleBridge.cs:10577~10580`). 그 적에게
   `detectionRange` 가 저작돼 있으면 「때릴 수 없는데 사냥판을 따라가 방어유닛 앞에서 얼어붙는」
   상태가 된다. `MovementSystem.cs:500~506` 이 같은 함정을 이미 길게 경고하고 있다.
   이건 도달 불가한 방어가 아니라 **실제 저작 실수를 막는 가드**다 — 「어차피 항상 있으니」로
   지우지 말 것.
1. **어그로된 적은 건너뛴다** — `hunting = 0`, `target = Entity.Null`. 어그로 최우선(계약 2)의
   이행이며, `MovementSystem` 의 `Chasing` 분기가 사냥 분기보다 앞이라 이중 안전망이다.
2. 방어유닛 후보 스냅샷 — `Faction.DefenderUnit` + `Health`, `WithNone<PendingDeployment, DeadTag>`.
   **`DefenderFieldSystem` 과 같은 조건**이어야 한다. 다르면 「감지는 했는데 이동판에 소스가 없다」가
   생기고, 그게 `MovementSystem:77` 주석이 「네 번째 자」라고 부르는 부류다.
3. 적별로 후보를 훑어 **legal** 만 남긴다 — `targetMask` · `PlacementLayers.CanTarget` ·
   `EnemyTargetFilter.classMask`. `AttackSystem` 후보 루프와 같은 술어다(계약 4). 이 필터가
   거점타격형을 별도 게이트 없이 배제한다.
4. 반경 판정은 **`AttackReach.InReach(atkPos, tgtPos, detectionTiles, tileSize, selfR, tgtR)`**
   하나다(계약 3). `detectionTiles < 0` 이면 반경 판정을 건너뛴다(무제한).
5. 남은 후보 중 **최근접** 하나를 고른다. 랭킹은 `NearestTargeting.RanksBefore` 를 **재사용**한다
   (최근접 → 동거리는 낮은 `simId`). 새 랭커를 만들지 않는다.
6. 후보가 있으면 `hunting = 1`, `target = 그 엔티티`. 없으면 둘 다 비운다(관성은 unit 4).

**왜 순수 함수를 새로 만들지 않나.** 판정 세 조각이 전부 이미 있는 술어다 — 반경은 `AttackReach`,
랭킹은 `NearestTargeting`, legal 은 `PlacementLayers`. 남는 것은 「음수면 무제한」 한 줄뿐이라
별도 static 으로 빼면 제약 10 이 경계하는 과잉 추상화다. 그래서 검증은 **합성 월드 EditMode**
(시스템 수준)로 한다 — 타게팅은 sim-critical 이므로 테스트 자체는 면제하지 않는다.

**비용.** 방어유닛은 판당 10~20기, 감지 적은 웨이브당 수기~수십기다. 적×방어유닛 직선 스캔은
프레임당 수백 회로, 매 프레임 그리드 BFS 를 도는 `DefenderFieldSystem` 보다 훨씬 싸다.
BFS 를 적별로 굽는 설계(B안)를 버린 이유 중 하나다.

## 완료 기준

- compile 통과 · EditMode 전체 초록(선행 실패 2건 제외).
- EditMode 신규 — 합성 월드:
  - 반경 3, 간격 2칸 방어유닛 → `hunting == 1`, `target` 이 그 엔티티.
  - 반경 3, 간격 4칸 → `hunting == 0`.
  - 반경 −1(무제한), 간격 20칸 → `hunting == 1`.
  - 반경 3 안에 **못 때리는** 방어유닛(마스크 밖)만 있으면 `hunting == 0` — 계약 4.
  - `Aggroed` 가 붙은 적은 반경 안에 대상이 있어도 `hunting == 0` — 계약 2.
  - **`AttackState` 없이 구워진 적**(`attackMethod` 켜고 `outputs` 빈 저작)은 `detectionRange` 가
    저작돼 있어도 `hunting == 0` — 절차 0, fail-closed.
  - 같은 거리 후보 2기면 `simId` 가 낮은 쪽이 뽑힌다(결정론).
- **거동 무변** — ⚠ 골든 `Verify` 는 이 판정에 못 쓴다. 코퍼스가 이 spec 이전부터 stale 이고
  (unit 1 완료 기준 참조) `configHash` 도 스키마 변경으로 이미 움직였다. 대신 **이 unit 의
  변경 한 줄만 임시로 끄고 verify 를 돌려 켠 실행과 이벤트/킬을 대조한다** — 같으면 무변이다.
  이 unit 은 아무도 `DetectedTarget` 을 읽지 않으므로 애초에 거동이 안 바뀐다.
