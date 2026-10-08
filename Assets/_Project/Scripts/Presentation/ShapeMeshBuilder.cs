using System;
using System.Collections.Generic;
using UnityEngine;

namespace Somnia.Battle.Presentation
{
    // directional-attack-shape unit 7 — 공격 판정 도형(부채꼴·띠)을 **메시로 만드는 유일한 자리.**
    //
    // 소비처 2곳이 같은 함수를 지난다: 배치 프리뷰 가이드(`TilemapMapView.SetShapeGuide`, unit 6)와 공격 순간
    // 참격 자국(`ProjectileViewPool.GetShapeMarkMesh`, unit 7). 그래서 「가이드는 참말인데 참격은 거짓말」이
    // 구조적으로 불가능하다 — 둘이 같은 정점을 그린다. 예전 참격은 반각 30° 를 텍스처에, 3:1 을 메시에 손으로
    // 구워 두고 균일 배율로 키워서 저작(각·사거리)이 바뀌면 그림만 옛 모양으로 남았다.
    //
    // 관습: **+Y 가 중심(찌르는) 방향 · XY 평면 · 원점 = 꼭짓점(발밑)**. 가이드는 grid 자식으로 눕고, 참격은
    // 파티클 `startRotation3D x=90°` 로 +Y→+Z 가 되어 transform.forward(= 타겟 방향)를 따른다 — 빌더는 어느
    // 평면에 놓이는지 모른다. 단위도 모른다(호출부가 타일 × cellSize 로 환산해 넘긴다).
    //
    // 마크(참격) 메시는 채움과 테를 **한 메시**에 담고 `uv.x` 로 가른다(0 = 채움 · 1 = 테). 정점색으로 가르지
    // 않는 이유: 메시 파티클은 정점색 채널을 파티클 색 스트림이 덮어써 메시 자체의 정점색이 셰이더에 안 닿는다.
    // 알파는 4×1 램프 텍스처(`SlashMark_Ramp.png`, texel 0 = 채움 · 1~3 = 테)가 준다.
    public static class ShapeMeshBuilder
    {
        public const int Segments = 24;
        // 테 폭(타일). 가이드·참격이 같은 값 — 둘이 겹쳐 보일 때 윤곽이 일치해야 「가이드 = 판정 = 참격」이 눈으로 확인된다.
        public const float DefaultRimWidthTiles = 0.07f;

        private const float FillU = 0f;
        private const float RimU = 1f;
        private const float MarkV = 0.5f;   // 램프 1행의 가운데

        // ── 가이드용(채움/테 각각 별 메시 · UV 없음) — unit 6 에서 옮김. 정점 순서 무변. 값이 바뀐 것은 부채꼴 테의 꼭짓점·호 모서리
        //    뿐이다(`AppendFanRim` 의 마이터 — 가이드 화면도 그만큼 같이 바뀐다: 발밑 7cm 튀어나옴이 사라진다) ──

        // 부채꼴 채움: 꼭짓점(원점) + 호.
        public static void BuildFan(Mesh mesh, float angleDeg, float rOuter)
        {
            var verts = new List<Vector3>(Segments + 2); var tris = new List<int>(Segments * 3);
            AppendFan(verts, tris, null, angleDeg, rOuter);
            Commit(mesh, verts, tris, null);
        }

        // 부채꼴 테: 호 띠 + 두 직선 가장자리 띠(안쪽으로 `w`). 모서리 겹침은 알파가 조금 진해질 뿐이라 허용.
        public static void BuildFanOutline(Mesh mesh, float angleDeg, float r, float w)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            AppendFanRim(verts, tris, null, angleDeg, r, w);
            Commit(mesh, verts, tris, null);
        }

        // 띠 채움: 꼭짓점(원점)에서 +Y 로 `length`, 좌우 `halfWidth`. sim 의 `BandGate` 상자(along ∈ [0, L], |across| ≤ w) 와 같은 도형.
        public static void BuildBand(Mesh mesh, float halfWidth, float length)
        {
            var verts = new List<Vector3>(4); var tris = new List<int>(6);
            AppendBand(verts, tris, null, halfWidth, length);
            Commit(mesh, verts, tris, null);
        }

