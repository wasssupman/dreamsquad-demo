# 0 — 네온 에셋 임포트 (순수 추가 커밋)

## 목적

이후 unit 이 참조할 새 에셋만 프로젝트에 추가한다. 기존 에셋·씬·코드 무변경 —
이 커밋 revert 시 새 파일만 사라진다.

## 변경 대상

- `Assets/_Project/Art/lobby_bg_neon_night.png` (신규) — 사용자 제공 네온 시티 밤 배경 1670×941
- `Assets/_Project/Art/Depth/lobby_bg_neon_depth.png` (신규) — 위 배경용 저주파 뎁스맵 640×360
- `Assets/_Project/Art/LobbyIcons/neon_glyph_squad.png` (신규) — 시안 추출 흰 글리프(사람들)
- `Assets/_Project/Art/LobbyIcons/neon_glyph_deck.png` (신규) — 시안 추출 흰 글리프(카드)
- `Assets/_Project/Art/LobbyIcons/neon_glyph_history.png` (신규) — 시안 추출 흰 글리프(트로피)

## 구현

1. 시안 원본(스크래치패드 파이프라인)에서 글리프 3종을 휘도 기반 알파로 추출
   (흰 글리프 + 투명 배경, 채도 필터로 뱃지/네온 오염 제거).
2. 뎁스맵은 정식 파이프라인으로 베이크 — `Assets/_Project/Modules/DepthParallax/Tools~/depth_bake.py`
   `--flatten --max-width 640` (DA-V2 Small 추론 → 2/98 정규화 → median+blur 평탄화 → 640×360 R8).
   완료 기준: p99.5 gradient < 10 (lobby-background-parallax unit 2 레시피 준수).
3. 파일 복사 후 Unity refresh 로 `.meta` 생성 확인, 텍스처 임포트 타입:
   배경·글리프 = Sprite(2D and UI) — 기존 `lobby_bg_night.png` 임포트 설정과 동일 계열.
   뎁스맵 = 기존 `lobby_bg_depth.png` 설정과 동일 계열(Default, sRGB off 여부 대조).
4. 커밋: 신규 5파일 + `.meta` 5짝만 경로 명시 스테이징.

## 완료 기준

- Unity 콘솔 임포트 에러 0, 5개 에셋이 프로젝트 뷰에서 정상 프리뷰.
- `git show --stat` 에 신규 10파일(에셋+메타)만 포함.

> 2026-07-31 완료 — 커밋 `334e705e`. 임포트 에러 0, 신규 10파일만 포함 확인.
> 글리프는 시안 추출본 화질 한계(42~75px)로 **절차 재작화**(512→128 다운샘플)로 대체 — 계약 동일(흰 실루엣+투명 배경).
> spriteMode 는 MCP modify 가 Multiple(2)로 남겨 .meta 직접 수정으로 Single(1) 확정.
>
> **rev `5fe90891`**: 초판 뎁스맵(수제 수직 그라디언트)이 빌딩 구조와 안 맞아 패럴랙스가
> 어색하다는 피드백 → `depth_bake.py --flatten` 정식 베이크로 교체 (p99.5 gradient 2.0).
