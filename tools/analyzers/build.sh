#!/usr/bin/env bash
# Story 001 · AC-3-A4② + Story 007 · AC-3-B2② —— 输入侧两个分析器构建脚本(可重跑)
#
# 产出(各一份,均在 unity/Assets/Editor.Tools.Analyzers/):
#   LegacyInputAnalyzer.dll    —— Story 001 DY0001:全 Assets 禁 UnityEngine.Input(工程级)
#   UiEventSymbolAnalyzer.dll  —— Story 007 DY0002:直读程序集源内禁 UI 事件符号(作用域级)
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
OUT_DIR="$REPO_ROOT/unity/Assets/Editor.Tools.Analyzers"

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

echo "== 工具链 =="
echo "   Unity Data : $DATA"
echo "   框架引用   : $APP_DIR"
echo "   CodeAnalysis: $ROSLYN/Microsoft.CodeAnalysis.dll"

sha256_of() {
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$1" | awk '{print $1}'
    else
        shasum -a 256 "$1" | awk '{print $1}'
    fi
}

# ── 构建单个分析器:<name> <src 相对本脚本目录> ──
# 产出 $OUT_DIR/<name>.dll + $SCRIPT_DIR/<name>.dll.sha256(一行两列:源 hash + DLL hash,
# 由 EditMode 测试比对新鲜度;源改了没重跑本脚本 ⇒ 源 hash 失配;DLL 被换 ⇒ DLL hash 失配)。
build_one() {
    local name="$1" src="$SCRIPT_DIR/$2"
    local out="$OUT_DIR/$name.dll"
    local sidecar="$SCRIPT_DIR/$name.dll.sha256"

    if [ ! -f "$src" ]; then
        echo "ERROR: 分析器源不存在:$src" >&2
        return 1
    fi

    echo "== $name 构建 =="
    echo "   源码       : $src"
    echo "   输出       : $out"

    "$DOTNET" exec "$CSC" \
        -nologo \
        -target:library \
        -nostdlib+ \
        -langversion:9.0 \
        -deterministic \
        -out:"$out" \
        @"$RSP" \
        "$src"

    echo "OK: $out ($(stat -c %s "$out" 2>/dev/null || stat -f %z "$out") bytes)"

    printf '%s  %s\n' "$(sha256_of "$src")" "$(sha256_of "$out")" > "$sidecar"
    echo "sidecar: $sidecar"
}

build_one LegacyInputAnalyzer   "LegacyInputAnalyzer/LegacyInputAnalyzer.cs"
build_one UiEventSymbolAnalyzer "UiEventSymbolAnalyzer/UiEventSymbolAnalyzer.cs"

echo "下一步(设 RoslynAnalyzer label + PluginImporter 校正,batch;两 DLL 一次处理):"
echo "  unity build <project> --target StandaloneLinux64 --executeMethod DaYiJingCheng.EditorTools.Gates.RoslynAnalyzerLabel.SetLabel"
