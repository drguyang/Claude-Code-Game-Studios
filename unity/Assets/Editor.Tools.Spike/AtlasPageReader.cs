// S2 · PAGES_MAX spike 运行期回读者(019-b · sprint-09 · 2026-10-14)
//
// 用途:batch 模式读不到图集页数(UIDocument.panel 恒 null —— probe3 实测),
//   本组件在**编辑器 Play** 时挂膀,真渲染发生后回读运行期 DynamicAtlas 页数并打 Console。
//
// ▍它测什么(承接已裁定口径)
//   用户裁定 2026-10-14 = 「抬门槛到 1024 再实测」+「编辑器取样(数据最全)」。
//   上游 AtlasPageProbe.SetupEditorSample 已建好:临时场景 + 抬门槛内存面板 + 18 张图。
//   本组件补上 batch 拿不到的最后一环:**运行期页数 N** 与 **溢出行为**。
//
// ▍判据(可证伪)
//   ① 第一帧真渲染后 ≈0.5s:读出页数 N(反射 DynamicAtlasCore.m_Pages 或等价真值)
//   ② 若页数读不到:报不可达 + 列出实际字段名(供下一轮按真名读,不猜)
//   ③ 18 张全部可见 ⇒ 若页数 = 1 则说明 1024² × 15 张装进了同一页(4096²)
//      ⇒ 该读数即「门槛 1024」下的实测页数,是冻结件的核心数值
//
// ⚠️ 零写入、零生产路径改动:本组件只读 + 打 Console。
// ⚠️ 临时资产(AtlasProbe* 前缀)已被 .gitignore 拦下,永不提交。
//
// ▍引擎面:UI Toolkit 图集行为 post-cutoff(ADR-013 Knowledge Risk HIGH,§6.6 假设 6
//   spike 未跑)⇒ 本组件的实测输出**同时**是 ADR-013 §6.6 假设 6 的结清输入。

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.EditorTools.Spike
{
    /// <summary>运行期图集页数回读者(仅编辑器 Play 用,零出货影响)。</summary>
    public class AtlasPageReader : MonoBehaviour
    {
        private const string LogTag = "[AtlasPageReader]";

        private UIDocument _doc;
        private float _startedAt = -1f;
        private bool _reported;
        private int _frameCount;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                Debug.LogError($"{LogTag} 缺 UIDocument 组件");
                enabled = false;
                return;
            }
            _startedAt = Time.realtimeSinceStartup;
            Debug.Log($"{LogTag} READER_ARMED|panelAttached={_doc.panelSettings != null}");
        }

        private void Update()
        {
            if (_reported) return;
            _frameCount++;

            // 等渲染真实发生:给足 60 帧 + 0.5s,让引擎分配器完成分配
            if (_frameCount < 60 || Time.realtimeSinceStartup - _startedAt < 0.5f)
                return;

            _reported = true;
            Report();
        }

        /// <summary>反射链:UIDocument.panel → m_Renderer → m_Atlas → 页集合。</summary>
        private void Report()
        {
            try
            {
                object panel = GetPanel();
                if (panel == null)
                {
                    Debug.Log($"{LogTag} RESULT|status=PANEL_NULL|" +
                              $"panelSettingsNull={_doc.panelSettings == null}");
                    return;
                }

                object atlas = FindAtlas(panel);
                if (atlas == null)
                {
                    Debug.Log($"{LogTag} RESULT|status=ATLAS_NOT_FOUND|panelType={panel.GetType().FullName}");
                    DumpPanelFields(panel);
                    return;
                }

                int pages = CountPages(atlas);
                Debug.Log($"{LogTag} RESULT|status=OK|atlasType={atlas.GetType().FullName}|" +
                          $"pageCount={pages}|frames={_frameCount}");

                // 顺带:把 atlas 上的关键字段值一并打出来(尺寸 / 分配器状态),供冻结件引用
                DumpAtlasState(atlas);
            }
            catch (Exception e)
            {
                Debug.Log($"{LogTag} RESULT|status=EXCEPTION|type={e.GetType().Name}|msg={e.Message}");
            }
        }

        private object GetPanel()
        {
            // 运行时 VisualElement.panel 是 public(与 batch 下 doc.panel 不同 —— 这里是 rootVisualElement)
            var root = _doc.rootVisualElement;
            return root?.panel;
        }

        private object FindAtlas(object panel)
        {
            // 面板 → 渲染器 → 动态图集(逐层反射,任一环缺失即报不可达)
            foreach (string fieldName in new[] { "m_Renderer", "renderer", "m_PanelRenderer" })
            {
                var f = panel.GetType().GetField(fieldName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f == null) continue;

                object renderer = f.GetValue(panel);
                if (renderer == null) continue;

                foreach (string aName in new[] { "m_Atlas", "atlas", "m_DynamicAtlas" })
                {
                    var af = renderer.GetType().GetField(aName,
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (af == null) continue;

                    object atlas = af.GetValue(renderer);
                    if (atlas != null) return atlas;
                }

                Debug.Log($"{LogTag} TRACE|rendererFound={renderer.GetType().FullName}|" +
                          $"butAtlasFieldMissing");
            }
            return null;
        }

        private int CountPages(object atlas)
        {
            foreach (string pName in new[] { "m_Pages", "pages", "m_AllocatedPages", "m_PagesList" })
            {
                var pf = atlas.GetType().GetField(pName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (pf == null) continue;

                if (pf.GetValue(atlas) is ICollection coll)
                    return coll.Count;

                Debug.Log($"{LogTag} TRACE|pagesField={pName}|notACollection|" +
                          $"type={pf.FieldType.Name}");
            }

            // 页集合字段名不确定时,列出全部候选(不猜,报真名供下一轮读)
            var names = new System.Text.StringBuilder();
            foreach (FieldInfo f in atlas.GetType().GetFields(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                names.Append(f.Name).Append("|");
            Debug.Log($"{LogTag} TRACE|pagesFieldNotFound|atlasFields={names}");
            return -1;
        }

        private void DumpAtlasState(object atlas)
        {
            var sb = new System.Text.StringBuilder();
            foreach (FieldInfo f in atlas.GetType().GetFields(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
            {
                string n = f.Name.ToLowerInvariant();
                if (!(n.Contains("size") || n.Contains("alloc") || n.Contains("page") ||
                      n.Contains("excess"))) continue;
                try
                {
                    object v = f.GetValue(atlas);
                    if (v is ICollection c) sb.Append($"{f.Name}=count{c.Count}|");
                    else sb.Append($"{f.Name}={v}|");
                }
                catch { /* 读不出的字段跳过,不阻断报告 */ }
            }
            Debug.Log($"{LogTag} ATLAS_STATE|{sb}");
        }

        private void DumpPanelFields(object panel)
        {
            var sb = new System.Text.StringBuilder();
            foreach (FieldInfo f in panel.GetType().GetFields(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                sb.Append(f.Name).Append("|");
            Debug.Log($"{LogTag} PANEL_FIELDS|{sb}");
        }
    }
}
