#!/usr/bin/env bash
# Story 001 · AC-3-A4② —— Legacy Input 分析器构建脚本(可重跑)
#
# 产出:unity/Assets/Editor.Tools.Analyzers/LegacyInputAnalyzer.dll
# 工具链:Unity 捆绑 dotnet(NetCoreRuntime)+ csc(DotNetSdkRoslyn)+
#        Microsoft.CodeAnalysis(.CSharp).dll(同 DotNetSdkRoslyn,4.3.x)
#   —— 只对捆绑 CodeAnalysis 编译(NuGet 4.8+ 会被 Unity 宿主静默不加载,unity-specialist 预检①)。
#
# 用法:
#   bash tools/analyzers/build.sh
#   UNITY_EDITOR_DATA=/path/to/Editor/Data bash tools/analyzers/build.sh   # 显式指定 Unity Editor Data
#
# 目标框架说明(偏差登记):原预检要求 netstandard2.0,但本机无 NuGet、无 NETStandard.Library.Ref
#   2.0 参考包,且 System.Collections.Immutable 不在 netstandard API 面(分析器签名强依赖它)——
#   netstandard 目标在本环境不可行。改为 net6.0 目标,对齐捆绑运行时(NetCoreRuntime 6.0.x)
#   与捆绑 csc 的宿主运行时;Unity 内 Roslyn 宿主若为 net6+,加载无碍,最终以 batch 实测为准。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC="$SCRIPT_DIR/LegacyInputAnalyzer/LegacyInputAnalyzer.cs"
OUT_DIR="$REPO_ROOT/unity/Assets/Editor.Tools.Analyzers"
OUT="$OUT_DIR/LegacyInputAnalyzer.dll"

# ── 定位 Unity Editor Data ──
DATA="${UNITY_EDITOR_DATA:-}"
if [ -z "$DATA" ]; then
    CANDIDATES=(
        "/XYFS01/sysu_tyu2_2/unity-editor-install/Editor/Data"
        "$HOME/Unity/Hub/Editor/"*/Editor/Data
        "/opt/unity/Editor/Data"
        "$HOME/.local/share/Unity/Editor/Data"
    )
    for c in "${CANDIDATES[@]}"; do
        if [ -f "$c/DotNetSdkRoslyn/csc.dll" ]; then
            DATA="$c"
            break
        fi
    done
fi
if [ -z "$DATA" ] || [ ! -f "$DATA/DotNetSdkRoslyn/csc.dll" ]; then
    echo "ERROR: 找不到 Unity Editor Data(需 DotNetSdkRoslyn/csc.dll)。" >&2
    echo "       请设 UNITY_EDITOR_DATA=<...>/Editor/Data 后重跑。" >&2
    exit 1
fi

DOTNET="$DATA/NetCoreRuntime/dotnet"
[ -x "$DOTNET" ] || DOTNET="$DATA/NetCoreRuntime/dotnet.exe"
if [ ! -e "$DOTNET" ]; then
    echo "ERROR: 找不到捆绑 dotnet:$DATA/NetCoreRuntime/dotnet(.exe)" >&2
    exit 1
fi

CSC="$DATA/DotNetSdkRoslyn/csc.dll"
ROSLYN="$DATA/DotNetSdkRoslyn"

# 框架引用:NetCoreRuntime/shared/Microsoft.NETCore.App/<最高版本>/*.dll(与 csc 宿主同运行时)
APP_DIR="$(ls -d "$DATA"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ 2>/dev/null | sort -V | tail -1)"
if [ -z "$APP_DIR" ]; then
    echo "ERROR: 找不到 shared/Microsoft.NETCore.App/<ver>:$DATA/NetCoreRuntime" >&2
    exit 1
fi

mkdir -p "$OUT_DIR"
RSP="$(mktemp)"
trap 'rm -f "$RSP"' EXIT

{
    for dll in "$APP_DIR"/*.dll; do
        printf -- '-r:"%s"\n' "$dll"
    done
    printf -- '-r:"%s"\n' "$ROSLYN/Microsoft.CodeAnalysis.dll"
    printf -- '-r:"%s"\n' "$ROSLYN/Microsoft.CodeAnalysis.CSharp.dll"
} > "$RSP"

echo "== LegacyInputAnalyzer 构建 =="
echo "   Unity Data : $DATA"
echo "   框架引用   : $APP_DIR"
echo "   CodeAnalysis: $ROSLYN/Microsoft.CodeAnalysis.dll"
echo "   输出       : $OUT"

"$DOTNET" exec "$CSC" \
    -nologo \
    -target:library \
    -nostdlib+ \
    -langversion:9.0 \
    -deterministic \
    -out:"$OUT" \
    @"$RSP" \
    "$SRC"

echo "OK: $OUT ($(stat -c %s "$OUT" 2>/dev/null || stat -f %z "$OUT") bytes)"

# ── 新鲜度 sidecar(2026-09-25 复核 W2)──
# 一行两个 hash:源码 sha256 + DLL sha256,由 EditMode 测试比对。
# 源改了没重跑本脚本 ⇒ 源 hash 失配;DLL 被换/未重跑 ⇒ DLL hash 失配。两者皆红。
sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    else
        shasum -a 256 "$1" | awk '{print $1}'
    fi
}
SIDECAR="$SCRIPT_DIR/LegacyInputAnalyzer.dll.sha256"
printf '%s  %s\n' "$(sha256_of "$SRC")" "$(sha256_of "$OUT")" > "$SIDECAR"
echo "sidecar: $SIDECAR"

echo "下一步(设 RoslynAnalyzer label + PluginImporter 校正,batch):"
echo "  unity build <project> --target StandaloneLinux64 --executeMethod DaYiJingCheng.EditorTools.Gates.RoslynAnalyzerLabel.SetLabel"
