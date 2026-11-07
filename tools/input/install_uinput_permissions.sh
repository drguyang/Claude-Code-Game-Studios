#!/usr/bin/env bash
# 一次性授权:让 `gu` 能造 uinput 虚拟手柄,并让**真包**能读新建的 event 节点。(2026-11-07)
#
# 为什么需要它(实测事实,不是猜测):
#   · /dev/uinput          = root:root 0600  ⇒ 当前用户**造不出**虚拟设备
#   · /dev/input/eventN    = root:input 0660、无 ACL ⇒ 当前用户**读不到**任何手柄
#   · 键盘能读是因为 Unity 在图形会话下走 X11,不经 evdev —— 所以键鼠走查一直正常
#
# 本脚本只改两条 udev 规则(按**用户组**授权,不改 mode 到 0666、不动组成员):
#   ① uinput        → root:<user> 0660
#   ② event*        → root:<user> 0660
# 可逆:`sudo rm /etc/udev/rules.d/99-dyjc-input.rules && sudo udevadm control --reload-rules`
#
# 用法:
#   sudo bash tools/input/install_uinput_permissions.sh
set -euo pipefail

if [ "$(id -u)" != "0" ]; then
  echo "[install] 须 sudo 运行: sudo bash $0" >&2
  exit 1
fi

TARGET_USER="${SUDO_USER:-${1:-}}"
if [ -z "$TARGET_USER" ]; then
  echo "[install] 取不到目标用户 —— 用: sudo SUDO_USER=<name> bash $0" >&2
  exit 1
fi
TARGET_GROUP="$(id -gn "$TARGET_USER")"

echo "[install] 目标用户 = $TARGET_USER (组 $TARGET_GROUP)"

RULES=/etc/udev/rules.d/99-dyjc-input.rules
cat > "$RULES" <<EOF
# 大医精诚 · 真包手柄注入(2026-11-07,tools/input/install_uinput_permissions.sh 生成)
# ① 注入器造虚拟手柄
KERNEL=="uinput", SUBSYSTEM=="misc", MODE="0660", GROUP="$TARGET_GROUP"
# ② 游戏读虚拟手柄(以及真手柄)
SUBSYSTEM=="input", KERNEL=="event*", MODE="0660", GROUP="$TARGET_GROUP"
EOF
echo "[install] 写入 $RULES"

udevadm control --reload-rules
# `--action=change` 才能对**已存在**的节点重刷权限
udevadm trigger --action=change --subsystem-match=misc
udevadm trigger --action=change --subsystem-match=input
sleep 1

rc=0
echo "--- 校验 ---"
if runuser -u "$TARGET_USER" -- test -w /dev/uinput; then
  echo "✅ /dev/uinput 可写"
else
  echo "❌ /dev/uinput 仍不可写(ls -l /dev/uinput)" >&2
  rc=1
fi

NODE="$(ls /dev/input/event* 2>/dev/null | head -1 || true)"
if [ -n "$NODE" ] && runuser -u "$TARGET_USER" -- test -r "$NODE"; then
  echo "✅ $NODE 可读(现有 event 节点)"
else
  echo "❌ 现有 event 节点仍不可读($NODE)" >&2
  rc=1
fi

echo
echo "下一步:"
echo "  python3 tools/input/vpad.py --info        # 应打印 [vpad] node=/dev/input/eventN"
echo "撤销: sudo rm $RULES && sudo udevadm control --reload-rules"
exit "$rc"
