using System;
using System.Security.Cryptography;
using System.Text;

namespace Project.Launch.Tools
{
    public static class OfflineUuidGenerator
    {
        /// <summary>
        /// 生成标准离线 UUID（兼容 HMCL / Java 实现）
        /// 算法：MD5("OfflinePlayer:" + username) → 设置 version/variant → 按大端序输出 hex
        /// </summary>
        public static string GenerateUuidString(string username)
        {
            // 1. 构造输入字符串
            string input = $"OfflinePlayer:{username}";

            // 2. 计算 MD5 哈希
            byte[] hash;
            using (MD5 md5 = MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            }

            // 3. 设置 UUID 版本（version 3）
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
            // 4. 设置变体（IETF variant）
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

            // 5. ★ 手动拼接 hex 字符串（按大端序，不用 Guid 类）
            // 格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
            return string.Format(
                "{0:x2}{1:x2}{2:x2}{3:x2}-{4:x2}{5:x2}-{6:x2}{7:x2}-{8:x2}{9:x2}-{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}",
                hash[0], hash[1], hash[2], hash[3],
                hash[4], hash[5],
                hash[6], hash[7],
                hash[8], hash[9],
                hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]
            );
        }

        // 如果你还需要返回 Guid 类型（用于现有代码），保留此方法并标记为 Obsolete
        [Obsolete("此方法返回的 Guid 可能与 Java 服务器不兼容，请使用 GenerateUuidString 替代")]
        public static Guid Generate(string username)
        {
            string uuidString = GenerateUuidString(username);
            return Guid.ParseExact(uuidString, "D");
        }
    }
}