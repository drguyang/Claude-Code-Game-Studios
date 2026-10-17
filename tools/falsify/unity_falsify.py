#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""证伪轮驱动 —— **停用生产守卫 ⇒ 断言必须转红**,并守住「失败路径不留痕」。

立规缘由
────────
`/story-done` 的 Falsifiability Gate(S12-4,2026-10-15 立)要求:凡命中
定序 / 共存互斥 / 定义域收窄 / 一次性幂等 / 阈值边界 五类形态的夹具,
须逐条反证 —— 「停用生产码的对应守卫 ⇒ 该断言转红」;仍绿 = **假绿夹具** = BLOCKING。

此前这些反证一律写在 `/tmp` 的一次性 shell 里,**每次重写、每次靠人记得还原**。
2026-10-17 补口轮实测到该做法的**具体代价**:

    M1/M3 停用「组缺失 ⇒ throw」后,生产码落入分支 ④,而该分支
    **先 `settings.AddLabel(假组名)` 再 `AssetDatabase.SaveAssets()`**
    ⇒ 守卫一破,**失败的测试把两个假标签写进了入库的**
       `unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset`。
    夹具原有的「无残留」断言只查**组** —— 组确实没建,**建的是标签**。

同型事故 `production/sprint-status.yaml:149` 已登记过一次(S14-8 静默改写 `audio.asset`)。
⇒ 本驱动把那两条纪律固化为**代码**,而不是留在人的记忆里:

  ① **改 → 跑 → 还原在同一进程内完成**(`try/finally`),崩溃也还原;
  ② 还原后 **SHA-256 逐字节校验**;
  ③ 跑测后 **`git diff --quiet` 守住「受保护路径」** —— 非零即判**失败**,
     哪怕断言确实转红了。理由:证伪轮的目的**不是**「弄红一个测试」,
     而是「证明守卫在承重」;**过程中污染入库资产 = 这次证伪本身有缺陷**。

判据(退出码)
──────────────
    0 = 全部变异**恰如预期**转红,且受保护路径零 diff
    1 = 有变异**逃逸**(断言没红 = 假绿夹具)或**红得比预期多**(判别面不清)
    2 = 夹具前提不成立(目标行文本对不上 / 起跑时受保护路径本就脏 —— 是**夹具过期信号**)
    3 = **还原或守卫失败**(SHA-256 不符 / 跑测后受保护路径有 diff)—— 最严重,须立即人工介入

用法
──────
    python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/<name>.json
    python3 tools/falsify/unity_falsify.py --spec … --dry-run     # 只校验前提,不跑 Unity
    python3 tools/falsify/unity_falsify.py --spec … --guard <path>  # 追加受保护路径

⚠️ **Unity batch 独占工程** —— 起跑前请确认编辑器已关(`unity/Temp/UnityLockfile` 不存在)。
   本驱动**不**代你查(查了也要靠运气,竞态仍在);它只保证**自己不并发**。
