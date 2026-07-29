#!/usr/bin/env bash

set -euo pipefail
export LC_ALL=C

readonly APP_IDENTIFIER="com.playlinks.somnia.dev"
readonly ANDROID_ALIAS="somnia-dev"
readonly IOS_TEAM_ID="69DK98XF77"
readonly IOS_SIGNING_IDENTITY="Apple Distribution"
readonly IOS_PROFILE_NAME="somnia_dev_adhoc"
readonly DEFAULT_UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity"
readonly PROJECT_SETTINGS_RELATIVE="ProjectSettings/ProjectSettings.asset"
readonly MOBILE_RP_ASSET_RELATIVE="Assets/Settings/Mobile_RPAsset.asset"
readonly BUILD_LOCK_RELATIVE="Library/DreamSquadMobileBuild.lock"

TARGET=""
BUILD_VERSION=""
BUILD_NUMBER=""
BUILD_ATTEMPT=""
KEYSTORE_ARGUMENT=""
UNITY_EDITOR=""
UNITY_ANDROID_MODULE=""
PROJECT_ROOT=""
MOBILE_OUTPUT_ROOT=""
OUTS_OUTPUT_ROOT=""
STEM=""
STEM_ROOT=""
BUILD_TIMESTAMP=""
OUT_STEM=""
ANDROID_OUTPUT_DIR=""
ANDROID_APK=""
ANDROID_OUT_APK=""
IOS_OUTPUT_DIR=""
IOS_XCODE_DIR=""
IOS_ARCHIVE=""
IOS_EXPORT_DIR=""
IOS_IPA=""
IOS_OUT_IPA=""
TEMP_BASE="${TMPDIR:-/tmp}"
TEMP_DIR=""
RAW_UNITY_LOG=""
FINAL_CHECK_ARMED=0
ANDROID_STORE_PASSWORD=""
ANDROID_KEY_PASSWORD=""
KEYSTORE_PATH=""
INSTALLED_PROFILE_PATH=""
INSTALLED_PROFILE_UUID=""
VALIDATED_PROFILE_UUID=""
VALIDATED_PROFILE_EXPIRY=""
VALIDATED_PROFILE_DEVICE_COUNT=""
AAPT2=""
APKSIGNER=""
KEYTOOL=""
COMMIT_SHA=""
BUILD_HEAD_FULL=""
SERIALIZATION_BASELINE_DIR=""
SERIALIZATION_BASELINE_CAPTURED=0
SERIALIZATION_RESTORE_ARMED=0
UNITY_LAUNCH_ACTIVE=0
DEFERRED_SIGNAL_EXIT=0
BUILD_LOCK_DIR=""
BUILD_LOCK_HELD=0

usage() {
  cat <<'EOF'
Usage:
  ./scripts/mobile/build.sh android --version <version> --build <number> [--attempt <number>] [--keystore <path>]
  ./scripts/mobile/build.sh ios     --version <version> --build <number> [--attempt <number>]
  ./scripts/mobile/build.sh both    --version <version> --build <number> [--attempt <number>] [--keystore <path>]

Arguments:
  --version   Numeric version in major.minor or major.minor.patch form.
  --build     Positive build number used for Android versionCode and iOS CFBundleVersion.
  --attempt   Positive retry number. Adds -attemptN to a new output stem.
  --keystore  Android keystore. Defaults to:
              ~/Library/Application Support/Playlinks/Signing/Android/somnia-dev.keystore

Environment:
  UNITY_EDITOR_PATH  Overrides the Unity editor executable.
EOF
}

info() {
  printf '[DreamSquad Build] %s\n' "$*"
}

fail() {
  printf '[DreamSquad Build] ERROR: %s\n' "$*" >&2
  exit 1
}

is_worktree_clean() {
  local status

  status="$(git -C "$PROJECT_ROOT" status --porcelain --untracked-files=all)" ||
    return 1
  [ -z "$status" ]
}

cleanup() {
  local exit_code=$?
  trap - EXIT

  ANDROID_STORE_PASSWORD=""
  ANDROID_KEY_PASSWORD=""
  unset ANDROID_STORE_PASSWORD ANDROID_KEY_PASSWORD
  unset DREAMSQUAD_BUILD_VERSION DREAMSQUAD_BUILD_NUMBER DREAMSQUAD_BUILD_OUTPUT
  unset DREAMSQUAD_ANDROID_KEYSTORE DREAMSQUAD_ANDROID_KEYSTORE_PASSWORD
  unset DREAMSQUAD_ANDROID_KEY_PASSWORD DREAMSQUAD_KEYTOOL_STORE_PASSWORD

  if [ "$SERIALIZATION_BASELINE_CAPTURED" -eq 1 ] &&
    [ "$UNITY_LAUNCH_ACTIVE" -eq 0 ] &&
    [ -d "$SERIALIZATION_BASELINE_DIR" ]; then
    if ! reconcile_known_unity_serialization; then
      printf '%s\n' \
        '[DreamSquad Build] ERROR: Could not safely reconcile Unity serialization changes.' >&2
      exit_code=1
    fi
  fi

  if [ -n "$TEMP_DIR" ]; then
    case "$TEMP_DIR" in
      "$TEMP_BASE"/dreamsquad-mobile.*)
        if [ -d "$TEMP_DIR" ]; then
          rm -rf -- "$TEMP_DIR"
        fi
        ;;
    esac
  fi

  if [ "$FINAL_CHECK_ARMED" -eq 1 ] && ! is_worktree_clean; then
    printf '%s\n' \
      '[DreamSquad Build] ERROR: The build changed the Git worktree. Inspect and restore it before another build.' >&2
    exit_code=1
  fi

  if ! release_build_lock; then
    printf '%s\n' \
      '[DreamSquad Build] ERROR: Could not release the mobile build lock.' >&2
    exit_code=1
  fi

  exit "$exit_code"
}

handle_build_signal() {
  local signal_exit="$1"

  if [ "$UNITY_LAUNCH_ACTIVE" -eq 1 ]; then
    DEFERRED_SIGNAL_EXIT="$signal_exit"
    return
  fi

  exit "$signal_exit"
}

finish_unity_launch() {
  UNITY_LAUNCH_ACTIVE=0
}

exit_if_unity_signal_deferred() {
  local signal_exit

  if [ "$DEFERRED_SIGNAL_EXIT" -ne 0 ]; then
    signal_exit="$DEFERRED_SIGNAL_EXIT"
    DEFERRED_SIGNAL_EXIT=0
    exit "$signal_exit"
  fi
}

trap cleanup EXIT
trap 'handle_build_signal 129' HUP
trap 'handle_build_signal 130' INT
trap 'handle_build_signal 143' TERM

require_command() {
  command -v "$1" >/dev/null 2>&1 ||
    fail "Required command is unavailable: $1"
}

orphan_unity_licensing_pids() {
  local expected_uid="$1"

  awk -v expected_uid="$expected_uid" '
    NF >= 4 && $1 ~ /^[0-9]+$/ && $2 == 1 && $3 == expected_uid {
      executable = $4
      for (field_index = 5; field_index <= NF; field_index++) {
        executable = executable " " $field_index
      }
      sub(/^.*\//, "", executable)
      if (executable == "Unity.Licensing.Client") {
        print $1
      }
    }
  '
}

snapshot_unity_processes() {
  /bin/ps -ww -axo pid=,ppid=,uid=,comm=
}

assert_no_orphan_unity_licensing_client() {
  local current_uid
  local process_snapshot
  local orphan_pids

  current_uid="$(/usr/bin/id -u)" ||
    fail "Could not determine the current macOS user."
  process_snapshot="$(snapshot_unity_processes)" ||
    fail "Could not inspect Unity licensing processes."
  orphan_pids="$(
    printf '%s\n' "$process_snapshot" |
      orphan_unity_licensing_pids "$current_uid"
  )"

  if [ -n "$orphan_pids" ]; then
    orphan_pids="${orphan_pids//$'\n'/,}"
    fail "Detached Unity Licensing Client detected (PID: $orphan_pids). Quit Unity/Hub, re-check the PID, terminate it manually, and rerun."
  fi
}

