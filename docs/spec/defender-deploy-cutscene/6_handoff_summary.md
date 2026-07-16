# 6 — Handoff Summary (Cannon ~ Healer 시점, 2026-07-16)

## Commit

| 해시 | 내용 | 주의 |
|---|---|---|
| `bc452d78` | Cannon 49프레임+뎁스 + spec 정합성 | **제목이 내용과 다름** ↓ |
| `459e2d80` | Sniper 49프레임+뎁스 · 리맵 툴 기본값 정정 | |
| `5849e370` | FireCaster·Healer 49프레임+뎁스 | |
| `d6cc06a3` | (별도 spec) 뎁스 계단 리맵 — `cutscene-depth-layering` | |

> **`bc452d78` 함정**: 제목이 "feat(result-screen-lobby-exit): unit 0 — 결과창 다시하기 →
> 로비로" 다. 병행 세션과 공유 인덱스를 쓰다 난 사고로 **Cannon 작업 108파일이 그 커밋에
> 혼입**됐다. 작업물은 온전하나 제목으로는 못 찾는다 — **경로로 검색할 것**.

## Implemented

- 컷신 **7종** 보유: Ranger · Archer · Guardian · Cannon · Sniper · FireCaster · Healer.
  전부 49프레임(Ranger 33) + 정적 뎁스 1장 + `Defender_*.asset` 할당 완료.
- 뎁스는 전부 `cutscene-depth-layering`(`d6cc06a3`)의 **4단 계단 리맵** 적용본.
- 스펙 정합성 정리: README 앵커 좌상단→**좌하단** 정정, 폐지된 수명 계약 표시,
  `누끼 자산` 계약을 유닛-불변으로 재작성, **소스 수급 요청(배경 검정) 계약 신설**.

## Key Files

- `7_sniper_firecaster_healer.md` — **최신 절차 + 배경색 원리 (필독)**
- `5_cannon_frames.md` — 체커보드 가짜 투명 함정 상세
- `README.md` — feature-wide 계약(`누끼 자산` · `소스 수급 요청` bullet)
- `docs/spec/cutscene-depth-layering/` — 뎁스 리맵(별도 spec, 툴 `Tools~/depth_layer_remap.py`)

## Verified

- 7종 전부 Unity 재확인: frames null 0 · 001→049 순 · Sprite/Single/alphaIsTransparency/무압축,
  뎁스 R8/linear/no-mip. 콘솔 error·warning 0.
- 계단 보존 88.5~97.2% · 접힘 회귀 0.
- **Play 미검증** — 아래 Follow-up.

## Notes (되돌리면 안 되는 의도)

- **배경은 검정이어야 한다 (핵심)**: 전 컷신이 알파 평탄화 소스에서 왔다. Ranger/Archer 가
  무사했던 건 배경이 검정이어서다 — 글로우를 검정에 합성하면 `P = a×F`(premultiplied)라
  밝기에서 알파를 되살릴 수 있다. **체커** → 반투명 VFX 에 격자가 배어듦(복원 불가).
  **순백** → 흰 셔츠·색종이를 삼킴(MarginValue 스윕으로도 온전한 구간 없음).
  `0_sprite_pipeline` 의 "검정 배경"은 Ranger 소스의 우연이 아니라 **원리적으로 옳은 선택**이다.
- **역순 리넘버는 규칙이 아니다**: 계약은 *재생 = 줌-인*. Ranger/Archer 만 소스가 줌-아웃이라
  뒤집었고, Cannon~Healer 는 정방향이다.
- **재생 방향 판정에 커버리지 프록시를 쓰지 말 것**: VFX 가 지배하는 아트에선 무효
  (Healer 는 중간이 峰). 유닛마다 다른 근거가 필요하다(눈 폭·육안 등).
- **color dilation 필수**: 투명부 RGB 가 밝으면 bilinear 확대가 halo 를 번지게 한다.
- **Healer 격자는 알고 넣은 것**(사용자 결정). 버그로 오해해 파헤치지 말 것.

## Follow-up

- **Play 검증 (전 유닛)** — `scale`/`offset` 이 Cannon 2.6 · Sniper 2.6 · FireCaster 3.0 ·
  Healer 3.0 / offset 전부 (0,0) 로 **계산 시작값**이다. 좌하단 위치·크기 체감 튜닝 필요.
  Ranger/Archer/Guardian 은 튜닝 완료본.
- **Healer 재수급** — 배경 검정/알파로 다시 받으면 격자 해소. 프레임만 덮으면 GUID 유지.
- **남은 9 디펜더** — 수급 시 README 의 "소스 수급 요청" 계약 적용.
- **Cannon 고해상도** — 현 276×204, 화면 3.1배 확대. 원본 1932×1428 은 디스크에 없다.
