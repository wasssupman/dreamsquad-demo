#!/usr/bin/env bash

set -euo pipefail

readonly TEST_SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd -P)"
readonly BUILD_SCRIPT="$TEST_SCRIPT_DIR/../build.sh"
readonly TEST_TEMP_BASE="$(cd "${TMPDIR:-/tmp}" && pwd -P)"
TEST_TEMP_DIR=""

cleanup_test() {
  if [ -n "$TEST_TEMP_DIR" ]; then
    case "$TEST_TEMP_DIR" in
      "$TEST_TEMP_BASE"/dreamsquad-mobile-shell-test.*)
        rm -rf -- "$TEST_TEMP_DIR"
        ;;
    esac
  fi
}

# shellcheck source=../build.sh
source "$BUILD_SCRIPT"
trap cleanup_test EXIT

TEST_TEMP_DIR="$(mktemp -d "$TEST_TEMP_BASE/dreamsquad-mobile-shell-test.XXXXXX")"
readonly FAKE_EDITOR_ROOT="$TEST_TEMP_DIR/6000.4.3f1"
readonly FAKE_UNITY="$FAKE_EDITOR_ROOT/Unity.app/Contents/MacOS/Unity"
readonly HUB_ANDROID_MODULE="$FAKE_EDITOR_ROOT/PlaybackEngines/AndroidPlayer"
readonly CONTENTS_IOS_MODULE="$FAKE_EDITOR_ROOT/Unity.app/Contents/PlaybackEngines/iOSSupport"
readonly FAKE_AAPT2="$HUB_ANDROID_MODULE/SDK/build-tools/35.0.0/aapt2"
readonly FAKE_KEYTOOL="$HUB_ANDROID_MODULE/OpenJDK/bin/keytool"

mkdir -p "$(dirname "$FAKE_UNITY")"
mkdir -p "$HUB_ANDROID_MODULE"
mkdir -p "$CONTENTS_IOS_MODULE"
mkdir -p "$(dirname "$FAKE_AAPT2")"
mkdir -p "$(dirname "$FAKE_KEYTOOL")"
touch "$FAKE_UNITY"
touch "$FAKE_AAPT2"
touch "$FAKE_KEYTOOL"
chmod +x "$FAKE_UNITY" "$FAKE_AAPT2" "$FAKE_KEYTOOL"

UNITY_EDITOR="$FAKE_UNITY"

actual_android_module="$(find_unity_playback_engine AndroidPlayer)"
[ "$actual_android_module" = "$HUB_ANDROID_MODULE" ] ||
  fail "Unity Hub sibling Android module discovery regressed."
UNITY_ANDROID_MODULE="$actual_android_module"

actual_ios_module="$(find_unity_playback_engine iOSSupport)"
[ "$actual_ios_module" = "$CONTENTS_IOS_MODULE" ] ||
  fail "Unity Contents iOS module discovery regressed."

ANDROID_SDK_ROOT=""
ANDROID_HOME=""
[ "$(find_android_tool aapt2 "")" = "$FAKE_AAPT2" ] ||
  fail "Android SDK tool discovery did not reuse the resolved module."
[ "$(find_keytool)" = "$FAKE_KEYTOOL" ] ||
  fail "Bundled keytool discovery did not reuse the resolved module."

if find_unity_playback_engine MissingSupport >/dev/null; then
  fail "Missing Unity platform support must not resolve successfully."
fi

(
  TARGET=""
  BUILD_VERSION=""
  BUILD_NUMBER=""
  BUILD_ATTEMPT=""
  KEYSTORE_ARGUMENT=""
  parse_arguments both --version 0.1.0 --build 1 --attempt 2
  [ "$TARGET" = "both" ] ||
    fail "Attempt parsing changed the requested target."
  [ "$BUILD_ATTEMPT" = "2" ] ||
    fail "A valid build attempt was not parsed."
)

for invalid_attempt in 0 -1 retry 2147483648; do
  if (
    TARGET=""
    BUILD_VERSION=""
    BUILD_NUMBER=""
    BUILD_ATTEMPT=""
    KEYSTORE_ARGUMENT=""
    parse_arguments android \
      --version 0.1.0 \
      --build 1 \
      --attempt "$invalid_attempt"
  ) >/dev/null 2>&1; then
    fail "An invalid build attempt was accepted."
  fi
