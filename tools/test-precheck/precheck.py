#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""新测试自检前置 —— 可复用脚本(sprint-13 S13-5 · 复盘行动项 4)。

为什么有这件:sprint-12 返工期把三类自检散落成一次性 `python -c` 片段
(`review-medcons-story-002-2026-10-15.md:375` X-2「括号改配平须编译期才可见」·
`fix-s11-12-...md:253` 的静态自检四项),**未固化为可复用步骤** ⇒ 下一个 story
又要重造。本脚本收成单一入口 + 「何时必跑」说明。

用法:
    python3 tools/test-precheck/precheck.py <文件...>      # 逐个自检
    python3 tools/test-precheck/precheck.py --dir <目录>    # 递归扫 *.cs / *.json
    python3 tools/test-precheck/precheck.py --json          # 机读输出

退出码:0 = 全过;1 = 有 FAIL(CI 可直接用)。

⚠️ 本脚本是**前置粗筛**,不是替代编译/测试 —— 它抓的是「编译期才可见」的低级错误
(X-2 类括号失配、JSON 词法失败、断言与消息自相矛盾),让它们在上 batch 之前就红。

何时必跑(承返工期实测):
  ① 手改 / 新增测试件后、跑 batch 之前 —— 括号失配会让**整条编译链红**(X-2 实测),
     一次 batch 编译 ~6 s + 域重载,前置 0.1 s 的配平检查省一整轮。
  ② 手改 JSON 数据表后、烘焙之前 —— JSON 词法失败在阶段 1 才报,前置可当场定位。
  ③ 断言消息与断言值不自洽 —— 返工期「断言自相矛盾」实测过(fix-s11-14 类)。

自检项(与返工期四项 + QA 计划三项对齐):
  [1] 括号配平 { } ( ) [ ]      —— C# 字符串/注释感知(不把字面量里的括号算进去)
  [2] JSON 合法性               —— .json 文件(或 --json-literal 片段)
  [3] 断言自相矛盾              —— 消息里的数字与断言值/实参不一致
  [4] [Test] 计数               —— 报告用(非判据)
  [5] 悬空引用                  —— 文件内引用的类型名在同仓其它文件无定义(粗筛,告警级)
