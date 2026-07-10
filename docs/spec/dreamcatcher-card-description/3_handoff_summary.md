# Handoff — dreamcatcher-card-description

## Commit
- `fe4ba372` feat(dreamcatcher): 카드 설명 필드 + … (2 spec 합본 커밋)

## Implemented
- `DreamcatcherCard.description` (`[TextArea] string`) 추가 — SO 필드 끝에 append(기존 에셋 inert).
- 덱빌더 상세 팝업(`PopupBody`): 헤더(축·타입) + effects[] 자동 수치라인 + authored description 블록.
  effects 없는 Unit 카드는 description 이 유일 본문(이전 빈칸 해소).
- **Unit 카드는 axis 칩 제거** — axis 는 Squad 전용 대상 필터라 개별 부착 카드엔 무의미.
- 카드 22장 description 기입: Squad 11(대상/플레이버), Unit 5(메커니즘 내용 그대로), Active 6(provisional).

## Key Files
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — description 필드
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — `PopupBody` 렌더 + axis 칩 게이팅
- `Assets/_Project/Data/Dreamcatcher/Card_*.asset`, `Active_*.asset` — description 값

## Verified
- compile 클린(에러 0). EditMode 15/15(CatalogSync/DeckRules/DcTrigger). PlayMode 8/8.
- UI 육안 검증(팝업 텍스트/레이아웃)은 후속 스모크 권장 — 자동화 불가 구간.

## Notes
- effects[] 자동 수치라인은 밸런스 SoT 유지, description 은 보완(메커니즘/플레이버). 수치를 description
  에 중복 기입하지 말 것(드리프트 방지) — 단 Unit 카드는 effects 가 없어 수치를 description 에 담음.
- Active 카드는 카탈로그(덱빌더) 밖이라 팝업 비노출 — description 은 손패 peek 후속용.

## Follow-up
- 인게임 손패(`DreamcatcherHandView.BindCard`) 롱프레스/홀드 peek 로 description 노출(후속 spec).
- Active 문안 provisional → 실제 SkillData 값 대조 후 확정.
