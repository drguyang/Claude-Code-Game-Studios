# Story 013 QA Evidence —— 反幻想守门登记面

- **Story**: Story 013: 反幻想守门登记面(P0 / 后阶段)
- **Status**: Complete
- **Date**: 2026-09-27
- **Tester**: 3 侧静态确认 + 登记
- **Evidence type**: UI (ADVISORY) + 登记文档(BLOCKING)

---

## AC-3-F1a① 反幻想门 · 目视半条(3 侧交付物)

### 3 侧零渲染不变量确认

**结论**: ✅ 3 侧零按键提示浮层 / 零 QTE 提示条 / 零「按 X 键」持久 HUD 代码路径

**依据**:
- 3 的程序集 `Gameplay.Input` 中**零 UI 渲染代码**（无 Canvas、无 UI Toolkit、无 IMGUI 提示层）
- 3 不引用 `UnityEngine.UI`、`UnityEngine.EventSystems`、`UnityEngine.UILayout` 等 UI 命名空间
- 3 的唯一「呈现」出口是 `FocusNavigationIntent`（只读意图）+ `BindingQuery`（只读查询），均不涉及渲染
- `InputDebugView`(Story 012) 已被 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 条件编译包裹，Release 构建中零代码路径

**门条目登记**:
- 联合归属: **42(呈现) + 48(教学)**
- 3 侧角色: 键名供给方 + 零渲染不变量的静态确认
- 走查执行: BLOCKED-BY 42/48
- 证据产出(截图集 + 主创签核): BLOCKED-BY 42/48

---

## AC-3-F1a② 手柄单机走查半条(登记 + 交接)

### 3 侧交付物

**结论**: ✅ 登记 + 键名交接

**iconKey 键名清单**:
- 来源: `BindingQuery.IconKeyOf(device, bindingPath)` — 稳定键名字符串，非素材
- 格式: `{DeviceClass}.{BindingPath}` (例: `Kbd.E`, `Pad.North`, `Pad.LeftStick_Up`, `Xr.PrimaryAction`)
- 派生规则: 设备类前缀 + 设备内路径段逐段首字母大写，"_" 连接复合 part
- 3 侧不产生素材图集，素材归属 42/美术

**两纸走查登记**:
| 走查对象 | 归属系统 | 状态 |
|---|---|---|
| 脉案(39) | 39 脉案系统 | 待 39 界面就位后走查 |
| 20 P0 容器界面 | 20 库存与物品 | 待 20 界面就位后走查 |

**联合语义**: F1a①(目视零提示) 与 F1a②(手柄两纸走查) **缺一即败**

**注意**: 43 出诊箱纸归 F1b(P1a)，P0 不出「出诊箱纸」

---

## AC-3-F1b 后阶段扩展(登记不执行)

**结论**: ✅ 已登记，P0 不适用

- VR 走查 + 43 出诊箱纸 = F1b，P1a/P1b 阶段签核
- P0 轮无走查证据产出

---

## 门登记汇总

| AC | 3 侧交付 | 联合归属 | 执行方 | 状态 |
|---|---|---|---|---|
| F1a① | 零渲染不变量的静态确认 + 门条目登记 | 42 + 48 | 42/48 | BLOCKED-BY |
| F1a② | iconKey 键名清单交接 + 走查登记 | 42 + 48 | 42/48 + 39/20 | BLOCKED-BY |
| F1b | 阶段门挂账 | — | — | 已登记(P0 不适用) |

---

## 签核

- [x] 3 侧零渲染不变量静态确认
- [x] 门条目登记(EPIC / 故事链)
- [x] iconKey 键名清单交接文档
- [x] F1b P1a/P1b 阶段门挂账
