# S25-4d · 口述文案跨表对齐 + 构建期跨表门(2026-11-07)

| 字段 | 值 |
|---|---|
| **卡** | `production/epics/tutorial-onboarding/story-004-three-media-scheduling-paper-modal.md` |
| **债** | `TD-tutorial-01`(同日**结案**) |
| **权威** | `AC-48-15`(教学口述零字幕是禁项)· `AC-44-15`(字幕文本单一真源 = 44 事件表)· ADR-014 §三 |
| **裁定** | 用户 2026-11-07:「甲:对齐两张表文案并补上跨表校验」 |
| **commit** | 见本轮提交(基线 `cdbefccc` + 工作树) |

---

## 1. 做了什么

| 面 | 落点 |
|---|---|
| **数据** | `assets/data/48_tutorial_content.json` 四条 `narration[].text` → 事件表 `subtitle_text` **逐字同值**;重烘 `unity/Assets/DataCooked/48_tutorial_content.cooked.bytes`(1904 B) |
| **门** | `unity/Assets/Editor.Tools.Gates/TutorialNarrationSubtitleCrossRefGate.cs`:C1 缺行 / C2 无字幕(缺失或空串)/ C3 文本不一致 + G1 口述 0 行 / G2 事件表 0 行 / G3 cue 重复 |
| **接线** | `AssemblyGates.RunAll`(菜单 / reload / `BuildGate` 三处共用)+ `gate_wiring_manifest_test` 的 `EntryWiringManifest` 与 `ClassVerdict` 同批补登 |
| **判据** | `unity/Assets/Tests/EditMode/Tutorial/tutorial_narration_subtitle_crossref_test.cs` **9 条** |

**方向**:只查「48 → 44」。事件表里另有字幕文本(病人语声 / 环境音)是合法的,不反向报红。

---

## 2. 对齐后的四行(逐字)

| cue | 两表统一后的文本 |
|---|---|
| `Narration_TeachObserve_01` | 喘着气,气色、神态,都在脸上。 |
| `Narration_TeachJudge_01` | 哼了一声,看明白了,就翻本子记一笔。 |
| `Narration_TeachClose_01` | 叹口气,处置妥当,一个病例才算了结。 |
| `Narration_TeachArchive_01` | 哼着小曲,要紧的,都写进册子。 |

复算:
```bash
python3 - <<'PY'
import json
c=json.load(open('assets/data/48_tutorial_content.json',encoding='utf-8'))
a=json.load(open('assets/data/audio_events.json',encoding='utf-8'))
at={r['cue']:r.get('subtitle_text') for r in a['rows']}
print([(r['cueId'], r['text']==at.get(r['cueId'])) for r in c['narration']])
PY
# 期望:四条全 True
```

---

## 3. 真负例:这条门抓得住原本的 bug(不是空转)

把 48 JSON 临时还原成 `HEAD`(分叉版)并重烘,然后**两条路各跑一次**:

| 路 | 结果 | 证据 |
|---|---|---|
| **夹具** | `test_realArtifacts_narrationAndSubtitleAreAligned` **红**,四条 `[C3]` 全报(含两边原文) | `Logs/s25-4-xref-neg.xml`(9 跑 8 过 1 红) |
| **构建** | `Build Finished, Result: Failure`,`[48↔44][C3]` 四条在构建日志 | `Logs/s25-4-build-neg.log:976 / 997 / 1018 / 1039`;失败行 `:1138` |

⇒ 该门在生产链路里**真的会拦**,且拦的正是历史上真实存在的那四条分叉。

**还原**:JSON `0929c1b9…` / 烘焙产物 `942fdbfe…` 逐字节回位;还原后干净构建 `Success`
(`Logs/s25-4-build-final.log:8559`,起始 `2026-11-07T07:15:11Z`)。

---

## 4. 证伪轮(两处,各自单红)

| 变异 | 内容 | 红 |
|---|---|---|
| **M1** | 停用 C3 的文本比较(`if (false && …)`) | `test_detect_textMismatch_reportsBothSides` **单红**(其余 8 绿) |
| **M2** | 删掉 G1(口述 0 行)守卫 | `test_emptyNarration_isRed_notVacuousGreen` **单红** |

还原 = SHA-256 `f40f4dc6…` 逐字节一致,`grep -c FALSIFY` = **0**。

---

## 5. 回归三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`7076 / 7039 / 0 / 0 / 37`** | `2026-11-07T07:20:04Z` | S25-4d 全量(终树) | `cdbefccc` + 工作树 |
| PlayMode | **`364 / 360 / 0 / 2 / 2`** | `2026-11-07T07:21:12Z` | 同批 | 同上 |

**等式判据**:`7076 − 7067 = 9` = 本件新增 9 条;PlayMode `364 = 364`(零回归)。

⚠️ **中途红过一次(6 条,如实登记)**:改文案后 `tutorial_content_validation_test`(5 条)与
`tutorial_disease_name_source_test`(1 条)的**种子锚点**找不到旧句子 —— 那是夹具自带的
「前提守卫」在正常工作(而不是静默变异了个空)。处置 = 把锚点更新成新文案,**不是**放宽守卫。

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s25-4-em-final.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s25-4-pm-final.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

---

## 6. 边界(禁借绿)

1. 本门读**烘焙产物** ⇒ 「改了源 JSON 忘重烘」时看的是旧产物(两边都旧、可能仍自洽)⇒ 可能绿。
   该形态由 `cooked_drift_all_artifacts_test` 兜 —— **互补,不是重复**。
2. 构建级真负例是**历史观测**(本轮跑过一次),常驻的是门本身 + 9 条夹具。
3. 本条只解决「文案两处各写一份」;**没有**解决三媒体的另两条腿(示范 / 纸),那两条仍是
   `BLOCKED-BY` 13 / 6+4(见 `story-004` 的 AC 注与 `S25-4` 收口记录)。
