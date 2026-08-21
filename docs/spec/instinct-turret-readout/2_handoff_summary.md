# 2 — 인계 요약 (instinct-turret-readout)

## Commit

- `92333fbd` feat(instinct-turret-readout): units 0~1 — 아군 본능은 노랗고 포신은 겨눈 쪽으로 돈다

## Implemented

- **아군/적 본능이 색으로 갈린다** — 아군 노랑 / 적 빨강. 라이브 Duel 좌 2기 · 우 2기로 확인.
- 프로젝트 소유 프랍 변형 2벌(`Instinct_Ally` / `Instinct_Enemy`)을 만들고 본능 SO 3종이 그것을
  가리킨다. **벤더(KayKit) 프리팹은 편집하지 않았다.**
- **포신이 겨눈 쪽으로 돈다** — 받침·터렛은 고정, `cannon_barrel_*` 만 월드 Y yaw.
- 조준 신호는 기존 `UnitAttackVisualEvent` 하나. **신규 큐 0 · 신규 컴포넌트 0 · sim 변경 0.**
- 브리지가 뷰를 **셀로** 등록한다(`_structureTurretsByCell`) — 뷰는 맵 수명, 엔티티는 판 수명이라
  엔티티 참조로 묶으면 매 판 재배선이 필요하다.

## Key Files

- `Assets/_Project/Scripts/Presentation/StructureTurretView.cs` — 포신 프리젠터(전부)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - `_structureTurretsByCell` 선언 · `SpawnStructureViews` 등록 · `ClearStructureViews` 해제
  - `DrainUnitAttackVisualEvents` 의 본능 분기(`defData == null → continue` **앞**)
- `Assets/_Project/Prefabs/Structures/Instinct_{Ally,Enemy}.prefab`
- `Assets/_Project/Data/Structures/Structure_{Guard,Watch,Test}Instinct.asset`

## Verified

- EditMode 2 lane: 2,518개 / 실패 1 — `UnitKitCatalogTests.CatalogDescriptions_UseThreeFixedSections`
  (malphite 문안 30자 > 28자). **사전 실패**이며 이 spec 과 무관하다: 해당 SO·요약 코드가 워킹트리
  clean 이고 원인 커밋은 `4bfba2c2`(말파이트 배치 스킬에 광역 피해 40 추가 → 문장이 길어졌다).
  `docs/reference/test-procedure.md` 의 «EditMode 는 기지 실패 없음(2026-08-16)» 은 이제 낡았다.
- 라이브 Play(Duel): 본능 4기 등록 · 아군 본능 포신 추적(yaw 0 → 90.8/80.5 → 52.3/43.3) ·
  적 본능 yaw 0 유지(방어유닛 미배치 = 겨눌 대상 없음) · 콘솔 신규 에러 0.
- 워킹트리 오염 없음 — Play 진입으로 유닛 SO 가 딸려 더러워지지 않았음을 `git status` 로 확인.

## Notes (되돌리지 말 것)

- **포신 참조는 SerializeField 다.** 이름 문자열로 찾지 않는다 — 변형마다 노드 이름이 색을 달고
  다닌다(`cannon_barrel_yellow` / `cannon_barrel_red`).
- **드레인의 본능 분기는 `defData == null → continue` 앞**이어야 한다. 그 아래는 전부 방어유닛
  전용이라 거점은 거기까지 못 간다(회오리 VFX 분기가 같은 이유로 위에 있다).
- **방향은 뷰 공간**(`BoardSpace.ToView`)에서 뺀다. sim 벡터를 그대로 쓰면 누운 보드에서 엉뚱한
  축으로 돈다.
- **회전은 월드 회전으로 쓴다.** 프랍은 브리지가 world rotation = identity 로 세운다.
- **v1 은 「쏠 때 돈다」다.** 쿨 중 지속 추적이 아니다 — 본능에는 지속 조준 상태가 없고, 만들면
  sim 이 바뀐다(README §결정).

## Follow-up

- 사용자 Play 체감: 회전 속도 540°/s 가 「돌고 나서 쏜다」로 읽히는가.
- README §후속 후보: 총구에서 탄 발사 · 쿨 중 지속 조준 · 마음 프랍 진영색 · 파괴된 본능 프랍.
- 별건: `UnitKitCatalogTests` 사전 실패(malphite 문안)는 이 spec 밖에서 처리한다.