"""

import argparse
import json
import os
import re
import sys


# ══════════════════════════════════════════════════════════════════════
# [1] 括号配平 —— C# 感知(剥离字符串 / 逐字串 / 字符 / 行注释 / 块注释)
# ══════════════════════════════════════════════════════════════════════

def strip_cs_noise(src):
    """把 C# 源码里的字符串 / 字符字面量 / 注释替换为等长空白(保留换行与列位)。

    返回 (clean, n_lines)。这样括号计数只统计**代码位**的括号 ——
    字面量里的 `{` / `)` 不再污染配平(返工期若用朴素 count 会误报)。
    """
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        # 行注释
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                out.append(' ')
                i += 1
            continue
        # 块注释
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            out.append('  ')
            i += 2
            while i < n and not (src[i] == '*' and i + 1 < n and src[i + 1] == '/'):
                out.append('\n' if src[i] == '\n' else ' ')
                i += 1
            if i < n:
                out.append('  ')
                i += 2
            continue
        # 逐字字符串 @"..." ("" 是转义引号)
        if c == '@' and i + 1 < n and src[i + 1] == '"':
            out.append('  ')
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        out.append('  ')
                        i += 2
                        continue
                    out.append(' ')
                    i += 1
                    break
                out.append('\n' if src[i] == '\n' else ' ')
                i += 1
            continue
        # 插值字符串 $"...{expr}..." —— 简化处理:整段按字符串剥离(表达式内括号不单独算,
        # 因为表达式本身通常配平;真失配由编译抓)。$@"..." / @$"..." 同处理。
        if c == '$':
            j = i + 1
            if j < n and src[j] == '@':
                j += 1
            if j < n and src[j] == '"':
                out.append(' ' * (j - i + 1))
                i = j + 1
                while i < n:
                    if src[i] == '\\':
                        out.append('  ')
                        i += 2
                        continue
                    if src[i] == '"':
                        out.append(' ')
                        i += 1
                        break
                    out.append('\n' if src[i] == '\n' else ' ')
                    i += 1
                continue
        # 普通字符串 "..."
        if c == '"':
            out.append(' ')
            i += 1
            while i < n:
                if src[i] == '\\':
                    out.append('  ')
                    i += 2
                    continue
                if src[i] == '"':
                    out.append(' ')
                    i += 1
                    break
                out.append('\n' if src[i] == '\n' else ' ')
                i += 1
            continue
        # 字符字面量 '...'
        if c == "'":
            out.append(' ')
            i += 1
            while i < n:
                if src[i] == '\\':
                    out.append('  ')
                    i += 2
                    continue
                if src[i] == "'":
                    out.append(' ')
                    i += 1
                    break
                out.append(' ')
                i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def check_brackets(path, src):
    """返回 (ok, detail)。按行栈式匹配,报首个失配的行列。"""
    clean = strip_cs_noise(src)
    pairs = {')': '(', ']': '[', '}': '{'}
    opens = set('([{')
    stack = []
    line = 1
    for idx, ch in enumerate(clean):
        if ch == '\n':
            line += 1
            continue
        if ch in opens:
            stack.append((ch, line))
        elif ch in pairs:
            if not stack:
                return False, f"第 {line} 行多出一个 '{ch}'(栈空)"
            top, tl = stack.pop()
            if top != pairs[ch]:
                return False, f"第 {line} 行 '{ch}' 与第 {tl} 行的 '{top}' 不匹配"
    if stack:
        ch, tl = stack[-1]
        return False, f"第 {tl} 行的 '{ch}' 未闭合(余 {len(stack)} 个未配对)"
    return True, "配平"


# ══════════════════════════════════════════════════════════════════════
# [2] JSON 合法性
# ══════════════════════════════════════════════════════════════════════

def check_json(path, src):
    try:
        json.loads(src)
        return True, "合法"
    except json.JSONDecodeError as e:
        return False, f"第 {e.lineno} 行第 {e.colno} 列:{e.msg}"


# ══════════════════════════════════════════════════════════════════════
# [3] 断言自相矛盾 —— 消息里的**明确期望措辞**与断言值不一致
# ══════════════════════════════════════════════════════════════════════

# ⚠️ 判据刻意**极保守**(2026-10-16 实测收紧):首版按「消息里出现任何 = N / 档 N」
#   判,对 `medcons_outcome_judge_test.cs` 误报 3 条、对本 story 新件误报 1 条 ——
#   全是假红(消息里的 `= 0` 指代的是**别的量**,如「漏发 = 0」,不是期望值)。
#   **假红比漏报更糟**(会让人整体忽略该检查)⇒ 只认**明确措辞**:「应为 N」「应等于 N」
#   「期望 N」「expected N」「恰 N 条/个/组」,且限定在**单条断言语句**内(括号配平截断,
#   不跨到相邻断言 —— 首版按固定 4 行收集,把下一条断言的数字也吞了)。

_EXPECT_WORDS = re.compile(r'(?:应为|应等于|期望|expected)\s*(-?\d+)|恰\s*(-?\d+)\s*[条个组]')


def _assert_statements(src):
    """逐条切出 `Assert.…( … )` 语句(括号配平截断;返回 (行号, 语句文本))。"""
    out = []
    for m in re.finditer(r'Assert\.\w+\s*\(', src):
        start = m.start()
        depth = 0
        i = m.end() - 1  # 指向 '('
        n = len(src)
        while i < n:
            c = src[i]
            if c == '(':
                depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    break
            elif c == '"':  # 跳过字符串,避免串内 ')' 提前收尾
                i += 1
                while i < n and src[i] != '"':
                    if src[i] == '\\':
                        i += 1
                    i += 1
            i += 1
        stmt = src[start:i + 1]
        line = src.count('\n', 0, start) + 1
        out.append((line, stmt))
    return out


def check_assertion_contradiction(path, src):
    """极保守粗筛:单条断言语句内,消息里**明确写出**的期望数字与断言首参不符时报告。"""
    findings = []
    clean = strip_cs_noise(src)   # 用去噪版切语句(字符串已空白化,不会再被串内括号干扰)
    for line, stmt in _assert_statements(clean):
        m = re.search(r'Assert\.AreEqual\s*\(\s*(-?\d+)\s*,', stmt)
        if not m:
            continue
        expected = int(m.group(1))
        # 只在**原始源码**的同区间取消息文本(去噪版把字符串抹了)
        raw = src.split('\n')
        # 消息文本:去噪版里字符串位置为空白 ⇒ 回到 raw 抓该行附近的引号串
        window = '\n'.join(raw[max(0, line - 2):line + 3])
        hits = [int(x) for tup in _EXPECT_WORDS.findall(window) for x in tup if x]
        if hits and expected not in hits:
            findings.append(
                f"第 {line} 行:断言期望值 {expected},而消息明确写出 {hits} —— "
                f"疑似自相矛盾(返工期 fix-s11-14 类)")
    return findings


# ══════════════════════════════════════════════════════════════════════
# [4] [Test] 计数(报告用)
# ══════════════════════════════════════════════════════════════════════

def count_tests(src):
    clean = strip_cs_noise(src)
    return len(re.findall(r'\[Test\]', clean)) + len(re.findall(r'\[TestCase', clean))


# ══════════════════════════════════════════════════════════════════════
# [5] 悬空引用 —— **默认关**(--dangling 开启)。粗筛,假报多:
#     把命名空间末段(`Consequences` / `Contracts`)误当类型名 ⇒ 2026-10-16 实测
#     对本 story 新件误报 6 个。保留为**显式开关**,供人工排查用,不参与默认判据。
# ══════════════════════════════════════════════════════════════════════

_NS_REF = re.compile(r'\bDaYiJingCheng(?:\.\w+)+')


def check_dangling(path, src, repo_root):
    """告警列表(粗筛,默认关):`DaYiJingCheng.*` 引用的**末段**在同仓无类型定义。
    ⚠️ 已知假报面:命名空间末段(`Consequences` / `Contracts` / `Sim`)会被当成类型名
    ⇒ 2026-10-16 实测误报。故**默认关闭**(--dangling 开启),仅供人工排查。"""
    names = {m.group(0).split('.')[-1] for m in _NS_REF.finditer(src)}
    if not names:
        return []
    found = set()
    full_root = os.path.join(repo_root, 'unity', 'Assets')
    if os.path.isdir(full_root):
        for dp, _, fs in os.walk(full_root):
            for f in fs:
                if not f.endswith('.cs'):
                    continue
                try:
                    t = open(os.path.join(dp, f), encoding='utf-8', errors='ignore').read()
                except OSError:
                    continue
                for nm in names:
                    if nm not in found and re.search(
                            r'\b(?:class|struct|interface|enum)\s+' + re.escape(nm) + r'\b', t):
                        found.add(nm)
    missing = sorted(names - found)
    return [f"引用末段名未在同仓找到类型定义(粗筛,含命名空间误报):{', '.join(missing)}"] if missing else []


# ══════════════════════════════════════════════════════════════════════
# 驱动
# ══════════════════════════════════════════════════════════════════════

def run_one(path, repo_root, dangling=False):
    try:
        src = open(path, encoding='utf-8').read()
    except OSError as e:
        return {'path': path, 'error': str(e), 'fails': [f"无法读取:{e}"], 'warns': [],
                'tests': 0}
    ext = os.path.splitext(path)[1].lower()
    fails, warns = [], []

    if ext == '.json':
        ok, det = check_json(path, src)
        if not ok:
            fails.append(f"[2] JSON:{det}")
        return {'path': path, 'error': None, 'fails': fails, 'warns': warns, 'tests': 0}

    # .cs
    ok, det = check_brackets(path, src)
    if not ok:
        fails.append(f"[1] 括号配平:{det}")
    contra = check_assertion_contradiction(path, src)
    for c in contra:
        fails.append(f"[3] 断言自相矛盾:{c}")
    n_tests = count_tests(src)
    if dangling and repo_root:
        for w in check_dangling(path, src, repo_root):
            warns.append(f"[5] {w}")
    return {'path': path, 'error': None, 'fails': fails, 'warns': warns, 'tests': n_tests}


def main():
    ap = argparse.ArgumentParser(description="新测试自检前置(sprint-13 S13-5)")
    ap.add_argument('files', nargs='*', help="待检文件(.cs / .json)")
    ap.add_argument('--dir', help="递归扫该目录下 *.cs / *.json")
    ap.add_argument('--json', action='store_true', help="机读输出")
    ap.add_argument('--dangling', action='store_true',
                    help="启用 [5] 悬空引用粗筛(默认关 —— 假报多,仅人工排查用)")
    ap.add_argument('--repo-root', default=None, help="仓根(悬空引用粗筛用;默认自动探测)")
    args = ap.parse_args()

    repo_root = args.repo_root
    if repo_root is None:
        # 脚本位于 <repo>/tools/test-precheck/ ⇒ 上溯两级
        repo_root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

    targets = list(args.files)
    if args.dir:
        for dp, _, fs in os.walk(args.dir):
            for f in fs:
                if f.endswith(('.cs', '.json')):
                    targets.append(os.path.join(dp, f))
    if not targets:
        ap.error("须给至少一个文件,或 --dir <目录>")

    results = [run_one(t, repo_root, args.dangling) for t in sorted(set(targets))]
    total_fail = sum(len(r['fails']) for r in results)
    total_warn = sum(len(r['warns']) for r in results)

    if args.json:
        print(json.dumps({'results': results, 'fails': total_fail, 'warns': total_warn},
                         ensure_ascii=False, indent=2))
    else:
        for r in results:
            status = 'FAIL' if r['fails'] else ('WARN' if r['warns'] else 'OK  ')
            extra = f"  [Test]={r['tests']}" if r['tests'] else ""
            print(f"[{status}] {r['path']}{extra}")
            for f in r['fails']:
                print(f"        ✗ {f}")
            for w in r['warns']:
                print(f"        ⚠ {w}")
        print(f"\n汇总:FAIL {total_fail} · WARN {total_warn} · 文件 {len(results)}")

    return 1 if total_fail else 0


if __name__ == '__main__':
    sys.exit(main())
