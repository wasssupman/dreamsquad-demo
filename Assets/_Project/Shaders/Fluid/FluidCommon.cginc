// fluid-paint-mixing unit 1 — 유체 솔버 공용 정점/헬퍼.
// Ported from PavelDoGreat/WebGL-Fluid-Simulation (baseVertexShader).
//   Copyright (c) 2017 Pavel Dobryakov — MIT License.
//   https://github.com/PavelDoGreat/WebGL-Fluid-Simulation/blob/master/LICENSE
#ifndef WASSUP_FLUID_COMMON_INCLUDED
#define WASSUP_FLUID_COMMON_INCLUDED

#include "UnityCG.cginc"

// (1/w, 1/h) — 이웃 샘플 오프셋 및 advection 스텝 스케일. sim 필드 텍셀.
float2 _TexelSize;

// 이웃 UV(vL/vR/vT/vB)를 쓰는 패스(divergence/curl/vorticity/pressure/gradientSubtract)용.
struct v2fNeighbors
{
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
    float2 vL  : TEXCOORD1;
    float2 vR  : TEXCOORD2;
    float2 vT  : TEXCOORD3;
    float2 vB  : TEXCOORD4;
};

// 단순 UV 패스(advection/splat/clear/display)용.
struct v2fSimple
{
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

v2fNeighbors VertNeighbors(appdata_img v)
{
    v2fNeighbors o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv  = v.texcoord;
    o.vL  = v.texcoord - float2(_TexelSize.x, 0.0);
    o.vR  = v.texcoord + float2(_TexelSize.x, 0.0);
    o.vT  = v.texcoord + float2(0.0, _TexelSize.y);
    o.vB  = v.texcoord - float2(0.0, _TexelSize.y);
    return o;
}

v2fSimple VertSimple(appdata_img v)
{
    v2fSimple o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv  = v.texcoord;
    return o;
}

#endif // WASSUP_FLUID_COMMON_INCLUDED
