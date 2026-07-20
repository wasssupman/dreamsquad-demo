using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wassup.Core;

namespace Wassup.EditorTools
{
    public static class FirstSessionTutorialMenu
    {
        private const string MenuPath = "Wassup/Tutorial/Reset First Session Tutorial";

        [MenuItem(MenuPath, false, 200)]
        private static void ResetTutorial()
        {
            string path = ProfileStore.Path;
            if (!File.Exists(path))
            {
                ShowAlreadyReset("프로필 파일이 없습니다. 다음 로비 진입은 신규 튜토리얼 대상입니다.");
                return;
            }

            try
            {
                bool changed = ProfileStore.ResetTutorialProgressAt(
                    path, loadedProfile: null, out string backupPath);
                if (!changed)
                {
                    ShowAlreadyReset("두 튜토리얼이 이미 초기화되어 있습니다.");
                    return;
                }

                Debug.Log($"[FirstSessionTutorial] 튜토리얼 진행 상태 초기화 완료. profile={path}, backup={backupPath}");
                EditorUtility.DisplayDialog(
                    "초기화 완료",
                    "OutgameScene에서 Play 후 게임에 진입하면 튜토리얼을 다시 볼 수 있습니다.\n\n" +
                    "BattleScene 직접 Play에서는 안전장치 때문에 표시되지 않습니다.",
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "초기화 실패",
                    $"프로필을 변경하지 못했습니다.\n{exception.Message}",
                    "확인");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanResetTutorial() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static void ShowAlreadyReset(string message)
        {
            Debug.Log($"[FirstSessionTutorial] {message} profile={ProfileStore.Path}");
            EditorUtility.DisplayDialog(
                "이미 초기화됨",
                message + "\n\nOutgameScene에서 Play 후 게임에 진입하세요.",
                "확인");
        }
    }
}
