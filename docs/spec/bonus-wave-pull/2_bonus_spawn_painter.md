# 2 — 보너스 포탈 저작 도구 (에디터)

## 목적

`MapPainterWindow` 에서 보너스 포탈 칸을 찍을 수 있게 한다. 런타임 축(unit 1)과 커밋을 가르는
이유는 asmdef(Runtime ↔ Editor)와 테스트 lane(코어 ↔ Assets)이 갈리기 때문이다.

## 변경 대상

- `Assets/_Project/Editor/MapPainterWindow.cs`
- `Assets/_Project/Tests/EditModeAssets/` — 저작 검증 테스트

## 구현

1. `Tool` enum 에 `BonusSpawn` 추가. 브러시바 라벨은 「보너스」.

2. `Spawn`/`Goal` 과 같은 **click-only** 규율을 따른다 — 드래그 페인트 금지. 그 둘이
   click-only 인 이유(재토글 깜빡임)가 그대로 적용된다.

3. 5개 지점을 건드린다(선례 `Spawn`·`Structure`·`Waypoint` 와 동형):
   enum · 브러시바 · 페인트 핸들러 · Load/Save · 검증.

4. 다른 도구가 그 칸을 덮을 때의 처리: `Road`/`Deco` 가 `RemoveSpawn`/`RemoveGoal` 을
   부르는 것과 같이 `RemoveBonusSpawn` 도 부른다. 벽이 된 칸에 포탈이 남지 않게 한다.

5. 검증은 unit 1 의 `BonusSpawnAuthoringRules` 를 **그대로 호출**한다. 페인터가 자기
   규칙을 복제하면 「툴 통과 → 런타임 폴백」이 생긴다(`waypoint-routing` unit 5 선례).

## 완료 기준

- [x] 컴파일 에러 0
- [x] 페인터에서 보너스 칸 2개를 찍고 저장 → 문서에 반영, 다시 열면 그대로 표시
- [x] 벽 칸에 찍으면 검증 에러가 보인다
- [x] 보너스 칸 위에 Road/Deco 를 칠하면 보너스 칸이 제거된다
- [x] EditMode + **Assets lane** green

**확인 2026-08-24** — 컴파일 + Assets lane green. 레인 스폰과 상호 배타, 상한 도달 시 loud warn(리뷰 L2·L3).
