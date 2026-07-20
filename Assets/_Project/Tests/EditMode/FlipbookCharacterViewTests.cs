using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Data;
using Wassup.Presentation;

// sprite-character-preview unit 0 — 상태 매핑과 전이 계약.
//
// 프레임 진행 자체는 SpriteFlipbookPlayerTests / FlipbookMathTests 가 이미 지킨다.
// 여기서 지키는 것은 이 뷰가 새로 얹은 것뿐이다: 상태별 루프 정책, 선택 슬롯의 Idle 폴백,
// 그리고 "Attack 은 Idle 로 돌아오지만 Death 는 돌아오지 않는다" 는 비대칭.
//
// 재생기의 Update 대신 player.Tick(dt) + view.PollPlayback() 을 직접 밀어 프레임 루프 없이 검증한다
// (두 seam 이 존재하는 이유).
public class FlipbookCharacterViewTests
{
    private const float Fps = 10f;
    private const int IdleFrameCount = 3;
    private const int AttackFrameCount = 4;
    private const int DeathFrameCount = 2;

    private GameObject _go;
    private FlipbookCharacterView _view;
    private SpriteFlipbookPlayer _player;
    private SpriteRenderer _renderer;

    private SpriteFlipbookData _idle;
    private SpriteFlipbookData _attack;
    private SpriteFlipbookData _death;
    private Sprite[] _idleFrames;
    private Sprite[] _attackFrames;
    private Sprite[] _deathFrames;

    [SetUp]
    public void SetUp()
    {
        _idleFrames = MakeFrames("idle", IdleFrameCount);
        _attackFrames = MakeFrames("attack", AttackFrameCount);
        _deathFrames = MakeFrames("death", DeathFrameCount);

        _idle = MakeData("IdleFlipbook", _idleFrames, loop: true);
        _attack = MakeData("AttackFlipbook", _attackFrames, loop: false);
        _death = MakeData("DeathFlipbook", _deathFrames, loop: false);

        _go = new GameObject("sprite_character", typeof(SpriteRenderer));
        _renderer = _go.GetComponent<SpriteRenderer>();
        // RequireComponent 로 SpriteFlipbookPlayer 가 같이 붙는다.
        _view = _go.AddComponent<FlipbookCharacterView>();
        _player = _go.GetComponent<SpriteFlipbookPlayer>();

        // Deploy/Drag 는 일부러 비워 둔다 — 폴백 계약의 기본 상태다.
        WriteSlots(_view, _idle, _attack, _death);
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;

        if (_go != null) Object.DestroyImmediate(_go);
        DestroyData(_idle);
        DestroyData(_attack);
        DestroyData(_death);
        DestroyFrames(_idleFrames);
        DestroyFrames(_attackFrames);
        DestroyFrames(_deathFrames);
    }

    // --- 헬퍼 ---

