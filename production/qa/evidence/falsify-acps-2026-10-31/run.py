#!/usr/bin/env python3
# AC-PS-04/05/06/09 证伪轮:逐条变异 → 跑 PauseFocusNavigationTest → 判红 → 还原 → SHA-256 校验
import hashlib, io, os, shutil, subprocess, sys, xml.etree.ElementTree as ET

ROOT = "/home/gu/文档/nm/nm2/Claude-Code-Game-Studios"
UNITY = os.path.join(ROOT, "unity")
EDITOR = os.path.expanduser("~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity")
BAK = "/home/gu/.claude/jobs/181888b1/tmp/ps_falsify_bak"

MUTATIONS = [
    dict(
        key="Q1", target="unity/Assets/Gameplay.Boot/ModalInputDriver.cs",
        old="            bool rising = pressed && !_held;",
        new="            bool rising = false;   // FALSIFY",
        expect=["test_acps04_startOpens_resumeCloses_thenStartReopens"],
        why="边沿化停用 ⇒ 手柄 start 不再是上升沿",
    ),
    dict(
        key="Q2", target="unity/Assets/Gameplay.UI/Skeuomorphic/Screens/PauseScreen.cs",
        old="            else if (rowIndex == 1) _sink?.OpenSaveSlots();",
        new="            else if (rowIndex == 1) _sink?.Resume();   // FALSIFY",
        expect=["test_acps05_submitOnSaveSlotsRow_switchesScreen_leavingOneContainer"],
        why="「存档位」行改走 Resume ⇒ 不换屏",
    ),
    dict(
        key="Q3", target="unity/Assets/Gameplay.Presentation/Interaction/ModalGate.cs",
        old="            if (_modal.IsOpen) return false;",
        new="            bool falsifyOff = true;   // FALSIFY\n            if (_modal.IsOpen && !falsifyOff) return false;",
        expect=["test_acps06_modalOpen_gateRejectsAndMovementIsFrozen"],
        why="停用模态门方向② ⇒ 壳开着 E/F 仍放行",
    ),
    dict(
        key="Q4", target="unity/Assets/Gameplay.Boot/ModalInputFreeze.cs",
        old="        public Vector2 FilterMovement(Vector2 moveAxis) => IsBlocked ? Vector2.zero : moveAxis;",
        new="        public Vector2 FilterMovement(Vector2 moveAxis) => moveAxis;   // FALSIFY",
        expect=["test_acps06_modalOpen_gateRejectsAndMovementIsFrozen"],
        why="停用移动轴折零 ⇒ 暂停时还能走",
    ),
    dict(
        key="Q5", target="unity/Assets/Gameplay.Boot/ModalInputDriver.cs",
        old="        public void OpenPause() => _driver.OpenModal(UI.Skeuomorphic.ModalId.Pause);",
        new="        public void OpenPause()   // FALSIFY\n        {\n            if (_driver.CurrentModal == UI.Skeuomorphic.ModalId.None)\n                _driver.OpenModal(UI.Skeuomorphic.ModalId.Pause);\n        }",
        expect=["test_acps09_fullLoop_gamepadOnly", "test_acps09_fullLoop_keyboardOnly"],
        why="只在「无模态」时才开 ⇒ 存档位屏上按 start 回不到暂停壳",
    ),
]


def sha(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()


def run_fixture(tag):
    xml = os.path.join(UNITY, "Logs", f"pm-{tag}.xml")
    log = os.path.join(UNITY, "Logs", f"pm-{tag}.log")
    for p in (xml, log):
        if os.path.exists(p):
            os.remove(p)
    subprocess.run(
        [EDITOR, "-batchmode", "-nographics", "-projectPath", UNITY,
         "-runTests", "-testPlatform", "PlayMode",
         "-testFilter", "PauseFocusNavigationTest",
         "-testResults", xml, "-logFile", log],
        stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    if not os.path.exists(xml):
        return None, None, "no-xml(编译红?)"
    t = ET.parse(xml).getroot()
    reds = [tc.get("name") for tc in t.iter("test-case") if tc.get("result") == "Failed"]
    summary = "total=%s passed=%s failed=%s" % (t.get("total"), t.get("passed"), t.get("failed"))
    return reds, summary, None


os.makedirs(BAK, exist_ok=True)
for m in MUTATIONS:
    src = os.path.join(ROOT, m["target"])
    bak = os.path.join(BAK, os.path.basename(m["target"]) + "." + m["key"])
    if not os.path.exists(bak):
        shutil.copy2(src, bak)
    m["sha"] = sha(bak)

results = []
for m in MUTATIONS:
    src = os.path.join(ROOT, m["target"])
    orig = io.open(os.path.join(BAK, os.path.basename(m["target"]) + "." + m["key"]), encoding="utf-8").read()
    assert orig.count(m["old"]) == 1, (m["key"], "锚点不唯一/不存在")
    if sha(src) != m["sha"]:
        shutil.copy2(os.path.join(BAK, os.path.basename(m["target"]) + "." + m["key"]), src)
    io.open(src, "w", encoding="utf-8").write(orig.replace(m["old"], m["new"]))

    reds, summary, err = run_fixture("falsify-" + m["key"])

    shutil.copy2(os.path.join(BAK, os.path.basename(m["target"]) + "." + m["key"]), src)
    restored_ok = sha(src) == m["sha"]

    hit = [e for e in m["expect"] if reds and e in reds]
    verdict = "RED" if (reds and len(hit) == len(m["expect"])) else ("PARTIAL" if reds else "GREEN(假存活)")
    results.append((m["key"], verdict, len(reds or []), m["expect"], hit, m["why"], summary, err, restored_ok))
    print("%s 还原SHA256=%s | %s | %s | 命中 %d/%d %s" % (
        m["key"], "一致" if restored_ok else "不一致!!", verdict, summary or err,
        len(hit), len(m["expect"]), m["why"]), flush=True)
    if reds:
        for r in reds:
            print("   RED", r, flush=True)

print("\n==== 汇总 ====", flush=True)
for key, verdict, n, expect, hit, why, summary, err, ok in results:
    print("%s | %s | 红 %s | 期望命中 %d/%d | SHA256 %s | %s" % (
        key, verdict, n, len(hit), len(expect), "一致" if ok else "不一致", why), flush=True)
print("DONE", flush=True)
