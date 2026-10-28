// `MinimalWorldFactory.IsRecoverableSlotLoadFailure` 的**真值表**夹具(2026-10-28 代码评审 **A-23**)。
//
// 立此件缘由:合成世界的槽位装载失败会**降级为空目录**(=`PlaceableChecker` 恒 `SlotNotFound`
// ⇒ 玩家建不了任何东西),而那条 `catch` 的判据此前**内联**在 catch 里 ——
// 「`ArgumentNullException` 会不会被吞」只能靠读代码回答,没有任何夹具能钉住。
// 吞掉的后果不是崩溃,是**误导**:报「产物缺失或损坏…修法:先跑关卡工具导出」,
// 而真因是某次重构把 null 传进了装载链 ⇒ 开发者去重烘产物,方向全错。
//
// ⚠️ 本件钉的是**谓词**,不是「`Create()` 的那条 `catch` 真的调用了它」——
//   后者需要盘上一份损坏的 cooked 产物,`LoadSlotCatalog` 读的是真实数据目录,不可注入
//   ⇒ 该半边**登记为未证**(禁借绿)。
//
// ⚠️ 不写盘(`FsWriteBoundaryGate` 无关)。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>装载失败分类谓词:哪些可降级、哪些必须冒泡。</summary>
    [TestFixture]
    public class minimal_world_slot_load_failure_test
    {
        /// <summary>环境类失败 ⇒ 可降级(空目录 + 响亮登记)。</summary>
        /// <remarks><b>判别力</b>:把谓词的返回改成恒 `false` ⇒ 本测 6 条全红
        /// (而「降级」这条生产行为也就没了 —— 启动期硬失败取代了响亮登记)。</remarks>
        [Test]
        public void test_isRecoverable_environmentFailures_areRecoverable()
        {
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new InvalidOperationException("产物缺失")), Is.True,
                "`DataCookedDisk` 的 E-13 口径用 `InvalidOperationException` 报「产物不在」");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new InvalidDataException("字节损坏")), Is.True);
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new IOException("读失败")), Is.True);
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new UnauthorizedAccessException("无权限")), Is.True);
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new FileNotFoundException("文件不在")), Is.True,
                "`FileNotFoundException : IOException` —— 子类须被父类分支吸收");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new ArgumentException("产物名为空")), Is.True,
                "**裸** `ArgumentException`:装载链自抛(空产物名),属环境类而非编码缺陷");
        }

        /// <summary>编码缺陷 / 无关异常 ⇒ **必须冒泡**(不得被伪装成「产物损坏」)。</summary>
        /// <remarks><b>判别力</b>:删掉谓词里 `is ArgumentNullException` 那一行 ⇒ 本条第一句红;
        /// 删掉 `is ArgumentOutOfRangeException` ⇒ 第二句红;把 `NotSupportedException` 加回白名单
        /// ⇒ 第三句红。</remarks>
        [Test]
        public void test_isRecoverable_programmingDefects_propagate()
        {
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new ArgumentNullException("rows")), Is.False,
                "null 参数 = 编码缺陷 ⇒ 吞掉会把开发者引向「重烘产物」");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new ArgumentOutOfRangeException("rowCount")), Is.False,
                "越界参数 = 编码缺陷 ⇒ 同上");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new NotSupportedException("未实现的读取")), Is.False,
                "非环境类;全链路无已证实的抛出源 ⇒ 不扩面");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new NullReferenceException()), Is.False,
                "NRE 必须冒泡(文件头「编程错误照常冒泡」的唯一原成员)");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new InvalidCastException()), Is.False);
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(
                new Exception("其它")), Is.False, "基类异常不在白名单 —— 白名单制而非黑名单制");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(null), Is.False,
                "`null` 不是异常实例;返回 false 而非抛(谓词在 catch 之外也可能被复用)");
        }

        /// <summary>
        /// **类型伪装陷阱**:以 `ArgumentException` 静态类型传入的 `ArgumentNullException`
        /// 仍须判为**不可降级**。
        /// </summary>
        /// <remarks><b>判别力</b>:若谓词只按「`is ArgumentException`」一遍判断(不先挡两个子类)
        /// ⇒ 本条红。这是 C# 里最容易写错的那一半 —— `catch (ArgumentException)` 会吞掉两个子类。</remarks>
        [Test]
        public void test_isRecoverable_argumentNullTypedAsArgumentException_stillPropagates()
        {
            ArgumentException asBase = new ArgumentNullException("rows");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(asBase), Is.False,
                "静态类型是基类不代表运行期是「裸」ArgumentException —— 子类必须先挡");

            ArgumentException outOfRange = new ArgumentOutOfRangeException("rowCount");
            Assert.That(MinimalWorldFactory.IsRecoverableSlotLoadFailure(outOfRange), Is.False);
        }
    }
}
