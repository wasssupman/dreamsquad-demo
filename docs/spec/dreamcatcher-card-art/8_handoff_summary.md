# 8 · Handoff — visible 카드 아이콘 아트 개편

## Commit

- `b796562a` feat(dreamcatcher): overhaul visible card art

## Implemented

- `DreamcatcherCard.visible != 0` 전 카드 37장(유닛 31 + Active 6)의 아트를 단순 아이콘 방향으로 전면 교체했다.
- 카드별 `displayName`과 `description`을 생성 입력의 정본으로 사용하고, 핵심 게임플레이 동작 하나를 중앙 심볼로 압축했다.
- 기존에 단독 배정된 PNG 21장은 같은 경로에 교체해 meta/GUID와 SO 참조를 보존했다.
- art가 없거나 다른 visible 카드와 공유하던 16장은 id 기반 PNG와 고유 meta를 추가하고 SO `art`에 연결했다.
- visible 카드 37장 모두 서로 다른 Sprite를 사용한다. `Card_EmberBite`와 `Card_LastFlame`의 기존 공유도 분리했다.
- 전 이미지는 1024×1536 세로형이며 Sprite Single, mipmap off, sRGB, alpha transparency 계약을 유지한다.
- 이미지 내부 텍스트·숫자·카드 프레임·타로/RPG 장면·물리적 드림캐쳐 오브젝트를 배제했다.
- visible 축을 전수 검사하는 `DreamcatcherCardArtTests`를 Assets EditMode 어셈블리에 추가했다.

## Key Files

- `Assets/_Project/Art/DreamcatcherCards/`
- `Assets/_Project/Data/Dreamcatcher/{Card_,Active_}*.asset`
- `Assets/_Project/Tests/EditModeAssets/DreamcatcherCardArtTests.cs`
- `docs/spec/dreamcatcher-card-art/7_visible_card_icon_overhaul.md`
- `docs/spec/dreamcatcher-card-art/README.md`

## Verified

- YAML 전수 검사: visible 37장, art GUID 37개, 실제 PNG 경로 37개, 중복 0.
- 이미지 전수 검사: 37장 모두 1024×1536.
- importer YAML 전수 검사: Sprite Single, mipmap off, sRGB, alpha transparency 설정 일치.
- Unity refresh/domain reload 완료 후 C# 컴파일 에러 0, Console error 0.
- `Wassup.Tests.EditMode.Assets`는 세 차례 실행 요청했지만 도메인 리로드 중 UnityMCP 세션이 끊겨 job 결과를 회수하지 못했다.
- 2026-08-20 사용자 Play 육안 확인: 컬렉션·상세 이미지 표시와 단순 아이콘 방향 통과.

## Notes

- 새 카드 아트의 의미 정본은 SO의 `displayName` + `description`이다. 수치와 모든 조건을 한 장면에 나열하지 않는다.
- 기존 PNG 경로의 교체는 의도적이다. visible 카드의 구 타로풍 아트로 되돌리지 않는다.
- `visible == 0` 카드의 SO와 art 배정은 unit 7 범위 밖이라 변경하지 않았다.
- 신규 카드도 visible로 노출하려면 고유 art 배정이 필요하며, 추가한 Assets 테스트가 누락·공유·규격 이탈을 검출한다.

## Follow-up

- 모바일 메모리 예산이 필요해질 때 1024×1536 원본의 플랫폼 압축과 Sprite Atlas 도입을 별도 작업으로 검토한다.
- 인게임 3중1 선택 화면 아트화는 이 unit 범위 밖이며 기존 README 후속 후보에 남겨 둔다.
