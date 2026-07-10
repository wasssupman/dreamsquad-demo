# 2 — MovementSystem 추격 분기 확장

## 목적

`MovementSystem` 의 `Chasing` 분기를 확장해, aggro 가 없으면 `HuntTarget` 을 anchor 로 self-walk 한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

## 구현

기존 chasing 분기(`MovementSystem.cs:68`):
```csharp
if (ai == AiState.Chasing && aggroLookup.HasComponent(entity)) {
    // guardian anchor 로 self-walk ... continue;
}
```
확장:
```csharp
if (ai == AiState.Chasing) {
    float3 anchor; bool hasAnchor = false;
    if (aggroLookup.HasComponent(entity) && guardianPos.TryGetValue(guardian, out var gpos)) {
        anchor = gpos; hasAnchor = true;                    // 기존 aggro
    } else if (huntTargetLookup.HasComponent(entity)) {     // 신규 헌터
        var t = huntTargetLookup[entity].value;
        if (t != Entity.Null && transformLookup.HasComponent(t)) { anchor = pos(t); hasAnchor = true; }
    }
    if (hasAnchor) { /* step toward anchor + MovementCellTrim.Apply (기존 로직 공유) */ }
    continue; // anchor 없으면 정지(guardian/target 소멸)
}
```

- **anchor step 로직은 기존 aggro 코드 재사용** — 두 소스가 같은 step/cell-trim 통과. 코드 중복 최소(anchor 만 분기).
- `huntTargetLookup` = `GetComponentLookup<HuntTarget>(true)`. 타겟 위치는 **`defenderPos` 스냅샷**(별도 RO 쿼리)로 조회 — 이동 루프의 `RefRW<LocalTransform>` 와 aliasing 회피(하단 소결정). 방어유닛은 타일 고정이라 프레임 내 스냅샷 = 라이브 동등. (초안의 "transformLookup 라이브 조회" 는 aliasing 위반이라 폐기됨.)
- **무회귀 (정정, critic)**: aggro 있는 기존 Chasing 은 **직선이 안 막히는 common case 에서만** 첫 분기로 동일 동작이다. 단 aggro 가디언도 방어유닛(=`Place` 셀, non-walkable)이라 **off-lane 가디언의 blocked-diagonal 케이스는 aggro chase 도 원래 wall-stick 을 가졌고**, wall-slide/softlock 가드가 공유 anchor 브랜치에 들어가 그 케이스 동작을 **바꾼다(고착→슬라이드/마칭, 개선이지만 변경)**. EditMode 는 맵 없이 이 경로를 못 태우므로 "미검증 개선". 즉 "무회귀"는 정확히는 **"common case 무회귀 + blocked case 개선(미검증)"**. 헌터 Chasing 자체는 nightmare-catcher 이전엔 존재 불가라 순수 신규.

## 완료 기준

- [x] 보스가 HuntTarget 방어유닛으로 self-walk(cell-trim 통과, walk 타일 유지) — 코드. Play 확인은 unit 3.
- [x] 사거리 도달 → FSM 이 Engaging 전환 → chasing 분기 이탈(anchor 무관, ai!=Chasing).
- [x] 기존 aggro Chasing 무변경 — anchor 분기 첫 가지가 guardian(기존)과 동일, EditMode 649/651 무회귀.
- [ ] (렌즈 B) huntTargetLookup RO·맥락 경계 — units 1·2 묶음 실행 중.

## 구현 소결정 (aliasing 회피)

- **위치 조회는 `defenderPos` 스냅샷**(별도 RO 쿼리)로 확정 — spec 초안의 "transformLookup 라이브 조회"는 이동 루프의 `RefRW<LocalTransform>` 와 **aliasing 위반**(ComponentLookup<LocalTransform> RO + query RW 동시 = 금지)이라 불가. guardianPos 선례와 동일 패턴(별도 쿼리는 안전). 방어유닛은 타일 고정이라 프레임 내 스냅샷 = 라이브와 동등.

확인 2026-07-11 — 컴파일 클린 + EditMode 649/651 그린 + 커밋 `43e23954`.

## rev — 축 분리 wall-slide (spawn-freeze 버그, 실플레이 진단 2026-07-11)

**증상**: 헌터 보스가 스폰 자리에서 안 움직이고 갇힘(Chasing 상태인데 9.6초간 이동 0). 방어유닛에 접근·교전 실패.

**근본 원인(런타임 증거)**: 이 맵은 **적 경로가 단일 walk 레인**(y=1)이고 **방어유닛은 경로 밖 벽 셀**(y=2/y=7)에 배치된다. 직선 추격(대각선)의 첫 스텝이 곧장 벽 셀로 넘어가 `MovementCellTrim`이 매 프레임 제자리로 clamp → 고착. aggro 추격은 가디언이 보통 경로 위라 안 겪던 케이스(직선 추격의 가정 = 타겟이 walkable 직선상).

**수정**: 직선 스텝이 clamp되면(벽) **축 분리 슬라이드**(x만 → 실패 시 z만)로 walkable 축을 타고 접근. 보스는 레인을 따라 x=9까지 미끄러져 방어유닛 사거리(2타일)에 진입 → FSM 이 Engaging 으로 전환 → 정지·공격. 로직은 순수함수 `MovementChase.SlideStep` 로 추출(EditMode `MovementChaseTests` 6종 — 대각/x슬라이드/z슬라이드/fully-boxed/결정론).

**⚠ softlock 가드 (critic 지적, rev 2)**: Chasing 분기는 `continue` 로 flow/goal 을 스킵한다 → Chasing 보스는 **goal 로 전진하지 않는다**. 여기에 fully-boxed(양축 벽/concave)가 겹치면 보스가 **영구 freeze → 교전도 leak 도 못 함 → wave-stall(softlock)** 가능. 이는 cosmetic 한계가 아닌 잠재 정지 결함. **가드**: `SlideStep` 이 진행 0(current 반환)이면 `continue` 하지 않고 **flow-march 로 폴백** — 얼어붙는 대신 goal 로 전진(leak 이 freeze 보다 낫다). 이로써 fully-boxed 보스는 마칭으로 degrade, softlock 불가.

**"근본 해법(flow-field 재사용)" 이 왜 비싼가 (구조적 이유)**: 방어유닛은 `MapTileType.Place` 셀에만 배치되고, flow walkMask 는 `Walk` 셀만 walkable(= Place 셀 = walkMask 0 = 벽). `FlowFieldBuilder.Build` 는 goal 이 non-walkable 이면 **early-return**(방어유닛 셀 목표 flow 를 아예 못 만듦). 따라서 "기존 goal-BFS 불변식 재사용" 은 불가하고, 근본 해법은 **방어유닛의 최근접 walkable 이웃 multi-source BFS + per-boss 필드 재빌드**라는 신규 기계다(보스 추격 feature 로는 과대 스코프 → README 후속). 헌터는 방어유닛 셀에 **도달할 필요 없이 사거리 진입만** 하면 되므로 wall-slide 는 표준 slide-along-wall collision-response 로서 정당. 두 리뷰(ecs-reviewer PASS · 적대 critic) 모두 "땜빵 아닌 정당한 실용 근사"로 판정.

**한계(후속)**: greedy 근사라 concave 지형에서 특정 방어유닛엔 완전 미도달 가능(그땐 softlock 가드로 마칭 폴백 → leak). 타겟 지향 pathfinding(위 신규 기계)은 두 번째 보스 맵 전에 검토.
