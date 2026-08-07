using System;
using System.IO;
using System.Text;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// 共享文本编码检测：UTF-8 BOM → 严格 UTF-8 校验 → GBK 兜底。
    /// CSV/INI/PROPERTIES/TXT 等键值文本格式统一复用，保证"加载原编码、保存按原编码写回"，
    /// 避免改编码后游戏/Excel 读不了。
    /// </summary>
    public static class TextEncodingDetector
    {
        private static bool _codePagesRegistered;
        private static readonly object RegisterLock = new object();

        /// <summary>自动识别文件编码：UTF-8 BOM → 严格 UTF-8 校验通过 → GBK 兜底。</summary>
        public static Encoding Detect(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(true);

            try
            {
                var strict = new UTF8Encoding(false, true);
                strict.GetString(bytes);
                return new UTF8Encoding(false);
            }
            catch (DecoderFallbackException)
            {
                return GetGbEncoding();
            }
        }

        /// <summary>GBK 编码（注册 CodePages 提供程序，线程安全只注册一次）。</summary>
        public static Encoding GetGbEncoding()
        {
            lock (RegisterLock)
            {
                if (!_codePagesRegistered)
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    _codePagesRegistered = true;
                }
            }
            return Encoding.GetEncoding(936); // GBK
        }
    }
}