parse_arguments() {
  if [ "$#" -eq 0 ]; then
    usage >&2
    exit 2
  fi

  case "$1" in
    -h|--help)
      usage
      exit 0
      ;;
    android|ios|both)
      TARGET="$1"
      shift
      ;;
    *)
      usage >&2
      fail "Target must be android, ios, or both."
      ;;
  esac

  local seen_version=0
  local seen_build=0
  local seen_attempt=0
  local seen_keystore=0

  while [ "$#" -gt 0 ]; do
    case "$1" in
      --version)
        [ "$seen_version" -eq 0 ] || fail "--version was provided more than once."
        [ "$#" -ge 2 ] || fail "--version requires a value."
        BUILD_VERSION="$2"
        seen_version=1
        shift 2
        ;;
      --build)
        [ "$seen_build" -eq 0 ] || fail "--build was provided more than once."
        [ "$#" -ge 2 ] || fail "--build requires a value."
        BUILD_NUMBER="$2"
        seen_build=1
        shift 2
        ;;
      --attempt)
        [ "$seen_attempt" -eq 0 ] || fail "--attempt was provided more than once."
        [ "$#" -ge 2 ] || fail "--attempt requires a value."
        BUILD_ATTEMPT="$2"
        seen_attempt=1
        shift 2
        ;;
      --keystore)
        [ "$seen_keystore" -eq 0 ] || fail "--keystore was provided more than once."
        [ "$#" -ge 2 ] || fail "--keystore requires a value."
        KEYSTORE_ARGUMENT="$2"
        seen_keystore=1
        shift 2
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        fail "An unknown option was provided."
        ;;
    esac
  done

  [ "$seen_version" -eq 1 ] || fail "--version is required."
  [ "$seen_build" -eq 1 ] || fail "--build is required."

  if ! [[ "$BUILD_VERSION" =~ ^[0-9]+\.[0-9]+(\.[0-9]+)?$ ]]; then
    fail "--version must use major.minor or major.minor.patch numeric form."
  fi

  if ! [[ "$BUILD_NUMBER" =~ ^[1-9][0-9]*$ ]]; then
    fail "--build must be a positive integer."
  fi

  if [ "${#BUILD_NUMBER}" -gt 10 ] ||
    { [ "${#BUILD_NUMBER}" -eq 10 ] && [[ "$BUILD_NUMBER" > "2147483647" ]]; }; then
    fail "--build must not exceed 2147483647."
  fi

  if [ "$seen_attempt" -eq 1 ] && ! [[ "$BUILD_ATTEMPT" =~ ^[1-9][0-9]*$ ]]; then
    fail "--attempt must be a positive integer."
  fi
  if [ "$seen_attempt" -eq 1 ] &&
    { [ "${#BUILD_ATTEMPT}" -gt 10 ] ||
      { [ "${#BUILD_ATTEMPT}" -eq 10 ] &&
        [[ "$BUILD_ATTEMPT" > "2147483647" ]]; }; }; then
    fail "--attempt must not exceed 2147483647."
  fi

  if [ "$TARGET" = "ios" ] && [ "$seen_keystore" -eq 1 ]; then
    fail "--keystore is only valid for android or both."
  fi
}

