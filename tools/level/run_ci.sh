#!/usr/bin/env bash
# 关卡工具 CI —— 一条命令跑完 C1–C6 一致性检查(ADR-022 §四 / story-006 AC-4)。
#
# 用法:
#   bash tools/level/run_ci.sh
#   UNITY_EDITOR=/path/to/Editor/Unity bash tools/level/run_ci.sh
#
# 退出码(**三态,与 LevelCiRunner 同一约定**):
#   0 = 无硬失败(告警 / 未跑项可以有)
#   1 = 有硬失败(C1–C6 的 throw 项)
#   2 = 检查链跑不起来(关卡源构造失败 / 未捕获异常 / Unity 起不来)
#
# ⚠️ **Unity batch 需独占工程** —— 编辑器开着时 Unity 拿不到锁,会以**别的码**退出。
#    本脚本在开跑前查 `unity/Temp/UnityLockfile`,命中即**直接退 2** 并说明原因:
#    「门没跑」与「门跑了且红」在 CI 日志里必须可区分,否则红绿都失去意义。
#
# ⚠️ **退出码可能被外层 shell 吞掉**:若以 `bash run_ci.sh | tee log` 的形式跑,
#    `$?` 是 tee 的码。要保留真实退出码,用 `set -o pipefail` 或 `bash run_ci.sh > log 2>&1`。

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/unity"
LOG_DIR="$PROJECT/Logs"
LOG="$LOG_DIR/level-ci.log"

# ── 定位 Unity 编辑器本体 ──
# ⚠️ 必须用编辑器本体(`.../Editor/Unity`),`.local/bin/unity` 是 CLI 包装器、不认 `-batchmode`。
UNITY="${UNITY_EDITOR:-}"
if [ -z "$UNITY" ]; then
    for c in "$HOME/Unity/Hub/Editor/"*/Editor/Unity; do
        [ -x "$c" ] && UNITY="$c"
    done
fi
if [ -z "$UNITY" ] || [ ! -x "$UNITY" ]; then
    echo "[level-ci] 找不到 Unity 编辑器本体(设置 UNITY_EDITOR=... 或装到 ~/Unity/Hub/Editor/)"
    exit 2
fi

# ── 独占检查:编辑器开着就不跑 ──
if [ -f "$PROJECT/Temp/UnityLockfile" ]; then
    echo "[level-ci] unity/Temp/UnityLockfile 存在 —— 编辑器正在占用工程,batch 无法取锁。"
    echo "[level-ci] 请先关闭编辑器再跑。**本次未执行**(退出码 2,不是 1)。"
    exit 2
fi

mkdir -p "$LOG_DIR"

echo "[level-ci] 编辑器: $UNITY"
echo "[level-ci] 工程:   $PROJECT"
echo "[level-ci] 日志:   $LOG"

cd "$PROJECT" || { echo "[level-ci] cd 失败"; exit 2; }

# `-executeMethod` **必须**带 `-quit`(CLAUDE.md 的 CLI 纪律:与 `-runTests` 相反)。
# `-nographics`:一致性检查不碰渲染,也不需要 GPU —— CI runner 上没有 GPU。
"$UNITY" \
    -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -executeMethod DaYiJingCheng.EditorTools.Level.LevelCiRunner.RunBatch \
    -logFile "$LOG"
CODE=$?

if [ $CODE -eq 0 ]; then
    echo "[level-ci] ✓ 通过(退出码 0)"
elif [ $CODE -eq 1 ]; then
    echo "[level-ci] ✗ 硬失败(退出码 1)—— 逐条见日志:"
    grep -E "^\s+· |✗|⚠|○" "$LOG" | tail -40
else
    echo "[level-ci] ? 检查链未能跑起来(退出码 $CODE)。日志尾部:"
    tail -30 "$LOG"
fi

exit $CODE
