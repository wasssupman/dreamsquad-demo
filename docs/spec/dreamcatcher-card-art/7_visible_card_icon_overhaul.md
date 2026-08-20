# 7 · visible 카드 아이콘 아트 전면 개편

## 목적

`DreamcatcherCard.visible != 0` 인 전 카드의 복잡한 타로풍 이미지를 작은 카드 슬롯에서도 즉시 읽히는 단순 아이콘 아트로 교체한다. art가 비어 있는 노출 카드에는 새 이미지를 추가하고, 서로 다른 카드가 같은 이미지를 공유하는 배정도 분리한다.

## 변경 대상

- `Assets/_Project/Art/DreamcatcherCards/*.png(.meta)` — 노출 카드 37장(유닛 31 + Active 6)의 이미지 교체·추가.
- `Assets/_Project/Data/Dreamcatcher/{Card_,Active_}*.asset` — 노출 카드별 `art` 참조 완비.
- `Assets/_Project/Tests/EditModeAssets/DreamcatcherCardArtTests.cs` — 노출 카드의 art·크기·고유 배정·임포트 계약.
- `docs/spec/dreamcatcher-card-art/README.md` — 최신 아트 방향과 작업 단위 반영.

## 구현

### 생성 입력

- 각 SO의 `displayName`과 `description`을 카드별 기본 프롬프트로 사용한다.
- 설명의 수치·조건을 장면으로 모두 풀지 않고, 플레이 핵심 동작 하나만 심볼로 압축한다.
- Dreamcatcher라는 기능명 때문에 물리적인 드림캐쳐 오브젝트를 넣지 않는다.

### 공통 시각 계약

- 1024×1536 세로형, 모바일 캐주얼 디펜스 게임 UI 아이콘.
- 중앙에 큰 심볼 하나(캔버스 약 60~70%), 단순한 둥근 3D/벡터풍 형태, 선명한 실루엣.
- 배경은 카드별 주조색의 부드러운 2색 그라디언트와 원형 글로우까지만 허용한다.
- 보조 표시는 궤적·충격파·스택 링처럼 메커닉 판독에 필요한 한 종류만 쓴다.
- 이미지 내부 텍스트·숫자·카드 프레임·보석 장식·성/전장 장면·타로·다크 RPG·실사 표현 금지.

### 연결·보존

- 기존 노출 카드가 단독으로 쓰는 PNG는 같은 경로에 교체해 meta/GUID와 SO 참조를 보존한다.
- art가 없거나 다른 노출 카드와 이미지를 공유하던 카드는 카드 id 기반 새 PNG와 고유 meta를 추가하고 SO `art`를 연결한다.
- `visible == 0` 카드의 SO와 아트 배정은 이번 작업에서 변경하지 않는다.

## 완료 기준

- [x] `visible != 0` 카드 37장 모두 `art != null`, 서로 다른 Sprite를 사용한다.
- [x] 대상 37장 모두 1024×1536 Sprite(Single), mipmap off, sRGB, alpha transparency 설정이다.
- [x] 작은 썸네일에서 카드마다 핵심 심볼 하나가 구분되며 텍스트·프레임·복잡한 장면이 없다.
- [x] Unity 임포트/컴파일 에러 0. Assets lane 회귀 테스트를 추가하고 동일 계약의 정적 전수 검사를 통과했다.
- [x] OutgameScene 드림캐쳐 컬렉션·상세 팝업에서 신규 아트가 잘림/빈 슬롯 없이 표시된다.

완료 확인: 2026-08-20 — 사용자 Play 육안 확인 통과. visible 37장 art·고유 경로·1024×1536·Sprite importer 계약 정적 전수 검사와 Unity 컴파일 0 에러를 확인했다. `Wassup.Tests.EditMode.Assets` 실행 요청은 도메인 리로드 중 MCP 세션 단절로 결과를 회수하지 못했으며, 신규 `DreamcatcherCardArtTests`가 같은 계약을 회귀 고정한다. 구현은 이 문서와 동일 커밋.
