#!/usr/bin/env bash
# 切换 Claude Code 上游模型：同步代理与两个配置文件 -> 重启代理 -> 实测连通
# 用法: switch-model.sh <model-id>
set -euo pipefail

MODEL="${1:?用法: switch-model.sh <model-id>，例如 mimo-v2.6-flash}"

# --- 路径 ---
PROXY="$HOME/claude-proxy.js"
SETTINGS="$HOME/.claude/settings.json"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WS="$(cd "$SCRIPT_DIR/../../../.." && pwd)"   # <ws>/.trae/skills/switch-claude-model/scripts -> <ws>
OPENCODE="$WS/.claude/opencode.json"

# --- 1. 校验模型名格式（防注入）与目标文件 ---
if ! [[ "$MODEL" =~ ^[a-zA-Z0-9._-]+$ ]]; then
  echo "错误: 模型名只允许字母、数字、点、下划线、连字符: $MODEL" >&2
  exit 1
fi
for f in "$PROXY" "$SETTINGS" "$OPENCODE"; do
  [ -f "$f" ] || { echo "错误: 文件不存在: $f" >&2; exit 1; }
done

# --- 2. 确认模型存在于上游模型列表 ---
# 本地 DNS 会污染 opencode.ai（返回 302 到 m.baidu.com），必须绕过。
# 优先用公共 DNS 取真实 IP，失败则回退到已验证的 Cloudflare IP。
API_KEY="$(python3 -c "import json;print(json.load(open('$SETTINGS'))['env']['ANTHROPIC_API_KEY'])")"
UPSTREAM_IP="$(timeout 6 dig +short @8.8.8.8 opencode.ai 2>/dev/null | grep -Eo '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | head -1)"
UPSTREAM_IP="${UPSTREAM_IP:-172.65.90.20}"
echo "[1/5] 校验上游模型列表 (opencode.ai -> $UPSTREAM_IP)..."
MODELS_JSON="$(curl -sk -m 15 --resolve "opencode.ai:443:$UPSTREAM_IP" \
  "https://opencode.ai/zen/go/v1/models" -H "Authorization: Bearer $API_KEY")" \
  || { echo "错误: 无法获取上游模型列表" >&2; exit 1; }
if ! MODELS_JSON="$MODELS_JSON" TARGET="$MODEL" python3 -c "
import json, os
data = json.loads(os.environ['MODELS_JSON'])
ids = [m['id'] for m in data['data']]
target = os.environ['TARGET']
if target not in ids:
    print('可用模型:', ', '.join(ids))
    raise SystemExit(1)
"; then
  echo "错误: 上游模型列表中不存在 $MODEL" >&2
  exit 1
fi
echo "      $MODEL 存在"

# --- 3. 同步三个位置 ---
echo "[2/5] 更新配置文件..."
python3 - "$SETTINGS" "$MODEL" <<'PY'
import json, sys
path, model = sys.argv[1], sys.argv[2]
data = json.load(open(path))
data["env"]["ANTHROPIC_MODEL"] = model
data["env"]["ANTHROPIC_DEFAULT_SONNET_MODEL"] = model
with open(path, "w") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)
    f.write("\n")
PY
python3 - "$OPENCODE" "$MODEL" <<'PY'
import json, sys
path, model = sys.argv[1], sys.argv[2]
data = json.load(open(path))
data["env"]["ANTHROPIC_MODEL"] = model
data["env"]["ANTHROPIC_DEFAULT_SONNET_MODEL"] = model
with open(path, "w") as f:
    json.dump(data, f, indent=2, ensure_ascii=False)
    f.write("\n")
PY
python3 - "$PROXY" "$MODEL" <<'PY'
import re, sys
path, model = sys.argv[1], sys.argv[2]
txt = open(path).read()
txt, n1 = re.subn(
    r"^const DEFAULT_TARGET_MODEL = '[^']*';",
    f"const DEFAULT_TARGET_MODEL = '{model}';", txt, count=1, flags=re.M)
txt, n2 = re.subn(
    r"^// 所有请求统一映射到 [^（]*（无视 Claude Code 请求的 opus/sonnet/haiku）",
    f"// 所有请求统一映射到 {model}（无视 Claude Code 请求的 opus/sonnet/haiku）",
    txt, count=1, flags=re.M)
assert n1 == 1, "DEFAULT_TARGET_MODEL 行未找到"
open(path, "w").write(txt)
PY
grep -q "^const DEFAULT_TARGET_MODEL = '$MODEL';$" "$PROXY" || { echo "错误: 代理文件替换失败" >&2; exit 1; }
echo "      三个位置已同步"

# --- 4. 杀掉所有旧代理（防止重复进程争用端口），重新启动 ---
# 锚定 ^node 只匹配真正以 node 开头的进程；用宽泛模式会误杀调用本脚本的包装 shell。
proxy_pids() {
  pgrep -af '^node ' 2>/dev/null | grep -F 'claude-proxy.js' | cut -d' ' -f1
}
echo "[3/5] 重启代理..."
OLD_PIDS="$(proxy_pids || true)"
if [ -n "$OLD_PIDS" ]; then
  echo "$OLD_PIDS" | xargs kill 2>/dev/null || true
  sleep 1
  REMAIN="$(proxy_pids || true)"
  if [ -n "$REMAIN" ]; then
    echo "$REMAIN" | xargs kill -9 2>/dev/null || true
    sleep 1
  fi
fi
cd "$HOME"
nohup node "$PROXY" > /tmp/claude-proxy.log 2>&1 &
sleep 2
NEW_PID="$(proxy_pids || true)"
[ -n "$NEW_PID" ] || { echo "错误: 代理启动失败，日志:" >&2; cat /tmp/claude-proxy.log >&2; exit 1; }
echo "      pid=$NEW_PID"

# --- 5. 确认端口归属与启动日志 ---
echo "[4/5] 检查端口与模型映射..."
if [ "$(echo "$NEW_PID" | wc -l)" -ne 1 ]; then
  echo "错误: 仍有多个代理进程: $NEW_PID" >&2
  exit 1
fi
ss -ltn 2>/dev/null | grep -q "127.0.0.1:8123" || { echo "错误: 8123 端口未监听" >&2; exit 1; }
grep -a -q "model rewrite: ALL -> $MODEL" /tmp/claude-proxy.log \
  || { echo "错误: 代理日志未确认模型映射:" >&2; grep -a "model rewrite" /tmp/claude-proxy.log >&2; exit 1; }
echo "      8123 监听中，映射 -> $MODEL"

# --- 6. 真实问答闭环 ---
echo "[5/5] claude 问答闭环测试（最多 170s）..."
cd "$WS"
# claude -p 会 resume 上次会话，回复可能不含 "OK"，所以只判断非空且不含错误关键词
ANSWER="$(timeout 170 claude --dangerously-skip-permissions -p "Reply with exactly: OK" 2>/dev/null || true)"
if [ -n "$ANSWER" ] && ! printf '%s' "$ANSWER" | grep -qi "error\|失败\|failed"; then
  echo "      通过: claude 返回非空响应（前80字: $(printf '%s' "$ANSWER" | head -c 80)...）"
  echo "完成: 当前模型 $MODEL"
else
  echo "错误: 问答测试失败，实际输出: $ANSWER" >&2
  exit 1
fi
