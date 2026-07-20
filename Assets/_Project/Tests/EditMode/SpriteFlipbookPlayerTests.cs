using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

// sprite-flipbook-player unit 2 — 재생기 자체의 계약 테스트.
//
// FlipbookMath 테스트가 못 잡는 것을 여기서 잡는다: 재생기의 **문장 순서** 계약
// ("프레임 반영 → 그다음 완료 판정")과 렌더러 가시성 소유권. 순수 함수만 테스트하면
// 판정을 반영 앞으로 옮겨도 전부 green 이라, 마지막 프레임을 건너뛰는 회귀를 놓친다.
//
// Update 대신 public Tick(dt) 을 직접 밀어 프레임 루프 없이 EditMode 에서 검증한다
// (그 seam 이 존재하는 이유).
public class SpriteFlipbookPlayerTests
{
    private const float Fps = 10f;
    private const int FrameCount = 4;

    private GameObject _go;
    private SpriteFlipbookData _data;
    private Sprite[] _frames;

    [SetUp]
    public void SetUp()
    {
        _frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            var tex = new Texture2D(2, 2);
            tex.name = $"tex_{i}";
            _frames[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            _frames[i].name = $"frame_{i}";
        }

        _data = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        WriteData(_data, _frames, Fps, loop: false);

        _go = new GameObject("flipbook", typeof(SpriteRenderer));
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        if (_data != null) Object.DestroyImmediate(_data);
        foreach (var s in _frames)
        {
            if (s == null) continue;
            var tex = s.texture;
            Object.DestroyImmediate(s);
            if (tex != null) Object.DestroyImmediate(tex);
        }
    }