resolve_project() {
  local script_dir
  local git_root

  script_dir="$(cd "$(dirname "$0")" && pwd -P)"
  PROJECT_ROOT="$(cd "$script_dir/../.." && pwd -P)"

  require_command git
  git_root="$(git -C "$PROJECT_ROOT" rev-parse --show-toplevel 2>/dev/null)" ||
    fail "The script must run from a Git worktree."
  git_root="$(cd "$git_root" && pwd -P)"
  [ "$git_root" = "$PROJECT_ROOT" ] ||
    fail "The script is not located at the project Git root."

  [ -d "$TEMP_BASE" ] ||
    fail "The temporary directory is unavailable."
  TEMP_BASE="$(cd "$TEMP_BASE" && pwd -P)"
  case "$TEMP_BASE" in
    "$PROJECT_ROOT"|"$PROJECT_ROOT"/*)
      fail "The temporary directory must be outside the Git project."
      ;;
  esac

  is_worktree_clean ||
    fail "Git worktree must be clean, including untracked files, before building."
  FINAL_CHECK_ARMED=1
}

acquire_build_lock() {
  local library_dir="$PROJECT_ROOT/Library"

  [ -d "$library_dir" ] && [ ! -L "$library_dir" ] ||
    fail "Unity Library must be a regular directory before acquiring the build lock."
  BUILD_LOCK_DIR="$PROJECT_ROOT/$BUILD_LOCK_RELATIVE"
  if ! mkdir "$BUILD_LOCK_DIR" 2>/dev/null; then
    fail "Another mobile build is running or a stale mobile build lock remains."
  fi
  BUILD_LOCK_HELD=1
  chmod 700 "$BUILD_LOCK_DIR" ||
    fail "Could not secure the mobile build lock."
}

release_build_lock() {
  [ "$BUILD_LOCK_HELD" -eq 1 ] || return 0
  /bin/rmdir -- "$BUILD_LOCK_DIR" || return 1
  BUILD_LOCK_HELD=0
}

configure_paths() {
  local attempt_suffix=""

  BUILD_HEAD_FULL="$(git -C "$PROJECT_ROOT" rev-parse HEAD)"
  [[ "$BUILD_HEAD_FULL" =~ ^[0-9a-fA-F]{40}$ ]] ||
    fail "Could not determine the full Git commit SHA."
  COMMIT_SHA="$(git -C "$PROJECT_ROOT" rev-parse --short=8 HEAD)"
  [[ "$COMMIT_SHA" =~ ^[0-9a-fA-F]{8}$ ]] ||
    fail "Could not determine an eight-character Git commit SHA."
  if [ -n "$BUILD_ATTEMPT" ]; then
    attempt_suffix="-attempt${BUILD_ATTEMPT}"
  fi

  UNITY_EDITOR="${UNITY_EDITOR_PATH:-$DEFAULT_UNITY_EDITOR}"
  MOBILE_OUTPUT_ROOT="$PROJECT_ROOT/Builds/Mobile"
  OUTS_OUTPUT_ROOT="$PROJECT_ROOT/Builds/outs"
  STEM="DreamSquad-Demo-${BUILD_VERSION}-${BUILD_NUMBER}-${COMMIT_SHA}${attempt_suffix}"
  STEM_ROOT="$MOBILE_OUTPUT_ROOT/$STEM"
  BUILD_TIMESTAMP="$(date '+%Y%m%d-%H%M%S')"
  [[ "$BUILD_TIMESTAMP" =~ ^[0-9]{8}-[0-9]{6}$ ]] ||
    fail "Could not determine the build timestamp."
  OUT_STEM="dreamquad-demo--${BUILD_VERSION}-${BUILD_NUMBER}-${BUILD_TIMESTAMP}-${COMMIT_SHA}${attempt_suffix}"

  ANDROID_OUTPUT_DIR="$STEM_ROOT/Android"
  ANDROID_APK="$ANDROID_OUTPUT_DIR/$STEM.apk"
  ANDROID_OUT_APK="$OUTS_OUTPUT_ROOT/$OUT_STEM.apk"

  IOS_OUTPUT_DIR="$STEM_ROOT/iOS"
  IOS_XCODE_DIR="$IOS_OUTPUT_DIR/Xcode"
  IOS_ARCHIVE="$IOS_OUTPUT_DIR/$STEM.xcarchive"
  IOS_EXPORT_DIR="$IOS_OUTPUT_DIR/Export"
  IOS_IPA="$IOS_OUTPUT_DIR/$STEM.ipa"
  IOS_OUT_IPA="$OUTS_OUTPUT_ROOT/$OUT_STEM.ipa"
}

generate_keystore_serialization_variant() {
  local source_file="$1"
  local destination_file="$2"

  awk -v replacement="  AndroidKeystoreName: '{inproject}: '" '
    $0 == "  AndroidKeystoreName: " {
      replacements++
      print replacement
      next
    }
    { print }
    END {
      if (replacements != 1) {
        exit 1
      }
    }
  ' "$source_file" > "$destination_file"
}

generate_ios_batching_serialization_variant() {
  local source_file="$1"
  local destination_file="$2"

  awk '
    $0 == "  m_BuildTargetBatching:" {
      in_batching = 1
    }
    in_batching && $0 == "  - m_BuildTarget: iPhone" {
      has_iphone = 1
    }
    in_batching && $0 == "  - m_BuildTarget: Android" {
      android_entries++
      in_android = 1
      static_lines = 0
      dynamic_lines = 0
      print
      next
    }
    in_android && $0 == "    m_StaticBatching: 1" {
      static_lines++
      print
      next
    }
    in_android && $0 == "    m_DynamicBatching: 0" {
      dynamic_lines++
      print
      if (static_lines != 1 || dynamic_lines != 1) {
        invalid_android = 1
        in_android = 0
        next
      }
      print "  - m_BuildTarget: iPhone"
      print "    m_StaticBatching: 1"
      print "    m_DynamicBatching: 0"
      inserted++
      in_android = 0
      next
    }
    in_android {
      invalid_android = 1
      in_android = 0
    }
    {
      print
    }
    in_batching && $0 ~ /^  [A-Za-z_]/ &&
      $0 != "  m_BuildTargetBatching:" {
      in_batching = 0
    }
    END {
      if (inserted != 1 || has_iphone || android_entries != 1 ||
        invalid_android || in_android) {
        exit 1
      }
    }
  ' "$source_file" > "$destination_file"
}

generate_mobile_rp_serialization_variant() {
  local source_file="$1"
  local destination_file="$2"

  awk '
    $0 == "  m_PrefilterPointSamplingUpsampling: 0" {
      removals++
      next
    }
    { print }
    END {
      if (removals != 1) {
        exit 1
      }
    }
  ' "$source_file" > "$destination_file"
}

capture_serialization_baseline() {
  local project_settings="$PROJECT_ROOT/$PROJECT_SETTINGS_RELATIVE"
  local mobile_rp_asset="$PROJECT_ROOT/$MOBILE_RP_ASSET_RELATIVE"
  local project_baseline
  local project_keystore_variant
  local project_batching_variant

  [ "$(git -C "$PROJECT_ROOT" rev-parse HEAD)" = "$BUILD_HEAD_FULL" ] ||
    fail "Git HEAD changed before the build baseline was captured."
  is_worktree_clean ||
    fail "Git worktree changed before the build baseline was captured."
  [ -f "$project_settings" ] && [ ! -L "$project_settings" ] ||
    fail "Tracked ProjectSettings baseline is unavailable."
  [ -f "$mobile_rp_asset" ] && [ ! -L "$mobile_rp_asset" ] ||
    fail "Tracked mobile render-pipeline baseline is unavailable."

  SERIALIZATION_BASELINE_DIR="$TEMP_DIR/serialization-baseline"
  mkdir "$SERIALIZATION_BASELINE_DIR"
  project_baseline="$SERIALIZATION_BASELINE_DIR/ProjectSettings.asset"
  project_keystore_variant="$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset"
  project_batching_variant="$SERIALIZATION_BASELINE_DIR/ProjectSettings.batching.asset"

  /bin/cp -p "$project_settings" "$project_baseline"
  /bin/cp -p "$mobile_rp_asset" "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.asset"

  if ! generate_keystore_serialization_variant \
    "$project_baseline" \
    "$project_keystore_variant"; then
    rm -f -- "$project_keystore_variant"
  fi
  if ! generate_ios_batching_serialization_variant \
    "$project_baseline" \
    "$project_batching_variant"; then
    rm -f -- "$project_batching_variant"
  fi
  if [ -f "$project_batching_variant" ] &&
    ! generate_keystore_serialization_variant \
      "$project_batching_variant" \
      "$SERIALIZATION_BASELINE_DIR/ProjectSettings.both.asset"; then
    rm -f -- "$SERIALIZATION_BASELINE_DIR/ProjectSettings.both.asset"
  fi
  if ! generate_mobile_rp_serialization_variant \
    "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.asset" \
    "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.migrated.asset"; then
    rm -f -- "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.migrated.asset"
  fi

  SERIALIZATION_BASELINE_CAPTURED=1
}

find_matching_serialization_candidate() {
  local current_file="$1"
  shift
  local candidate

  for candidate in "$@"; do
    [ -f "$candidate" ] || continue
    if cmp -s "$current_file" "$candidate"; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

verify_tracked_build_input_hashes() {
  local relative_path
  local expected_blob
  local actual_blob

  for relative_path in "$PROJECT_SETTINGS_RELATIVE" "$MOBILE_RP_ASSET_RELATIVE"; do
    expected_blob="$(
      git -C "$PROJECT_ROOT" rev-parse "$BUILD_HEAD_FULL:$relative_path"
    )" || return 1
    actual_blob="$(git hash-object "$PROJECT_ROOT/$relative_path")" || return 1
    [ "$actual_blob" = "$expected_blob" ] || return 1
  done
}

prepare_atomic_restore_file() {
  local baseline_file="$1"
  local current_file="$2"
  local restore_name="$3"
  local allow_missing_current="$4"
  local restore_file="$BUILD_LOCK_DIR/$restore_name"

  [ "$BUILD_LOCK_HELD" -eq 1 ] && [ -d "$BUILD_LOCK_DIR" ] ||
    return 1
  [ -f "$baseline_file" ] && [ ! -L "$baseline_file" ] ||
    return 1
  [ ! -e "$restore_file" ] && [ ! -L "$restore_file" ] ||
    return 1
  if [ -e "$current_file" ] || [ -L "$current_file" ]; then
    [ -f "$current_file" ] && [ ! -L "$current_file" ] ||
      return 1
  else
    [ "$allow_missing_current" -eq 1 ] ||
      return 1
  fi

  /bin/cp -p "$baseline_file" "$restore_file" || return 1
  cmp -s "$restore_file" "$baseline_file"
}

commit_atomic_restore_file() {
  local baseline_file="$1"
  local current_file="$2"
  local restore_name="$3"
  local allow_missing_current="$4"
  local expected_current="${5:-}"
  local restore_file="$BUILD_LOCK_DIR/$restore_name"

  [ -f "$restore_file" ] && [ ! -L "$restore_file" ] ||
    return 1
  cmp -s "$restore_file" "$baseline_file" ||
    return 1
  if [ -e "$current_file" ] || [ -L "$current_file" ]; then
    [ -f "$current_file" ] && [ ! -L "$current_file" ] ||
      return 1
  else
    [ "$allow_missing_current" -eq 1 ] ||
      return 1
  fi
  if [ -n "$expected_current" ]; then
    cmp -s "$current_file" "$expected_current" ||
      return 1
  fi

  /bin/mv -f "$restore_file" "$current_file" || return 1
  cmp -s "$current_file" "$baseline_file"
}

reconcile_known_unity_serialization() {
  local current_head
  local status
  local current_status
  local line
  local project_dirty=0
  local mobile_rp_dirty=0
  local project_settings="$PROJECT_ROOT/$PROJECT_SETTINGS_RELATIVE"
  local mobile_rp_asset="$PROJECT_ROOT/$MOBILE_RP_ASSET_RELATIVE"
  local project_candidate=""
  local mobile_rp_candidate=""

  [ "$SERIALIZATION_BASELINE_CAPTURED" -eq 1 ] || return 1
  current_head="$(git -C "$PROJECT_ROOT" rev-parse HEAD)" || return 1
  [ "$current_head" = "$BUILD_HEAD_FULL" ] || return 1
  git -C "$PROJECT_ROOT" diff --cached --quiet -- || return 1
  status="$(
    git -C "$PROJECT_ROOT" status --porcelain --untracked-files=all
  )" || return 1
  if [ -n "$status" ] && [ "$SERIALIZATION_RESTORE_ARMED" -ne 1 ]; then
    return 1
  fi

  if [ -n "$status" ]; then
    while IFS= read -r line; do
      case "$line" in
        " M $PROJECT_SETTINGS_RELATIVE")
          project_dirty=1
          ;;
        " M $MOBILE_RP_ASSET_RELATIVE")
          mobile_rp_dirty=1
          ;;
        *)
          return 1
          ;;
      esac
    done <<< "$status"
  fi

  if [ "$project_dirty" -eq 1 ]; then
    project_candidate="$(
      find_matching_serialization_candidate \
        "$project_settings" \
        "$SERIALIZATION_BASELINE_DIR/ProjectSettings.keystore.asset" \
        "$SERIALIZATION_BASELINE_DIR/ProjectSettings.batching.asset" \
        "$SERIALIZATION_BASELINE_DIR/ProjectSettings.both.asset"
    )" || return 1
  fi
  if [ "$mobile_rp_dirty" -eq 1 ]; then
    mobile_rp_candidate="$(
      find_matching_serialization_candidate \
        "$mobile_rp_asset" \
        "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.migrated.asset"
    )" || return 1
  fi

  if [ "$project_dirty" -eq 1 ]; then
    cmp -s "$project_settings" "$project_candidate" || return 1
  fi
  if [ "$mobile_rp_dirty" -eq 1 ]; then
    cmp -s "$mobile_rp_asset" "$mobile_rp_candidate" || return 1
  fi
  if [ "$project_dirty" -eq 1 ]; then
    prepare_atomic_restore_file \
      "$SERIALIZATION_BASELINE_DIR/ProjectSettings.asset" \
      "$project_settings" \
      "ProjectSettings.restore" \
      0 || return 1
  fi
  if [ "$mobile_rp_dirty" -eq 1 ]; then
    prepare_atomic_restore_file \
      "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.asset" \
      "$mobile_rp_asset" \
      "Mobile_RPAsset.restore" \
      0 || return 1
  fi

  [ "$(git -C "$PROJECT_ROOT" rev-parse HEAD)" = "$BUILD_HEAD_FULL" ] ||
    return 1
  git -C "$PROJECT_ROOT" diff --cached --quiet -- || return 1
  current_status="$(
    git -C "$PROJECT_ROOT" status --porcelain --untracked-files=all
  )" || return 1
  [ "$current_status" = "$status" ] || return 1
  if [ "$project_dirty" -eq 1 ]; then
    cmp -s "$project_settings" "$project_candidate" || return 1
  fi
  if [ "$mobile_rp_dirty" -eq 1 ]; then
    cmp -s "$mobile_rp_asset" "$mobile_rp_candidate" || return 1
  fi
  if [ "$project_dirty" -eq 1 ]; then
    commit_atomic_restore_file \
      "$SERIALIZATION_BASELINE_DIR/ProjectSettings.asset" \
      "$project_settings" \
      "ProjectSettings.restore" \
      0 \
      "$project_candidate" || return 1
  fi
  if [ "$mobile_rp_dirty" -eq 1 ]; then
    commit_atomic_restore_file \
      "$SERIALIZATION_BASELINE_DIR/Mobile_RPAsset.asset" \
      "$mobile_rp_asset" \
      "Mobile_RPAsset.restore" \
      0 \
      "$mobile_rp_candidate" || return 1
  fi
  verify_tracked_build_input_hashes || return 1
  is_worktree_clean
}

settle_known_unity_serialization() {
  local sample

  for sample in 1 2 3; do
    reconcile_known_unity_serialization ||
      fail "Unity changed tracked files outside the approved serialization-only set."
    if [ "$sample" -lt 3 ]; then
      sleep 1
    fi
  done
  SERIALIZATION_RESTORE_ARMED=0
}

assert_output_available() {
  local output_path="$1"
  local label="$2"

  if [ -e "$output_path" ] || [ -L "$output_path" ]; then
    fail "$label output already exists. Existing output is never overwritten."
  fi
}

assert_output_root_safe() {
  local builds_dir="$PROJECT_ROOT/Builds"

  [ ! -L "$builds_dir" ] ||
    fail "Builds output directory must not be a symbolic link."
  [ ! -L "$MOBILE_OUTPUT_ROOT" ] ||
    fail "Builds/Mobile output directory must not be a symbolic link."
  [ ! -L "$OUTS_OUTPUT_ROOT" ] ||
    fail "Builds/outs output directory must not be a symbolic link."
  [ ! -L "$STEM_ROOT" ] ||
    fail "The build stem output directory must not be a symbolic link."

  if [ -e "$builds_dir" ] && [ ! -d "$builds_dir" ]; then
    fail "Builds output path must be a directory."
  fi
  if [ -e "$MOBILE_OUTPUT_ROOT" ] && [ ! -d "$MOBILE_OUTPUT_ROOT" ]; then
    fail "Builds/Mobile output path must be a directory."
  fi
  if [ -e "$OUTS_OUTPUT_ROOT" ] && [ ! -d "$OUTS_OUTPUT_ROOT" ]; then
    fail "Builds/outs output path must be a directory."
  fi
  if [ -e "$STEM_ROOT" ] && [ ! -d "$STEM_ROOT" ]; then
    fail "The build stem output path must be a directory."
  fi
}

preflight_common() {
  [ "$(uname -s)" = "Darwin" ] ||
    fail "This build wrapper requires macOS."
  assert_no_orphan_unity_licensing_client
  [ -x "$UNITY_EDITOR" ] ||
    fail "Unity 6000.4.3f1 is unavailable. Set UNITY_EDITOR_PATH to its executable."

  case "$TARGET" in
    android|both)
      UNITY_ANDROID_MODULE="$(find_unity_playback_engine AndroidPlayer)" ||
        fail "Unity Android Build Support is not installed."
      ;;
  esac

  case "$TARGET" in
    ios|both)
      find_unity_playback_engine iOSSupport >/dev/null ||
        fail "Unity iOS Build Support is not installed."
      ;;
  esac

  require_command unzip
  require_command shasum
}

find_unity_playback_engine() {
  local engine_name="$1"
  local unity_binary_dir
  local candidate_root
  local candidate

  unity_binary_dir="$(cd "$(dirname "$UNITY_EDITOR")" && pwd -P)" || return 1

  # Unity Hub currently installs platform modules beside Unity.app. Older layouts
  # can place them under Unity.app/Contents, so accept either exact module root.
  for candidate_root in "$unity_binary_dir/../../.." "$unity_binary_dir/.."; do
    [ -d "$candidate_root" ] || continue
    candidate_root="$(cd "$candidate_root" && pwd -P)"
    candidate="$candidate_root/PlaybackEngines/$engine_name"
    if [ -d "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done

  return 1
}

canonicalize_keystore() {
  local requested_path="$KEYSTORE_ARGUMENT"
  local keystore_dir
  local keystore_name

  if [ -z "$requested_path" ]; then
    requested_path="$HOME/Library/Application Support/Playlinks/Signing/Android/somnia-dev.keystore"
  elif [ "${requested_path#/}" = "$requested_path" ]; then
    requested_path="$PROJECT_ROOT/$requested_path"
  fi

  [ -f "$requested_path" ] ||
    fail "Android keystore is missing from the configured signing location."
  [ ! -L "$requested_path" ] ||
    fail "Android keystore must be a regular file, not a symbolic link."

  keystore_dir="$(dirname "$requested_path")"
  keystore_name="$(basename "$requested_path")"
  if ! keystore_dir="$(cd "$keystore_dir" 2>/dev/null && pwd -P)"; then
    fail "Android signing directory is not accessible."
  fi
  KEYSTORE_PATH="$keystore_dir/$keystore_name"

  case "$KEYSTORE_PATH" in
    "$PROJECT_ROOT"|"$PROJECT_ROOT"/*)
      fail "Android keystore must be stored outside the Git project."
      ;;
  esac
}

assert_secure_signing_file() {
  local signing_dir
  local current_uid
  local dir_uid
  local file_uid
  local dir_mode
  local file_mode

  signing_dir="$(dirname "$KEYSTORE_PATH")"
  current_uid="$(id -u)"
  dir_uid="$(stat -f '%u' "$signing_dir" 2>/dev/null)" ||
    fail "Could not inspect Android signing directory ownership."
  file_uid="$(stat -f '%u' "$KEYSTORE_PATH" 2>/dev/null)" ||
    fail "Could not inspect Android keystore ownership."
  dir_mode="$(stat -f '%Lp' "$signing_dir" 2>/dev/null)" ||
    fail "Could not inspect Android signing directory permissions."
  file_mode="$(stat -f '%Lp' "$KEYSTORE_PATH" 2>/dev/null)" ||
    fail "Could not inspect Android keystore permissions."

  [ "$dir_uid" = "$current_uid" ] && [ "$file_uid" = "$current_uid" ] ||
    fail "Android signing directory and keystore must be owned by the build user."

  case "$dir_mode" in
    *00) ;;
    *) fail "Android signing directory permissions must deny group and other access (0700 recommended)." ;;
  esac

  case "$file_mode" in
    *00) ;;
    *) fail "Android keystore permissions must deny group and other access (0600 recommended)." ;;
  esac

  [ -r "$KEYSTORE_PATH" ] ||
    fail "Android keystore is not readable by the build user."
}

find_android_tool() {
  local tool_name="$1"
  local override_path="$2"
  local unity_sdk
  local sdk_dir
  local candidate
  local selected

  if [ -n "$override_path" ]; then
    [ -x "$override_path" ] ||
      fail "Configured Android tool is not executable: $tool_name"
    printf '%s\n' "$override_path"
    return
  fi

  [ -n "$UNITY_ANDROID_MODULE" ] ||
    fail "Unity Android Build Support was not resolved before tool discovery."
  unity_sdk="$UNITY_ANDROID_MODULE/SDK"

  for sdk_dir in "${ANDROID_SDK_ROOT:-}" "${ANDROID_HOME:-}" "$unity_sdk"; do
    [ -n "$sdk_dir" ] || continue
    selected=""
    for candidate in "$sdk_dir"/build-tools/*/"$tool_name"; do
      if [ -x "$candidate" ]; then
        selected="$candidate"
      fi
    done
    if [ -n "$selected" ]; then
      printf '%s\n' "$selected"
      return
    fi
  done

  fail "Android SDK build tool is unavailable: $tool_name"
}