done

if (
  TARGET=""
  BUILD_VERSION=""
  BUILD_NUMBER=""
  BUILD_ATTEMPT=""
  KEYSTORE_ARGUMENT=""
  parse_arguments ios \
    --version 0.1.0 \
    --build 1 \
    --attempt 1 \
    --attempt 2
) >/dev/null 2>&1; then
  fail "A duplicate build attempt was accepted."
fi

readonly STEM_REPO="$TEST_TEMP_DIR/stem-repo"
mkdir -p "$STEM_REPO"
git -C "$STEM_REPO" init -q
printf 'stem fixture\n' > "$STEM_REPO/tracked.txt"
git -C "$STEM_REPO" add tracked.txt
git -C "$STEM_REPO" \
  -c user.name=DreamSquad \
  -c user.email=dreamsquad@example.invalid \
  commit -qm baseline
(
  PROJECT_ROOT="$STEM_REPO"
  BUILD_VERSION="0.1.0"
  BUILD_NUMBER="1"
  BUILD_ATTEMPT="3"
  UNITY_EDITOR_PATH="$FAKE_UNITY"
  date() {
    printf '20260727-153045\n'
  }
  configure_paths
  [ "$STEM" = "DreamSquad-Demo-0.1.0-1-${COMMIT_SHA}-attempt3" ] ||
    fail "The explicit build attempt was not added to the stem."
  [ "$OUT_STEM" = \
    "dreamquad-demo--0.1.0-1-20260727-153045-${COMMIT_SHA}-attempt3" ] ||
    fail "The timestamped outs artifact stem is incorrect."
  [ "$ANDROID_OUT_APK" = "$PROJECT_ROOT/Builds/outs/$OUT_STEM.apk" ] ||
    fail "The timestamped APK outs path is incorrect."
  [ "$IOS_OUT_IPA" = "$PROJECT_ROOT/Builds/outs/$OUT_STEM.ipa" ] ||
    fail "The timestamped IPA outs path is incorrect."
  mkdir -p "$ANDROID_OUTPUT_DIR"
  if (assert_output_available "$ANDROID_OUTPUT_DIR" Android) >/dev/null 2>&1; then
    fail "An attempted stem collision was accepted."
  fi
)
(
  PROJECT_ROOT="$STEM_REPO"
  BUILD_VERSION="0.1.0"
  BUILD_NUMBER="1"
  BUILD_ATTEMPT=""
  UNITY_EDITOR_PATH="$FAKE_UNITY"
  configure_paths
  [ "$STEM" = "DreamSquad-Demo-0.1.0-1-${COMMIT_SHA}" ] ||
    fail "The default stem changed when --attempt was omitted."
)

(
  PROJECT_ROOT="$STEM_REPO"
  TARGET="both"
  BUILD_VERSION="0.1.0"
  BUILD_NUMBER="2"
  BUILD_ATTEMPT=""
  UNITY_EDITOR_PATH="$FAKE_UNITY"
  date() {
    printf '20260727-160102\n'
  }
  configure_paths
  assert_output_root_safe
  mkdir -p "$ANDROID_OUTPUT_DIR" "$IOS_OUTPUT_DIR"
  printf 'verified apk\n' > "$ANDROID_APK"
  printf 'verified ipa\n' > "$IOS_IPA"
  publish_verified_artifacts
  cmp -s "$ANDROID_APK" "$ANDROID_OUT_APK" ||
    fail "The verified APK was not copied to Builds/outs."
  cmp -s "$IOS_IPA" "$IOS_OUT_IPA" ||
    fail "The verified IPA was not copied to Builds/outs."
  if (publish_verified_artifacts) >/dev/null 2>&1; then
    fail "An existing timestamped outs artifact was overwritten."
  fi
)