        // 띠 테: 네 변을 안쪽으로 `w` 만큼 두른 띠(모서리 겹침 허용).
        public static void BuildBandOutline(Mesh mesh, float halfWidth, float length, float w)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            AppendBandRim(verts, tris, null, halfWidth, length, w);
            Commit(mesh, verts, tris, null);
        }

        // ── 참격용(채움 + 테 한 메시 · uv.x 로 구분) ──

        public static void BuildSectorMark(Mesh mesh, float angleDeg, float r, float rimW)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            AppendFan(verts, tris, uvs, angleDeg, r);
            AppendFanRim(verts, tris, uvs, angleDeg, r, rimW);
            Commit(mesh, verts, tris, uvs);
        }

        public static void BuildBandMark(Mesh mesh, float halfWidth, float length, float rimW)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            AppendBand(verts, tris, uvs, halfWidth, length);
            AppendBandRim(verts, tris, uvs, halfWidth, length, rimW);
            Commit(mesh, verts, tris, uvs);
        }

        // `spec` 하나로 두 형을 가른다 — 호출부(브리지)가 kind 분기를 갖지 않게.
        public static void BuildMark(Mesh mesh, in ShapeMarkSpec spec)
        {
            float cs = spec.cellSize > 0f ? spec.cellSize : 1f;
            float len = spec.lengthTiles * cs;
            float rim = DefaultRimWidthTiles * cs;
            if (spec.kind == Somnia.Battle.Data.AttackShapeBaked.BandKind)
            {
                // 폭 0 저작(축 위 몸 걸침만 히트)도 선으로는 보이게 테 폭을 하한으로 — 가이드와 같은 규칙.
                float hw = Mathf.Max(spec.halfWidthTiles, DefaultRimWidthTiles) * cs;
                BuildBandMark(mesh, hw, len, rim);
            }
            else BuildSectorMark(mesh, spec.angleDeg, len, rim);
        }

        // ── 조각 ──

        private static void AppendFan(List<Vector3> verts, List<int> tris, List<Vector2> uvs, float angleDeg, float rOuter)
        {
            int n = Segments;
            float half = angleDeg * 0.5f * Mathf.Deg2Rad;
            int b = verts.Count;
            verts.Add(Vector3.zero);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 0.5f - half + (2f * half) * i / n;
                verts.Add(new Vector3(Mathf.Cos(a) * rOuter, Mathf.Sin(a) * rOuter, 0f));
            }
            for (int i = 0; i < n; i++) { tris.Add(b); tris.Add(b + i + 2); tris.Add(b + i + 1); }
            if (uvs != null) for (int i = 0; i < n + 2; i++) uvs.Add(new Vector2(FillU, MarkV));
        }

        private static void AppendFanRim(List<Vector3> verts, List<int> tris, List<Vector2> uvs, float angleDeg, float r, float w)
        {
            int n = Segments;
            float half = angleDeg * 0.5f * Mathf.Deg2Rad;
            int start = verts.Count;
            // 호 띠
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * 0.5f - half + (2f * half) * i / n;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(d * (r - w)); verts.Add(d * r);
            }
            for (int i = 0; i < n; i++)
            {
                int b = start + i * 2;
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            // 직선 가장자리 띠 2개 — 안쪽(중심 방향 +Y 쪽) 법선으로 오프셋.
            // 꼭짓점은 **마이터**로 닫는다: 폭 w 띠의 안쪽 선은 축 위 (0, w/sinθ) 를 지난다. 예전(unit 6)엔 꼭짓점에서 법선으로 w 만
            // 밀어 정점이 부채꼴 **각 밖**에 찍혔고(반각 30° 에서 축과 61°), 호 쪽 바깥 모서리도 반경을 w²/2r 넘었다 — 발밑 7cm·1mm 라
            // 눈엔 안 보였지만 「테 = 판정 안쪽」 단언이 깨진다(unit 7 테스트가 잡음). 두 정점을 도형 안으로 되돌린다.
            float sinHalf = Mathf.Sin(half);
            var apexInner = new Vector3(0f, sinHalf > 1e-4f ? Mathf.Min(w / sinHalf, r) : w, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                float a = Mathf.PI * 0.5f + side * half;
                var e = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                // 부채꼴 안쪽(중심 축 +Y 쪽)을 향하는 수직 — 오른쪽 가장자리(side −1)는 e 를 +90°, 왼쪽은 −90° 회전.
                // ⚠ 부호가 뒤집히면 테가 판정 도형 **밖**으로 나가 「가이드 = 판정」이 테 폭만큼 깨진다(unit 6 리뷰 nit).
                var nrm = new Vector3(side * e.y, -side * e.x, 0f);
                // 호 쪽 바깥 모서리는 호 **위**에(반경을 넘지 않게), 안쪽으로 도는 각은 반각을 넘지 않게(작은 반경·좁은 각에서 축을
                // 넘어 반대편으로 나가던 것 — 15°·r 0.2 테스트가 잡음). 가장자리에서 w 만큼 안쪽 = 호 위에서 atan(w/r) 회전.
                float inward = Mathf.Min(Mathf.Atan2(w, r), half);
                float ac = a - side * inward;
                var outerCorner = new Vector3(Mathf.Cos(ac) * r, Mathf.Sin(ac) * r, 0f);
                int b = verts.Count;
                verts.Add(Vector3.zero); verts.Add(apexInner); verts.Add(e * r); verts.Add(outerCorner);
                if (side < 0) { tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2); }
                else          { tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3); }
            }
            if (uvs != null) for (int i = start; i < verts.Count; i++) uvs.Add(new Vector2(RimU, MarkV));
        }

        private static void AppendBand(List<Vector3> verts, List<int> tris, List<Vector2> uvs, float halfWidth, float length)
        {
            int b = verts.Count;
            verts.Add(new Vector3(-halfWidth, 0f, 0f)); verts.Add(new Vector3(halfWidth, 0f, 0f));
            verts.Add(new Vector3(-halfWidth, length, 0f)); verts.Add(new Vector3(halfWidth, length, 0f));
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            if (uvs != null) for (int i = 0; i < 4; i++) uvs.Add(new Vector2(FillU, MarkV));
        }

        private static void AppendBandRim(List<Vector3> verts, List<int> tris, List<Vector2> uvs, float halfWidth, float length, float w)
        {
            int start = verts.Count;
            w = Mathf.Min(w, length * 0.5f, halfWidth);   // 아주 짧은/좁은 띠에서 안쪽 정점이 도형을 뒤집어 넘지 않게(리뷰 L-3)
            Vector3[] outer = { new Vector3(-halfWidth, 0f, 0f), new Vector3(halfWidth, 0f, 0f), new Vector3(halfWidth, length, 0f), new Vector3(-halfWidth, length, 0f) };
            Vector3[] inner = { new Vector3(-halfWidth + w, w, 0f), new Vector3(halfWidth - w, w, 0f), new Vector3(halfWidth - w, length - w, 0f), new Vector3(-halfWidth + w, length - w, 0f) };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4; int b = verts.Count;
                verts.Add(outer[i]); verts.Add(outer[j]); verts.Add(inner[i]); verts.Add(inner[j]);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3); tris.Add(b + 2);   // 단면 — 셰이더가 Cull Off 라 양면은 알파 2배일 뿐
            }
            if (uvs != null) for (int i = start; i < verts.Count; i++) uvs.Add(new Vector2(RimU, MarkV));
        }

        private static void Commit(Mesh mesh, List<Vector3> verts, List<int> tris, List<Vector2> uvs)
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }

    // 참격 메시의 캐시 키 겸 빌드 입력. 값은 전부 **판정에서 온다** — kind·각·반폭은 `AttackState.shape`(bake),
    // 길이는 사거리 + 내 몸(가이드·링과 같은 값 = 판정 상자의 점-대상 코어), cellSize 는 뷰 grid(맵별 타일 크기).
    // 같은 SO 에서 나온 값은 비트가 같아 양자화 없이 키가 된다.
    public readonly struct ShapeMarkSpec : IEquatable<ShapeMarkSpec>
    {
        public readonly byte kind;
        public readonly float angleDeg;        // Sector
        public readonly float halfWidthTiles;  // Band
        public readonly float lengthTiles;
        public readonly float cellSize;

        public ShapeMarkSpec(byte kind, float angleDeg, float halfWidthTiles, float lengthTiles, float cellSize)
        {
            this.kind = kind; this.angleDeg = angleDeg; this.halfWidthTiles = halfWidthTiles;
            this.lengthTiles = lengthTiles; this.cellSize = cellSize;
        }

        // bake 에서 되돌린다 — 저작 `angleDeg` 를 읽으면 reflex 저작이 Omni 로 접힌 경우 sim 과 갈린다.
        public static ShapeMarkSpec FromBaked(in Somnia.Battle.Data.AttackShapeBaked shape, float lengthTiles, float cellSize)
        {
            float ang = AngleDegOf(in shape);
            // 반폭 하한을 **키에서** 적용한다 — 하한 아래 값 둘이 같은 메시를 두 장 만들지 않게(리뷰 L-9). `BuildMark` 의 하한과 같은 값.
            float hw = shape.kind == Somnia.Battle.Data.AttackShapeBaked.BandKind
                ? Mathf.Max(shape.halfWidth, ShapeMeshBuilder.DefaultRimWidthTiles) : shape.halfWidth;
            return new ShapeMarkSpec(shape.kind, ang, hw, lengthTiles, cellSize);
        }

        // bake → 전체각(도). 가이드(`SetShapeGuide`)와 참격이 **같은 역산**을 읽어야 「같은 빌더 = 같은 윤곽」이 구조가 된다(리뷰 L-5).
        public static float AngleDegOf(in Somnia.Battle.Data.AttackShapeBaked shape) =>
            shape.kind == Somnia.Battle.Data.AttackShapeBaked.SectorKind
                ? 2f * Mathf.Atan2(shape.sinHalf, shape.cosHalf) * Mathf.Rad2Deg
                : 360f;

        public bool Equals(ShapeMarkSpec o) =>
            kind == o.kind && angleDeg == o.angleDeg && halfWidthTiles == o.halfWidthTiles
            && lengthTiles == o.lengthTiles && cellSize == o.cellSize;
        public override bool Equals(object obj) => obj is ShapeMarkSpec o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(kind, angleDeg, halfWidthTiles, lengthTiles, cellSize);
    }
}
