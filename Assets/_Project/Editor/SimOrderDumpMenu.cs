using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using Wassup.Battle;

namespace Wassup.EditorTools
{
    // battle-sim-extraction unit 0 — BattleSimGroup 러닝 월드의 유효 시스템 총순서 덤프.
    //
    // EditMode 재구성 월드로 대체할 수 없다: 그룹 정렬은 선언 어트리뷰트의 위상 정렬이고,
    // 미선언 지점의 tie-break 이 시스템 등록/생성 순서에 의존한다. 캡처 대상이 바로 그
    // tie-break 결과이므로 실제 부트스트랩 월드(Play)에서 읽어야 한다.
    public static class SimOrderDumpMenu
    {
        private const string OutputPath = "docs/spec/battle-sim-extraction/order-capture.md";
        internal const string DumpOnNextPlayKey = "Wassup.SimOrderDump.DumpOnNextPlay";
        // 그룹 정렬은 첫 OnUpdate 에서 확정된다 — 그 이후를 보장하는 여유 프레임.
        private const int MinFramesBeforeDump = 10;

        [MenuItem("Wassup/Battle/Sim Order/Dump Now (Play 중)", false, 300)]
        private static void DumpNow()
        {
            if (!TryDump(out string error))
                Debug.LogError($"[SimOrderDump] {error}");
        }

        [MenuItem("Wassup/Battle/Sim Order/Dump On Next Play (1회 예약)", false, 301)]
        private static void ArmDumpOnNextPlay()
        {
            EditorPrefs.SetBool(DumpOnNextPlayKey, true);
            Debug.Log("[SimOrderDump] 다음 Play 진입 시 1회 자동 덤프 예약됨. BattleScene 으로 Play 하라.");
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            if (!EditorPrefs.GetBool(DumpOnNextPlayKey, false)) return;
            EditorApplication.update += TryAutoDump;
        }

        private static void TryAutoDump()
        {
            if (!EditorApplication.isPlaying)
            {
                // 덤프 전에 Play 종료 — 예약은 유지해 다음 Play 에서 재시도.
                EditorApplication.update -= TryAutoDump;
                return;
            }
            if (Time.frameCount < MinFramesBeforeDump) return;
            // 그룹 미생성(아웃게임 씬 등)이면 다음 프레임 재시도.
            if (!TryDump(out _)) return;
            EditorApplication.update -= TryAutoDump;
            EditorPrefs.SetBool(DumpOnNextPlayKey, false);
            // batch 실행이면 덤프가 곧 목적 — 에디터 자체를 종료한다.
            if (Application.isBatchMode)
            {
                Debug.Log("[SimOrderDump] batchmode — 덤프 완료, 에디터 종료.");
                EditorApplication.Exit(0);
                return;
            }
            // 부트스트랩이 시작한 Play 면 덤프 완료 후 자동 종료.
            if (SessionState.GetBool(SimOrderCaptureBootstrap.AutoExitPlayKey, false))
            {
                SessionState.SetBool(SimOrderCaptureBootstrap.AutoExitPlayKey, false);
                EditorApplication.ExitPlaymode();
            }
        }

        private static bool TryDump(out string error)
        {
            error = null;
            if (!EditorApplication.isPlaying)
            {
                error = "Play 중에만 덤프할 수 있다.";
                return false;
            }
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                error = "기본 월드가 없다.";
                return false;
            }
            BattleSimGroup group = world.GetExistingSystemManaged<BattleSimGroup>();
            if (group == null)
            {
                error = "BattleSimGroup 미생성 — BattleScene Play 인지 확인.";
                return false;
            }

            var rows = new List<string>();
            using (NativeList<SystemHandle> handles = group.GetAllSystems(Allocator.Temp))
            {
                for (int i = 0; i < handles.Length; i++)
                {
                    // Entities 6.4: handle→Type 직행 public API 가 없다 — 이름으로 역해석.
                    SystemTypeIndex sti = world.Unmanaged.GetSystemTypeIndex(handles[i]);
                    string fullName = TypeManager.GetSystemName(sti).ToString();
                    Type type = ResolveType(fullName);
                    rows.Add(FormatRow(i + 1, fullName, type));
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# BattleSimGroup 유효 시스템 총순서 캡처 (battle-sim-extraction unit 0)");
            sb.AppendLine();
            sb.AppendLine($"- 캡처: {DateTime.Now:yyyy-MM-dd HH:mm} · Unity {Application.unityVersion} · HEAD `{ReadGitHeadShort()}`");
            sb.AppendLine($"- 시스템 수: **{rows.Count}** (기대 44 — 다르면 시스템 증감 후 재캡처된 것)");
            sb.AppendLine("- 이 표가 틱 페이즈 순서의 정본이다. \"어트리뷰트\" 열은 각 시스템의 **선언** — 비어 있으면 이 자리의 순서는 정렬 tie-break 산물이다.");
            sb.AppendLine("- 신규 핀 여부는 핀 커밋에서 이 문서에 수기로 표기한다.");
            sb.AppendLine();
            sb.AppendLine("| # | 시스템 | 선언 어트리뷰트 (Before/After) |");
            sb.AppendLine("|---|---|---|");
            foreach (string row in rows) sb.AppendLine(row);

            string root = Directory.GetParent(Application.dataPath).FullName;
            string outPath = Path.Combine(root, OutputPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[SimOrderDump] {rows.Count}개 시스템 순서 덤프 완료 → {OutputPath}");
            return true;
        }

        private static Type ResolveType(string fullName)
        {
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        private static string FormatRow(int index, string fullName, Type type)
        {
            int lastDot = fullName.LastIndexOf('.');
            string shortName = lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
            if (type == null)
                return $"| {index} | `{shortName}` | (Type 역해석 실패 — 어트리뷰트 미상) |";

            var attrs = new List<string>();
            foreach (UpdateBeforeAttribute a in type.GetCustomAttributes(typeof(UpdateBeforeAttribute), false)
                         .Cast<UpdateBeforeAttribute>())
                attrs.Add($"Before({a.SystemType.Name})");
            foreach (UpdateAfterAttribute a in type.GetCustomAttributes(typeof(UpdateAfterAttribute), false)
                         .Cast<UpdateAfterAttribute>())
                attrs.Add($"After({a.SystemType.Name})");
            string attrText = attrs.Count == 0 ? "" : string.Join(" · ", attrs);
            return $"| {index} | `{shortName}` | {attrText} |";
        }

        private static string ReadGitHeadShort()
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                string headPath = Path.Combine(root, ".git", "HEAD");
                if (!File.Exists(headPath)) return "unknown";
                string head = File.ReadAllText(headPath).Trim();
                if (!head.StartsWith("ref:", StringComparison.Ordinal))
                    return head.Substring(0, Math.Min(8, head.Length));
                string refRel = head.Substring(4).Trim();
                string refPath = Path.Combine(root, ".git", refRel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(refPath)) return refRel;
                string hash = File.ReadAllText(refPath).Trim();
                return hash.Substring(0, Math.Min(8, hash.Length));
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