licensing_fixture="$(
  printf '%s\n' \
    '101 1 501 /Applications/Unity Hub.app/Contents/Unity.Licensing.Client' \
    '102 1 501 Unity.Licensing.Client' \
    '103 77 501 /Applications/Unity.Licensing.Client' \
    '104 1 502 /Applications/Unity.Licensing.Client' \
    '105 1 501 /Applications/Unity.Licensing.Client.Helper' \
    '106 1 501 Unity.Licensing.Clien' \
    'malformed process row'
)"
detected_pids="$(
  printf '%s\n' "$licensing_fixture" |
    orphan_unity_licensing_pids 501
)"
[ "$detected_pids" = $'101\n102' ] ||
  fail "Orphan Unity licensing process classification regressed."

current_uid="$(/usr/bin/id -u)"
licensing_error="$TEST_TEMP_DIR/licensing-error.log"
if (
  snapshot_unity_processes() {
    printf '901 1 %s /Applications/Secret User/Unity.Licensing.Client\n' "$current_uid"
  }
  assert_no_orphan_unity_licensing_client
) >"$licensing_error" 2>&1; then
  fail "An orphan Unity licensing client did not block preflight."
fi
grep -F 'PID: 901' "$licensing_error" >/dev/null ||
  fail "The licensing failure did not report the numeric PID."
if grep -F 'Secret User' "$licensing_error" >/dev/null; then
  fail "The licensing failure exposed process command data."
fi
if (
  snapshot_unity_processes() {
    return 1
  }
  assert_no_orphan_unity_licensing_client
) >/dev/null 2>&1; then
  fail "A failed process snapshot was treated as a clean licensing preflight."
fi

(
  plist_value() {
    case "$2" in
      UISupportedInterfaceOrientations:0)
        printf 'UIInterfaceOrientationLandscapeLeft\n'
        ;;
      UISupportedInterfaceOrientations:1)
        printf 'UIInterfaceOrientationLandscapeRight\n'
        ;;
      *)
        return 1
        ;;
    esac
  }
  verify_landscape_orientation_array fixture.plist UISupportedInterfaceOrientations
)

if (
  plist_value() {
    case "$2" in
      UISupportedInterfaceOrientations:0)
        printf 'UIInterfaceOrientationPortrait\n'
        ;;
      UISupportedInterfaceOrientations:1)
        printf 'UIInterfaceOrientationLandscapeRight\n'
        ;;
      *)
        return 1
        ;;
    esac
  }
  verify_landscape_orientation_array fixture.plist UISupportedInterfaceOrientations
) >/dev/null 2>&1; then
  fail "A portrait-capable iOS orientation plist passed verification."
fi

(
  UNITY_LAUNCH_ACTIVE=1
  DEFERRED_SIGNAL_EXIT=0
  handle_build_signal 130
  [ "$DEFERRED_SIGNAL_EXIT" -eq 130 ] ||
    fail "A signal received during Unity was not deferred for cleanup."
)
set +e
(
  UNITY_LAUNCH_ACTIVE=1
  DEFERRED_SIGNAL_EXIT=130
  finish_unity_launch
  [ "$UNITY_LAUNCH_ACTIVE" -eq 0 ] ||
    exit 1
  exit_if_unity_signal_deferred
)
deferred_signal_status=$?
set -e
[ "$deferred_signal_status" -eq 130 ] ||
  fail "A deferred Unity signal did not preserve its exit status."

saved_project_root="${PROJECT_ROOT:-}"
PROJECT_ROOT="$TEST_TEMP_DIR/not-a-git-worktree"
if is_worktree_clean >/dev/null 2>&1; then
  fail "A failed Git status was treated as a clean worktree."
fi
PROJECT_ROOT="$saved_project_root"

readonly SERIALIZATION_REPO="$TEST_TEMP_DIR/serialization-repo"
readonly SERIALIZATION_TEMP="$TEST_TEMP_DIR/serialization-runtime"
mkdir -p \
  "$SERIALIZATION_REPO/ProjectSettings" \
  "$SERIALIZATION_REPO/Assets/Settings" \
  "$SERIALIZATION_REPO/Library" \
  "$SERIALIZATION_TEMP"