find_keytool() {
  local bundled_keytool
  local system_keytool

  if [ -n "${KEYTOOL_PATH:-}" ]; then
    [ -x "$KEYTOOL_PATH" ] ||
      fail "Configured keytool is not executable."
    printf '%s\n' "$KEYTOOL_PATH"
    return
  fi

  [ -n "$UNITY_ANDROID_MODULE" ] ||
    fail "Unity Android Build Support was not resolved before keytool discovery."
  bundled_keytool="$UNITY_ANDROID_MODULE/OpenJDK/bin/keytool"
  if [ -x "$bundled_keytool" ]; then
    printf '%s\n' "$bundled_keytool"
    return
  fi

  system_keytool="$(command -v keytool || true)"
  [ -n "$system_keytool" ] ||
    fail "keytool is unavailable."
  printf '%s\n' "$system_keytool"
}

prompt_android_passwords() {
  printf 'Android keystore password: ' >&2
  if ! IFS= read -r -s ANDROID_STORE_PASSWORD; then
    printf '\n' >&2
    fail "Could not read the Android keystore password."
  fi
  printf '\n' >&2
  [ -n "$ANDROID_STORE_PASSWORD" ] ||
    fail "Android keystore password must not be empty."

  printf 'Android key password (press Enter to reuse keystore password): ' >&2
  if ! IFS= read -r -s ANDROID_KEY_PASSWORD; then
    printf '\n' >&2
    fail "Could not read the Android key password."
  fi
  printf '\n' >&2

  if [ -z "$ANDROID_KEY_PASSWORD" ]; then
    ANDROID_KEY_PASSWORD="$ANDROID_STORE_PASSWORD"
  fi
}

