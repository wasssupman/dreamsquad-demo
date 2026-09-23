using System.IO;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 골든 파일의 자리와 읽기/쓰기.
    //
    // 왜 코어에 있나: 이 골든을 굽는 자리가 둘이다 — Unity 에디터 메뉴(정본 베이커)와
    // 헤드리스 `dotnet test`. 두 벌을 두면 한쪽이 다른 경로에 쓰고, 그러면 「검증은
    // 통과하는데 파일은 옛것」이 된다. `System.IO` 는 BCL 이라 엔진 참조가 아니다.
    //
    // 경로를 상수로 박지 않고 **올라가며 찾는** 이유: 에디터의 작업 디렉터리는 프로젝트
    // 루트지만 `dotnet test` 는 `bin/Debug/netX/` 안에서 돈다. 표식(`Assets/_Project`)을
    // 찾아 올라가면 둘 다 같은 폴더에 닿는다.
    public static class CoreGoldenStore
    {
        public const string RelativeDir = "Assets/_Project/Tests/GoldenCore";

        private const string Marker = "Assets/_Project/Tests";

        private static string _root;

        /// <summary>저장소 루트. 못 찾으면 null(호출자가 «굽지 않는다» 로 실패해야 한다).</summary>
        public static string RepoRoot => _root ?? (_root = FindRoot());

        public static string Dir => RepoRoot == null ? null : Path.Combine(RepoRoot, RelativeDir);

        public static string PathOf(string scenario)
            => Dir == null ? null : Path.Combine(Dir, scenario + ".trace.txt");

        public static bool Exists(string scenario)
        {
            string p = PathOf(scenario);
            return p != null && File.Exists(p);
        }

        public static CoreTrace Read(string scenario)
            => CoreTrace.Deserialize(File.ReadAllText(PathOf(scenario)));

        /// <summary>
        /// 저장. **직렬화 왕복을 통과한 것만** 쓴다(쓰기 → 읽기 → 다시 쓰기가 바이트로
        /// 같아야 한다). 왕복을 나중에 붙이면, 파일에 못 태울 것을 실은 채 코퍼스를 다
        /// 만든 뒤에야 알게 된다 — 옛 포맷이 첫날 이 게이트를 세운 이유다.
        /// </summary>
        public static void Write(string scenario, CoreTrace trace)
        {
            string text = trace.Serialize();
            string again = CoreTrace.Deserialize(text).Serialize();
            if (again != text)
                throw new IOException($"골든 '{scenario}' 왕복 실패 — 재직렬화 바이트가 다르다. 저장하지 않는다.");

            Directory.CreateDirectory(Dir);
            File.WriteAllText(PathOf(scenario), text);
        }

        private static string FindRoot()
        {
            // 작업 디렉터리와 어셈블리 자리 둘 다에서 올라가 본다 — 러너마다 다르다.
            string[] starts = { Directory.GetCurrentDirectory(), System.AppContext.BaseDirectory };
            for (int s = 0; s < starts.Length; s++)
            {
                var dir = starts[s];
                for (int depth = 0; depth < 12 && !string.IsNullOrEmpty(dir); depth++)
                {
                    if (Directory.Exists(Path.Combine(dir, Marker))) return dir;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            return null;
        }
    }
}
