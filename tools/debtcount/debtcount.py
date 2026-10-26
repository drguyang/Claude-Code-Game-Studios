#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""debtcount —— 债册 §五「当前预期输出」块直生成器。

**为什么存在**(2026-10-27 四十一定 · 用户裁定):
    §五 的输出块原来是**手抄**的。实测它自三十五订起**停更三轮** ——
    三十六 / 三十七 / 三十八订各自改了计数,**却没人刷它**。读者跑 §五 命令得
    `条目 123`、块里写 `条目 111`,第一反应是「命令坏了 / 文件被改过」,而不是
    「这个块过期了」。它**不会自己红**:没有任何机械门读它 ⇒ 失效是**静默**的。

    手工纪律已连续三次被跳过,故改为**脚本直生成**:块内容由本脚本从索引块算出并
    就地重写。**跑一次 = 块一定是最新的**,没有「忘了刷」这条失败路径。

**输入**:`docs/tech-debt-register.md` 的 `<!-- TD-INDEX:BEGIN -->`…`END` 区间。
**输出**:同文件内 `<!-- TD-COUNT:BEGIN -->`…`<!-- TD-COUNT:END -->` 之间的 fenced
          ```text 块(整段替换)。

**用法**:
    python3 tools/debtcount/debtcount.py            # 就地重写块
    python3 tools/debtcount/debtcount.py --check    # 只校验,块过期 ⇒ 非零退出(CI 可用)
    python3 tools/debtcount/debtcount.py --print    # 只打印,不写盘

⚠️ **`--check` 是「某件有门」的判据载体** —— 它必须能红。红法见 §自证。
⚠️ **本脚本只写 `TD-COUNT` 区间**,不碰索引块、不碰订次注、不碰任何正文。
"""

from __future__ import annotations

import argparse
import collections
import re
import sys
from pathlib import Path

REGISTER = Path("docs/tech-debt-register.md")

INDEX_RE = re.compile(r"<!-- TD-INDEX:BEGIN -->(.*?)<!-- TD-INDEX:END -->", re.S)
COUNT_RE = re.compile(r"(<!-- TD-COUNT:BEGIN -->\n)(.*?)(<!-- TD-COUNT:END -->)", re.S)

# 31 个 P0 epic —— 与 §一 R1/R2/R3/R4 的判据口径同源。
# ⚠️ 这里**不硬编码 31 个名字**:脚本只需要「分母是 31」这个事实,
#    而 epic 名单本身归 production/epics/ 的真源。硬编码会在增删 epic 时静默失配。
P0_EPIC_TOTAL = 31


def fatal(msg: str) -> "NoReturn":  # noqa: F821
    """结构损坏的统一出口 —— **退出码 2**(区别于「块过期」的 1)。

    ⚠️ 不能写成 `raise SystemExit("字符串")`:Python 对非整数参数取退出码 **1**,
    会与「块过期」撞码,使调用方无法区分「刷一下就好」与「文件结构坏了」。
    2026-10-27 实测踩过一次(M-DC3 得 1,M-DC2 得 2)。
    """
    print(msg, file=sys.stderr)
    raise SystemExit(2)


def parse_rows(text: str) -> list[list[str]]:
    """从索引块切出数据行。只认以 `| \\`TD-` 开头的行(表头 / 分隔行天然不匹配)。"""
    blk = INDEX_RE.search(text)
    if blk is None:
        fatal(
            "FATAL: 找不到 `<!-- TD-INDEX:BEGIN -->`…`<!-- TD-INDEX:END -->` 区间。\n"
            "       块边界被删 / 改名 ⇒ 本脚本无法求值。**拒绝在无索引块的情况下报绿。**"
        )
    return [
        [c.strip() for c in line.strip().strip("|").split("|")]
        for line in blk.group(1).split("\n")
        if line.startswith("| `TD-")
    ]


def compute(rows: list[list[str]]) -> dict:
    """§五 的四个读数。口径必须与 §五 正文的命令**逐字一致**,否则两处会各说各话。"""
    if not rows:
        fatal(
            "FATAL: 索引块内 `| \\`TD-` 行数 = 0。\n"
            "       **空集不得冒充绿** —— 零条债在数学上会让「未闭 P0 面 = 0」成立。"
        )

    st = collections.Counter(r[2] for r in rows)
    p = collections.Counter(r[3].strip("*") for r in rows)

    # M3 Exit ② 的求值对象 = **未闭**的 P0 面债。
    # 已清偿的债不再构成出口障碍 —— 否则该条在任何情况下都不可能为 0。
    live = [r for r in rows if r[2] != "closed" and r[3].strip("*") == "Y"]
    ep = collections.Counter(r[1].strip("`") for r in live)
    closed_y = sum(1 for r in rows if r[2] == "closed" and r[3].strip("*") == "Y")

    return {
        "total": len(rows),
        "status": dict(st),
        "p0": dict(p),
        "live": len(live),
        "closed_y": closed_y,
        "epics": dict(ep),
        "epic_n": len(ep),
    }


def render(d: dict) -> str:
    """生成块内容(含 fenced 代码块)。格式与 §五 的历史手抄形态**保持一致**,
    使既有读者不必重新学怎么读。"""
    ep = d["epics"]
    # epic 行按数量降序、名字升序;每行最多 3 个,列宽对齐 —— 只为可读,不承载语义。
    items = sorted(ep.items(), key=lambda kv: (-kv[1], kv[0]))
    lines = []
    for i in range(0, len(items), 3):
        chunk = items[i : i + 3]
        lines.append("    " + "".join(f"{k}: {v}".ljust(26) for k, v in chunk).rstrip())

    gate = "真" if d["live"] == 0 else (
        f"假 —— {d['live']} 条,分布在 {d['epic_n']}/{P0_EPIC_TOTAL} 个 epic"
    )

    body = [
        "```text",
        f"条目 {d['total']} | 状态 {d['status']} | P0 面 {d['p0']}",
        f"未闭 P0 面 = {d['live']} （已闭但曾标 P0 面的 {d['closed_y']} 条不计）",
        f"M3 Exit 第二条合取（{P0_EPIC_TOTAL} 个 epic,每个名下「未闭 TD 的 P0 面 = 0」）⇒ {gate}",
        *lines,
        "```",
        "",
    ]
    return "\n".join(body)


def main() -> int:
    ap = argparse.ArgumentParser(description="债册 §五 输出块直生成器")
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--check", action="store_true", help="只校验;块过期 ⇒ 退出码 1")
    g.add_argument("--print", dest="do_print", action="store_true", help="只打印,不写盘")
    args = ap.parse_args()

    if not REGISTER.exists():
        fatal(f"FATAL: 找不到 {REGISTER}(cwd = {Path.cwd()})")

    text = REGISTER.read_text(encoding="utf-8")
    fresh = render(compute(parse_rows(text)))

    m = COUNT_RE.search(text)
    if m is None:
        fatal(
            "FATAL: 找不到 `<!-- TD-COUNT:BEGIN -->`…`<!-- TD-COUNT:END -->` 哨兵。\n"
            "       **哨兵缺失 ≠ 块正确** —— 本脚本拒绝在无处可写时报成功。"
        )

    current = m.group(2)
    if args.do_print:
        sys.stdout.write(fresh)
        return 0

    if args.check:
        if current.strip() == fresh.strip():
            print("✅ §五 输出块与索引块一致")
            return 0
        print("❌ §五 输出块已过期 —— 跑 `python3 tools/debtcount/debtcount.py` 重生成", file=sys.stderr)
        print("--- 块内现状 ---", file=sys.stderr)
        sys.stderr.write(current)
        print("--- 应为 ---", file=sys.stderr)
        sys.stderr.write(fresh)
        return 1

    if current.strip() == fresh.strip():
        print("✅ 已是最新,未写盘")
        return 0

    REGISTER.write_text(text[: m.start(2)] + fresh + text[m.end(2) :], encoding="utf-8")
    print("✅ §五 输出块已重生成")
    sys.stdout.write(fresh)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
