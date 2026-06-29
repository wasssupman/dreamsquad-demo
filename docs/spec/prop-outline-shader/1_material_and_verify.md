# 1 — 전 프랍 머티리얼/프리팹 적용 + Play 검증

## 목적

unit 0 셰이더를 forest 전 프랍에 적용하고, Play→게임뷰 스크린샷으로 외곽선/베이스 룩을 육안 검증·수치 튜닝한다.

## 변경 대상

- `Assets/_Project/Prefabs/Props/forest/mat/*_cast.mat` (7종, 인플레이스 셰이더 교체)
- `Assets/_Project/Prefabs/Props/forest/mat/PropOutline_Sprite_Unlit.mat` (신규 공유 머티리얼)
- `Assets/_Project/Prefabs/Props/forest/*.prefab` (27종, 위 공유 머티리얼로 SpriteRenderer 재할당)

## 적용 방식

- **7 cast 머티리얼 인플레이스**: 셰이더를 `Wassup/Prop Outline (Sprite)` 로 교체. 원래 Simple Lit(tree, rock_l, rock_m) → `_LIT_ON` On, 나머지(flower p/w/y, rock_s) → Off. `_MainTex`/`_BaseColor`/`_Cutoff` 자동 보존.
- **27 패키지-기본 프랍**: 패키지 `Sprite-Unlit-Default` 는 편집 불가 → 공유 `PropOutline_Sprite_Unlit.mat`(Lit Off) 신규 생성 후 프리팹 27개의 SpriteRenderer 에 재할당(`PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`, 씬 불간섭).
- 공통 값: `_OutlineColor` 어두운 톤, `_OutlineWidth` 0.05(상대), `_Cutoff` 0.5, `_ZWrite` On, `_OUTLINE_ON` On.

## 계약

- 씬 SaveScene 금지 — 머티리얼/프리팹 에셋만 변경(사용자 씬 WIP 보존). [[feedback-scene-save-bakes-wip]]
- 프리팹 편집은 `LoadPrefabContents` 격리 방식.

## 완료 기준

- Play: 168개 라이브 프랍 전부 `Wassup/Prop Outline (Sprite)` 렌더, console 0.
- 내부 스트로크가 배경 무관하게 보이고 발밑 링 없음, 베이스 룩 회귀 없음.
- 사용자 육안 통과. 통과 시 확인 일자 + 커밋 해시 추가.

확인: 2026-06-29 사용자 육안 통과 ("이게 좋다"). 내부 스트로크, width 0.03, 다크 색. 168 라이브 프랍 적용, console 0. 커밋 50a5c5e. 스크린샷 `Assets/Screenshots/prop_outline_v8_w03_boulder.png` · `v9_w03_board.png`.