printf '%s\n' \
  'PlayerSettings:' \
  '  AndroidKeystoreName: ' \
  '  m_BuildTargetBatching:' \
  '  - m_BuildTarget: Android' \
  '    m_StaticBatching: 1' \
  '    m_DynamicBatching: 0' \
  '  m_BuildTargetShaderSettings: []' \
  > "$SERIALIZATION_REPO/ProjectSettings/ProjectSettings.asset"
printf '%s\n' \
  'MonoBehaviour:' \
  '  m_PrefilterReflectionProbeAtlas: 1' \
  '  m_PrefilterPointSamplingUpsampling: 0' \
  '  m_ShaderVariantLogLevel: 0' \
  > "$SERIALIZATION_REPO/Assets/Settings/Mobile_RPAsset.asset"
for spine_relative in "${SPINE_MATERIAL_SERIALIZATION_RELATIVES[@]}"; do
  mkdir -p "$(dirname "$SERIALIZATION_REPO/$spine_relative")"
  printf '%s\n' \
    'Material:' \
    '  m_EditorClassIdentifier: ' \
    '  m_LockedProperties:' \
    > "$SERIALIZATION_REPO/$spine_relative"
done
for spine_relative in "${SPINE_ATLAS_SERIALIZATION_RELATIVES[@]}"; do
  mkdir -p "$(dirname "$SERIALIZATION_REPO/$spine_relative")"
  printf '%s\n' \
    'MonoBehaviour:' \
    '  m_EditorClassIdentifier: ' \
    '  atlasFile: {fileID: 4900000}' \
    > "$SERIALIZATION_REPO/$spine_relative"
done
printf 'other baseline\n' > "$SERIALIZATION_REPO/Other.asset"
printf 'Library/\n' > "$SERIALIZATION_REPO/.gitignore"
git -C "$SERIALIZATION_REPO" init -q
git -C "$SERIALIZATION_REPO" add \
  .gitignore \
  Assets/Settings/Mobile_RPAsset.asset \
  Other.asset \
  ProjectSettings/ProjectSettings.asset \
  "${SPINE_MATERIAL_SERIALIZATION_RELATIVES[@]}" \
  "${SPINE_ATLAS_SERIALIZATION_RELATIVES[@]}"
git -C "$SERIALIZATION_REPO" \
  -c user.name=DreamSquad \
  -c user.email=dreamsquad@example.invalid \
  commit -qm baseline

PROJECT_ROOT="$SERIALIZATION_REPO"
BUILD_LOCK_DIR=""
BUILD_LOCK_HELD=0
acquire_build_lock
if (
  BUILD_LOCK_DIR=""
  BUILD_LOCK_HELD=0
  acquire_build_lock
) >/dev/null 2>&1; then
  fail "A concurrent mobile build acquired the project lock."
fi

TEMP_DIR="$SERIALIZATION_TEMP"
BUILD_HEAD_FULL="$(git -C "$PROJECT_ROOT" rev-parse HEAD)"
SERIALIZATION_BASELINE_DIR=""
SERIALIZATION_BASELINE_CAPTURED=0
SERIALIZATION_RESTORE_ARMED=0
capture_serialization_baseline

project_file="$SERIALIZATION_REPO/ProjectSettings/ProjectSettings.asset"
mobile_rp_file="$SERIALIZATION_REPO/Assets/Settings/Mobile_RPAsset.asset"
other_file="$SERIALIZATION_REPO/Other.asset"
project_baseline="$SERIALIZATION_BASELINE_DIR/ProjectSettings.asset"
mobile_rp_baseline="$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.asset"

awk '
  $0 == "    m_DynamicBatching: 0" {
    next
  }
  { print }
' "$project_baseline" > "$TEST_TEMP_DIR/malformed-batching.asset"
if generate_ios_batching_serialization_variant \
  "$TEST_TEMP_DIR/malformed-batching.asset" \
  "$TEST_TEMP_DIR/invalid-batching-variant.asset"; then
  fail "An incomplete Android batching entry produced an iPhone variant."
fi

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
  "$project_file"
unarmed_project_hash="$(shasum -a 256 "$project_file" | awk '{print $1}')"
if reconcile_known_unity_serialization; then
  fail "An allowed serialization change was restored without a Unity launch."