keytool_certificate_digest() {
  local output

  if ! output="$(
    (
      export DREAMSQUAD_KEYTOOL_STORE_PASSWORD="$ANDROID_STORE_PASSWORD"
      LC_ALL=C "$KEYTOOL" \
        -list -v \
        -keystore "$KEYSTORE_PATH" \
        -alias "$ANDROID_ALIAS" \
        -storepass:env DREAMSQUAD_KEYTOOL_STORE_PASSWORD
    ) 2>/dev/null
  )"; then
    fail "Android keystore password or alias is invalid."
  fi

  printf '%s\n' "$output" |
    awk -F': ' '/SHA256:/{print $2; exit}' |
    tr -d ':[:space:]' |
    tr '[:lower:]' '[:upper:]'
}

preflight_android() {
  require_command perl
  canonicalize_keystore
  assert_secure_signing_file

  AAPT2="$(find_android_tool aapt2 "${AAPT2_PATH:-}")"
  APKSIGNER="$(find_android_tool apksigner "${APKSIGNER_PATH:-}")"
  KEYTOOL="$(find_keytool)"

  prompt_android_passwords

  local signing_digest
  signing_digest="$(keytool_certificate_digest)"
  [ -n "$signing_digest" ] ||
    fail "Could not read the somnia-dev signing certificate."
}

plist_value() {
  /usr/libexec/PlistBuddy -c "Print :$2" "$1" 2>/dev/null
}

verify_landscape_orientation_array() {
  local plist_path="$1"
  local key="$2"
  local first
  local second
  local third

  first="$(plist_value "$plist_path" "$key:0" || true)"
  second="$(plist_value "$plist_path" "$key:1" || true)"
  third="$(plist_value "$plist_path" "$key:2" || true)"

  [ -z "$third" ] ||
    fail "IPA $key contains an unsupported extra orientation."
  if ! {
    [ "$first" = "UIInterfaceOrientationLandscapeLeft" ] &&
      [ "$second" = "UIInterfaceOrientationLandscapeRight" ]
  } && ! {
    [ "$first" = "UIInterfaceOrientationLandscapeRight" ] &&
      [ "$second" = "UIInterfaceOrientationLandscapeLeft" ]
  }; then
    fail "IPA $key must contain exactly both landscape orientations."
  fi
}

decode_profile() {
  /usr/bin/security cms -D -i "$1" > "$2" 2>/dev/null
}

validate_profile() {
  local profile_path="$1"
  local decoded_path="$2"
  local name
  local uuid
  local team
  local application_identifier
  local expiration
  local expiration_iso
  local expiration_epoch
  local current_epoch
  local provisions_all_devices
  local get_task_allow
  local devices
  local device_count

  decode_profile "$profile_path" "$decoded_path" ||
    fail "Could not decode the iOS provisioning profile."

  name="$(plist_value "$decoded_path" Name || true)"
  uuid="$(plist_value "$decoded_path" UUID || true)"
  team="$(plist_value "$decoded_path" TeamIdentifier:0 || true)"
  application_identifier="$(plist_value "$decoded_path" Entitlements:application-identifier || true)"
  expiration="$(plist_value "$decoded_path" ExpirationDate || true)"
  expiration_iso="$(
    awk '
      /<key>ExpirationDate<\/key>/ { found = 1; next }
      found && /<date>/ {
        value = $0
        sub(/^.*<date>/, "", value)
        sub(/<\/date>.*$/, "", value)
        print value
        exit
      }
    ' "$decoded_path"
  )"
  provisions_all_devices="$(plist_value "$decoded_path" ProvisionsAllDevices || true)"
  get_task_allow="$(plist_value "$decoded_path" Entitlements:get-task-allow || true)"
  devices="$(plist_value "$decoded_path" ProvisionedDevices || true)"
  device_count="$(
    printf '%s\n' "$devices" |
      awk '$0 !~ /Array \{/ && $0 !~ /^[[:space:]]*}/ && $0 ~ /[^[:space:]]/ { count++ } END { print count + 0 }'
  )"

  [ "$name" = "$IOS_PROFILE_NAME" ] ||
    fail "Unexpected iOS provisioning profile name."
  [ -n "$uuid" ] ||
    fail "The iOS provisioning profile has no UUID."
  [ "$team" = "$IOS_TEAM_ID" ] ||
    fail "The iOS provisioning profile belongs to an unexpected team."
  [ "$application_identifier" = "$IOS_TEAM_ID.$APP_IDENTIFIER" ] ||
    fail "The iOS provisioning profile does not match the application identifier."
  [ "$provisions_all_devices" != "true" ] ||
    fail "An Ad Hoc provisioning profile is required, not an enterprise profile."
  [ "$get_task_allow" != "true" ] ||
    fail "A distribution provisioning profile is required."
  [ "$device_count" -gt 0 ] ||
    fail "The iOS provisioning profile has no registered devices."

  expiration_epoch=""
  if [ -n "$expiration_iso" ]; then
    expiration_epoch="$(
      date -j -u -f '%Y-%m-%dT%H:%M:%SZ' "$expiration_iso" '+%s' 2>/dev/null || true
    )"
  fi
  if [ -z "$expiration_epoch" ]; then
    expiration_epoch="$(
      date -j -f '%a %b %d %T %Z %Y' "$expiration" '+%s' 2>/dev/null || true
    )"
  fi
  current_epoch="$(date '+%s')"
  [ -n "$expiration_epoch" ] ||
    fail "Could not parse the iOS provisioning profile expiration date."
  [ "$expiration_epoch" -gt "$current_epoch" ] ||
    fail "The iOS provisioning profile has expired."

  VALIDATED_PROFILE_UUID="$uuid"
  VALIDATED_PROFILE_EXPIRY="$expiration"
  VALIDATED_PROFILE_DEVICE_COUNT="$device_count"
}

