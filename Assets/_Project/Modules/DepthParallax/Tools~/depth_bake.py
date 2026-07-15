#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
depth_bake.py — offline depth-map baker for the Wassup DepthParallax module.

WHAT
    Takes a folder of 2D art frames (a cutscene flip-book or a single card
    illustration) and produces R8 grayscale depth PNG(s) that the
    DepthParallax UGUI shader samples for a 2.5D parallax effect. The output
    is imported into Unity with the correct settings by `DepthMapBaker.cs`
    (textureType=Default, sRGB=false/linear, no-mip, Uncompressed, R8,
    Bilinear, Clamp, non-atlased). This script never touches Unity.

    Convention: WHITE = NEAR, BLACK = FAR. Depth Anything already predicts
    larger values for nearer surfaces, so its native output maps straight to
    white=near. (Runtime polarity is a shader knob `depthSign`; assets always
    keep the white=near convention. Use --invert only if a given model/version
    comes out flipped at eyeball check.)

MODEL / LICENSING  (HARD CONSTRAINT — read before swapping models)
    Default model: "depth-anything/Depth-Anything-V2-Small-hf"  (Apache-2.0)
        -> commercial-safe. This is the ONLY DA-V2 tier allowed for shipping
           assets.
    FORBIDDEN for commercial assets (non-commercial / restrictive licenses):
        * Depth-Anything-V2  Base / Large / Giant  -> CC-BY-NC-4.0
        * Depth Pro (Apple)                         -> ASCL (research-only)
    If DA-V2 Small quality is insufficient, the commercial-clean higher-quality
    fallbacks are:
        * MiDaS DPT-Large  ("Intel/dpt-large")       -> MIT
        * Depth Anything V1 Large                     -> Apache-2.0
    This script refuses to load a known-forbidden model id.

PROCEDURE (per docs/spec/depth-parallax/4_depth_baker_editor.md)
    1. DEFAULT — SINGLE STATIC DEPTH. Infer ONE representative frame (the
       most zoomed / sharpest) and reuse it for every color frame. Cutscene
       zoom is tiny and parallax amplitude is <=4%, so per-frame drift is
       sub-perceptual. Output = 1 depth PNG (deployCutsceneDepth length 1).
    2. ESCALATION (--per-frame) — only when the silhouette ACTUALLY animates.
       Per-frame INDEPENDENT extraction flickers, so we mitigate with:
         (a) one shared GLOBAL percentile normalization (below), and
         (b) optional light temporal EMA (--temporal-ema) to damp jitter.
       NOTE: this art has no programmatic zoom and no ground-truth
       reprojection, so we do NOT assume a "known zoom transform". If true
       silhouette motion needs alignment, warp a single representative depth
       to each frame by MEASURED registration (ECC / feature match), never by
       an assumed transform. That measured-warp path is intentionally left as
       a documented option, not an assumed-zoom shortcut.
    3. GLOBAL PERCENTILE (2 / 98) normalization across ALL frames — never
       per-frame min-max (per-frame min-max makes the plane "breathe").
    4. On 8-bit quantize: DITHER (break banding) + mild GAUSSIAN BLUR (soften
       the depth cliff at silhouette edges -> less halo / edge-smear when the
       shader offsets UVs; cel-art backgrounds are pure-far, so the silhouette
       is a cliff).
    5. Output = HALF-RES R8 grayscale PNG.

    Cel-art weakness (hand-touch budget, done in an image editor AFTER bake):
    floating props / weapons / boots / hair / flat interiors read poorly and
    may need paint-over. Eyeball a few frames for outline halo and polarity.

DEPENDENCIES
    pip install "transformers>=4.40" torch pillow numpy
    (scipy optional — used for the Gaussian blur if present; a numpy fallback
     is built in so scipy is not required.)

USAGE
    # default: single static depth from the sharpest frame
    python depth_bake.py <input_frame_dir> <output_dir>

    # per-frame escalation (only if the silhouette really moves)
    python depth_bake.py <input_frame_dir> <output_dir> --per-frame

    # tuning knobs
    python depth_bake.py in/ out/ --blur-sigma 1.0 --dither 0.5 \
        --percentile-low 2 --percentile-high 98 --temporal-ema 0.0