    // private 직렬화 필드에 값을 넣는다. 필드명은 unit 3 오소링 유틸도 의존하는 계약이다.
    private static void WriteData(SpriteFlipbookData data, Sprite[] frames, float fps, bool loop)
    {
        var so = new SerializedObject(data);
        var arr = so.FindProperty("frames");
        arr.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.FindProperty("fps").floatValue = fps;
        so.FindProperty("loop").boolValue = loop;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private SpriteFlipbookPlayer AddPlayer(bool disableRendererWhenFinished = false)
    {
        // playOnEnable 기본값(true)이 AddComponent 시점의 OnEnable 에서 발동하지만
        // flipbook 이 아직 없어 no-op 이다. 명시적으로 Play(data) 를 호출해 시작한다.
        var player = _go.AddComponent<SpriteFlipbookPlayer>();
        if (disableRendererWhenFinished)
        {
            var so = new SerializedObject(player);
            so.FindProperty("disableRendererWhenFinished").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return player;
    }

    // --- 순서 계약: 반영 → 판정 ---

    [Test]
    public void OneShot_TickedAtFrameRate_EndsOnLastFrame()
    {
        // 현실적인 tick 간격(dt < 프레임 간격)에서 완주와 최종 포즈를 확인한다.
        // 주의: 이 테스트는 "반영 → 판정" 순서를 판별하지 못한다 — 잘게 돌리면 마지막 프레임이
        // 완료보다 여러 tick 앞서 반영되므로 순서를 뒤집어도 통과한다(mutation 으로 확인함).
        // 그 계약은 OneShot_SingleTickPastEnd_StillRendersLastFrame 이 단독으로 지킨다.
        var player = AddPlayer();
        var renderer = _go.GetComponent<SpriteRenderer>();
        player.Play(_data);

        for (int i = 0; i < 100 && player.IsPlaying; i++)
            player.Tick(1f / 60f);

        Assert.That(player.IsPlaying, Is.False, "원샷이 완주하지 않았다.");
        Assert.That(renderer.sprite, Is.SameAs(_frames[FrameCount - 1]),
            "완주 시점에 마지막 프레임이 렌더러에 반영되어 있지 않다.");
    }

    [Test]
    public void OneShot_SingleTickPastEnd_StillRendersLastFrame()
    {
        // 순서 계약을 실제로 판별하는 테스트는 이것이다.
        // 잘게 tick 하면(dt < 프레임 간격) 마지막 프레임이 완료 판정보다 여러 프레임 앞서 반영되므로,
        // 판정을 반영 앞으로 옮겨도 통과해 버린다. 한 번의 tick 이 마지막 프레임 진입과 완주를 동시에
        // 건너뛰게 만들어야 "반영 → 판정" 순서가 유일한 통과 조건이 된다.
        var player = AddPlayer();
        var renderer = _go.GetComponent<SpriteRenderer>();
        player.Play(_data);
        Assert.That(renderer.sprite, Is.SameAs(_frames[0]), "전제 실패: Play 가 첫 프레임을 안 걸었다.");

        player.Tick(FlipbookMath.Duration(Fps, FrameCount));

        Assert.That(player.IsPlaying, Is.False);
        Assert.That(renderer.sprite, Is.SameAs(_frames[FrameCount - 1]),
            "완료 판정이 프레임 반영보다 먼저 실행돼 마지막 프레임을 건너뛰었다.");
    }

    [Test]
    public void OneShot_AdvancesThroughEveryFrameInOrder()
    {
        var player = AddPlayer();
        var renderer = _go.GetComponent<SpriteRenderer>();
        player.Play(_data);

        // Tick **뒤에** 읽는다. 앞에서 읽으면 마지막 Tick 이 반영한 프레임을 보지 못해
        // 완주 프레임 검사가 한 칸 모자란 값으로 통과해 버린다.
        int previous = 0;
        for (int i = 0; i < 100 && player.IsPlaying; i++)
        {
            player.Tick(1f / 60f);
            int actual = System.Array.IndexOf(_frames, renderer.sprite);
            Assert.That(actual, Is.GreaterThanOrEqualTo(previous), "프레임이 역행했다.");
            previous = actual;
        }

        Assert.That(previous, Is.EqualTo(FrameCount - 1));
    }

    [Test]
    public void Play_AppliesFirstFrameImmediately()
    {
        // Play 직후 한 프레임 동안 이전 스프라이트가 남으면 깜빡인다.
        var player = AddPlayer();
        var renderer = _go.GetComponent<SpriteRenderer>();

        player.Play(_data);

        Assert.That(renderer.sprite, Is.SameAs(_frames[0]));
    }

    [Test]
    public void Play_Retrigger_RestartsFromFirstFrame()
    {
        var player = AddPlayer();
        var renderer = _go.GetComponent<SpriteRenderer>();
        player.Play(_data);
        player.Tick(0.25f);
        Assert.That(renderer.sprite, Is.Not.SameAs(_frames[0]), "전제 실패: 재생이 전진하지 않았다.");

        player.Play();

        Assert.That(renderer.sprite, Is.SameAs(_frames[0]));
        Assert.That(player.IsPlaying, Is.True);
    }

    // --- 루프 ---

    [Test]
    public void Loop_NeverFinishesAndIsLoopingIsTrue()
    {
        WriteData(_data, _frames, Fps, loop: true);
        var player = AddPlayer();
        player.Play(_data);

        for (int i = 0; i < 200; i++) player.Tick(1f / 60f);

        Assert.That(player.IsPlaying, Is.True);
        // IsLooping 이 없으면 폴링 소비자가 이 상태를 "아직 재생 중" 과 구분하지 못해 영구 대기한다.
        Assert.That(player.IsLooping, Is.True);
    }

    [Test]
    public void OneShot_IsLoopingIsFalse()
    {
        var player = AddPlayer();
        player.Play(_data);
        Assert.That(player.IsLooping, Is.False);
    }

    // --- 렌더러 가시성 소유권 ---

    [Test]
    public void WhenFlagOff_PlayerNeverTouchesRendererEnabled()
    {
        // 플래그를 안 쓰는 소비자는 렌더러 enabled 를 온전히 자기가 소유한다.
        var player = AddPlayer(disableRendererWhenFinished: false);
        var renderer = _go.GetComponent<SpriteRenderer>();
        renderer.enabled = false;

        player.Play(_data);
        for (int i = 0; i < 100 && player.IsPlaying; i++) player.Tick(1f / 60f);

        Assert.That(renderer.enabled, Is.False, "재생기가 소유하지 않은 렌더러 상태를 건드렸다.");
    }

    [Test]
    public void WhenFlagOn_FinishDisablesRendererAndPlayReenables()
    {
        var player = AddPlayer(disableRendererWhenFinished: true);
        var renderer = _go.GetComponent<SpriteRenderer>();

        player.Play(_data);
        Assert.That(renderer.enabled, Is.True);

        for (int i = 0; i < 100 && player.IsPlaying; i++) player.Tick(1f / 60f);
        Assert.That(renderer.enabled, Is.False, "완주 후 렌더러가 꺼지지 않았다.");

        player.Play();
        Assert.That(renderer.enabled, Is.True, "재생 시 렌더러가 다시 켜지지 않았다.");
    }

    [Test]
    public void WhenFlagOn_StopAlsoDisablesRenderer()
    {
        // 중도 취소에서 안 끄면 취소된 원샷의 중간 프레임이 화면에 멈춰 남는다.
        var player = AddPlayer(disableRendererWhenFinished: true);
        var renderer = _go.GetComponent<SpriteRenderer>();
        player.Play(_data);
        player.Tick(0.15f);

        player.Stop();

        Assert.That(player.IsPlaying, Is.False);
        Assert.That(renderer.enabled, Is.False);
    }

    [Test]
    public void WhenFlagOff_StopLeavesRendererAlone()
    {
        var player = AddPlayer(disableRendererWhenFinished: false);
        var renderer = _go.GetComponent<SpriteRenderer>();
        player.Play(_data);
        player.Tick(0.15f);

        player.Stop();

        Assert.That(renderer.enabled, Is.True);
    }

    // --- 빈 데이터 ---

    [Test]
    public void Play_WithNoFrames_DoesNotStartPlaying()
    {
        var empty = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        try
        {
            var player = AddPlayer();
            player.Play(empty);
            Assert.That(player.IsPlaying, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(empty);
        }
    }

    [Test]
    public void Tick_WhenNotPlaying_IsNoOp()
    {
        var player = AddPlayer();
        var renderer = _go.GetComponent<SpriteRenderer>();
        renderer.sprite = _frames[2];

        player.Tick(10f);

        Assert.That(renderer.sprite, Is.SameAs(_frames[2]));
    }
}
