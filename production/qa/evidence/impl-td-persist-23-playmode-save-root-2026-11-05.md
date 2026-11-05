# `TD-persist-23` 兑现轮 —— PlayMode 夹具的存档根指到临时目录

| 字段 | 值 |
|---|---|
| **日期** | 2026-11-05(晚) |
| **用户指令** | 「按 TD-persist-23 把 PlayMode 夹具的存档根修了」 |
| **三元组** | 截至 `2026-11-05 21:0x CST` · 批次 `TD-persist-23 兑现轮` · commit `4cf8e4ad` + 工作树 |

---

## 1. 病根与修法

| | |
|---|---|
| **病根** | 任一 PlayMode 夹具 `AddComponent<BootRoot>()` 而**不设** `SaveRootOverride` / `SaveBasePathOverride` ⇒ `BootRoot` 走默认存档根 `Application.persistentDataPath/saves` ⇒ 启动序 `FirstSessionSave` 往**玩家真实存档目录**写 `save_auto.bin` |
| **修法 ①** | 21 个夹具(22 个 `AddComponent<BootRoot>` 站点)各补一行 `X.SaveRootOverride = PlayModeTempSaveRoot.New("<夹具名>");` —— 用 `SaveRootOverride`(**多世界形态**)而不是 `SaveBasePathOverride`,因为「不设覆盖」时 `BootRoot` 走的本来就是多世界那条路;换缝会**改掉被测行为** |
| **修法 ②** | 新增机械门 `playmode_save_root_gate_test.cs`(EditMode):扫 `Assets/Tests/PlayMode/**/*.cs`,凡含 `AddComponent<BootRoot>` 而**去注释后**既无 `.SaveRootOverride` 也无 `.SaveBasePathOverride` ⇒ 红;空集守卫 = 扫到的真启动序文件 < 10 时也红 |
| **辅助** | `unity/Assets/Tests/PlayMode/PlayModeTempSaveRoot.cs`(新):发一个**互不相同**的临时根(`Path.GetTempPath()/dyjc-pm-<夹具名>-<8 位 guid>`)。固定目录会让「A 写的档 B 读到」变成隐式依赖 |

**已覆盖的 6 个文件**未动:`save_slot_save_wiring_test` / `main_menu_wiring_test`(`SaveBasePathOverride`)、
`multi_world_boot_wiring_test` / `resume_existing_save_playmode_test` /
`adr041_bak_rescue_walkthrough` / `multi_world_visual_walkthrough`(`SaveRootOverride`)。

## 2. 判据(前后对照,可复跑)

```bash
S=~/.config/unity3d/DaYiJingCheng/大医精诚/saves
cp -a "$S" /tmp/saves.before
# 跑全量 PlayMode …
diff -rq /tmp/saves.before "$S"
```

| | 修前(2026-11-05 20:32) | 修后(2026-11-05 21:0x) |
|---|---|---|
| 差异 | `只在 live 中存在:save_auto.bin`(1622 B / `DYJQ` / `SaveVersion 5`) | **零输出** |

## 3. 证伪轮(3 变异各红在靶点;SHA-256 逐字节还原)

| 变异 | 做法 | 结果 |
|---|---|---|
| **M1** 漏一个夹具 | 删掉 `case_opened_wiring_test.cs` 的覆盖行 | 红,报文**点名** `case_opened_wiring_test` |
| **M2** 判据串失效 | `BootSpawn` 改成 `AddComponent<BootRootX>` | 红,命中**空集守卫**:「只扫到 0 个…(下限 10),本条此刻**没有**判别力」 |
| **M3** 注释顶替门 | 把覆盖行改成 `// _boot.SaveRootOverride = …` | 红(证明判据锚在**剥注释后的代码**上,不是锚在文本上) |

## 4. 回归

| 套件 | 结果 | 等式 / 对照 |
|---|---|---|
| EditMode | **`6941 / 6904 / 0 / 0 / 37`**(`ed-tdp23.xml`) | `6941 − 6940 = 1` = 新增门数 ✅ |
| PlayMode | **`341 / 337 / 0 / 2 / 2`**(`pm-tdp23.xml`) | 与改前**逐位相同** —— 补覆盖**不改**任何被测行为 ✅ |
| 构建 | `Build Finished, Result: Success` · `错误数 = 0` · 18.7 s(`player-build-tdp23.log`) | 门链无报错(⚠️ **绿 ≠ 门跑了**,此处不据此主张任何门已被验证) |

## 5. 未做 / 边界

1. **临时目录不清理**。`Path.GetTempPath()` 下会累积 `dyjc-pm-*` 目录(每个几 KB)。理由:
   既有夹具(`save_slot_save_wiring_test` 等)同样只建不删;`TearDown` 里删除若抛异常,
   会把「清理」变成二次失败。⇒ 需要的话由系统临时区回收。
2. **`FsWriteBoundaryGate` 未扩面**。它扫的是测试里的**直接写盘调用**,看不见 `BootRoot` 的间接写;
   本轮的补法是把那一面**交给新门**,不是在旧门里堆规则(债条解除动作 ③ 的「不做也行,但须写明」)。
3. **本修复只保证「回归不写真实存档根」**;`BootRoot` 默认根本身没变(生产形态就该如此)。
   若将来有**新的** PlayMode 夹具忘了设覆盖,由 M1 验过的那条门拦。