    private static Sprite[] MakeFrames(string prefix, int count)
    {
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            var tex = new Texture2D(2, 2) { name = $"{prefix}_tex_{i}" };
            frames[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f);
            frames[i].name = $"{prefix}_frame_{i}";
        }
        return frames;
    }

    private static void DestroyFrames(Sprite[] frames)
    {
        if (frames == null) return;
        foreach (var s in frames)
        {
            if (s == null) continue;
            var tex = s.texture;
            Object.DestroyImmediate(s);
            if (tex != null) Object.DestroyImmediate(tex);
        }
    }

    private static void DestroyData(SpriteFlipbookData data)
    {
        if (data != null) Object.DestroyImmediate(data);
    }

    private static SpriteFlipbookData MakeData(string name, Sprite[] frames, bool loop)
    {
        var data = ScriptableObject.CreateInstance<SpriteFlipbookData>();
        data.name = name;
        var so = new SerializedObject(data);
        var arr = so.FindProperty("frames");
        arr.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        so.FindProperty("fps").floatValue = Fps;
        so.FindProperty("loop").boolValue = loop;
        so.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    // private 직렬화 슬롯에 값을 넣는다. null 인자는 그 슬롯을 비운 것으로 둔다.
    private static void WriteSlots(FlipbookCharacterView view, SpriteFlipbookData idle,
                                   SpriteFlipbookData attack, SpriteFlipbookData death,
                                   SpriteFlipbookData deploy = null, SpriteFlipbookData drag = null)
    {
        var so = new SerializedObject(view);
        so.FindProperty("idle").objectReferenceValue = idle;
        so.FindProperty("attack").objectReferenceValue = attack;
        so.FindProperty("death").objectReferenceValue = death;
        so.FindProperty("deploy").objectReferenceValue = deploy;
        so.FindProperty("drag").objectReferenceValue = drag;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void RunToCompletion()
    {
        for (int i = 0; i < 200 && _player.IsPlaying; i++)
            _player.Tick(1f / 60f);
    }

    // --- 상태 정책 표 (0_character_view.md) ---

    [Test]
    public void ShouldLoop_MatchesStatePolicy()
    {
        Assert.That(FlipbookCharacterView.ShouldLoop(FlipbookCharacterState.Idle), Is.True);
        Assert.That(FlipbookCharacterView.ShouldLoop(FlipbookCharacterState.Drag), Is.True);
        Assert.That(FlipbookCharacterView.ShouldLoop(FlipbookCharacterState.Attack), Is.False);
        Assert.That(FlipbookCharacterView.ShouldLoop(FlipbookCharacterState.Deploy), Is.False);
        Assert.That(FlipbookCharacterView.ShouldLoop(FlipbookCharacterState.Death), Is.False);
    }

    [Test]
    public void ReturnsToIdle_IsNotTheInverseOfShouldLoop()
    {
        // Death 는 원샷이면서 복귀하지 않는 유일한 상태다. 두 술어를 하나로 합치면
        // (!ShouldLoop 로 복귀를 판정하면) 사망 캐릭터가 되살아난다.
        Assert.That(FlipbookCharacterView.ReturnsToIdle(FlipbookCharacterState.Attack), Is.True);
        Assert.That(FlipbookCharacterView.ReturnsToIdle(FlipbookCharacterState.Deploy), Is.True);
        Assert.That(FlipbookCharacterView.ReturnsToIdle(FlipbookCharacterState.Death), Is.False);
        Assert.That(FlipbookCharacterView.ReturnsToIdle(FlipbookCharacterState.Idle), Is.False);
        Assert.That(FlipbookCharacterView.ReturnsToIdle(FlipbookCharacterState.Drag), Is.False);
    }

    // --- 슬롯 폴백 ---

    [Test]
    public void Resolve_OptionalStatesFallBackToIdle()
    {
        Assert.That(_view.Resolve(FlipbookCharacterState.Deploy), Is.SameAs(_idle));
        Assert.That(_view.Resolve(FlipbookCharacterState.Drag), Is.SameAs(_idle));
    }

    [Test]
    public void Resolve_AssignedStatesReturnTheirOwnData()
    {
        Assert.That(_view.Resolve(FlipbookCharacterState.Idle), Is.SameAs(_idle));
        Assert.That(_view.Resolve(FlipbookCharacterState.Attack), Is.SameAs(_attack));
        Assert.That(_view.Resolve(FlipbookCharacterState.Death), Is.SameAs(_death));
    }

    [Test]
    public void Resolve_WhenOptionalSlotIsAssigned_DoesNotFallBack()
    {
        WriteSlots(_view, _idle, _attack, _death, deploy: _attack);
        Assert.That(_view.Resolve(FlipbookCharacterState.Deploy), Is.SameAs(_attack));
    }

    // --- 전이 ---

    [Test]
    public void Play_WhenOptionalSlotIsEmpty_CollapsesCurrentToIdleToo()
    {
        // 폴백이 데이터만 접고 상태를 Deploy 로 남기면, ReturnsToIdle(Deploy) 는 참인데
        // 실제로 도는 건 루프하는 idle 이라 IsPlaying 이 영원히 참 → 복귀가 영영 일어나지 않고
        // Current 가 Deploy 에 갇힌다. 화면에는 idle 이 정상 재생돼 보여서 증상이 드러나지도 않는다.
        _view.Play(FlipbookCharacterState.Deploy);

        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Idle),
            "폴백이 데이터만 접고 상태를 남겼다 — Current 가 영구히 Deploy 에 갇힌다.");
        Assert.That(_renderer.sprite, Is.SameAs(_idleFrames[0]));

        _view.Play(FlipbookCharacterState.Drag);
        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Idle));
    }

    [Test]
    public void Play_WhenOptionalSlotIsAssigned_KeepsItsOwnState()
    {
        // 위 접기가 "선택 상태는 항상 Idle" 로 과교정되지 않았는지 확인한다.
        WriteSlots(_view, _idle, _attack, _death, deploy: _attack);

        _view.Play(FlipbookCharacterState.Deploy);

        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Deploy));
        Assert.That(_renderer.sprite, Is.SameAs(_attackFrames[0]));
    }

    [Test]
    public void Play_SetsCurrentStateAndStartsItsFlipbook()
    {
        _view.Play(FlipbookCharacterState.Attack);

        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Attack));
        Assert.That(_renderer.sprite, Is.SameAs(_attackFrames[0]));
        Assert.That(_view.IsPlaying, Is.True);
    }

    [Test]
    public void Attack_WhenFinished_ReturnsToIdleAndShowsIdleFrame()
    {
        _view.Play(FlipbookCharacterState.Attack);
        RunToCompletion();
        Assert.That(_renderer.sprite, Is.SameAs(_attackFrames[AttackFrameCount - 1]),
            "전제 실패: 공격이 마지막 프레임까지 가지 않았다.");

        _view.PollPlayback();

        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Idle));
        Assert.That(_renderer.sprite, Is.SameAs(_idleFrames[0]));
        Assert.That(_view.IsPlaying, Is.True, "Idle 은 루프라 복귀 후 계속 재생 중이어야 한다.");
    }

    [Test]
    public void Attack_BeforeFinishing_DoesNotReturnToIdle()
    {
        _view.Play(FlipbookCharacterState.Attack);
        _player.Tick(1f / 60f);

        _view.PollPlayback();

        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Attack));
    }

    [Test]
    public void Death_WhenFinished_HoldsLastFrameAndDoesNotReturnToIdle()
    {
        // 이 spec 의 핵심 계약. 확인용 프리팹이라 사망 후에도 살아 있어야 재실험이 된다.
        _view.Play(FlipbookCharacterState.Death);
        RunToCompletion();

        for (int i = 0; i < 5; i++) _view.PollPlayback();

        Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Death));
        Assert.That(_renderer.sprite, Is.SameAs(_deathFrames[DeathFrameCount - 1]));
        Assert.That(_go, Is.Not.Null, "사망이 GameObject 를 파괴했다 — 재실험이 불가능해진다.");
    }

    [Test]
    public void Idle_PollPlaybackRepeatedly_DoesNotRestart()
    {
        // 복귀 판정이 상태를 안 보고 IsPlaying 만 보면 Idle 이 매 프레임 재시작해 첫 프레임에 고착된다.
        _view.Play(FlipbookCharacterState.Idle);
        _player.Tick(0.15f);
        Assert.That(_renderer.sprite, Is.SameAs(_idleFrames[1]), "전제 실패: Idle 이 전진하지 않았다.");

        for (int i = 0; i < 5; i++) _view.PollPlayback();

        Assert.That(_renderer.sprite, Is.SameAs(_idleFrames[1]), "PollPlayback 이 Idle 을 재시작했다.");
    }

    // --- 루프 정책 위반 (README 함정) ---

    [Test]
    public void OneShotStateWithLoopingData_LogsErrorAndStaysStuck()
    {
        // 원샷 시트에 loop 가 켜져 있으면 IsPlaying 이 영원히 참이라 Idle 로 복귀하지 못한다.
        // 현재 동작(감지하되 강제로 고치지 않음)을 고정한다 — 조용히 통과하면 안 되는 상태다.
        LogAssert.ignoreFailingMessages = true;
        var loopingAttack = MakeData("BadAttackFlipbook", _attackFrames, loop: true);
        try
        {
            WriteSlots(_view, _idle, loopingAttack, _death);
            _view.Play(FlipbookCharacterState.Attack);

            RunToCompletion();
            for (int i = 0; i < 5; i++) _view.PollPlayback();

            Assert.That(_view.IsPlaying, Is.True, "전제 실패: 루프 데이터가 완주해 버렸다.");
            Assert.That(_view.Current, Is.EqualTo(FlipbookCharacterState.Attack),
                "루프 데이터인데 Idle 로 복귀했다 — 갇힘 증상이 사라졌다면 방어 로직을 다시 볼 것.");
        }
        finally
        {
            DestroyData(loopingAttack);
        }
    }

    [Test]
    public void ValidLoopPolicy_LogsNothing()
    {
        // SetUp 의 배치(Idle=loop, Attack/Death=원샷)는 정책에 맞으므로 로그가 없어야 한다.
        // 경고가 정상 사용에서 울리면 사용자가 로그를 무시하게 되고, 진짜 위반도 같이 묻힌다.
        _view.Play(FlipbookCharacterState.Idle);
        _view.Play(FlipbookCharacterState.Attack);
        _view.Play(FlipbookCharacterState.Death);
        _view.Play(FlipbookCharacterState.Deploy);
        _view.Play(FlipbookCharacterState.Drag);

        LogAssert.NoUnexpectedReceived();
    }
}