fi
[ "$(shasum -a 256 "$project_file" | awk '{print $1}')" = "$unarmed_project_hash" ] ||
  fail "An unarmed serialization change was overwritten."
/bin/cp "$project_baseline" "$project_file"
SERIALIZATION_RESTORE_ARMED=1

for project_variant in \
  ProjectSettings.keystore.asset \
  ProjectSettings.batching.asset \
  ProjectSettings.both.asset; do
  /bin/cp \
    "$SERIALIZATION_BASELINE_DIR/$project_variant" \
    "$project_file"
  reconcile_known_unity_serialization ||
    fail "An allowed ProjectSettings serialization group was rejected."
  cmp -s "$project_file" "$project_baseline" ||
    fail "ProjectSettings was not restored byte-for-byte."
done

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.migrated.asset" \
  "$mobile_rp_file"
reconcile_known_unity_serialization ||
  fail "The allowed mobile RP serialization group was rejected."
cmp -s "$mobile_rp_file" "$mobile_rp_baseline" ||
  fail "Mobile_RPAsset was not restored byte-for-byte."

for spine_index in "${!SPINE_MATERIAL_SERIALIZATION_RELATIVES[@]}"; do
  /bin/cp \
    "${SPINE_MATERIAL_VARIANTS[spine_index]}" \
    "$SERIALIZATION_REPO/${SPINE_MATERIAL_SERIALIZATION_RELATIVES[spine_index]}"
done
for spine_index in "${!SPINE_ATLAS_SERIALIZATION_RELATIVES[@]}"; do
  /bin/cp \
    "${SPINE_ATLAS_VARIANTS[spine_index]}" \
    "$SERIALIZATION_REPO/${SPINE_ATLAS_SERIALIZATION_RELATIVES[spine_index]}"
done
for spine_relative in "${SPINE_GENERATED_SERIALIZATION_RELATIVES[@]}"; do
  printf 'generated by Unity fixture\n' > "$SERIALIZATION_REPO/$spine_relative"
done
reconcile_known_unity_serialization ||
  fail "The complete Unity 6.4 Spine serialization group was rejected."
is_worktree_clean ||
  fail "The complete Unity 6.4 Spine serialization group was not restored cleanly."

/bin/cp \
  "${SPINE_MATERIAL_VARIANTS[0]}" \
  "$SERIALIZATION_REPO/${SPINE_MATERIAL_SERIALIZATION_RELATIVES[0]}"
printf 'generated by Unity fixture\n' > \
  "$SERIALIZATION_REPO/${SPINE_GENERATED_SERIALIZATION_RELATIVES[0]}"
if reconcile_known_unity_serialization; then
  fail "A partial Spine serialization group was accepted."
fi
[ -f "$SERIALIZATION_REPO/${SPINE_GENERATED_SERIALIZATION_RELATIVES[0]}" ] ||
  fail "A rejected partial Spine group removed an untracked file."
/bin/cp \
  "${SPINE_MATERIAL_BASELINES[0]}" \
  "$SERIALIZATION_REPO/${SPINE_MATERIAL_SERIALIZATION_RELATIVES[0]}"
rm -f -- "$SERIALIZATION_REPO/${SPINE_GENERATED_SERIALIZATION_RELATIVES[0]}"

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.both.asset" \
  "$project_file"
/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.migrated.asset" \
  "$mobile_rp_file"
reconcile_known_unity_serialization ||
  fail "A combined allowed serialization group was rejected."
is_worktree_clean ||
  fail "Allowed serialization reconciliation did not restore a clean worktree."

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.batching.asset" \
  "$project_file"
awk '
  $0 == "  - m_BuildTarget: iPhone" {
    in_iphone = 1
  }
  in_iphone && $0 == "    m_DynamicBatching: 0" {
    next
  }
  { print }
' "$project_file" > "$TEST_TEMP_DIR/partial-batching.asset"
/bin/cp "$TEST_TEMP_DIR/partial-batching.asset" "$project_file"
partial_hash="$(shasum -a 256 "$project_file" | awk '{print $1}')"
if reconcile_known_unity_serialization; then
  fail "An incomplete iPhone batching group was restored."
