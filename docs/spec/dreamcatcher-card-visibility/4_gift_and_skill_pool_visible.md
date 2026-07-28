# 4. 선물 페이즈 풀 + 스킬 풀에 visible 반영

## 목적

`visible == 0` 인 카드가 선물 페이즈로 새어 들어오는 두 경로를 막는다:

1. **림의 선물** — 무의식 풀/fallback 풀이 `category` 만 보고 숨김 카드를 지급할 수 있다.
2. **루시드의 선물** — 스킬 롤(`SkillLoadoutController`)이 SkillData 만 알아서, 숨김 Active 카드를 래핑하는 스킬이 롤되면 그 카드가 그대로 지급된다.

루시드 쪽은 **스킬 풀 자체에서 제외**한다 (사용자 결정 2026-07-27) — `FindActiveCard` 에서 스킵하면 큐가 조용히 짧아지는 것보다, 롤 자체가 노출 가능한 스킬만 대상으로 도는 것이 맞다.

## 변경 대상

- `Assets/_Project/Scripts/Core/SkillLoadoutController.cs` — `cardCatalog` SerializeField + `FilterHiddenSkills` 순수 static + 풀 설정 지점(Awake/Configure×2) 필터
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — `ResolveRimGift` 한 줄 필터
- `Assets/_Project/Tests/EditMode/SkillLoadoutControllerTests.cs` — 필터 테스트
- 씬 — `SkillLoadoutController.cardCatalog` 에 `DreamcatcherCardCatalog` 에셋 할당

## 구현

**림 풀**: `ResolveRimGift` 의 카탈로그 순회 맨 앞에 unit 1 과 같은 관용구 한 줄. pool(무의식)과 fallback(non-Active) 둘 다 이 줄 뒤에 있으므로 동시에 걸러진다.

```csharp
if (c.visible == 0) continue; // 숨김 카드는 선물 풀에서도 제외 (unit 4)
```

**스킬 풀**: `FilterHiddenSkills(pool, catalog)` — plain 값 입력 → plain 값 출력 순수 static (제약 10, `DeckPrune` 과 같은 shape). 규칙:

- 스킬을 **제외**하는 조건 = 카탈로그에 그 스킬을 래핑하는 `type == Active` 카드가 존재하고, 그 래핑 카드가 **전부** `visible == 0`.
- 래핑 카드가 아예 없는 스킬은 **보존** — 기존 "No Active card wraps skill" 경고 경로(gift 시점) 그대로.
- `catalog == null` 이면 무필터 통과(배선 없는 에디터 씬/테스트 하위호환).

적용 지점은 **풀이 설정되는 곳** (Awake 의 defaultPool 복사 + Configure 두 오버로드). Roll 시점이 아니라 풀 자체를 거르므로 `Pool` 프로퍼티(BattleLogger 의 pool+seed 리플레이 기록)와 실제 롤 대상이 항상 일치한다.

## 완료 기준

- [x] 컴파일 통과 (2026-07-27)
- [x] EditMode: 숨김 Active 카드를 래핑하는 스킬이 풀에서 제외된다 / 래핑 카드 없는 스킬은 보존된다 / catalog null 이면 무필터 + 림의 선물 pool/fallback 양쪽에서 숨김 카드 제외 (`SkillLoadoutControllerTests` 8건 추가, 총 15/15 green 2026-07-27)
- [x] 씬 YAML: `cardCatalog` fileID 비-0 (BattleScene.unity, guid 7e41a0cd…) + `SceneTransitionSmokeTest` 로 BattleScene 로드/부트스트랩 통과 (2026-07-27)
- [ ] Play: 무의식 카드 하나를 `visible = 0` 으로 두고 림의 선물에서 등장하지 않는 것 확인 (에디터 육안)
