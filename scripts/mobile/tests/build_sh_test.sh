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

printf 'build_sh_test=pass\n'