fi
[ "$(shasum -a 256 "$project_file" | awk '{print $1}')" = "$partial_hash" ] ||
  fail "A rejected partial batching change was overwritten."
/bin/cp "$project_baseline" "$project_file"

symlink_target="$TEST_TEMP_DIR/symlink-target.asset"
printf 'symlink target\n' > "$symlink_target"
symlink_target_hash="$(shasum -a 256 "$symlink_target" | awk '{print $1}')"
/bin/mv "$project_file" "$TEST_TEMP_DIR/project-file.backup"
ln -s "$symlink_target" "$project_file"
if reconcile_known_unity_serialization; then
  fail "A symlinked tracked settings path was restored."
fi
[ "$(shasum -a 256 "$symlink_target" | awk '{print $1}')" = "$symlink_target_hash" ] ||
  fail "A symlink target was overwritten during reconciliation."
rm -f -- "$project_file"
/bin/mv "$TEST_TEMP_DIR/project-file.backup" "$project_file"

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
  "$project_file"
printf '  userChange: 1\n' >> "$project_file"
unexpected_hash="$(shasum -a 256 "$project_file" | awk '{print $1}')"
if reconcile_known_unity_serialization; then
  fail "An allowed group with an extra same-file change was restored."
fi
[ "$(shasum -a 256 "$project_file" | awk '{print $1}')" = "$unexpected_hash" ] ||
  fail "An unexpected same-file change was overwritten."
/bin/cp "$project_baseline" "$project_file"

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
  "$project_file"
printf 'other user change\n' >> "$other_file"
if reconcile_known_unity_serialization; then
  fail "An unrelated tracked change was hidden by serialization reconciliation."
fi
cmp -s \
  "$project_file" \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" ||
  fail "An allowed change was partially restored beside an unrelated change."
/bin/cp "$project_baseline" "$project_file"
printf 'other baseline\n' > "$other_file"

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
  "$project_file"
printf 'untracked user change\n' > "$SERIALIZATION_REPO/Untracked.asset"
if reconcile_known_unity_serialization; then
  fail "An untracked change was hidden by serialization reconciliation."
fi
cmp -s \
  "$project_file" \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" ||
  fail "An allowed change was partially restored beside an untracked change."
/bin/cp "$project_baseline" "$project_file"
rm -f -- "$SERIALIZATION_REPO/Untracked.asset"

(
  sleep_calls=0
  sleep() {
    sleep_calls=$((sleep_calls + 1))
    if [ "$sleep_calls" -eq 1 ]; then
      /bin/cp \
        "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
        "$project_file"
    fi
  }
  settle_known_unity_serialization
)
is_worktree_clean ||
  fail "A delayed allowed serialization write was not reconciled."

if (
  sleep_calls=0
  sleep() {
    sleep_calls=$((sleep_calls + 1))
    if [ "$sleep_calls" -eq 1 ]; then
      printf '  delayedUserChange: 1\n' >> "$project_file"
    fi
  }
  settle_known_unity_serialization
) >/dev/null 2>&1; then
  fail "A delayed unexpected serialization write was accepted."
fi
grep -F 'delayedUserChange' "$project_file" >/dev/null ||
  fail "A delayed unexpected change was overwritten."
/bin/cp "$project_baseline" "$project_file"

/bin/cp \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
  "$project_file"
printf 'new committed head\n' > "$other_file"
git -C "$SERIALIZATION_REPO" add Other.asset
git -C "$SERIALIZATION_REPO" \
  -c user.name=DreamSquad \
  -c user.email=dreamsquad@example.invalid \
  commit -qm 'move head'
if reconcile_known_unity_serialization; then
  fail "A changed Git HEAD was reconciled against a stale baseline."
fi
cmp -s \
  "$project_file" \
  "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" ||
  fail "A changed HEAD caused a stale baseline restore."

release_build_lock ||
  fail "The project build lock could not be released."
[ ! -e "$SERIALIZATION_REPO/$BUILD_LOCK_RELATIVE" ] ||
  fail "The released project build lock was left behind."

printf 'build_sh_test=pass\n'
