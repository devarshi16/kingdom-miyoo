#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
: "${KNL_IMPORT_NATIVE_DIR:?Set KNL_IMPORT_NATIVE_DIR to the private host codec build}"
CECIL="${KNL_IMPORT_CECIL:-/usr/lib/mono/gac/Mono.Cecil/0.11.0.0__0738eb9f132ed756/Mono.Cecil.dll}"
T="$(mktemp -d)"; trap 'rm -rf "$T"' EXIT
mcs -optimize+ -main:NativeImportRegression -out:"$T/import-tests.exe" -r:"$CECIL" engine/UnityEngine/Core/JsonParser.cs tools/native_import/managed/*.cs tests/NativeImportRegression.cs
LD_LIBRARY_PATH="$KNL_IMPORT_NATIVE_DIR" mono "$T/import-tests.exe"
