# continuous-agent-movement — handoff

## Commit

| 커밋 | 범위 |
|---|---|
| `942ca7f5` | unit 0 — `SimFieldInstaller` 추출 (BattleBridge −155줄) |
| `b1457a5a` | unit 1 — `NavGrid` 도입 + walk 마스크 단일 소유 |
| (이 커밋) | unit 2~8 — 술어 교체 · 원형 충돌 · 8-이웃 가중 필드 · 재빌드 · 평활화 · 분리 |

## Implemented

- **벽 질의 단일 진입점** — `NavGrid`(프레임 뷰). 정적 마스크 + 동적 장애물을 **호출자가 프레임마다 조립**하고, 순수 함수는 조립된 것 하나만 받는다. 이 분리가 아키텍처 호환의 실체다.
- **벽 술어를 지형 기반으로** — `flow == 0` → `walkMask == 0`. 유일한 의미 변화는 "골에서 도달 불가한 Walk 셀이 통행 가능"이며, 이것이 D1-b 의 전제다.
- **원형 에이전트 충돌 + 슬라이드** — 축 분리 해결. 전진 가장자리가 지나는 셀을 **스윕**해 중간 벽을 건너뛰지 않는다.
- **8-이웃 가중 다익스트라** — 직교 10 / 대각 14(×10 정수). 코너컷 방지가 BFS 확장과 flow 채우기 양쪽에 적용. L 자 소멸.
- **장애물 변경 시 필드 재빌드**(D1-b) — 순서 무관 XOR 시그니처로 dirty 판정, 기존 배열에 in-place. `Aggroed` + `AggroChaseCell` 동반 무효화.
- **LOS 평활화** — 필드 전방 K=8 셀 후보 + **반지름 포함** 가시선. 이것이 "한 줄기 직선"을 만든다.
- **겹침 해소** — 2단계(누적 후 일괄 적용)로 순서 무관.

## Key Files

| 경로 | 역할 |
|---|---|
| `Battle/Movement/NavGrid.cs` | 벽 질의 단일 진입점 (프레임 뷰) |
| `Battle/Movement/AgentCollision.cs` | 원형 충돌 + 슬라이드 (축 분리 스윕) |
| `Battle/Movement/PathSmoothing.cs` | string pulling + 반지름 가시선 |
| `Battle/Movement/Separation.cs` | 겹침 밀어냄 (순수) |
| `Battle/Movement/AgentSeparationSystem.cs` | 이웃 수집만 담당 |
| `Battle/Effects/FlowFieldBuilder.cs` | 8-이웃 가중 다익스트라 |
| `Battle/Effects/FlowFieldRebuildSystem.cs` | D1-b 재빌드 |
| `Battle/Effects/ObstacleSignature.cs` | 순서 무관 dirty 판정 |
| `Bridge/SimFieldInstaller.cs` | 판 단위 sim 필드 3종 할당/해제 |

## Verified

- **EditMode 1981 중 1979 통과 · 실패 0** (skip 2 = 기존 `[Ignore]`). 착수 전 1919 → +62 가 이 spec 의 신규 테스트.
- **`ecs-reviewer` 3회** — unit 1 / unit 2+3 / unit 2~8. 최종 CRITICAL·HIGH 0건. 지적 반영 완료:
  - HIGH: 순찰병 스폰 `radius` 누락 → 주입
  - M1: `AgentCollision` 중간 셀 건너뜀 → 스윕으로 교체 + 회귀 테스트
  - M2: `AgentSeparationSystem` 의 `LeapFlight` 미제외 → 쿼리 필터 추가
- **Play 육안**: unit 0·1 까지 확인 완료. **unit 2~8 은 미확인 — 아래 참조.**

## Notes (되돌리면 안 되는 의도)

- **`NavGrid` 는 저장 상태가 아니다.** 합성 마스크를 캐시하면 벽의 진실이 두 곳이 되어 단일 진입점 계약이 깨진다. unit 5 에서 그 캐시를 명시적으로 **기각**했다(180바이트 Temp 절약과 계약을 맞바꿀 수 없다).
- **`walkMask` 는 `FlowFieldSingleton` 단독 소유.** 사본을 만들면 double dispose 로 죽는다.
- **`dist` 는 ×10 스케일 정수다.** 절대값을 비교하는 새 소비자를 만들지 말 것 — 상대 비교와 `int.MaxValue` 센티넬만 쓴다.
- **평활화는 필드를 대체하지 않는다.** 후보를 필드가 만들기 때문에 오목 지형에서 갇히지 않는다. 순수 스티어링 회피로 바꾸면 그 성질을 잃는다.
- **`Separation` 은 반드시 누적 후 일괄 적용.** 순회 중 위치를 갱신하면 결정론이 깨진다.
- **완전 봉쇄는 허용된다.** 연결성 가드를 새로 만들지 말 것 — `destructible-blocking-hazards` 가 "적이 부순다"로 이미 답한다.

## Follow-up

**Play 검증이 남았다.** 아래 순서로 보면 각 unit 의 기여가 분리돼 보인다.

1. **코너 품질** (unit 3) — 벽에 비스듬히 부딪힌 적이 미끄러지는가, 1타일 복도를 통과하는가
2. **L 자 소멸** (unit 4) — 열린 구역(Serpent·Zig·Coil 폭 7~14)에서 대각으로 가는가
3. **직선 이동** (unit 7, **이 spec 의 검증 질문**) — 45°가 아닌 기울기에서 축 정렬 구간 없이 한 줄기 직선인가. 장애물을 놓으면 모서리를 스치는 두 직선이 되는가
4. **봉쇄** (unit 5) — 차단 해저드로 완전히 막으면 적이 벽면에 모여 때리고, 파괴 직후 이동이 재개되는가
5. **밀집** (unit 8) — 한 점에 뭉치지 않는가, 1타일 복도에서 정체하되 **교착하지 않는가**

⚠ **검증 맵을 45° 기울기로 잡지 말 것** — unit 4 만으로도 직선으로 보여 unit 7 의 효과를 확인할 수 없다.

### 남은 후보

- **맵 복도 폭 확장** — 이 spec 의 이득이 실제로 드러나는 지형. 현재 맵 6종은 가로 연속 Walk 폭이 1타일 위주라 코너 품질 이상은 보이지 않는다. 경로 단축에 따른 밸런스 재튜닝 동반.
- **스폰 예고 라인 재표현** — `BattleBridge:1807` 은 **필드 경로**를 그리므로 평활화 후 실제 이동선과 어긋난다.
- **`DefenderFieldSystem` 이 장애물을 무시한다** — 보스 사냥 경로가 해저드를 통과한다. 이 spec 이전부터 있던 동작이며 범위 밖(ecs-review 확인).
- **항법 격자 세분화(2x)** — 코너 표현이 실제로 부족할 때만.
