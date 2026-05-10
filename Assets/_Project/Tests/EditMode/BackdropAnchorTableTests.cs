using NUnit.Framework;
using UnityEngine;
using Wassup.Data.Season;
using Wassup.Presentation.Backdrop;

namespace Wassup.Tests.EditMode
{
    public class BackdropAnchorTableTests
    {
        // boardCenter = (5,0,7), boardHalfWorld = (5,7), pad = 1.5 * 1.0 = 1.5
        private static readonly Vector3 Center   = new(5f, 0f, 7f);
        private static readonly Vector2 HalfA    = new(5f, 7f);
        private const           float   PadA     = 1.5f;
        private const           float   TileA    = 1f;

        // boardCenter = (0,2,0), boardHalfWorld = (3,4), pad = 2 * 0.5 = 1.0
        private static readonly Vector3 Center2  = new(0f, 2f, 0f);
        private static readonly Vector2 HalfB    = new(3f, 4f);
        private const           float   PadB     = 2f;
        private const           float   TileB    = 0.5f;

        private static Vector3 Resolve(EdgeAnchor anchor, Vector3 c, Vector2 h, float p, float t)
            => BackdropAnchorTable.Resolve(anchor, c, h, p, t);

        // ── Config A ──────────────────────────────────────────────────────────
        [Test] public void NorthLeft_A()   => Assert.AreEqual(new Vector3(0f,  0f, 15.5f), Resolve(EdgeAnchor.NorthLeft,   Center, HalfA, PadA, TileA));
        [Test] public void NorthCenter_A() => Assert.AreEqual(new Vector3(5f,  0f, 15.5f), Resolve(EdgeAnchor.NorthCenter, Center, HalfA, PadA, TileA));
        [Test] public void NorthRight_A()  => Assert.AreEqual(new Vector3(10f, 0f, 15.5f), Resolve(EdgeAnchor.NorthRight,  Center, HalfA, PadA, TileA));
        [Test] public void EastTop_A()     => Assert.AreEqual(new Vector3(11.5f,0f, 14f),  Resolve(EdgeAnchor.EastTop,     Center, HalfA, PadA, TileA));
        [Test] public void EastMiddle_A()  => Assert.AreEqual(new Vector3(11.5f,0f,  7f),  Resolve(EdgeAnchor.EastMiddle,  Center, HalfA, PadA, TileA));
        [Test] public void EastBottom_A()  => Assert.AreEqual(new Vector3(11.5f,0f,  0f),  Resolve(EdgeAnchor.EastBottom,  Center, HalfA, PadA, TileA));
        [Test] public void SouthRight_A()  => Assert.AreEqual(new Vector3(10f, 0f, -1.5f), Resolve(EdgeAnchor.SouthRight,  Center, HalfA, PadA, TileA));
        [Test] public void SouthCenter_A() => Assert.AreEqual(new Vector3(5f,  0f, -1.5f), Resolve(EdgeAnchor.SouthCenter, Center, HalfA, PadA, TileA));
        [Test] public void SouthLeft_A()   => Assert.AreEqual(new Vector3(0f,  0f, -1.5f), Resolve(EdgeAnchor.SouthLeft,   Center, HalfA, PadA, TileA));
        [Test] public void WestBottom_A()  => Assert.AreEqual(new Vector3(-1.5f,0f,  0f),  Resolve(EdgeAnchor.WestBottom,  Center, HalfA, PadA, TileA));
        [Test] public void WestMiddle_A()  => Assert.AreEqual(new Vector3(-1.5f,0f,  7f),  Resolve(EdgeAnchor.WestMiddle,  Center, HalfA, PadA, TileA));
        [Test] public void WestTop_A()     => Assert.AreEqual(new Vector3(-1.5f,0f, 14f),  Resolve(EdgeAnchor.WestTop,     Center, HalfA, PadA, TileA));

        // ── Config B ──────────────────────────────────────────────────────────
        [Test] public void NorthLeft_B()   => Assert.AreEqual(new Vector3(-3f, 2f,  5f),   Resolve(EdgeAnchor.NorthLeft,   Center2, HalfB, PadB, TileB));
        [Test] public void NorthCenter_B() => Assert.AreEqual(new Vector3( 0f, 2f,  5f),   Resolve(EdgeAnchor.NorthCenter, Center2, HalfB, PadB, TileB));
        [Test] public void NorthRight_B()  => Assert.AreEqual(new Vector3( 3f, 2f,  5f),   Resolve(EdgeAnchor.NorthRight,  Center2, HalfB, PadB, TileB));
        [Test] public void EastTop_B()     => Assert.AreEqual(new Vector3( 4f, 2f,  4f),   Resolve(EdgeAnchor.EastTop,     Center2, HalfB, PadB, TileB));
        [Test] public void EastMiddle_B()  => Assert.AreEqual(new Vector3( 4f, 2f,  0f),   Resolve(EdgeAnchor.EastMiddle,  Center2, HalfB, PadB, TileB));
        [Test] public void EastBottom_B()  => Assert.AreEqual(new Vector3( 4f, 2f, -4f),   Resolve(EdgeAnchor.EastBottom,  Center2, HalfB, PadB, TileB));
        [Test] public void SouthRight_B()  => Assert.AreEqual(new Vector3( 3f, 2f, -5f),   Resolve(EdgeAnchor.SouthRight,  Center2, HalfB, PadB, TileB));
        [Test] public void SouthCenter_B() => Assert.AreEqual(new Vector3( 0f, 2f, -5f),   Resolve(EdgeAnchor.SouthCenter, Center2, HalfB, PadB, TileB));
        [Test] public void SouthLeft_B()   => Assert.AreEqual(new Vector3(-3f, 2f, -5f),   Resolve(EdgeAnchor.SouthLeft,   Center2, HalfB, PadB, TileB));
        [Test] public void WestBottom_B()  => Assert.AreEqual(new Vector3(-4f, 2f, -4f),   Resolve(EdgeAnchor.WestBottom,  Center2, HalfB, PadB, TileB));
        [Test] public void WestMiddle_B()  => Assert.AreEqual(new Vector3(-4f, 2f,  0f),   Resolve(EdgeAnchor.WestMiddle,  Center2, HalfB, PadB, TileB));
        [Test] public void WestTop_B()     => Assert.AreEqual(new Vector3(-4f, 2f,  4f),   Resolve(EdgeAnchor.WestTop,     Center2, HalfB, PadB, TileB));
    }
}
