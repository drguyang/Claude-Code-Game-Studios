#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""run_matrix.py —— ADR-012 双格确定性矩阵的本机可跑执行体(2026-10-27 立)

背景(必读,否则会误读本工具的判据面):
  ADR-012 §一 裁决的是「**三格**全免费常驻矩阵」。2026-10-17 探针实测 + 用户裁定:
  第三格(Linux-ARM64-IL2CPP)在本 pinned 工具链(Unity 6000.3.24f1)**目标不存在**,
  登记为「长期未达」,不降格、不换面。2026-10-27 用户再次裁定:
  **落两格(Mono + IL2CPP-x64)+ 第三格登记未达**。

  ⇒ 本工具跑的是**可达的两格**,并且**在报告里显式打印第三格的未达状态** ——
     不允许「跑了两个就说矩阵齐了」。判据面 = 2/3,报告首行必须写明。

两格是什么(照 ADR-012 §Validation 的既有命名):
  格 1  Linux-x64-Mono      —— Editor 进程内跑(EditMode 单元级哈希 + PlayMode 集成级字节)
  格 2  Linux-x64-IL2CPP    —— 切 Standalone 后端 → `-runTests -testPlatform StandaloneLinux64`
                               (构建并运行 player,判据 = player 落盘件 vs 金标准外部 diff)

判据(与 AC-29 / ADR-012 口径逐字对齐,不自创):
  ① 每格的测试套件零 failed;
  ② 两格各自把 19 条场景哈希落盘(AC29_OUT);
  ③ 两格落盘件**逐位相同**(外部 diff,不是测试内断言 —— 防自指);
  ④ 两格落盘件都与**独立 Python 金标准**(`tests/unit/item_database/golden/golden-v1.txt`)相同;
  ⑤ 落盘件头自报的后端标记必须**确实是两个不同后端**(防「拿 Mono 结果冒充 IL2CPP」)。

  ⚠️ ① 单独成立**不构成**矩阵绿 —— 这正是 ADR-012「测试绿但 diff 不同等价于红」的原话。

用法:
  python3 tools/determinism-matrix/run_matrix.py --cell mono      # 只跑格 1
  python3 tools/determinism-matrix/run_matrix.py --cell il2cpp    # 只跑格 2
  python3 tools/determinism-matrix/run_matrix.py --cell both      # 两格 + 对拍(默认)

  `--dry-run` 只打印将要执行的命令与前置检查结论,**不跑 Unity、不写盘**。

⚠️ Unity batch 独占工程:本工具在开跑前检查 `unity/Temp/UnityLockfile` 与活进程;
   发现占用即**退出 2**(不抢、不杀)。跑完格 2 后**自动把后端归位**并做 SHA-256 校验
   (承 `Ac29Il2CppSwitch` 的注释义务与本项目「还原判据 = SHA-256 逐字节」纪律)。
"""

import argparse
import hashlib
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UNITY_PROJECT = os.path.join(REPO, "unity")
UNITY_BIN = os.path.expanduser("~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity")
GOLDEN = os.path.join(REPO, "tests", "unit", "item_database", "golden", "golden-v1.txt")
PROJECT_SETTINGS = os.path.join(UNITY_PROJECT, "ProjectSettings", "ProjectSettings.asset")

# 两格的确定性测集(ADR-012 双级:单元级哈希 + 集成级字节)
# ⚠️ 这两张表只列**确定性判据面**的类,不是全量回归 —— 全量回归归 smoke-check。
CELLS = {
    "mono": {
        "label": "格 1 · Linux-x64-Mono",
        "platform": "EditMode",          # 另加 PlayMode,见下
        "extra_platform": "PlayMode",
        # ⚠️ 2026-10-27 实测订正:原过滤器写 `…|DeterminismGoldenCrossplatform|…`,
        #    漏了 `ItemDatabase.DeterminismGoldenFixturesTest`(它不匹配 Crossplatform)
        #    ⇒ 首跑 EditMode 只到 48 测,**缺了直接读 golden-v1.txt 的那一族**。
        #    改用 `DeterminismGolden` 前缀覆盖两者。
        "filter": "GoldenHashV1|DeterminismGolden|TelemetryGolden",
    },
    "il2cpp": {
        "label": "格 2 · Linux-x64-IL2CPP",
        "platform": "StandaloneLinux64",
        "extra_platform": None,
        "filter": "DeterminismGoldenCrossplatform",
    },
}

CELL3_REGISTRATION = """
⛔ 第三格 · Linux-ARM64-IL2CPP —— **未达(长期)**
   依据:2026-10-17 `BuildTargetProbeCli` 探针实测 —— 46 个 `BuildTarget` 中**零个含 arm**;
        `StandaloneLinux64` 是**唯一受支持**的 Linux 目标(x64)。
        用户裁定「维持三格原样,登记为长期未达」(不降格 / 不换面 / 不改挂)。
   复核条件:引擎升级后重跑探针命令(见 `production/qa/evidence/impl-td-audio-01-fix-2026-10-17.md` §十三);
        若届时出现受支持的 ARM64 目标,该格自然解锁。
   ⇒ **本工具永远只跑两格。跑到两格全绿 ≠ 矩阵齐。**