"""

import argparse
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

# ── 默认受保护路径:证伪轮**不得**留下 diff 的入库资产 ──────────────────────────
#    依据 = 2026-10-17 补口轮实测事故(见文件头)+ TD 登记
#    「证伪轮副作用:失败路径污染入库资产」。
DEFAULT_GUARDS = [
    "unity/Assets/AddressableAssetsData",
    "unity/Assets/Audio",
]

EXIT_OK = 0
EXIT_ESCAPE = 1
EXIT_PRECONDITION = 2
EXIT_RESTORE = 3


# ══════════════════════════════════════════════════════════════════════════════
# 工具
# ══════════════════════════════════════════════════════════════════════════════

def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def git(repo: Path, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=repo, capture_output=True, text=True)


def guards_fingerprint(repo: Path, guards) -> str:
    """受保护路径的**工作树指纹** = `git status --short` + `git diff` 的合并文本。

    ⚠️ **刻意不要求「起跑时干净」**(2026-10-17 首版教训):开发中的仓本来就常带
    未提交的正当改动(本例:`data-core.asset` 是 S14-6 的既有改动)。
    要求「起跑干净」会让驱动在**绝大多数真实场景**下直接退出码 2 —— 那不是严谨,是不可用。
    正确判据 = **前后指纹相同**:证伪轮不得**新增**任何痕迹,既有改动不受影响。
    """
    st = git(repo, "status", "--short", "--", *guards).stdout
    df = git(repo, "diff", "--", *guards).stdout
    return st + "\n@@DIFF@@\n" + df


def parse_results(xml_path: Path):
    """→ (summary_dict, red_name_list)。red = result 既非 Passed 也非 Skipped。"""
    root = ET.parse(xml_path).getroot()
    summary = {
        "total": int(root.get("total", 0)),
        "passed": int(root.get("passed", 0)),
        "failed": int(root.get("failed", 0)),
        "skipped": int(root.get("skipped", 0)),
    }
    reds = [tc.get("name") for tc in root.iter("test-case")
            if tc.get("result") not in ("Passed", "Skipped")]
    return summary, reds


def apply_mutation(repo: Path, mut: dict) -> None:
    """按「行号 + 期望原文」施加变异。对不上即抛 —— **不猜**。"""
    f = repo / mut["file"]
    lines = f.read_text(encoding="utf-8").split("\n")
    ln = int(mut["line"])
    actual = lines[ln - 1]
    if actual != mut["expect"]:
        raise PreconditionError(
            f"{mut['file']}:{ln} 原文对不上\n"
            f"    期望 {mut['expect']!r}\n    实读 {actual!r}"
        )
    lines[ln - 1] = mut["replace"]
    f.write_text("\n".join(lines), encoding="utf-8")


class PreconditionError(RuntimeError):
    pass


class RestoreError(RuntimeError):
    pass


# ══════════════════════════════════════════════════════════════════════════════
# 主流程
# ══════════════════════════════════════════════════════════════════════════════

def run(repo: Path, spec: dict, guards, dry_run: bool, keep_logs: bool) -> int:
    mutations = spec["mutations"]
    mode = spec.get("mode", "EditMode")
    test_filter = spec.get("filter")
    unity_project = spec.get("unityProject", "unity")

    # ── 起跑前置:记下受保护路径的**指纹**(不是「要求干净」—— 见 guards_fingerprint)──
    guard_before = guards_fingerprint(repo, guards)
    if guard_before.strip() and guard_before.split("@@DIFF@@")[0].strip():
        print("ℹ️ 受保护路径起跑时已有改动(允许)—— 判据是**前后指纹相同**,不是「本来干净」:")
        print("   " + guard_before.split("@@DIFF@@")[0].strip().replace("\n", "\n   "))

    backup = Path(tempfile.mkdtemp(prefix="falsify-bak-"))
    before = {}      # Path -> sha256(起跑基线)
    backups = {}     # Path -> Path(备份落点)
    for mut in mutations:
        f = repo / mut["file"]
        if f in before:
            continue
        before[f] = sha256(f)
        dst = backup / f"{len(before)}-{f.name}"
        shutil.copy2(f, dst)
        backups[f] = dst

    def restore_all():
        for f, bak in backups.items():
            shutil.copy2(bak, f)
        bad = [str(f.relative_to(repo)) for f, d in before.items() if sha256(f) != d]
        if bad:
            raise RestoreError("SHA-256 还原校验失败:" + ", ".join(bad))

    verdict = EXIT_OK
    try:
        for mut in mutations:
            name = mut["name"]
            print(f"\n──── 施加 {name}:{mut['file']}:{mut['line']} ────")

            # 每个变异都从**干净基线**出发(逐个施加、逐个还原 —— 不叠变异)
            restore_all()
            apply_mutation(repo, mut)

            if dry_run:
                print("  (--dry-run:仅校验前提,不跑 Unity)")
                continue

            out_xml = repo / "unity" / "Logs" / f"falsify-{name}.xml"
            cmd = ["unity", "test", unity_project, "--mode", mode, "--output", str(out_xml)]
            if test_filter:
                cmd += ["--filter", test_filter]
            print("  $ " + " ".join(cmd))
            proc = subprocess.run(cmd, cwd=repo, capture_output=True, text=True)
            # ⚠️ 断言转红时 `unity test` 自身非零退出 —— **那是预期**,不作失败判据。
            print(f"  unity 退出码 = {proc.returncode}(红即非零,属预期)")

            if not out_xml.exists():
                print(f"  !! 未产出结果 XML:{out_xml} —— 编译失败?")
                verdict = max(verdict, EXIT_ESCAPE)
                continue

            summary, reds = parse_results(out_xml)
            print(f"  total={summary['total']} passed={summary['passed']} "
                  f"failed={summary['failed']} skipped={summary['skipped']}")
            for r in reds:
                print("    RED:", r)

            expected = set(mut.get("expectRed", []))
            got = set(reds)

            if not expected.issubset(got):
                print(f"  ❌ 逃逸:预期转红但没红 = {sorted(expected - got)}")
                print("     ⇒ 该夹具**假绿**(S12-4 BLOCKING):输入构造让变异前后结果相同。")
                verdict = max(verdict, EXIT_ESCAPE)
            elif mut.get("exactRed") and got != expected:
                print(f"  ❌ 红的条数与预期不符:预期 {sorted(expected)},实得 {sorted(got)}")
                print("     ⇒ 判别面不清:变异打到了预期之外的断言。")
                verdict = max(verdict, EXIT_ESCAPE)
            else:
                print(f"  ✅ 恰如预期转红:{sorted(expected)}")

            if not keep_logs and out_xml.exists():
                out_xml.unlink()

    finally:
        try:
            restore_all()
            print("\n──── 还原:SHA-256 逐字节校验通过 ────")
        except RestoreError as e:
            print(f"\n!!! 还原失败:{e}")
            print(f"!!! 备份仍在 {backup} —— 请人工恢复后再继续")
            return EXIT_RESTORE

    # ── 判据二:跑完之后,受保护路径的指纹必须与起跑时**逐字相同** ──
    #    这正是 2026-10-17 事故缺的那一步:断言红了,但入库资产被写脏。
    print("──── 判据二:受保护路径**指纹未变**(证伪轮不留痕)────")
    guard_after = guards_fingerprint(repo, guards)
    if guard_after != guard_before:
        print("❌ 证伪轮**改动了受保护路径** —— 断言是否转红都不作数:")
        before_st = guard_before.split("@@DIFF@@")[0].strip()
        after_st = guard_after.split("@@DIFF@@")[0].strip()
        if before_st != after_st:
            print("  起跑 git status:\n   " + before_st.replace("\n", "\n   "))
            print("  跑后 git status:\n   " + after_st.replace("\n", "\n   "))
        else:
            print("  (status 相同,但**内容**变了 —— 逐行 diff 见下)")
        print("   ⇒ 夹具的失败路径缺清理(`finally` + 断言「查对象 + 查该方法能写到的每一处」)。")
        print("   ⇒ 修复方向见 docs/tech-debt-register.md「证伪轮副作用:失败路径污染入库资产」。")
        return EXIT_RESTORE
    print("✅ 指纹未变 —— 受保护路径:" + ", ".join(guards))

    shutil.rmtree(backup, ignore_errors=True)

    if verdict == EXIT_OK:
        print(f"\n══ 证伪轮通过:{len(mutations)} 个变异全部恰如预期 ══")
    else:
        print(f"\n══ 证伪轮**未通过**(退出码 {verdict})══")
    return verdict


def main() -> int:
    ap = argparse.ArgumentParser(description="证伪轮驱动(停用守卫 ⇒ 断言须转红 + 不留痕)")
    ap.add_argument("--spec", required=True, help="变异规格 JSON")
    ap.add_argument("--repo", default=None, help="仓根(默认:本脚本上溯两层)")
    ap.add_argument("--guard", action="append", default=None,
                    help=f"追加受保护路径(可重复;默认 {DEFAULT_GUARDS})")
    ap.add_argument("--dry-run", action="store_true", help="只校验前提(行号/原文),不跑 Unity")
    ap.add_argument("--keep-logs", action="store_true", help="保留每次跑测的结果 XML")
    args = ap.parse_args()

    repo = Path(args.repo).resolve() if args.repo else Path(__file__).resolve().parents[2]
    spec = json.loads(Path(args.spec).read_text(encoding="utf-8"))
    guards = list(args.guard) if args.guard else list(DEFAULT_GUARDS)

    print(f"仓根   : {repo}")
    print(f"规格   : {args.spec} —— {spec.get('name', '(未命名)')}")
    print(f"受保护 : {', '.join(guards)}")

    try:
        return run(repo, spec, guards, args.dry_run, args.keep_logs)
    except PreconditionError as e:
        print(f"\n!! 夹具前提不成立:\n{e}")
        print("   ⇒ 是**夹具过期信号**(生产码行号/文本变了),不是判别力缺口。")
        return EXIT_PRECONDITION


if __name__ == "__main__":
    sys.exit(main())
