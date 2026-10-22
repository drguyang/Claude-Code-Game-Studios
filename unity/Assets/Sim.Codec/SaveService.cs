// ADR-010 §四/§六 —— SaveService 实现。
//
// 权威来源:
//   ADR-010 §一 —— 全二进制 codec,显式小端
//   ADR-010 §四 —— 校验和 + 自动回退 + 原子写 + 双档
//   ADR-010 §六 —— Checkpoint(SaveSlot) 唯一写入口
//   ADR-010 §三 —— 义务 1:WorldSeed 生成 + 存档头持久化
//
// 实现要点:
//   - 序列化在主线程,写盘在后台线程
//   - 原子写 = tmp + flush + rename
//   - 双档 bak:final 存在则先 final → bak
//   - 校验和 = SHA256,覆盖 checksum 字段之后的全部字节
//   - 后台线程禁调 Unity API

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档服务。文件 I/O + 校验和 + 原子写 + 双档。</summary>
    public sealed class SaveService : ISaveService
    {
        private readonly string _basePath;
        private readonly ISaveCodec _codec;
        private readonly IFoldedPredicate _folded;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="basePath">存档目录(须可写)。</param>
        /// <param name="codec">存档编解码器。</param>
        /// <param name="folded">
        /// 折叠谓词(7a 义务 G-2 · `IFoldedPredicate` 住 `Sim.Contracts`、实现住 `Sim`)。
        /// <para><b>为何走构造函数注入而不是给 <c>Checkpoint</c> 加形参</b>:ADR-010 <c>:331</c>
        /// 逐字 <c>interface ISaveService { void Checkpoint(SaveSlot slot); }</c>,并带
        /// **反射断言「公开方法参数集恰等于 <c>{SaveSlot}</c>」** ⇒ 加形参会直接打红该断言
        /// (ADR-025 §① 2026-10-21 甲案裁定 ④)。</para>
        /// <para><b>可空</b>:折叠尚未进入存档路径(本类的 <c>SaveBytes</c> 只收原始字节,
        /// <c>SaveCodec</c> 只有单事件编解码)⇒ 允许不注入。**未注入 ≠ 可折叠**:
        /// 消费方拿 <c>SaveService.FoldedPredicate</c> 判断是否可用,本类不替它决定。</para>
        /// </param>
        public SaveService(string basePath, ISaveCodec codec, IFoldedPredicate folded = null)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));
            _folded = folded;
            Directory.CreateDirectory(_basePath);
        }

        /// <summary>
        /// 注入的折叠谓词(未注入 ⇒ <c>null</c>)。
        /// <para>只读暴露:让装配层与测试可以核对「注入的是哪个实现」,不必反射私有字段。</para>
        /// </summary>
        public IFoldedPredicate FoldedPredicate => _folded;

        /// <summary>原子写:tmp → flush → rename。双档 bak。</summary>
        public void SaveBytes(SaveSlot slot, byte[] data)
        {
            string finalPath = GetFinalPath(slot);
            string tmpPath = GetTmpPath(slot);
            string bakPath = GetBakPath(slot);

            // 双档:final 存在则先 final → bak
            if (File.Exists(finalPath))
            {
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(finalPath, bakPath);
            }

            // 写 tmp
            File.WriteAllBytes(tmpPath, data);

            // flush + rename(原子)
            using (var fs = new FileStream(tmpPath, FileMode.Open, FileAccess.ReadWrite))
            {
                fs.Flush(true);
            }
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tmpPath, finalPath);
        }

        /// <summary>读档:校验和验证 + 自动回退 bak。</summary>
        public byte[] LoadBytes(SaveSlot slot)
        {
            string finalPath = GetFinalPath(slot);
            string bakPath = GetBakPath(slot);

            if (File.Exists(finalPath))
            {
                byte[] data = File.ReadAllBytes(finalPath);
                if (ValidateChecksum(data)) return data;
            }

            // 回退 bak
            if (File.Exists(bakPath))
            {
                byte[] data = File.ReadAllBytes(bakPath);
                if (ValidateChecksum(data)) return data;
            }

            throw new InvalidDataException($"存档损坏且无法回退:slot={slot.SlotSeq}");
        }

        /// <summary>校验和验证:SHA256 覆盖 checksum 字段之后的全部字节。</summary>
        public static bool ValidateChecksum(byte[] data)
        {
            if (data == null || data.Length < 36) return false;

            // 读取存储的 checksum(字节 4..35)
            byte[] stored = new byte[32];
            Array.Copy(data, 4, stored, 0, 32);

            // 计算实际 checksum(字节 36..end)
            byte[] actual = new byte[32];
            using (var sha = SHA256.Create())
            {
                byte[] covered = new byte[data.Length - 36];
                Array.Copy(data, 36, covered, 0, covered.Length);
                actual = sha.ComputeHash(covered);
            }

            return CryptographicOperations.FixedTimeEquals(stored, actual);
        }

        /// <summary>计算并写入 checksum。</summary>
        public static byte[] AddChecksum(byte[] dataWithoutChecksum)
        {
            // dataWithoutChecksum = magic(4) + saveVersion(4) + worldSeed(8) + configVersion(4) + tick(8) + snapshotOffset(8) + body
            // 输出 = magic(4) + checksum(32) + saveVersion(4) + ... + body
            if (dataWithoutChecksum == null || dataWithoutChecksum.Length < 32)
                throw new ArgumentException("数据过短");

            byte[] result = new byte[dataWithoutChecksum.Length + 32];

            // magic(4)
            Array.Copy(dataWithoutChecksum, 0, result, 0, 4);

            // checksum(32) — 覆盖 saveVersion 之后的全部字节
            byte[] covered = new byte[dataWithoutChecksum.Length - 4];
            Array.Copy(dataWithoutChecksum, 4, covered, 0, covered.Length);
            byte[] checksum;
            using (var sha = SHA256.Create())
            {
                checksum = sha.ComputeHash(covered);
            }
            Array.Copy(checksum, 0, result, 4, 32);

            // saveVersion(4) + worldSeed(8) + ... + body
            Array.Copy(dataWithoutChecksum, 4, result, 36, dataWithoutChecksum.Length - 4);

            return result;
        }

        // 高层接口(Checkpoint/SaveOnExit/Load)归后续 story 实现

        private string GetFinalPath(SaveSlot slot) => Path.Combine(_basePath, $"save_{slot.SlotSeq}.bin");
        private string GetTmpPath(SaveSlot slot) => Path.Combine(_basePath, $"save_{slot.SlotSeq}.tmp");
        private string GetBakPath(SaveSlot slot) => Path.Combine(_basePath, $"save_{slot.SlotSeq}.bak");
    }
}
