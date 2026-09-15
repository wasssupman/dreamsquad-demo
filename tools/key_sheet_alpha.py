#!/usr/bin/env python3
# sprite-unit-backend unit 4 — 흰 배경 스프라이트 시트 → 알파. 사용: python3 tools/key_sheet_alpha.py Raw/roy_idle.png roy_idle.png
# 규칙은 docs/reference/lessons/03-rendering-assets.md 「흰 배경 시트는 …」 참조. 의존: Pillow, numpy.
# 흰 배경 → 알파. 테두리에서 연결된 near-white 만 지운다(내부 흰색=셔츠 보존), 경계는 소프트 알파.
import sys, numpy as np
from PIL import Image
from collections import deque
src, dst = sys.argv[1], sys.argv[2]
arr = np.asarray(Image.open(src).convert('RGBA')).astype(np.int16).copy()
mn = arr[:,:,:3].min(axis=2); h,w = mn.shape
cand = mn >= 200
visited = np.zeros((h,w), bool); dq = deque()
for x in range(w):
    for y in (0,h-1):
        if cand[y,x] and not visited[y,x]: visited[y,x]=True; dq.append((y,x))
for y in range(h):
    for x in (0,w-1):
        if cand[y,x] and not visited[y,x]: visited[y,x]=True; dq.append((y,x))
while dq:
    y,x = dq.popleft()
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1)):
        ny,nx=y+dy,x+dx
        if 0<=ny<h and 0<=nx<w and cand[ny,nx] and not visited[ny,nx]:
            visited[ny,nx]=True; dq.append((ny,nx))
alpha = np.full((h,w),255,np.int16)
soft = np.clip((250-mn)*(255/50),0,255).astype(np.int16)
alpha[visited] = soft[visited]
arr[:,:,3] = alpha
Image.fromarray(arr.astype(np.uint8)).save(dst)
print(f"{dst}: 배경 {int((alpha==0).sum())}/{h*w} 제거, 부분알파 {int(((alpha>0)&(alpha<255)).sum())}")