"""


def die(msg, code=1):
    print(f"❌ {msg}", file=sys.stderr)
    sys.exit(code)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def check_project_free():
    """Unity batch 独占工程 —— 有锁文件或活进程就退出,不抢不杀。"""
    lock = os.path.join(UNITY_PROJECT, "Temp", "UnityLockfile")
    if os.path.exists(lock):
        die(f"工程被占用:`{lock}` 存在 —— 请先关闭编辑器再跑。", 2)
    try:
        out = subprocess.run(
            ["ps", "-eo", "pid,cmd"], capture_output=True, text=True, timeout=15
        ).stdout
    except Exception as e:                       # noqa: BLE001 — 查不到不等于没占用
        die(f"无法检查进程表({e})—— 为安全起见不继续。", 2)
    busy = [
        line for line in out.splitlines()
        if re.search(r"/(Editor/Unity|Unity)\b", line) and "-batchmode" in line
    ]
    if busy:
        die("发现活 Unity batch 进程,先确认归属再跑:\n  " + "\n  ".join(busy), 2)


def read_backend():
    """读 ProjectSettings 里 Standalone 的 scriptingBackend(缺省 = Mono)。"""
    with open(PROJECT_SETTINGS, encoding="utf-8") as f:
        text = f.read()
    m = re.search(r"^  scriptingBackend:\s*\n((?:    \w+: \d+\s*\n)+)", text, re.M)
    if not m:
        return None                          # 键缺省 ⇒ Unity 视为 Mono2x
    for line in m.group(1).splitlines():
        k, v = line.split(":", 1)
        if k.strip() == "Standalone":
            return int(v.strip())
    return None


def run_unity(args, log_path, extra_env=None):
    os.makedirs(os.path.dirname(log_path), exist_ok=True)
    cmd = [UNITY_BIN, "-batchmode", "-nographics",
           "-projectPath", UNITY_PROJECT] + args + ["-logFile", log_path]
    env = None
    if extra_env:
        env = dict(os.environ)
        env.update(extra_env)
        print("  env 注入: " + " · ".join(f"{k}={v}" for k, v in extra_env.items()))
    print("  $ " + " ".join(cmd))
    proc = subprocess.run(cmd, capture_output=True, text=True, env=env)
    return proc.returncode


def parse_results(xml_path):
    if not os.path.exists(xml_path):
        return None
    root = ET.parse(xml_path).getroot()
    run = root if root.tag == "test-run" else root.find("test-run")
    if run is None:
        return None
    return {k: run.get(k) for k in ("total", "passed", "failed", "inconclusive", "skipped")}


def strip_comments(path):
    out = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            s = line.strip()
            if not s or s.startswith("#"):
                continue
            out.append(s)
    return sorted(out)


def backend_of(path):
    with open(path, encoding="utf-8") as f:
        first = f.readline()
    m = re.search(r"backend=([^|]+)\|", first)
    return m.group(1).strip() if m else "(未标注)"


def run_cell(name, outdir, dry_run):
    cfg = CELLS[name]
    print(f"\n══ {cfg['label']} ══")

    platforms = [cfg["platform"]] + ([cfg["extra_platform"]] if cfg["extra_platform"] else [])
    results = {}
    for plat in platforms:
        xml = os.path.join(outdir, f"matrix-{name}-{plat}.xml")
        log = os.path.join(outdir, f"matrix-{name}-{plat}.log")
        args = ["-runTests", "-testPlatform", plat,
                "-testFilter", cfg["filter"], "-testResults", xml]
        # ⚠️ 不得带 -quit —— 会让 Unity 在测试启动前退出(20 秒空跑、零 XML)
        #
        # ⚠️ 2026-10-27 实测订正(player 腿必须注入两个 env):
        #   首跑 IL2CPP 腿 3 测里 1 测 **Skipped** —— `test_ac21a29_allScenarioHashes_
        #   writtenAndCompared_whenGoldenReachable` 命中 `Assert.Ignore`(金标准不可达),
        #   即 **19 条就地断言整段没跑**,只有「落盘」那一半生效。根因见夹具 `:289-294`:
        #   player 内 `Application.dataPath` 指向 `Game_Data` ⇒ 仓库相对回退路径不可达。
        #   夹具自陈「判决由外部 diff 承担」,但 2026-09-24 原证据件第 2 跑注入 env 后
        #   是 **15/15 Passed、0 Skipped** ⇒ 本工具若不注入,**就比既有证据弱一格**。
        #   编辑器腿(EditMode/PlayMode)回退路径可达,注入无副作用,故两腿统一注入。
        out_path = os.path.join(outdir, f"computed-{name}-{plat}.txt")
        extra_env = {"AC29_OUT": out_path, "AC29_GOLDEN": GOLDEN}
        if dry_run:
            print(f"  (dry-run) AC29_OUT={out_path} AC29_GOLDEN={GOLDEN} "
                  f"{UNITY_BIN} -batchmode -nographics -projectPath {UNITY_PROJECT} "
                  + " ".join(args) + f" -logFile {log}")
            results[plat] = {"dry-run": True}
            continue
        rc = run_unity(args, log, extra_env=extra_env)
        results[plat] = {"exit": rc, "xml": xml, "out": out_path,
                         **({"parsed": parse_results(xml)} if parse_results(xml) else {})}
    return results


def main():
    ap = argparse.ArgumentParser(description="ADR-012 双格确定性矩阵(本机执行体)")
    ap.add_argument("--cell", choices=["mono", "il2cpp", "both"], default="both")
    ap.add_argument("--out", default=os.path.join(REPO, "unity", "Logs", "determinism-matrix"),
                    help="落盘目录(默认 unity/Logs/determinism-matrix,已 gitignore)")
    ap.add_argument("--dry-run", action="store_true", help="只打印命令与前置检查,不跑 Unity")
    args = ap.parse_args()

    outdir = os.path.abspath(args.out)
    print("═" * 72)
    print("ADR-012 确定性矩阵 —— 本机执行体")
    print("═" * 72)
    print(f"仓库      : {REPO}")
    print(f"Unity     : {UNITY_BIN}")
    print(f"金标准    : {GOLDEN}")
    print(f"落盘目录  : {outdir}")
    print("判据面    : **2 / 3 格**(第三格 ARM64 长期未达 —— 见下方登记)")
    print(CELL3_REGISTRATION)

    if not os.path.exists(UNITY_BIN):
        die(f"找不到 Unity 编辑器:`{UNITY_BIN}`")
    if not os.path.exists(GOLDEN):
        die(f"找不到金标准:`{GOLDEN}`")

    check_project_free()
    print("✅ 工程未被占用")

    backend_before = read_backend()
    sha_before = sha256(PROJECT_SETTINGS)
    print(f"✅ ProjectSettings 基线:SHA-256 `{sha_before[:16]}…` · Standalone backend = "
          f"{'IL2CPP(1)' if backend_before == 1 else 'Mono(缺省/0)'}")

    os.makedirs(outdir, exist_ok=True)
    summary = {}

    # ⚠️ 2026-10-27 实测订正 —— **归位用字节备份,不靠 `SetMono`**:
    #   首跑归位确实是**语义正确但字节不同**:`Ac29Il2CppSwitch.SetMono` 调
    #   `PlayerSettings.SetScriptingBackend(..., Mono2x)` ⇒ Unity 把 `Standalone: 0`
    #   显式写进 `scriptingBackend:` 段,而基线里**该键根本不存在**(缺省即 Mono2x)。
    #   结果 SHA-256 不匹配(工具如实 `exit 3`,**拦得对** —— 它证明了闸门有判别力)。
    #   `SetMono` 只保证「语义归位」,不保证「字节归位」。⇒ 开跑前存字节副本,归位时
    #   **cp 回该副本**,再以 SHA-256 校验(承「还原判据 = SHA-256 逐字节」;
    #   ⚠️ 仍**不得**用 `git checkout` —— 会抹掉未提交改动)。
    settings_backup = os.path.join(outdir, "ProjectSettings.baseline.bak")

    if args.cell in ("mono", "both"):
        summary["mono"] = run_cell("mono", outdir, args.dry_run)

    if args.cell in ("il2cpp", "both"):
        # 切 IL2CPP(先转成 Mono 基线,保证切换是一次真实变更)
        if not args.dry_run:
            shutil.copy2(PROJECT_SETTINGS, settings_backup)
            print(f"\n── ProjectSettings 字节副本已存:`{settings_backup}` ──")

            print("── 切 Standalone 后端 → IL2CPP ──")
            run_unity(["-quit", "-executeMethod", "Ac29Il2CppSwitch.SetIl2cpp"],
                      os.path.join(outdir, "switch-il2cpp.log"))
            if read_backend() != 1:
                die("后端切换未生效(ProjectSettings 里 Standalone 仍非 1)—— 格 2 无法构成「两个独立后端」。")
            print("✅ 后端已切到 IL2CPP")
        summary["il2cpp"] = run_cell("il2cpp", outdir, args.dry_run)

        # 归位 + SHA-256 校验(承「还原判据 = SHA-256 逐字节」)
        if not args.dry_run:
            print("\n── 后端归位(字节副本 cp 回 + SHA-256 校验)──")
            run_unity(["-quit", "-executeMethod", "Ac29Il2CppSwitch.SetMono"],
                      os.path.join(outdir, "switch-mono.log"))
            shutil.copy2(settings_backup, PROJECT_SETTINGS)
            sha_after = sha256(PROJECT_SETTINGS)
            if sha_after != sha_before:
                die(f"ProjectSettings **未逐字节还原**!\n  前 `{sha_before}`\n  后 `{sha_after}`\n"
                    f"  副本 `{settings_backup}` 仍在 —— 手工 cp 回后再继续"
                    f"(不要用 `git checkout` —— 会抹掉未提交改动)。", 3)
            print(f"✅ SHA-256 逐字节还原通过(`{sha_after[:16]}…`,= 基线)")

    if args.dry_run:
        print("\n(dry-run 结束 —— 未跑 Unity、未写盘)")
        return 0

    # ── 对拍 ────────────────────────────────────────────────────────────
    print("\n" + "═" * 72)
    print("对拍")
    print("═" * 72)

    # 每格可能跑多个平台(格 1 = EditMode + PlayMode),每个平台各写一份 AC29_OUT。
    # 取**条数 == 金标准条数**的那一份作为该格的代表落盘件;多份且都满 = 取排序首个并注明。
    golden_lines = strip_comments(GOLDEN)
    computed = {}
    for name, per_plat in summary.items():
        candidates = [info["out"] for info in per_plat.values()
                      if isinstance(info, dict) and info.get("out") and os.path.exists(info["out"])]
        if not candidates:
            print(f"⚠️ {name}: 该格无任何落盘件 —— 判据不完整")
            continue
        full = [c for c in candidates if len(strip_comments(c)) == len(golden_lines)]
        chosen = full[0] if full else candidates[0]
        dst = os.path.join(outdir, f"computed-{name}.txt")
        shutil.copy2(chosen, dst)
        computed[name] = dst
        note = f"(满 19 条的落盘件共 {len(full)}/{len(candidates)} 份)" if full else ""
        print(f"{name}: 落盘件 → `{dst}` · backend = **{backend_of(dst)}** {note}")

    ok = True
    for name, path in computed.items():
        lines = strip_comments(path)
        same = lines == golden_lines
        ok &= same
        print(f"{name} vs 金标准: {'✅ 逐位相同' if same else '❌ 不同'}"
              f"({len(lines)} vs {len(golden_lines)} 条)")

    # 判据 ① —— 每格测试套件零 failed(单独成立不构成绿,但失败必然红)
    for name, per_plat in summary.items():
        for plat, info in per_plat.items():
            parsed = info.get("parsed") if isinstance(info, dict) else None
            if not parsed:
                print(f"⚠️ {name}/{plat}: 无 XML 结果 —— 判据 ① 不成立")
                ok = False
                continue
            zero = parsed.get("failed") == "0"
            ok &= zero
            print(f"{name}/{plat} 零 failed: {'✅' if zero else '❌'} "
                  f"(total={parsed.get('total')} passed={parsed.get('passed')} "
                  f"failed={parsed.get('failed')} skipped={parsed.get('skipped')})")

    if len(computed) == 2:
        a, b = computed["mono"], computed["il2cpp"]
        same = strip_comments(a) == strip_comments(b)
        ok &= same
        print(f"mono vs il2cpp: {'✅ 逐位相同' if same else '❌ 不同'}")
        ba, bb = backend_of(a), backend_of(b)
        distinct = ba != bb
        ok &= distinct
        print(f"后端标记相异: {'✅' if distinct else '❌ 两格是同一后端 —— 对拍无意义'}"
              f"(`{ba}` vs `{bb}`)")

    print("\n" + "═" * 72)
    print(f"判据面 = 2/3 格 · 结果 = {'✅ 两格 VERIFIED' if ok else '❌ 未通过'}")
    print("⚠️ 第三格 ARM64 **未达**,本条**永远不得**被读作「三格矩阵齐」。")
    print("⚠️ 本工具**不覆盖** CI 常驻性 —— 那是 EXTERNAL(`TD-ci-01`),须用户提供凭据。")
    print("═" * 72)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
