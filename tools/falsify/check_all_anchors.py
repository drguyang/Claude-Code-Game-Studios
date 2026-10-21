#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""全库证伪 spec 锚点校验 —— **纯读,零写盘**,可常驻 CI。

立档缘由(2026-10-21 · S17-2 评审 D8)
────────────────────────────────────
`unity_falsify.py --check-only` 只校验**一份** spec。D8 的问题不是一个 spec 过期,
而是**没人跑过** —— 2026-10-21 实测:17 份 spec 里 **8 份共 10 条锚点过期**,
其中 6 份自 2026-10-17 起就没再核过。过期的后果不是「文档陈旧」,而是
**证据件里「N 变异全 exactRed」这句话不可复算** —— 任何人今天重跑都会在
`apply_mutation` 的原文校验处以退出码 2 中止,断言根本没机会跑。

⇒ 本脚本把「逐份 check-only」变成**一条命令 + 一个 CI job**,让锚点漂移
在提交时可见,而不是等下一次有人想复算证据时才暴露。

用法
────
    python3 tools/falsify/check_all_anchors.py            # 全部 spec
    python3 tools/falsify/check_all_anchors.py --quiet    # 只打印汇总与失败项

退出码
──────
    0 = 全部 spec 的锚点都对得上
    1 = 至少一份 spec 有过期锚点(附逐条「期望 / 实读 / 现居行号」)
    2 = specs 目录不存在 / 读不出

⚠️ **本脚本只读**:它不调 `apply_mutation`、不碰工作树、不进变异窗口。
   这正是它与 `--dry-run` 的区别 —— 后者**会写盘**(见 README「`--dry-run` 同样进变异窗口」)。
"""

import argparse
import json
import subprocess
import sys
from pathlib import Path

EXIT_OK = 0
EXIT_STALE = 1
EXIT_SETUP = 2


def main() -> int:
    ap = argparse.ArgumentParser(description="全库证伪 spec 锚点校验(纯读)")
    ap.add_argument("--repo", default=None, help="仓根(默认:本脚本上溯两层)")
    ap.add_argument("--quiet", action="store_true", help="只打印汇总与失败项")
    args = ap.parse_args()

    repo = Path(args.repo).resolve() if args.repo else Path(__file__).resolve().parents[2]
    specs_dir = repo / "tools" / "falsify" / "specs"
    if not specs_dir.is_dir():
        print(f"!! specs 目录不存在:{specs_dir}")
        return EXIT_SETUP

    specs = sorted(specs_dir.glob("*.json"))
    if not specs:
        print(f"!! specs 目录为空:{specs_dir}")
        return EXIT_SETUP

    driver = Path(__file__).resolve().parent / "unity_falsify.py"
    stale_specs = []

    for spec_path in specs:
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        n_mut = len(spec.get("mutations", []))

        # 逐条比对 = 复用驱动的 check_anchors(它已是纯读),但走子进程是为了
        # 让每条 spec 的失败**互不污染**:一份崩了不影响其余份的结论。
        proc = subprocess.run(
            [sys.executable, "-I", str(driver), "--spec", str(spec_path),
             "--repo", str(repo), "--check-only"],
            capture_output=True, text=True,
        )
        ok = proc.returncode == 0
        name = spec_path.name

        if ok:
            print(f"  ✅ {name:46s} {n_mut} 条锚点全绿")
        else:
            stale_specs.append((name, proc.stdout + proc.stderr))
            print(f"  ❌ {name:46s} {n_mut} 条锚点 —— **有过期**")
            if not args.quiet:
                for line in (proc.stdout + proc.stderr).splitlines():
                    if line.strip().startswith(("❌", "期望", "实读", "⇒", "!!")):
                        print("       " + line.strip())

    print()
    print(f"── {len(specs) - len(stale_specs)}/{len(specs)} 份 spec 锚点全绿"
          f" · {len(stale_specs)} 份过期 ──")

    if stale_specs:
        print()
        print("!! 锚点过期 = **证据件不可复算的信号**,不是判别力缺口。")
        print("   修法:逐条 `--check-only` 看实读与「现居行号」,更新 spec 的 `line`;")
        print("   若原文在盘上**零命中** ⇒ 生产码已被实质修改,须重审该变异的 `_why`。")
        return EXIT_STALE

    return EXIT_OK


if __name__ == "__main__":
    sys.exit(main())
