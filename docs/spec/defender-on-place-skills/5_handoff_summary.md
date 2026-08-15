# 5. Handoff — 전방 관통 일격 명중 수정 (unit 4)

## Commit

- `e3c231bf` fix — 전방 관통 일격이 적을 향해 나간다 (방향 규칙 교체 + 통로 폭)
- `d0b575ec` docs — 커밋 해시 기록
- `440a4eeb` fix — 리뷰 반영: 후보 집합 계약 + 조준 판별 테스트

## Implemented

- **총구 방향 규칙 교체**. 옛 규칙 `FindNearestPathDirection`("가장 가까운 길 칸 쪽")은 삭제.
  그 탐색은 맵을 y·x 오름차순으로 훑고 동점에서 먼저 찾은 칸을 지켜서, 배치 셀 이웃이 전부
  `Walk` 이면 **항상 남쪽**이 이겼다(실측 252칸 중 173칸이 (0,-1)).
- 새 규칙: 조준(`DeployedFacing`) 우선 → 없으면 사거리 안 최근접 적 → 후보가 없으면 미발사.
- 통로 반폭 `0.45` → `0.6` 타일. 레인 오프셋 ±0.5 로 옆에 선 적이 탈락하던 것을 덮는다.
- **후보 집합 = 「이번 프레임 합법 후보」**(`AttackSystem.targetCandidatesQuery` 와 동일).
  `IsLegalOnPlaceTarget` 이 `DeadTag`·`UltimateLeapState` 를 뺀다.
- 후보 수집이 배치당 1회. 방향 결정과 명중 판정이 같은 리스트를 본다.
- 신규 PlayMode 테스트 5케이스.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ApplyForwardOnPlaceProjectile`,
  `ResolveForwardBurstDirection`, `IsLegalOnPlaceTarget`, `ForwardBurstHalfWidthTiles`
- `Assets/_Project/Scripts/Battle/Combat/UltimateLeapState.cs` — 소비처 목록 6번
- `Assets/_Project/Tests/PlayMode/OnPlaceForwardProjectileTest.cs`
- `docs/spec/defender-on-place-skills/4_forward_burst_direction.md` — 계약 정본

## Verified

- PlayMode 20/20 — 신규 5 + Beam·OnPlaceDot·OnPlaceStack·Relocation·PlacementAura·BoardLimit
- EditMode 2416 중 실패 5건은 **기존 실패**(맵 문서 폭1 협곡 4건 = 은퇴한 규칙, Whirlpot 스켈레톤 1건)
- 두 결함 모두 **먼저 빨간 것을 확인한 뒤** 고쳤다(방향 고정 3케이스, 후보 필터 1케이스)
- 사용자 육안 Play 확인은 **미완**. 전투 중 적 근처에 마크스맨 배치 → 콘솔
  `On-place ...: ForwardProjectile affected=N` 의 N > 0

## Notes (되돌리지 말 것)

- **후보 필터를 빼지 말 것.** 판 밖(궁극기 도약) 적은 피해가 버려지는데 최근접이면 총구를
  가져간다. `affected` 는 버퍼에 넣기만 하면 올라가므로 로그까지 거짓 양성이 된다.
- **후보 없음 가드는 방향 퇴화 방지를 겸한다.** 없으면 `forward=(0,0)` → `along=0`,
  `lateral=|to|` 라 방향과 무관하게 반경 0.6칸을 때린다.
- **"발사하지 않는다" ≠ "스킬이 보존된다".** `_onPlaceTriggeredEntities.Add` 는 무조건 실행되어
  시도가 소진된다.
- 조준 테스트는 **직교 대조군**이 있어야 판별력이 있다. 조준 광선 위에만 적을 두면 "최근접만
  쫓는" 구현도 통과한다. 4방향을 각각 다른 칸에서 검사하는 것도 같은 이유(한 방향만 보면
  고장난 코드의 고정 방향과 우연히 일치해 새는 통과가 난다 — 실제로 났다).

## 시도했다가 되돌린 것

**전방 관통 연출(빔)** — 명중을 고쳐도 체감이 없어 `BusterBeam` 을 재사용한 0.25초 관통선을
붙였다가 **전부 되돌렸다**(사용자 결정). 이유 둘: 총 쏘는 유닛에 버스터즈 빔은 맞지 않는 그림이고,
근본 원인이 연출 부재가 아니었다 — 이 4종의 배치 스킬은 각자의 평소 공격과 **같은 방향·같은
직선**이고 크기만 1.3~2.8배다(머신거너 평소 5딜×10발=50/1.9초 vs 배치 70 1회). 연출은 화장이었다.
같은 시도를 반복하지 말 것. 작업 중 발견한 `BeamPresenter` 결함 2건은 README 후속 후보에 있다.

## Follow-up

README 「후속 후보」 참조. 우선순위 순:

1. **전방 관통 4종 배치 스킬 재설계** — 평소 공격과 구분되는 사건으로. 이번 수정이 닫은 것은
   "0마리를 맞힌다"뿐이고 사용자의 원래 보고("배치 스킬이 있는지 모르겠다")는 여기서 닫힌다.
2. 배치 페이즈 발동 정책(전투 시작 전 배치는 스킬을 통째로 낭비)
3. 배치 스킬을 브리지 밖으로(`ApplyOnPlaceEffect` 가 ECS 시스템이 아니다)
4. 재배치가 조준을 갱신하지 않음 · 테스트 더미에 통행 층 없음 · 짧은 빔 소실