"""

from __future__ import annotations  # lazy annotations so `str | None` / `list[str]` run on 3.9+

import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

# ── licensing guard ──────────────────────────────────────────────────────────
DEFAULT_MODEL = "depth-anything/Depth-Anything-V2-Small-hf"  # Apache-2.0

# Substrings that identify non-commercial / research-only checkpoints. Loading
# any of these for a shipping asset violates the pipeline license policy.
FORBIDDEN_MODEL_SUBSTRINGS = (
    "depth-anything-v2-base",
    "depth-anything-v2-large",
    "depth-anything-v2-giant",
    "depth-pro",
    "depthpro",
)

# Image extensions we treat as color frames.
FRAME_EXTS = (".png", ".jpg", ".jpeg", ".webp", ".bmp")


def die(msg: str) -> None:
    print(f"[depth_bake] ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def assert_model_allowed(model_id: str) -> None:
    low = model_id.lower()
    for bad in FORBIDDEN_MODEL_SUBSTRINGS:
        if bad in low:
            die(
                f"model '{model_id}' is CC-BY-NC / research-only and FORBIDDEN "
                f"for commercial assets. Use the default Apache-2.0 "
                f"'{DEFAULT_MODEL}', or commercial-clean fallbacks "
                f"'Intel/dpt-large' (MIT) or a Depth-Anything V1 Large (Apache)."
            )


# ── device / pipeline ────────────────────────────────────────────────────────
def pick_device(explicit: str | None) -> str:
    if explicit:
        return explicit
    try:
        import torch

        if torch.backends.mps.is_available():
            return "mps"
        if torch.cuda.is_available():
            return "cuda"
    except Exception:
        pass
    return "cpu"


def build_pipeline(model_id: str, device: str):
    # HuggingFace transformers "depth-estimation" pipeline. Returns per call a
    # dict with "predicted_depth" (raw float tensor) and "depth" (a PIL image
    # that is ALREADY per-image min-max normalized — we do NOT use it, because
    # we need the raw float to apply one GLOBAL normalization across frames).
    from transformers import pipeline

    print(f"[depth_bake] loading '{model_id}' on device='{device}' ...")
    return pipeline(task="depth-estimation", model=model_id, device=device)


def infer_raw_depth(pipe, image: Image.Image) -> np.ndarray:
    """Run the model and return the RAW float depth as a 2D numpy array.

    Larger value = nearer surface (Depth Anything convention). We keep the raw
    scale here; normalization happens once, globally, later.
    """
    out = pipe(image)
    depth = out["predicted_depth"]  # torch.Tensor, (H,W) or (1,H,W)
    arr = depth.squeeze().detach().cpu().numpy().astype(np.float32)
    return arr


# ── frame IO ─────────────────────────────────────────────────────────────────
def list_frames(input_dir: str) -> list[str]:
    if not os.path.isdir(input_dir):
        die(f"input dir not found: {input_dir}")
    names = [
        n
        for n in os.listdir(input_dir)
        if os.path.splitext(n)[1].lower() in FRAME_EXTS
    ]
    if not names:
        die(f"no image frames in: {input_dir}")
    # Natural sort so frame_001 < frame_002 < ... (index alignment matters for
    # per-frame output: color 001 <-> depth 001).
    names.sort(key=lambda s: [
        int(t) if t.isdigit() else t.lower()
        for t in _split_digits(s)
    ])
    return [os.path.join(input_dir, n) for n in names]


def _split_digits(s: str) -> list[str]:
    import re

    return re.findall(r"\d+|\D+", s)


def load_rgb(path: str) -> Image.Image:
    return Image.open(path).convert("RGB")


# ── representative-frame selection (for single static depth) ─────────────────
def sharpness_score(image: Image.Image) -> float:
    """Variance-of-Laplacian style sharpness. Higher = sharper / more in-focus.

    Cheap numpy version (no OpenCV): 4-neighbor Laplacian on a downscaled
    grayscale, return the variance. The most zoomed / sharpest frame tends to
    carry the cleanest depth cues.
    """
    g = np.asarray(image.convert("L").resize((256, 256), Image.BILINEAR),
                   dtype=np.float32)
    lap = (
        -4.0 * g
        + np.roll(g, 1, axis=0)
        + np.roll(g, -1, axis=0)
        + np.roll(g, 1, axis=1)
        + np.roll(g, -1, axis=1)
    )
    return float(lap.var())


def pick_representative(frame_paths: list[str]) -> int:
    best_i, best_s = 0, -1.0
    for i, p in enumerate(frame_paths):
        s = sharpness_score(load_rgb(p))
        if s > best_s:
            best_i, best_s = i, s
    print(f"[depth_bake] representative frame: "
          f"{os.path.basename(frame_paths[best_i])} (sharpness={best_s:.1f})")
    return best_i


# ── resize / blur / quantize ─────────────────────────────────────────────────
def resize_float(arr: np.ndarray, size_wh: tuple[int, int]) -> np.ndarray:
    """Bilinear-resize a float2D array to (W,H)."""
    img = Image.fromarray(arr.astype(np.float32), mode="F")
    img = img.resize(size_wh, Image.BILINEAR)
    return np.asarray(img, dtype=np.float32)


def gaussian_blur(arr: np.ndarray, sigma: float) -> np.ndarray:
    if sigma <= 0.0:
        return arr
    # Prefer scipy if available; otherwise a compact separable numpy fallback.
    try:
        from scipy.ndimage import gaussian_filter

        return gaussian_filter(arr, sigma=sigma, mode="nearest")
    except Exception:
        radius = max(1, int(round(sigma * 3.0)))
        x = np.arange(-radius, radius + 1, dtype=np.float32)
        k = np.exp(-(x * x) / (2.0 * sigma * sigma))
        k /= k.sum()
        padded = np.pad(arr, radius, mode="edge")
        # horizontal pass (along width / axis=1)
        tmp = np.apply_along_axis(
            lambda m: np.convolve(m, k, mode="valid"), axis=1, arr=padded)
        # vertical pass (along height / axis=0)
        out = np.apply_along_axis(
            lambda m: np.convolve(m, k, mode="valid"), axis=0, arr=tmp)
        return out.astype(np.float32)


def flatten_thin(norm01: np.ndarray) -> np.ndarray:
    """얇은 근경 구조(난간 살·가로등 기둥)를 흡수하는 강한 저역통과. 큰 영역 깊이는 보존.

    WHY: 한 장 이미지 UV 패럴랙스는 '가려진 픽셀'이 없다. 난간처럼 얇은 근경 뒤로 원경이 비치는
    구조에 근경 뎁스를 주면, 밀었을 때 뒤에 있어야 할 픽셀이 없어 늘어지고 찢어진다. 그래서 얇은
    구조를 뒤 배경과 같은 뎁스로 눕혀 상대 이동을 0 으로 만든다(늘어짐이 원천 발생하지 않음).
    대신 난간이 앞으로 튀어나오는 큐는 포기한다 — 레이어 분리 없이 한 장으로 가는 값.

    커널은 출력 폭 비율로 잡아 해상도 독립. 640px 기준 median 9 / blur 12 (검증값: 뎁스 절벽
    p99.5 gradient 70.3 → 4.5). 캐릭터 컷신 뎁스에는 쓰면 안 된다(실루엣이 뭉개짐) — 기본 off.
    """
    h, w = norm01.shape
    k = max(3, int(round(w * 0.014)))
    if k % 2 == 0:
        k += 1
    k = min(k, 9)  # PIL MedianFilter 실용 상한
    sigma = max(1.0, w * 0.019)
    img = Image.fromarray((np.clip(norm01, 0.0, 1.0) * 255).astype(np.uint8))
    img = img.filter(ImageFilter.MedianFilter(size=k))       # 얇은 구조 제거
    img = img.filter(ImageFilter.GaussianBlur(radius=sigma))  # 남은 절벽을 그라데이션으로
    return np.asarray(img, dtype=np.float32) / 255.0


def quantize_r8(norm01: np.ndarray, dither: float, rng: np.random.Generator
                ) -> np.ndarray:
    """[0,1] float -> uint8 with dither to break 8-bit banding."""
    d = norm01 * 255.0
    if dither > 0.0:
        d = d + rng.uniform(-dither, dither, size=d.shape)
    return np.clip(np.rint(d), 0, 255).astype(np.uint8)


def save_r8_png(u8: np.ndarray, out_path: str) -> None:
    # Mode 'L' = single-channel 8-bit grayscale PNG. Unity's DepthMapBaker then
    # forces the importer to R8 (single channel) on this file.
    Image.fromarray(u8, mode="L").save(out_path)
    print(f"[depth_bake] wrote {out_path}  ({u8.shape[1]}x{u8.shape[0]}, R8)")


# ── main bake ────────────────────────────────────────────────────────────────
def bake(args) -> None:
    assert_model_allowed(args.model)
    frame_paths = list_frames(args.input_dir)
    os.makedirs(args.output_dir, exist_ok=True)

    # Target = half of source resolution (또는 --max-width 로 상한).
    w0, h0 = load_rgb(frame_paths[0]).size
    tw, th = max(1, w0 // 2), max(1, h0 // 2)
    if args.max_width and tw > args.max_width:
        th = max(1, int(round(th * args.max_width / tw)))
        tw = args.max_width
    target_wh = (tw, th)
    print(f"[depth_bake] {len(frame_paths)} frames, source {w0}x{h0} "
          f"-> half-res {target_wh[0]}x{target_wh[1]}, "
          f"mode={'per-frame' if args.per_frame else 'single-static'}")

    device = pick_device(args.device)
    pipe = build_pipeline(args.model, device)
    rng = np.random.default_rng(args.seed)

    if not args.per_frame:
        _bake_single_static(args, pipe, frame_paths, target_wh, rng)
    else:
        _bake_per_frame(args, pipe, frame_paths, target_wh, rng)


def _percentile_norm(raw: np.ndarray, lo: float, hi: float, invert: bool,
                     contrast: float = 1.0) -> np.ndarray:
    """Normalize with fixed percentile cut points -> [0,1]. white=near.

    contrast > 1 pushes values away from 0.5 (the shader's depthCenter / hinge
    plane), widening near/far separation for a more DRAMATIC parallax. 1.0 = off.
    """
    p_lo, p_hi = lo, hi
    if p_hi <= p_lo:
        p_hi = p_lo + 1e-6
    norm = np.clip((raw - p_lo) / (p_hi - p_lo), 0.0, 1.0)
    # Depth Anything: high raw = near -> high norm = near = white. Good default.
    if invert:
        norm = 1.0 - norm
    if contrast != 1.0:
        norm = np.clip(0.5 + (norm - 0.5) * contrast, 0.0, 1.0)
    return norm.astype(np.float32)


def _bake_single_static(args, pipe, frame_paths, target_wh, rng) -> None:
    # 1) pick the sharpest / most zoomed frame, 2) infer ONCE, 3) normalize by
    #    its own 2/98 percentiles, 4) blur + dither, 5) write one PNG shared by
    #    every color frame (deployCutsceneDepth length 1).
    rep_i = args.rep_index if args.rep_index is not None else pick_representative(frame_paths)
    rep_path = frame_paths[rep_i]

    raw = infer_raw_depth(pipe, load_rgb(rep_path))
    raw = resize_float(raw, target_wh)

    lo = float(np.percentile(raw, args.percentile_low))
    hi = float(np.percentile(raw, args.percentile_high))
    norm = _percentile_norm(raw, lo, hi, args.invert, args.contrast)
    if args.flatten:
        norm = flatten_thin(norm)   # 얇은 근경 구조 흡수(배경용). 캐릭터에는 쓰지 말 것.
    norm = gaussian_blur(norm, args.blur_sigma)
    u8 = quantize_r8(np.clip(norm, 0.0, 1.0), args.dither, rng)

    stem = args.name or (os.path.splitext(os.path.basename(rep_path))[0] + "_depth")
    save_r8_png(u8, os.path.join(args.output_dir, stem + ".png"))
    print(f"[depth_bake] single static depth: p{args.percentile_low}={lo:.3f} "
          f"p{args.percentile_high}={hi:.3f} (shared by all "
          f"{len(frame_paths)} frames)")


def _bake_per_frame(args, pipe, frame_paths, target_wh, rng) -> None:
    # 1) infer every frame (raw), resize to half-res.
    raws: list[np.ndarray] = []
    for i, p in enumerate(frame_paths):
        raw = resize_float(infer_raw_depth(pipe, load_rgb(p)), target_wh)
        raws.append(raw)
        print(f"[depth_bake]  inferred {i + 1}/{len(frame_paths)}: "
              f"{os.path.basename(p)}")

    # 2) ONE global 2/98 percentile across ALL frames stacked (never per-frame
    #    min-max -> no breathing).
    stacked = np.concatenate([r.ravel() for r in raws])
    lo = float(np.percentile(stacked, args.percentile_low))
    hi = float(np.percentile(stacked, args.percentile_high))
    print(f"[depth_bake] GLOBAL norm: p{args.percentile_low}={lo:.3f} "
          f"p{args.percentile_high}={hi:.3f}")

    # 3) normalize each frame with the shared cut points, optional temporal EMA
    #    to damp per-frame flicker, then blur + dither + write.
    ema: np.ndarray | None = None
    for i, (raw, src) in enumerate(zip(raws, frame_paths)):
        norm = _percentile_norm(raw, lo, hi, args.invert, args.contrast)
        if args.temporal_ema > 0.0:
            a = args.temporal_ema
            ema = norm if ema is None else (a * norm + (1.0 - a) * ema)
            norm = ema
        norm = gaussian_blur(norm, args.blur_sigma)
        u8 = quantize_r8(np.clip(norm, 0.0, 1.0), args.dither, rng)

        stem = os.path.splitext(os.path.basename(src))[0] + "_depth"
        save_r8_png(u8, os.path.join(args.output_dir, stem + ".png"))


# ── CLI ──────────────────────────────────────────────────────────────────────
def parse_args(argv=None):
    p = argparse.ArgumentParser(
        description="Offline depth-map baker (Depth Anything V2 Small, "
                    "Apache-2.0). White=near, half-res R8 PNG output.")
    p.add_argument("input_dir", help="folder of color frames (flip-book or one card)")
    p.add_argument("output_dir", help="folder to write depth PNG(s) into")
    p.add_argument("--per-frame", dest="per_frame", action="store_true",
                   help="escalation: bake a depth per frame with GLOBAL "
                        "normalization (default is a single shared static depth)")
    p.add_argument("--model", default=DEFAULT_MODEL,
                   help=f"HF model id (default {DEFAULT_MODEL}; forbidden: "
                        f"DA-V2 Base/Large/Giant CC-BY-NC, Depth Pro ASCL)")
    p.add_argument("--device", default=None,
                   help="mps|cuda|cpu (default: autodetect, prefer mps)")
    p.add_argument("--rep-index", dest="rep_index", type=int, default=None,
                   help="single-static: force representative frame index "
                        "(default: sharpest frame)")
    p.add_argument("--name", default=None,
                   help="single-static: output file stem (default: "
                        "<representative>_depth)")
    p.add_argument("--percentile-low", dest="percentile_low", type=float, default=2.0,
                   help="global normalization low percentile (default 2)")
    p.add_argument("--percentile-high", dest="percentile_high", type=float, default=98.0,
                   help="global normalization high percentile (default 98)")
    p.add_argument("--blur-sigma", dest="blur_sigma", type=float, default=1.0,
                   help="Gaussian blur sigma at half-res to soften the depth "
                        "cliff (default 1.0; 0 = off)")
    p.add_argument("--flatten", action="store_true",
                   help="배경용: 얇은 근경 구조(난간·기둥)를 흡수하는 강한 저역통과. 한 장 UV "
                        "패럴랙스의 늘어짐을 원천 차단. 캐릭터 컷신 뎁스엔 쓰지 말 것(기본 off)")
    p.add_argument("--max-width", dest="max_width", type=int, default=0,
                   help="출력 폭 상한(0=소스의 half-res). 뎁스는 저주파라 배경도 640 이면 충분")
    p.add_argument("--contrast", type=float, default=1.0,
                   help="depth contrast around 0.5 (the shader hinge). >1 = more "
                        "dramatic near/far parallax (e.g. 1.6). default 1.0 = off")
    p.add_argument("--dither", type=float, default=0.5,
                   help="+-dither amplitude in 8-bit levels before quantize "
                        "(default 0.5; 0 = off)")
    p.add_argument("--temporal-ema", dest="temporal_ema", type=float, default=0.0,
                   help="per-frame only: EMA alpha (0..1) to damp flicker "
                        "(default 0 = off; 0.6 = mild smoothing)")
    p.add_argument("--invert", action="store_true",
                   help="flip polarity if the model comes out far=bright "
                        "(assets keep white=near; default matches DA output)")
    p.add_argument("--seed", type=int, default=1234,
                   help="dither RNG seed for determinism (default 1234)")
    return p.parse_args(argv)


def main(argv=None) -> None:
    bake(parse_args(argv))


if __name__ == "__main__":
    main()
