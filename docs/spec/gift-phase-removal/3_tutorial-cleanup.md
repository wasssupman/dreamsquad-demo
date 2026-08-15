# 3. 선물 튜토리얼 챕터 정리

## 목적

두 번째 판에 뜨던 선물 워크스루("10장 + 2장이 섞여 덱 순서가 배정됩니다")를 제거한다. 보여줄 연출이 사라졌으므로 문구만 남길 자리가 없다. 프로필 필드는 하위호환을 위해 남긴다(계약 7).

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.Gift.cs` — **파일 삭제** (`.meta` 동반)
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — L72 `SubscribeGift()` / L94 `UnsubscribeGift()` 호출 제거
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`

## 구현

### 컨트롤러

`FirstSessionTutorialController.Gift.cs` 를 통째로 삭제한다. 이 파일이 `giftView` SerializeField 와 홀드 핸들러 2개, `CompleteGiftProgress` 를 전부 갖고 있어 본체에서는 lifecycle 호출 2줄만 지우면 된다. 씬의 `FirstSessionTutorialController` 인스펙터에 남는 `giftView` 슬롯은 필드가 사라지면서 자동 소멸한다.

### TutorialProgress

제거: `GiftTutorialVersion` const · `ShouldRunGiftTutorial` · `IsGiftTutorialPending` · `CompleteGiftTutorial`.

**유지**: `ResetAll` 과 `ResetAllInJson` 의 `giftTutorialVersion` 처리 — `changed` 표현식 항과 0 대입 양쪽 모두. 기존 세이브에 남은 값을 계속 정리해야 하고, `ResetAllInJson` 의 `changed` 는 백업/파일 교체를 게이트하므로 항을 빼면 그 값이 유일한 차이일 때 디스크에 도달하지 못한다(해당 코드 주석 참조). const 가 사라져도 0 대입이라 문제없다.

`ShouldRunEffectTileHint` 주석(L102 부근)의 "페이즈 흐름(Gift → Gimmick → Placement)" 서술을 "(Gimmick → Placement)" 로 갱신한다.

건드리지 않는 것: `ShouldRunGimmickRevealHint`(기믹 리빌 홀드) · `ShouldRunEffectTileHint` · 로비 챕터 전부.

### 테스트

`TutorialProgressTests` 에서 삭제하는 API 를 쓰는 케이스(`GiftCompletion_*`, `GiftTutorial_RunsOnlyAfterCoreComplete_*` 등)를 제거한다. **`giftTutorialVersion` 을 프로필 필드로 직접 다루는 케이스**(직렬화 라운드트립, `ResetAll` 이 0 으로 되돌리는지)는 **유지**한다 — 계약 7 이 그 동작을 살려두기로 했으므로 회귀 방지가 필요하다.

## 완료 기준

- [ ] compile 성공, 콘솔 에러 0
- [ ] EditMode `TutorialProgressTests` 그린 (Gift API 케이스 제거, 필드 케이스 잔존)
- [ ] Play: 두 번째 판 진입 시 선물 안내 말풍선이 뜨지 않고 기믹 리빌 → 배치로 이어짐
- [ ] 기믹 리빌 홀드 안내(`gimmickRevealHintVersion`)는 정상 동작 — 선물 제거의 도미노가 아님을 확인
- [ ] RESET TUTORIAL 실행 후 `profile.json` 의 `giftTutorialVersion` 이 0 으로 돌아감
- [ ] 첫 판 core 튜토리얼 흐름 무변경
