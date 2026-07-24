# 0. Hook 맵 bake + 덱 + 풀 등록

## 목적

레인 길이 비대칭(2배)과 합류 후 공유 꼬리를 가진 첫 맵을 만들어 풀에 6번째로 넣는다. 런타임 코드 변경 0, authoring 에셋만.

## 변경 대상

- 신규: `Assets/_Project/Data/Maps/MapDocument_Hook.asset`
- 신규: `Assets/_Project/Scripts/Data/Decks/Deck_Hook.asset` (`Deck_Zig` 복제 + `deckId`/`waveSeed` 변경)
- 수정: `Assets/_Project/Data/Maps/MapDocumentPool.asset` (entries 에 6번째 페어 추가)

## 레이아웃 (확정 — 13×12)

`#`=Walk · `.`=Place · 공백=Deco · `S`=스폰 · `G`=골 · `M`=합류점

```
11| S.       .S |
10|.#.       .#.|
 9|.#        .#.|
 8| #        .#.|
 7| #.      ### |
 6|.#.   ...#   |
 5|.#  .####M.  |
 4|.#  .#...#.  |
 3| #. .#. .#.  |
 2| #####  .#.  |
 1| .....  .#.  |
 0|        .G.  |
   0123456789012
```

Walk 세그먼트 (y=0 이 아래):

| 구간 | 셀 | 역할 |
|---|---|---|
| `col x=1, y=2..11` | 10 | 롱 레인 진입 (S2 = (1,11)) |
| `row y=2, x=1..5` | 4 | 롱 레인 하단 가로 |
| `col x=5, y=2..5` | 3 | 롱 레인 중앙 상승 |
| `row y=5, x=5..9` | 4 | 롱 레인 합류 접근 |
| `col x=9, y=0..7` | 8 | **스파인** — 위 절반 러시, `(9,5)`=M 아래는 공유 꼬리, `(9,0)`=G |
| `row y=7, x=9..11` | 2 | 러시 훅 |
| `col x=11, y=7..11` | 4 | 러시 레인 진입 (S1 = (11,11)) |

- **스폰**: `(11,11)` 러시 / `(1,11)` 롱 — 둘 다 상단 모서리. 같은 방향에서 들어오는데 도착 시각이 2배 차이나는 게 이 맵의 정체다.
- **골**: `(9,0)` 하단. 단일 골.
- **합류점 M** = `(9,5)`. 공유 꼬리 = `(9,4)~(9,0)` 5칸.

## 지표 (설계 검증 완료)

- 레인 길이 **러시 13 / 롱 25 (비율 1.92)** — 기존 풀 편차 0~2 대비 12.
- 공유 셀 6 (기존 풀 0~1). **엔진이 계산한 chokepoint = `(9,5)` 단 하나** = 합류점, 설계 의도가 데이터에 그대로 찍혔다.
- walk 34 / place 42 / deco 80. walk 그래프 = **트리**(edges 33 = cells 34 − 1) → 죽은 복도 없음.
- 2×2 walk 블록 0, 스폰 2개 모두 골 도달.
- Enemy_Basic(2.5 tiles/s) 기준 골 도달 **러시 5.2s / 롱 10.0s**, 합류점 도달 러시 3.2s / 롱 8.0s.

> 이 맵이 강제하려는 결정: ① 러시 레인이 먼저 닿으니 초반 전력을 어디에 커밋할지 ② 롱 레인의 유예를 경제·배치 정비에 쓸지 ③ 전 트래픽이 지나는 공유 꼬리에 몇 자리를 투자할지.

## 구현

1. **Map Painter 로 그린다** (`Window/Wassup/Map Painter`) — 13×12 새 격자에 위 세그먼트대로 Walk 칠 → 스폰 2 / 골 1 토글 → Place·Deco 칠. 검증 errors=0 확인 후 Bake → `MapDocument_Hook.asset`.
   - 검증기는 **첫 위반만 표시**한다(`MapPainterWindow.cs:250`). 하나 고치면 다음 게 나올 수 있다.
   - 스폰·골 토글은 해당 셀을 Walk 로 바꾼다(`:212`,`:221`) — 주변에 2×2 가 생기지 않는지 재확인.
   - Deco 를 **직접 칠해야** 런타임 커빙이 스킵된다(칠하지 않으면 `DesignateDeco` 가 배치칸을 임의로 깎는다).
2. **`Deck_Hook.asset`**: `Deck_Zig` 복제 → `deckId: Deck_Hook`, `waveSeed: 20260806`. 나머지 필드는 5장이 전부 동일하므로 그대로 둔다.
3. **풀 등록**: `MapDocumentPool.asset` entries 에 `{document: MapDocument_Hook, deck: Deck_Hook}` 추가.

## 완료 기준

- [x] `MapDocument_Hook.asset` bake — 13×12, walk 34/place 42/deco 80, 디스크 재파싱으로 레이아웃 일치 확인
- [x] 검증 통과 — 2×2 0건, 스폰 2개 골 도달, forest(edges 33 = 34−1), 파생값 불일치 0. **엔진 계산 chokepoint = `(9,5)` 단 하나**(= 합류점, 설계 의도와 일치)
- [x] `Deck_Hook.asset` 생성, `deckId: Deck_Hook`, `waveSeed: 20260806`
- [x] 풀 entries 6개, 참조 유효 (index 5 = Hook/Deck_Hook)
- [x] EditMode green — 1266 중 1264 pass / **0 fail** / 2 skip (testrig 배치 실행)
- [x] Play — 개발 override(유닛 2)로 Hook 강제 진입, 렌더·pathing 정상 (확인 2026-07-24)
