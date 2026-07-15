#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
depth_layer_remap.py — cutscene-depth-layering unit 1

WHAT
    기존 뎁스 PNG 를 "근경 과장 + 몸통 L단 계단" 으로 리맵한다. 컷신 4종의 공통 구도
    (손/발이 카메라로 튀어나오고 몸이 뒤로 길게 붙음)에서, DA-V2 의 매끄러운 그라디언트는
    패럴랙스를 뭉갠다. 몸통을 이산 계단으로 끊고 손을 힌지 반대편으로 밀어 디오라마처럼
    층지게 만든다.

    입력/출력이 모두 뎁스 PNG 인 **순수 이미지 연산**이다. 모델 재추론을 하지 않는다:
      1. Ranger 뎁스는 툴 산출물이 아니라 사용자 제공 자산(threshold-pivot·contrast 2.2)이라
         재bake 하면 손으로 만든 뎁스가 소실된다.
      2. torch/transformers 불필요 → 튜닝 반복이 싸다.
      3. 툴 bake 3종 + 사용자 제공 1종을 균일하게 다룰 수 있다.

왜 힌지 분리가 핵심인가 (docs/spec/cutscene-depth-layering/0_remap_contract.md)
    셰이더의 UV 오프셋은 depth 에 선형이다:
        UvOffset = tilt * (depth - depthCenter) * amplitude * depthSign
    depthCenter(0.5)는 DepthParallaxSettings 의 **전역** 값이라 유닛별로 못 바꾼다. 따라서
    몸통을 힌지 아래([body_lo, body_hi], < 0.5)로 몰고 손을 위([near_lo, near_hi], > 0.5)로
    밀면 둘의 (depth - 0.5) 부호가 반대가 되어 **서로 반대 방향으로** 움직인다. 이게 "튀어나온
    건 극적, 뒤는 계단 대비"의 실제 구현이다. body_hi < 0.5 < near_lo 가 깨지면 효과가 사라진다.

    amplitude 는 건드리지 않는다(모바일 peak UV <= 4%). [0,1] 안의 *분포*만 바꿔 같은
    amplitude 로 더 큰 체감을 얻는다.

USAGE
    python depth_layer_remap.py <depth.png> <color.png> <out.png> --stats

    # 계단 수/강도 조정
    python depth_layer_remap.py in_depth.png color.png out.png \
        --levels 4 --near-keep 0.80 --body-lo 0.02 --body-hi 0.44 --blur-sigma 0.6

DEPENDENCIES
    numpy, pillow  (torch/transformers 불필요 — depth_bake.py 는 그것들을 지연 import 한다)
