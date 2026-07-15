# 4 — Handoff Summary

## Commit

`d6cc06a3` — feat(cutscene-depth-layering): 근경 과장 + 몸통 4단 계단 뎁스 리맵

## Implemented

- 컷신 뎁스를 **몸통 4단 계단 + 손 연속**으로 리맵해 패럴랙스를 이산 평면(디오라마)화. 4종 적용.
- **코드 변경 0**: `UvOffset = tilt×(depth−0.5)×amp×sign` 이 depth 에 선형이라, 뎁스 PNG 에 구운
  모양이 곧 변위 프로파일이다. 셰이더·모듈·런타임 무수정, `amplitude`(0.022) 불변.
- `Tools~/depth_layer_remap.py` — 기존 뎁스 후처리(모델 재추론 없음, numpy+PIL).
  `depth_bake.py` 의 `resize_float`/`gaussian_blur`/`quantize_r8`/`save_r8_png` 재사용
  (torch 는 그쪽에서 지연 import 라 안 끌려온다).
- 대역: 몸 `[0.02, 0.40]` 4단 · 손 `[0.40, 1.00]` 연속 · 간극 0 · blur 1.2 색px.
- 제자리 교체라 **GUID 불변** → `deployCutsceneDepth` 재할당 불필요, 임포트 설정(.meta) 보존.
- 가드 4종(경계 불연속·힌지 붙임·손끝 힌지 아래·계단 1단) exit 1 + 접힘 회귀 자동 경고.

## Key Files

- `Assets/_Project/Modules/DepthParallax/Tools~/depth_layer_remap.py` — 리맵 툴
- `docs/spec/cutscene-depth-layering/0_remap_contract.md` — **산식·불변식 (rev2 필독)**
- `Assets/_Project/Sprites/Cutscene/{Ranger,Archer,Guardian,Cannon}/Depth/*.png` — 리맵된 자산
- 원본 산식이 사는 곳: `docs/spec/depth-parallax/` (모듈 본체, 이 spec 은 자산만 건드린다)

## Verified

- 4종 `--stats` 가 README rev2 표 재현. 계단 보존 90.3~97.2%(가드 ≥85%). 접힘 회귀 0.
- GUID 4종 불변, 임포트 R8/linear/no-mip/무압축 유지, SO 참조 유효, 콘솔 error·warning 0.
- **Play 미검증** — 아래 Follow-up 참조.

## Notes (되돌리면 안 되는 의도)

- **손 뿌리는 몸과 같이 움직여도 된다.** 힌지를 넘어야 하는 건 손'끝'(`near_hi`)뿐이다.
  손 대역 *전체*를 힌지 위로 밀면(초기안 `near_lo=0.80`) 손/몸 경계에 0.36 절벽이 생겨
  **접힘 = 잔상**이 난다. 사용자 Play 에서 실제로 관측됐고 rev2 로 고쳤다. 되돌리지 말 것.
- **몸 대역을 힌지에 붙이지 말 것.** 1차 수정(`body_hi 0.48`)은 접힘은 잡았지만 몸통이 다시
  힌지에 주차돼(Ranger 9.1%→16.7%) spec 의 목적을 되돌렸다.
- **`blur_sigma` 는 색px 기준.** 뎁스 텍셀 기준으로 두면 full-res 뎁스(Ranger)만 완화를 절반
  받아 홀로 접힌다.
- **Ranger 뎁스는 재bake 금지** — 툴 산출물이 아니라 사용자 제공 자산이다.
- **Ranger 는 접힘 0 이 불가능**(원본 grad 12.59, 1px 하드 엣지 + `amp×W=14.08`).
  절대값이 아니라 baseline 대비 회귀로 판정한다.
- Guardian 은 투명부 0%지만 뎁스가 이미 배경을 far 로 분리해 무해 — opt-out 불필요(unit 2 실측).

## Follow-up

- **unit 3 (Play 재검증)** — 미착수. 볼 것: ① 손·발 잔상이 사라졌나(rev2 의 목적)
  ② 손끝이 몸과 반대로 밀리나 ③ 몸통이 층져 움직이나 ④ **기존 3종이 나빠지지 않았나**.
  나빠지면 유닛별 opt-out 이 계약(일괄 적용은 완주 조건 아님): `git checkout -- <depth 경로>`.
- 잔상이 남으면 `--blur-sigma` 3.0 색px 여지(Ranger 접힘 0.09%까지, 단 계단 82.6%로 뭉개짐).
- Cannon `0.40` 최상단 몸통 단이 얇다 — 체감 부족 시 Cannon 만 `--near-keep` 하향(예 0.70).
