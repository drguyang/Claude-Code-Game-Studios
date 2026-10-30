#!/usr/bin/env bash
# 53 三表烘焙 CI —— 一条命令烘完 outcome_table / delay_table / cost_table(`TD-medcons-11`)。
#
# 用法:
#   bash tools/bake/run_ci.sh
#   UNITY_EDITOR=/path/to/Editor/Unity bash tools/bake/run_ci.sh
#
# 退出码(**三态**,与 `LevelCiRunner` / `MedConsTableBakeRunner` 同一约定):
#   0 = 三张表全部烘焙成功
#   1 = 有硬失败(某张表被 binder 拒收 / 写盘失败)
#   2 = 烘焙链跑不起来(Unity 起不来 / 工程被占用 / 未捕获异常)
#
# ⚠️ **为什么要这个脚本,而不是直接写三条 `-executeMethod`**:
#   Unity batchmode **只执行第一条** `-executeMethod`,其余**静默丢弃且退出码仍 0**
#   (`TD-ci-02` 的六格实测)。⇒ 「一次烘三张」必须由**单个**入口内部循环
#   (`MedConsTableBakeRunner.RunBatch`),否则第二、三张永远不会被烘,而 CI 报绿。
#
# ⚠️ **Unity batch 需独占工程** —— 编辑器开着时拿不到锁,会以**别的码**退出。
#   本脚本开跑前查 `unity/Temp/UnityLockfile`,命中即**直接退 2**:「门没跑」与
#   「门跑了且红」在 CI 日志里必须可区分。
#
# ⚠️ **退出码可能被外层 shell 吞掉**:若以 `bash run_ci.sh | tee log` 的形式跑,
#   `$?` 是 tee 的码。要保留真实退出码,用 `set -o pipefail` 或重定向到文件。

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/unity"
LOG_DIR="$PROJECT/Logs"
LOG="$LOG_DIR/bake-53-tables.log"

# ── 定位 Unity 编辑器本体 ──
# ⚠️ 必须用编辑器本体(`.../Editor/Unity`);`.local/bin/unity` 是 CLI 包装器、不认 `-batchmode`。
UNITY="${UNITY_EDITOR:-}"
if [ -z "$UNITY" ]; then
    for c in "$HOME/Unity/Hub/Editor/"*/Editor/Unity; do
        [ -x "$c" ] && UNITY="$c"
    done
fi
if [ -z "$UNITY" ] || [ ! -x "$UNITY" ]; then
    echo "[bake-53] 找不到 Unity 编辑器本体(设置 UNITY_EDITOR=... 或装到 ~/Unity/Hub/Editor/)"
    exit 2
fi

if [ -f "$PROJECT/Temp/UnityLockfile" ]; then
    echo "[bake-53] unity/Temp/UnityLockfile 存在 —— 编辑器正在占用工程,batch 无法取锁。"
    echo "[bake-53] 请先关闭编辑器再跑。**本次未执行**(退出码 2,不是 1)。"
    exit 2
fi

mkdir -p "$LOG_DIR"

echo "[bake-53] 编辑器: $UNITY"
echo "[bake-53] 工程:   $PROJECT"
echo "[bake-53] 日志:   $LOG"

cd "$PROJECT" || { echo "[bake-53] cd 失败"; exit 2; }

# `-executeMethod` **必须**带 `-quit`(与 `-runTests` 相反)。
# `-nographics`:烘焙不碰渲染,CI runner 上没有 GPU。
"$UNITY" \
    -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -executeMethod DaYiJingCheng.EditorTools.Bake.MedConsTableBakeRunner.RunBatch \
    -logFile "$LOG"
CODE=$?

if [ $CODE -eq 0 ]; then
    echo "[bake-53] ✓ 三张表全部烘焙成功(退出码 0)"
elif [ $CODE -eq 1 ]; then
    echo "[bake-53] ✗ 烘焙硬失败(退出码 1)—— 逐条见日志:"
    grep -E "^\s*✗|烘焙中止于|磁盘中间态" "$LOG" | tail -20
else
    echo "[bake-53] ✗ 烘焙链跑不起来(退出码 $CODE,期望 0/1/2)—— 见日志尾部:"
    tail -20 "$LOG"
fi

exit $CODE
