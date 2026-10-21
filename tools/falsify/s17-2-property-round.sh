#!/usr/bin/env bash
# S17-2 性质测试证伪轮(2026-10-21)· 三个变异,每个「备份 → 变异 → 跑 → 还原 → sha256 校验」在同一进程内完成。
#
# 纪律(承 tools/falsify/README.md):
#   · 禁 git checkout / git stash —— 用备份 + cp 回 + SHA-256 逐字节比对
#   · 每个变异文本做 fail-closed 前置校验(原文不符 ⇒ 立刻中止,不写盘)
#   · 还原后 sha256 不符 ⇒ 脚本非零退出
#
# 预期(先写死,跑完再对照;不得把实际结果抄进这里):
# ⚠️ 首轮实测只红 5 —— 差的两条是 anyMinMax 的 (-1,1) 与 (-1000,1000):那两组样本和
#    只有 2 / 2000,落在冻结界内 ⇒ 对「钳到冻结界」不可观测。**夹具已修**(加 MinMaxSlackRaw
#    = 100000 的第四格)。下表 M1 的 7 = **修后**预期;首轮 5 已如实登记在卡面。
#
#   M1 env 侧加钳制(±32768)⇒ 红 7 例:
#        envMod_property_anyInput_equalsLinearSum_noClamp
#        envMod_property_anyMinMax_outputStillUnclamped ×4
#        envMod_aboveEnvModMax_passesThroughUnclamped
#        envMod_allNegative_passesThroughUnclamped
#      (绿:gddWorkedExample —— 28000 在 ±32768 内,钳不动;sameInput —— 两次同样钳)
#   M2 equip 上钳移除 ⇒ 红 6 例:
#        equipMod_property_anyTierVector_inClosedRange
#        equipMod_sumAboveCap_pinsToCap · positiveScores_haveNoIntermediateValue
#        equipMod_largeScore_int64NoWrap · nMaxBoundary_noWrap
#        equipMod_negativeTier_excludedFromScore   ← Σtier=2 ⇒ raw 131072 也越 CAP
#      (绿:allZeroTier —— score 0 不触上钳)
#   M3 tier<=0 → tier==0 ⇒ 红 1 例:
#        equipMod_negativeTier_excludedFromScore   ← Σtier = 2+(−100) = −98 ⇒ 下钳到 0 ≠ CAP

set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TARGET="$REPO/unity/Assets/Sim/Clinic/ClinicModifierEvaluator.cs"
BACKUP="$(mktemp /tmp/s17-2-orig-XXXXXX.cs)"
XMLDIR="$REPO/unity/Logs"

cp -p "$TARGET" "$BACKUP" || exit 1
ORIG_SHA="$(sha256sum "$TARGET" | cut -d' ' -f1)"
echo "ORIG_SHA=$ORIG_SHA"

restore() {
  cp -p "$BACKUP" "$TARGET"
  local now; now="$(sha256sum "$TARGET" | cut -d' ' -f1)"
  if [ "$now" != "$ORIG_SHA" ]; then
    echo "!!! 还原失败:sha256 $now != $ORIG_SHA" >&2
    return 1
  fi
  echo "RESTORED_OK sha256=$now"
}

run_mut() {
  local tag="$1" line="$2" old="$3" new="$4"
  echo "══════════════ $tag (line $line) ══════════════"
  python3 -I - "$TARGET" "$line" "$old" "$new" <<'PY' || { echo "PRECONDITION_FAIL $tag"; return 2; }
import sys
path, ln, old, new = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
lines = open(path, encoding='utf-8').read().split('\n')
actual = lines[ln-1]
if actual != old:
    print(f"FAIL-CLOSED: line {ln} 实为 {actual!r},期望 {old!r}")
    sys.exit(1)
lines[ln-1] = new
open(path, 'w', encoding='utf-8').write('\n'.join(lines))
print(f"MUTATED line {ln}")
PY

  local xml="$XMLDIR/falsify-$tag.xml"
  rm -f "$xml"
  ( cd "$REPO/unity" && timeout 900 unity test "$PWD" --mode EditMode \
      --output "$xml" --filter "ClinicModifierEvalTest" >/dev/null 2>&1 )

  if [ ! -f "$xml" ]; then
    echo "  ⚠️ 无 XML 产出(构建失败?)—— 记 NOT-RUN,不当作红"
  else
    python3 -I - "$xml" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print(f"  total={r.get('total')} passed={r.get('passed')} failed={r.get('failed')}")
for tc in r.iter('test-case'):
    if tc.get('result') != 'Passed':
        print(f"  RED: {tc.get('name')}")
PY
  fi

  restore || return 1
}

run_mut M1_envClamp 121 \
  '            return new Fix(sum);' \
  '            if (sum < -32768L) sum = -32768L; if (sum > 32768L) sum = 32768L;   // FALSIFY-M1
            return new Fix(sum);' || exit 3

run_mut M2_equipNoUpperClamp 163 \
  '            if (raw > EquipModCapRaw) raw = EquipModCapRaw;' \
  '            // FALSIFY-M2: 上钳停用' || exit 3

run_mut M3_tierLtToEq 150 \
  '                if (tier <= 0) continue;                       // tier = 0 无档,不计入(ADR-006 D-21-17)' \
  '                if (tier == 0) continue;                       // FALSIFY-M3' || exit 3

echo "══════════════ 收尾校验 ══════════════"
FINAL_SHA="$(sha256sum "$TARGET" | cut -d' ' -f1)"
[ "$FINAL_SHA" = "$ORIG_SHA" ] && echo "FINAL_OK $FINAL_SHA" || { echo "!!! FINAL MISMATCH"; exit 4; }
grep -c "FALSIFY-" "$TARGET" | sed 's/^/FALSIFY_residue=/'
rm -f "$BACKUP"