find_installed_profile() {
  local candidates="$TEMP_DIR/profile-candidates.txt"
  local decoded="$TEMP_DIR/profile-candidate.plist"
  local candidate
  local candidate_name
  local matching_count=0

  : > "$candidates"
  if [ -d "$HOME/Library/MobileDevice/Provisioning Profiles" ]; then
    find "$HOME/Library/MobileDevice/Provisioning Profiles" \
      -type f -name '*.mobileprovision' -print >> "$candidates"
  fi
  if [ -d "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles" ]; then
    find "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles" \
      -type f -name '*.mobileprovision' -print >> "$candidates"
  fi

  while IFS= read -r candidate; do
    if decode_profile "$candidate" "$decoded"; then
      candidate_name="$(plist_value "$decoded" Name || true)"
      if [ "$candidate_name" = "$IOS_PROFILE_NAME" ]; then
        INSTALLED_PROFILE_PATH="$candidate"
        matching_count=$((matching_count + 1))
      fi
    fi
  done < "$candidates"

  [ "$matching_count" -gt 0 ] ||
    fail "The somnia_dev_adhoc provisioning profile is not installed."
  [ "$matching_count" -eq 1 ] ||
    fail "Multiple somnia_dev_adhoc profiles are installed. Remove stale duplicates before building."
}

preflight_ios() {
  require_command xcodebuild
  require_command codesign
  require_command security

  if ! /usr/bin/security find-identity -v -p codesigning 2>/dev/null |
    grep -F "$IOS_SIGNING_IDENTITY" |
    grep -F "($IOS_TEAM_ID)" >/dev/null; then
    fail "An unlocked Keychain with the expected Apple Distribution identity and private key is required."
  fi

  find_installed_profile
  validate_profile "$INSTALLED_PROFILE_PATH" "$TEMP_DIR/installed-profile.plist"
  INSTALLED_PROFILE_UUID="$VALIDATED_PROFILE_UUID"
  info "iOS signing identity and Ad Hoc profile verified ($VALIDATED_PROFILE_DEVICE_COUNT registered devices)."
}

reserve_output_directories() {
  local builds_dir="$PROJECT_ROOT/Builds"

  if [ ! -d "$builds_dir" ]; then
    mkdir "$builds_dir"
  fi
  [ ! -L "$builds_dir" ] ||
    fail "Builds output directory became a symbolic link."

  if [ ! -d "$MOBILE_OUTPUT_ROOT" ]; then
    mkdir "$MOBILE_OUTPUT_ROOT"
  fi
  [ ! -L "$MOBILE_OUTPUT_ROOT" ] ||
    fail "Builds/Mobile output directory became a symbolic link."

  if [ ! -d "$STEM_ROOT" ]; then
    mkdir "$STEM_ROOT"
  fi
  [ ! -L "$STEM_ROOT" ] ||
    fail "The build stem output directory became a symbolic link."

  case "$TARGET" in
    android)
      mkdir "$ANDROID_OUTPUT_DIR"
      ;;
    ios)
      mkdir "$IOS_OUTPUT_DIR"
      ;;
    both)
      mkdir "$ANDROID_OUTPUT_DIR"
      mkdir "$IOS_OUTPUT_DIR"
      ;;
  esac
}

reserve_outs_directory() {
  if [ ! -d "$OUTS_OUTPUT_ROOT" ]; then
    mkdir "$OUTS_OUTPUT_ROOT"
  fi
  [ ! -L "$OUTS_OUTPUT_ROOT" ] ||
    fail "Builds/outs output directory became a symbolic link."
}

copy_verified_artifact() {
  local source_path="$1"
  local destination_path="$2"
  local label="$3"
  local temporary_path="${destination_path}.partial.$$"

  [ -f "$source_path" ] && [ ! -L "$source_path" ] ||
    fail "$label source artifact is missing or is not a regular file."
  assert_output_available "$destination_path" "$label outs"
  assert_output_available "$temporary_path" "$label temporary outs"

  if ! /bin/cp -p "$source_path" "$temporary_path"; then
    rm -f -- "$temporary_path"
    fail "Could not copy the verified $label to Builds/outs."
  fi
  if ! cmp -s "$source_path" "$temporary_path"; then
    rm -f -- "$temporary_path"
    fail "The $label copy in Builds/outs does not match the verified artifact."
  fi
  if ! /bin/ln "$temporary_path" "$destination_path"; then
    rm -f -- "$temporary_path"
    fail "Could not finalize the verified $label copy in Builds/outs."
  fi
  rm -f -- "$temporary_path" ||
    fail "Could not remove the temporary $label copy in Builds/outs."
}

publish_verified_artifacts() {
  reserve_outs_directory

  case "$TARGET" in
    android)
      copy_verified_artifact "$ANDROID_APK" "$ANDROID_OUT_APK" "APK"
      ;;
    ios)
      copy_verified_artifact "$IOS_IPA" "$IOS_OUT_IPA" "IPA"
      ;;
    both)
      copy_verified_artifact "$ANDROID_APK" "$ANDROID_OUT_APK" "APK"
      copy_verified_artifact "$IOS_IPA" "$IOS_OUT_IPA" "IPA"
      ;;
  esac
}

sanitize_android_unity_log() {
  local destination="$1"

  if ! (
    export DREAMSQUAD_REDACT_KEYSTORE="$KEYSTORE_PATH"
    export DREAMSQUAD_REDACT_STORE_PASSWORD="$ANDROID_STORE_PASSWORD"
    export DREAMSQUAD_REDACT_KEY_PASSWORD="$ANDROID_KEY_PASSWORD"
    /usr/bin/perl -pe '
      BEGIN {
        @redactions = grep { defined($_) && length($_) } (
          $ENV{"DREAMSQUAD_REDACT_KEYSTORE"},
          $ENV{"DREAMSQUAD_REDACT_STORE_PASSWORD"},
          $ENV{"DREAMSQUAD_REDACT_KEY_PASSWORD"}
        );
      }
      for $secret (@redactions) {
        s/\Q$secret\E/[REDACTED]/g;
      }
    ' "$RAW_UNITY_LOG"
  ) > "$destination"; then
    rm -f -- "$destination"
    fail "Could not sanitize the Android Unity log."
  fi
}