"""

from __future__ import annotations

import argparse
import os
import sys

import numpy as np
from PIL import Image

# 검증된 관례를 재구현하지 않는다: dither(8bit 밴딩), mode="L" R8 저장 트릭, numpy 폴백 blur.
# depth_bake 의 torch/transformers 는 함수 안에서 지연 import 되므로 여기서 끌려오지 않는다.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from depth_bake import gaussian_blur, quantize_r8, resize_float, save_r8_png  # noqa: E402

HINGE = 0.5  # DepthParallaxSettings.depthCenter (전역). 유닛별로 못 바꾼다.
AMPLITUDE = 0.022  # DepthParallaxSettings.amplitude — 통계 출력용(리맵은 이 값을 바꾸지 않는다)


def die(msg: str) -> None:
    print(f"[layer_remap] ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def load_depth01(path: str) -> np.ndarray:
    """뎁스 PNG -> float [0,1] 2D."""
    return np.asarray(Image.open(path).convert("L"), dtype=np.float32) / 255.0


def load_mask(color_path: str, size_wh: tuple, verbose: bool = True) -> np.ndarray:
    """색 프레임의 알파 -> 뎁스 해상도의 캐릭터 마스크(alpha > 0.5).

    색과 뎁스는 해상도가 다를 수 있다(예: Cannon 색 276x204 / 뎁스 138x102) → 뎁스에 맞춘다.
    알파가 없거나 전면 불투명이면(Guardian: 정사각 아트가 꽉 참, 장식 배경도 아트의 일부)
    마스크가 사실상 전체가 된다 — 실패시키지 않고 경고만 한다(판단은 unit 2).
    """
    img = Image.open(color_path)
    if img.mode != "RGBA":
        img = img.convert("RGBA")
    alpha = np.asarray(img, dtype=np.float32)[:, :, 3] / 255.0
    if (alpha.shape[1], alpha.shape[0]) != size_wh:
        alpha = resize_float(alpha, size_wh)
    mask = alpha > 0.5
    if verbose:
        cov = float(mask.mean())
        if cov > 0.98:
            print(f"[layer_remap] WARNING: 마스크가 사실상 전체다({cov*100:.1f}%). "
                  f"색 프레임에 투명부가 없다(Guardian 류) → 장식 배경까지 계단으로 끊긴다. "
                  f"unit 2 에서 배경 분리/opt-out 판단할 것.")
        elif cov < 0.02:
            die(f"마스크가 거의 비었다({cov*100:.1f}%). 색 프레임 알파를 확인해라.")
    return mask


MAX_BAND_GAP = 1e-6   # near_lo == body_hi 강제. 점프가 있으면 손가락 뿌리에 절벽 -> 접힘(잔상).
MAX_BODY_HI = 0.42    # 몸 대역 상한. 힌지에 붙이면 몸통이 다시 힌지에 주차돼 변위 0 이 된다.


def layer_remap(depth01: np.ndarray, mask: np.ndarray, levels: int, near_keep: float,
                near_lo: float, near_hi: float, body_lo: float, body_hi: float,
                blur_sigma: float) -> np.ndarray:
    """0_remap_contract.md 의 산식. 전부 뎁스 자체 해상도에서 수행."""
    # 불변식 1 (rev2 재정의): 몸통 전체는 힌지 아래, 손'끝'은 힌지 위.
    # 초기 계약은 손 대역 *전체*를 힌지 위로 요구했으나(near_lo > 0.5), 그게 경계 절벽을
    # 강제해 접힘을 만들었다. 손 뿌리는 힌지 아래여도 된다 — 손목·주먹이 몸과 같이 움직이는
    # 건 물리적으로 옳고, 반대 운동은 '손끝 vs 몸통'이면 성립한다.
    if not (body_hi <= HINGE < near_hi):
        die(f"불변식 위반: body_hi({body_hi}) <= {HINGE} < near_hi({near_hi}) 이어야 한다. "
            f"몸통은 힌지 아래, 손끝은 힌지 위여야 서로 반대로 움직인다.")
    # 불변식 2: 경계 연속. 점프가 있으면 손/몸 경계(손가락 뿌리)가 접힌다.
    if abs(near_lo - body_hi) > MAX_BAND_GAP:
        die(f"경계 불연속: near_lo({near_lo}) != body_hi({body_hi}). 두 대역 사이 점프는 "
            f"손가락 뿌리에 절벽을 만들어 접힘(잔상)을 낳는다. 극적임은 --near-hi 로 얻어라.")
    # 불변식 3: 몸 대역을 힌지에 붙이지 마라(1차 rev 실패 사유 — 주차 재발).
    if body_hi > MAX_BODY_HI:
        die(f"몸 대역이 힌지에 너무 가깝다: body_hi({body_hi}) > {MAX_BODY_HI}. "
            f"몸통 픽셀이 힌지에 주차돼 변위가 0 이 된다 — 리맵의 목적이 사라진다.")
    if levels < 2:
        die(f"--levels 는 2 이상이어야 한다(받은 값: {levels}).")

    # 1) 캐릭터-only 정규화. 투명부/배경이 정규화 범위를 잡아먹지 않게 마스크 안에서만 percentile.
    p2, p98 = np.percentile(depth01[mask], [2.0, 98.0])
    r = np.clip((depth01 - p2) / max(float(p98 - p2), 1e-6), 0.0, 1.0)

    out = np.empty_like(r)

    # 2~3) 근경(손): 연속 유지 + 힌지 위 대역으로 확장 → 가장 극적으로 움직인다.
    near = r >= near_keep
    out[near] = near_lo + (near_hi - near_lo) * (r[near] - near_keep) / max(1.0 - near_keep, 1e-6)

    # 4) 몸통: L단 이산 계단 + 전부 힌지 아래 → 손과 반대로, 층져서 움직인다.
    body = ~near
    q = np.floor(r[body] / max(near_keep, 1e-6) * levels) / max(levels - 1, 1)
    out[body] = body_lo + (body_hi - body_lo) * np.clip(q, 0.0, 1.0)

    # 5) 계단 경계만 완화. 평탄면은 남아야 이산감이 보존된다(sigma 를 키우면 램프로 뭉개짐).
    #    하드 계단 = depth 절벽 → 패럴랙스 시 실루엣 늘어짐(depth-parallax unit 8 "경계 급락").
    return np.clip(gaussian_blur(out, blur_sigma), 0.0, 1.0)


def stats(r: np.ndarray, mask: np.ndarray) -> tuple:
    """힌지 주차%(변위 거의 0인 픽셀)와 평균 변위."""
    ch = r[mask]
    parked = 100.0 * float(((ch > HINGE - 0.05) & (ch < HINGE + 0.05)).mean())
    disp = float(np.abs(ch - HINGE).mean()) * AMPLITUDE
    return parked, disp


def fold_metric(depth01: np.ndarray, color_wh: tuple) -> tuple:
    """접힘 판정 (0_remap_contract.md 불변식 3).

    셰이더는 _MainTex 의 UV 공간에서 샘플한다:
        uv_sample = uv + tilt*(depth-0.5)*amplitude
        d(uv_sample)/d(uv) = 1 + tilt*amplitude*d(depth)/d(uv)
    최악(tilt=±1)에서 g = |Δdepth/Δpx| * amplitude * W_color 가 1 이상이면 좌표가 뒤집혀
    이미지가 접힌다 → 스와이프 시 잔상.

    기준이 **뎁스 해상도가 아니라 색 텍스처 폭**임에 주의. amplitude*W_color 가
    Cannon(276)=6.07, Ranger(640)=14.1 이라 고해상도 유닛일수록 접힘에 취약하다.
    """
    w, h = color_wh
    d = resize_float(depth01, (w, h))
    gx = np.abs(np.diff(d, axis=1)) * AMPLITUDE * w
    gy = np.abs(np.diff(d, axis=0)) * AMPLITUDE * h
    g_max = float(max(gx.max(), gy.max()))
    fold_pct = 100.0 * float((np.concatenate([gx.ravel(), gy.ravel()]) >= 1.0).mean())
    return g_max, fold_pct


def main() -> None:
    p = argparse.ArgumentParser(description="기존 컷신 뎁스를 근경 과장 + 몸통 계단으로 리맵")
    p.add_argument("depth", help="입력 뎁스 PNG (기존 자산)")
    p.add_argument("color", help="같은 유닛의 색 프레임 PNG (알파 = 캐릭터 마스크)")
    p.add_argument("output", help="출력 뎁스 PNG (R8)")
    p.add_argument("--levels", type=int, default=4, help="몸통 계단 수 (기본 4)")
    p.add_argument("--near-keep", dest="near_keep", type=float, default=0.80,
                   help="이 위 = 손(근경), 아래 = 몸통 (정규화 후 기준, 기본 0.80)")
    p.add_argument("--near-lo", dest="near_lo", type=float, default=0.52,
                   help="손 대역 하한(>0.5). body_hi 와의 간극 <=0.05 — 크면 접힘(잔상)")
    p.add_argument("--near-hi", dest="near_hi", type=float, default=1.00,
                   help="손 대역 상한 = 손끝. **극적임은 여기서 나온다**(near_lo 가 아니라)")
    p.add_argument("--body-lo", dest="body_lo", type=float, default=0.02, help="몸 대역 하한")
    p.add_argument("--body-hi", dest="body_hi", type=float, default=0.48, help="몸 대역 상한(<0.5)")
    p.add_argument("--blur-sigma", dest="blur_sigma", type=float, default=1.2,
                   help="계단 경계 완화. **색 텍스처 px 기준**(기본 1.2). 뎁스/색 배율로 환산된다 "
                        "— 뎁스 텍셀 기준으로 두면 full-res 뎁스(Ranger)와 half-res(Cannon)의 "
                        "실효 완화가 2배 달라져 한쪽만 접힌다")
    p.add_argument("--dither", type=float, default=0.5, help="R8 양자화 dither (기본 0.5)")
    p.add_argument("--seed", type=int, default=1234, help="dither RNG seed")
    p.add_argument("--stats", action="store_true", help="리맵 전/후 힌지 주차%%·평균 변위 출력")
    args = p.parse_args()

    for f in (args.depth, args.color):
        if not os.path.isfile(f):
            die(f"파일 없음: {f}")

    d = load_depth01(args.depth)
    size_wh = (d.shape[1], d.shape[0])
    mask = load_mask(args.color, size_wh)

    # blur 는 색px 기준 계약 -> 이 유닛의 뎁스 텍셀 sigma 로 환산.
    # Ranger 뎁스는 full-res(배율 1.0), 나머지는 half-res(0.5). 이 환산이 없으면 Ranger 만
    # 절반의 완화를 받아 홀로 접힌다(rev2 실측).
    color_w = Image.open(args.color).size[0]
    depth_ratio = size_wh[0] / float(color_w)
    sigma_texels = args.blur_sigma * depth_ratio

    new = layer_remap(d, mask, args.levels, args.near_keep, args.near_lo, args.near_hi,
                      args.body_lo, args.body_hi, sigma_texels)

    # 접힘 가드는 --stats 와 무관하게 항상 돈다: 잔상은 조용히 나가면 안 되는 결함이다.
    # 기준은 baseline 대비 악화 여부다 — Ranger 처럼 원본이 이미 1px 하드 엣지를 가진 자산은
    # 접힘 0 이 불가능하므로(원본 grad 12.59), 절대값이 아니라 회귀만 본다.
    color_wh = Image.open(args.color).size
    g_max, fold_pct = fold_metric(new, color_wh)
    _p2, _p98 = np.percentile(d[mask], [2.0, 98.0])
    _before = np.clip((d - _p2) / max(float(_p98 - _p2), 1e-6), 0.0, 1.0)
    g_base, fold_base = fold_metric(_before, color_wh)

    if args.stats:
        p0, v0 = stats(_before, mask)
        p1, v1 = stats(new, mask)
        gain = 100.0 * (v1 - v0) / v0 if v0 > 0 else float("nan")
        print(f"[layer_remap] 힌지 주차: {p0:5.1f}% -> {p1:5.1f}%   "
              f"평균 변위: {v0:.4f} -> {v1:.4f}  ({gain:+.0f}%)")
        print(f"[layer_remap] 접힘 grad: {g_base:.2f} -> {g_max:.2f}   "
              f"접힘 픽셀: {fold_base:.2f}% -> {fold_pct:.2f}%   "
              f"(amp*W_color = {AMPLITUDE * color_wh[0]:.2f}, blur {args.blur_sigma}색px "
              f"= sigma {sigma_texels:.2f}텍셀)")

    if fold_pct > fold_base + 0.01:
        print(f"[layer_remap] WARNING: 접힘 회귀 {fold_base:.2f}% -> {fold_pct:.2f}% "
              f"(최대 grad {g_max:.2f}) — 스와이프 시 잔상이 원본보다 심해진다. "
              f"--blur-sigma 를 키우거나 --near-hi 를 낮춰라. "
              f"0_remap_contract.md rev2 참조.", file=sys.stderr)

    rng = np.random.default_rng(args.seed)
    u8 = quantize_r8(new, args.dither, rng)
    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
    save_r8_png(u8, args.output)
    print(f"[layer_remap] levels={args.levels} near_keep={args.near_keep} "
          f"body=[{args.body_lo},{args.body_hi}] near=[{args.near_lo},{args.near_hi}] "
          f"blur={args.blur_sigma}")


if __name__ == "__main__":
    main()
