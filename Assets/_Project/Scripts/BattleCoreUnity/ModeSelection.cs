using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 5c — **「이 판을 무엇으로 짓나」가 씬 경계를 넘는 자리.**
    //
    // 모드 선택은 3단이다: **테스트 모드 강제 > 로비/서버 지정 > 기본 모드 SO**. 그 순서를
    // 아는 함수는 `MatchDefinitionBuilder.ResolveMode` 하나이고(unit 4), 이 구조체는 그
    // 함수의 앞 두 칸을 나르는 값이다. 셋째 칸은 드라이버가 자기 저작으로 들고 있다.
    //
    // ⚠ **왜 칸이 둘인가.** 「강제」와 「지정」은 세기가 다르다 — 테스트 하네스가 고른 모드는
    // 로비가 무엇을 골랐든 이겨야 하고(그게 «강제»의 뜻이다), 로비 지정은 저작 기본값만
    // 이긴다. 한 칸으로 접으면 그 서열이 **부르는 쪽 순서**로 옮겨가고, 두 번째 호출처가
    // 생기는 날 둘이 갈린다.
    public readonly struct ModeSelection
    {
        /// <summary>① 테스트 모드 강제. 무엇보다 이긴다 — PlayMode 하네스가 여기 쓴다.</summary>
        public readonly MatchModeData TestMode;

        /// <summary>② 로비/서버 지정. 로비 UI 는 아직 없어 에디터 토글이 그 자리를 대신한다.</summary>
        public readonly MatchModeData Lobby;

        /// <summary>
        /// 재현의 둘째 축(첫째는 modeId). **0 = 지정 없음** — 드라이버의 저작 시드를 쓴다.
        /// 0 을 「시드 0 으로 돌려라」로 읽지 않는 이유: 그 값은 판을 고르는 수가 아니라
        /// 「아무도 안 골랐다」의 표현이고, 실제 0 시드가 필요하면 저작에서 준다.
        /// </summary>
        public readonly int Seed;

        public ModeSelection(MatchModeData testMode, MatchModeData lobby, int seed)
        {
            TestMode = testMode;
            Lobby = lobby;
            Seed = seed;
        }

        /// <summary>아무도 안 골랐다 — 드라이버 저작이 그대로 쓰인다.</summary>
        public static ModeSelection None => default;

        public static ModeSelection ForTest(MatchModeData mode, int seed = 0)
            => new ModeSelection(mode, null, seed);

        public static ModeSelection FromLobby(MatchModeData mode, int seed = 0)
            => new ModeSelection(null, mode, seed);
    }

    // 씬 경계 carry-in. 옛 `TestModeContext` 와 **같은 모양**이다 — 판을 짓는 컴포넌트는
    // 씬이 로드된 뒤에야 존재하므로, 그 전에 정해진 선택은 static 으로 넘길 수밖에 없다.
    //
    // ⚠ **1회 소비**다. 안 지우면 다음 판이 지난 판의 모드를 물려받고, 그 사고는 「두 번째
    // 판만 이상하다」로만 보인다(옛 전투에서 stale attempt 로 같은 일이 났다).
    // 상태는 이 한 칸뿐이고 판정은 없다 — 매니저가 아니다(절대 제약 1).
    //
    // demo-diet unit 0 — 옛 `TestModeContext`(플랜·프리셋)와 `DevMapOverride`(맵 인덱스)를 **이 한 칸으로 접었다.**
    // 선택(`ModeSelection`)과 입력(`MatchEntryInput`)이 같이 실려 같이 소비된다 — 둘이 다른 칸이면 「어느 판이
    // 어느 쪽을 먹었나」가 갈린다.
    public static class MatchEntryContext
    {
        private static ModeSelection _pending;
        private static MatchEntryInput _pendingInput;

        public static bool HasPending { get; private set; }

        // ⚠ **값으로 받는다**(`in` 아님). 참조 둘 + 정수 하나라 복사가 싸고, `in` 은 호출부를
        // C# 7.2 이상으로 묶는다 — 이 자리는 에디터 진단 코드(C# 6 컴파일)도 부른다.
        public static void Set(ModeSelection selection) => Set(selection, null);

        /// <summary>선택 + 입력. `input` null = 드라이버 저작 편성.</summary>
        public static void Set(ModeSelection selection, MatchEntryInput input)
        {
            _pending = selection;
            _pendingInput = input;
            HasPending = true;
        }

        public static void Clear()
        {
            _pending = default;
            _pendingInput = null;
            HasPending = false;
        }

        /// <summary>읽고 **지운다**. 비어 있으면 `ModeSelection.None`(입력은 버린다 — 입력도 필요하면 `out` 판).</summary>
        public static ModeSelection Consume() => Consume(out _);

        /// <summary>선택과 입력을 함께 읽고 **지운다**. 비어 있으면 `ModeSelection.None` · null.</summary>
        public static ModeSelection Consume(out MatchEntryInput input)
        {
            if (!HasPending) { input = null; return ModeSelection.None; }
            var s = _pending;
            input = _pendingInput;
            Clear();
            return s;
        }

#if UNITY_EDITOR
        // 에디터 「Test this plan」 캐리(옛 `TestModeContext.ApplyEditorTestCarry`). `WavePlanTestLauncher` 가 SessionState 에
        // 적은 플랜 GUID 를 씬 Awake/Start 보다 먼저(BeforeSceneLoad) 읽어 입력으로 무장한다. 빌드에선 strip.
        // 이미 걸린 선택(에디터 메뉴의 모드)이 있으면 그 선택은 두고 입력만 더한다.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyEditorPlanCarry()
        {
            const string key = "WavePlanTest.guid";
            string guid = UnityEditor.SessionState.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(guid)) return;
            UnityEditor.SessionState.EraseString(key); // 1회 소비

            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var plan = UnityEditor.AssetDatabase.LoadAssetAtPath<WavePlanAsset>(path);
            if (plan == null) return;
            var selection = HasPending ? _pending : ModeSelection.None;
            Set(selection, new MatchEntryInput { Kind = MatchEntryKind.TestMode, PlanOverride = plan });
            UnityEngine.Debug.Log($"[MatchEntryContext] 에디터 테스트 캐리 적용 — plan='{plan.displayName}'.");
        }
#endif
    }
}