run_unity_android() {
  local final_log="$ANDROID_OUTPUT_DIR/unity.log"
  local unity_exit

  assert_no_orphan_unity_licensing_client
  RAW_UNITY_LOG="$TEMP_DIR/android-unity.log"
  : > "$RAW_UNITY_LOG"
  chmod 600 "$RAW_UNITY_LOG"

  set +e
  SERIALIZATION_RESTORE_ARMED=1
  UNITY_LAUNCH_ACTIVE=1
  (
    export DREAMSQUAD_BUILD_VERSION="$BUILD_VERSION"
    export DREAMSQUAD_BUILD_NUMBER="$BUILD_NUMBER"
    export DREAMSQUAD_BUILD_OUTPUT="$ANDROID_APK"
    export DREAMSQUAD_ANDROID_KEYSTORE="$KEYSTORE_PATH"
    export DREAMSQUAD_ANDROID_KEYSTORE_PASSWORD="$ANDROID_STORE_PASSWORD"
    export DREAMSQUAD_ANDROID_KEY_PASSWORD="$ANDROID_KEY_PASSWORD"
    exec "$UNITY_EDITOR" \
      -quit \
      -batchmode \
      -nographics \
      -projectPath "$PROJECT_ROOT" \
      -buildTarget Android \
      -executeMethod Wassup.Editor.MobileBuild.DreamSquadMobileBuildCli.BuildAndroidQa \
      -logFile "$RAW_UNITY_LOG"
  )
  unity_exit=$?
  finish_unity_launch
  set -e

  sanitize_android_unity_log "$final_log"
  rm -f -- "$RAW_UNITY_LOG"
  RAW_UNITY_LOG=""
  settle_known_unity_serialization
  exit_if_unity_signal_deferred

  [ "$unity_exit" -eq 0 ] ||
    fail "Unity Android build failed. Inspect the Android unity.log."
  [ -f "$ANDROID_APK" ] ||
    fail "Unity reported success but did not produce the Android APK."
}

verify_android_apk() {
  local badging
  local apksigner_output
  local actual_digest
  local expected_digest
  local archive_entries
  local checksum
  local summary

  badging="$("$AAPT2" dump badging "$ANDROID_APK" 2>/dev/null)" ||
    fail "aapt2 could not inspect the APK."
  case "$badging" in
    *"package: name='$APP_IDENTIFIER'"*) ;;
    *) fail "APK package identifier verification failed." ;;
  esac
  case "$badging" in
    *"versionCode='$BUILD_NUMBER'"*) ;;
    *) fail "APK versionCode verification failed." ;;
  esac
  case "$badging" in
    *"versionName='$BUILD_VERSION'"*) ;;
    *) fail "APK versionName verification failed." ;;
  esac

  apksigner_output="$("$APKSIGNER" verify --verbose --print-certs "$ANDROID_APK" 2>/dev/null)" ||
    fail "APK signature verification failed."
  actual_digest="$(
    printf '%s\n' "$apksigner_output" |
      awk -F': ' '/certificate SHA-256 digest:/{print $2; exit}' |
      tr -d ':[:space:]' |
      tr '[:lower:]' '[:upper:]'
  )"
  expected_digest="$(keytool_certificate_digest)"
  [ -n "$actual_digest" ] && [ "$actual_digest" = "$expected_digest" ] ||
    fail "APK signer does not match the somnia-dev keystore."

  archive_entries="$(unzip -Z1 "$ANDROID_APK" 2>/dev/null)" ||
    fail "Could not inspect APK native libraries."
  printf '%s\n' "$archive_entries" |
    grep -Fx 'lib/arm64-v8a/libil2cpp.so' >/dev/null ||
    fail "APK does not contain the ARM64 IL2CPP runtime."
  if printf '%s\n' "$archive_entries" |
    awk -F/ '/^lib\/[^/]+\// && $2 != "arm64-v8a" { found = 1 } END { exit !found }'; then
    fail "APK contains an unexpected non-ARM64 ABI."
  fi
  if printf '%s\n' "$archive_entries" |
    grep -Ei '(^|/)libmono[^/]*\.so$' >/dev/null; then
    fail "APK contains a Mono runtime library instead of IL2CPP-only output."
  fi

  checksum="$(shasum -a 256 "$ANDROID_APK" | awk '{print $1}')"
  summary="$ANDROID_OUTPUT_DIR/build-summary.txt"
  {
    printf 'platform=android\n'
    printf 'applicationIdentifier=%s\n' "$APP_IDENTIFIER"
    printf 'version=%s\n' "$BUILD_VERSION"
    printf 'build=%s\n' "$BUILD_NUMBER"
    printf 'commit=%s\n' "$COMMIT_SHA"
    printf 'artifact=%s.apk\n' "$STEM"
    printf 'sha256=%s\n' "$checksum"
    printf 'arm64Il2cppVerified=true\n'
    printf 'somniaQaSignerVerified=true\n'
  } > "$summary"
  info "Android APK verified (package, version, ARM64 IL2CPP, signer)."
  info "Android SHA-256: $checksum"
}

run_unity_ios_export() {
  local final_log="$IOS_OUTPUT_DIR/unity.log"
  local unity_exit

  assert_no_orphan_unity_licensing_client
  set +e
  SERIALIZATION_RESTORE_ARMED=1
  UNITY_LAUNCH_ACTIVE=1
  (
    export DREAMSQUAD_BUILD_VERSION="$BUILD_VERSION"
    export DREAMSQUAD_BUILD_NUMBER="$BUILD_NUMBER"
    export DREAMSQUAD_BUILD_OUTPUT="$IOS_XCODE_DIR"
    exec "$UNITY_EDITOR" \
      -quit \
      -batchmode \
      -nographics \
      -projectPath "$PROJECT_ROOT" \
      -buildTarget iOS \
      -executeMethod Wassup.Editor.MobileBuild.DreamSquadMobileBuildCli.ExportIosQa \
      -logFile "$final_log"
  )
  unity_exit=$?
  finish_unity_launch
  set -e
  settle_known_unity_serialization
  exit_if_unity_signal_deferred

  [ "$unity_exit" -eq 0 ] ||
    fail "Unity iOS export failed. Inspect the iOS unity.log."

  [ -f "$IOS_XCODE_DIR/Unity-iPhone.xcodeproj/project.pbxproj" ] ||
    fail "Unity reported success but did not produce an Xcode project."
}

write_export_options() {
  local export_options="$IOS_OUTPUT_DIR/ExportOptions.plist"

  cat > "$export_options" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>method</key>
  <string>ad-hoc</string>
  <key>signingStyle</key>
  <string>manual</string>
  <key>teamID</key>
  <string>$IOS_TEAM_ID</string>
  <key>signingCertificate</key>
  <string>$IOS_SIGNING_IDENTITY</string>
  <key>provisioningProfiles</key>
  <dict>
    <key>$APP_IDENTIFIER</key>
    <string>$IOS_PROFILE_NAME</string>
  </dict>
  <key>stripSwiftSymbols</key>
  <true/>
  <key>manageAppVersionAndBuildNumber</key>
  <false/>
</dict>
</plist>
EOF
}

verify_xcode_signing_settings() {
  local settings_log="$IOS_OUTPUT_DIR/build-settings.log"

  if ! xcodebuild \
    -project "$IOS_XCODE_DIR/Unity-iPhone.xcodeproj" \
    -target Unity-iPhone \
    -configuration Release \
    -showBuildSettings > "$settings_log" 2>&1; then
    fail "Could not inspect Unity-iPhone Release build settings."
  fi

  grep -E '^[[:space:]]*CODE_SIGN_STYLE = Manual$' "$settings_log" >/dev/null ||
    fail "Unity-iPhone Release is not configured for manual signing."
  grep -E "^[[:space:]]*DEVELOPMENT_TEAM = $IOS_TEAM_ID$" "$settings_log" >/dev/null ||
    fail "Unity-iPhone Release has an unexpected development team."
  grep -E "^[[:space:]]*CODE_SIGN_IDENTITY = $IOS_SIGNING_IDENTITY$" "$settings_log" >/dev/null ||
    fail "Unity-iPhone Release has an unexpected signing identity."
  grep -E "^[[:space:]]*PROVISIONING_PROFILE_SPECIFIER = $IOS_PROFILE_NAME$" "$settings_log" >/dev/null ||
    fail "Unity-iPhone Release has an unexpected provisioning profile."
}

archive_and_export_ios() {
  local archive_log="$IOS_OUTPUT_DIR/archive.log"
  local export_log="$IOS_OUTPUT_DIR/export.log"
  local export_options="$IOS_OUTPUT_DIR/ExportOptions.plist"
  local ipa_count
  local exported_ipa
  local ipa_candidate

  verify_xcode_signing_settings
  write_export_options
  mkdir "$IOS_EXPORT_DIR"

  if ! xcodebuild \
    -project "$IOS_XCODE_DIR/Unity-iPhone.xcodeproj" \
    -scheme Unity-iPhone \
    -configuration Release \
    -destination 'generic/platform=iOS' \
    -archivePath "$IOS_ARCHIVE" \
    archive > "$archive_log" 2>&1; then
    fail "Xcode archive failed. Inspect the iOS archive.log."
  fi
  [ -d "$IOS_ARCHIVE" ] ||
    fail "xcodebuild reported success but did not produce an xcarchive."

  if ! xcodebuild \
    -exportArchive \
    -archivePath "$IOS_ARCHIVE" \
    -exportPath "$IOS_EXPORT_DIR" \
    -exportOptionsPlist "$export_options" > "$export_log" 2>&1; then
    fail "Xcode Ad Hoc export failed. Inspect the iOS export.log."
  fi

  ipa_count=0
  exported_ipa=""
  for ipa_candidate in "$IOS_EXPORT_DIR"/*.ipa; do
    [ -f "$ipa_candidate" ] || continue
    ipa_count=$((ipa_count + 1))
    exported_ipa="$ipa_candidate"
  done
  [ "$ipa_count" -eq 1 ] ||
    fail "Xcode export must produce exactly one IPA."
  [ ! -e "$IOS_IPA" ] && [ ! -L "$IOS_IPA" ] ||
    fail "Final IPA already exists and will not be overwritten."
  mv "$exported_ipa" "$IOS_IPA"
}

verify_ios_ipa() {
  local extract_dir="$TEMP_DIR/ipa"
  local app_count
  local app_path
  local app_candidate
  local info_plist
  local bundle_identifier
  local short_version
  local bundle_version
  local codesign_details
  local signed_team
  local embedded_profile
  local embedded_uuid
  local checksum
  local summary

  mkdir "$extract_dir"
  unzip -q "$IOS_IPA" -d "$extract_dir" ||
    fail "Could not extract the generated IPA."
  [ -d "$extract_dir/Payload" ] ||
    fail "IPA does not contain a Payload directory."

  app_count=0
  app_path=""
  for app_candidate in "$extract_dir"/Payload/*.app; do
    [ -d "$app_candidate" ] || continue
    app_count=$((app_count + 1))
    app_path="$app_candidate"
  done
  [ "$app_count" -eq 1 ] ||
    fail "IPA must contain exactly one top-level application bundle."
  info_plist="$app_path/Info.plist"
  [ -f "$info_plist" ] ||
    fail "IPA application bundle has no Info.plist."

  codesign --verify --deep --strict "$app_path" >/dev/null 2>&1 ||
    fail "IPA application code-signature verification failed."

  bundle_identifier="$(plist_value "$info_plist" CFBundleIdentifier || true)"
  short_version="$(plist_value "$info_plist" CFBundleShortVersionString || true)"
  bundle_version="$(plist_value "$info_plist" CFBundleVersion || true)"
  [ "$bundle_identifier" = "$APP_IDENTIFIER" ] ||
    fail "IPA bundle identifier verification failed."
  [ "$short_version" = "$BUILD_VERSION" ] ||
    fail "IPA short version verification failed."
  [ "$bundle_version" = "$BUILD_NUMBER" ] ||
    fail "IPA build number verification failed."
  verify_landscape_orientation_array \
    "$info_plist" \
    "UISupportedInterfaceOrientations"
  verify_landscape_orientation_array \
    "$info_plist" \
    "UISupportedInterfaceOrientations~ipad"

  codesign_details="$(codesign -dv --verbose=4 "$app_path" 2>&1)" ||
    fail "Could not inspect IPA signing metadata."
  signed_team="$(
    printf '%s\n' "$codesign_details" |
      awk -F= '/^TeamIdentifier=/{print $2; exit}'
  )"
  [ "$signed_team" = "$IOS_TEAM_ID" ] ||
    fail "IPA was signed by an unexpected team."

  embedded_profile="$app_path/embedded.mobileprovision"
  [ -f "$embedded_profile" ] ||
    fail "IPA does not contain an embedded provisioning profile."
  validate_profile "$embedded_profile" "$TEMP_DIR/embedded-profile.plist"
  embedded_uuid="$VALIDATED_PROFILE_UUID"
  [ "$embedded_uuid" = "$INSTALLED_PROFILE_UUID" ] ||
    fail "IPA embedded an unexpected provisioning profile."

  checksum="$(shasum -a 256 "$IOS_IPA" | awk '{print $1}')"
  summary="$IOS_OUTPUT_DIR/build-summary.txt"
  {
    printf 'platform=ios\n'
    printf 'applicationIdentifier=%s\n' "$APP_IDENTIFIER"
    printf 'version=%s\n' "$BUILD_VERSION"
    printf 'build=%s\n' "$BUILD_NUMBER"
    printf 'commit=%s\n' "$COMMIT_SHA"
    printf 'artifact=%s.ipa\n' "$STEM"
    printf 'sha256=%s\n' "$checksum"
    printf 'codesignVerified=true\n'
    printf 'adHocProfileVerified=true\n'
    printf 'landscapeOrientationVerified=true\n'
  } > "$summary"
  info "iOS IPA verified (bundle, version, landscape orientation, code signature, Ad Hoc profile)."
  info "iOS SHA-256: $checksum"
}

build_android() {
  info "Building Android APK..."
  run_unity_android
  verify_android_apk
}

build_ios() {
  info "Exporting the iOS Xcode project..."
  run_unity_ios_export
  archive_and_export_ios
  verify_ios_ipa
}

main() {
  parse_arguments "$@"
  resolve_project
  acquire_build_lock
  configure_paths
  assert_output_root_safe

  case "$TARGET" in
    android)
      assert_output_available "$ANDROID_OUTPUT_DIR" "Android"
      assert_output_available "$ANDROID_OUT_APK" "APK outs"
      ;;
    ios)
      assert_output_available "$IOS_OUTPUT_DIR" "iOS"
      assert_output_available "$IOS_OUT_IPA" "IPA outs"
      ;;
    both)
      assert_output_available "$ANDROID_OUTPUT_DIR" "Android"
      assert_output_available "$IOS_OUTPUT_DIR" "iOS"
      assert_output_available "$ANDROID_OUT_APK" "APK outs"
      assert_output_available "$IOS_OUT_IPA" "IPA outs"
      ;;
  esac

  preflight_common
  TEMP_DIR="$(mktemp -d "$TEMP_BASE/dreamsquad-mobile.XXXXXX")"
  chmod 700 "$TEMP_DIR"
  capture_serialization_baseline

  case "$TARGET" in
    android)
      preflight_android
      ;;
    ios)
      preflight_ios
      ;;
    both)
      preflight_android
      preflight_ios
      ;;
  esac

  reserve_output_directories

  case "$TARGET" in
    android)
      build_android
      ;;
    ios)
      build_ios
      ;;
    both)
      build_android
      build_ios
      ;;
  esac

  is_worktree_clean ||
    fail "The build changed the Git worktree."

  publish_verified_artifacts

  case "$TARGET" in
    android)
      info "APK: Builds/Mobile/$STEM/Android/$STEM.apk"
      info "APK copy: Builds/outs/$OUT_STEM.apk"
      ;;
    ios)
      info "IPA: Builds/Mobile/$STEM/iOS/$STEM.ipa"
      info "IPA copy: Builds/outs/$OUT_STEM.ipa"
      ;;
    both)
      info "APK: Builds/Mobile/$STEM/Android/$STEM.apk"
      info "IPA: Builds/Mobile/$STEM/iOS/$STEM.ipa"
      info "APK copy: Builds/outs/$OUT_STEM.apk"
      info "IPA copy: Builds/outs/$OUT_STEM.ipa"
      ;;
  esac

  info "Build completed. Firebase upload remains a manual step."
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  main "$@"
fi
