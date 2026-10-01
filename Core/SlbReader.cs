using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SlbViewer
{
    /// <summary>
    /// AutoCAD SLB (Slide Library) 文件读取器
    /// </summary>
    public class SlbReader
    {
        private const int SLIDE_NAME_LENGTH = 32; // 幻灯片名称字段长度
        private const int OFFSET_LENGTH = 4;      // 偏移量字段长度

        /// <summary>
        /// 获取目录表的起始位置（跳过文件头）
        /// </summary>
        private long GetDirectoryStartPosition(FileStream fs, BinaryReader reader)
        {
            fs.Seek(0, SeekOrigin.Begin);

            // 读取前面的字节来检查文件头
            byte[] headerCheck = reader.ReadBytes(Math.Min(40, (int)fs.Length));
            string headerStr = Encoding.ASCII.GetString(headerCheck, 0, Math.Min(headerCheck.Length, 29));

            if (headerStr.StartsWith("AutoCAD Slide Library"))
            {
                // 标准SLB文件头："AutoCAD Slide Library 1.0\r\n\x1a\0"
                // 文件头：29字节
                // 然后有3字节的元数据（可能是幻灯片数量等）
                // 目录从第32字节开始（偏移量32）
                return 32;
            }

            // 不是标准格式，从头开始
            return 0;
        }

        /// <summary>
        /// 读取 SLB 文件中的所有幻灯片名称
        /// </summary>
        /// <param name="filePath">SLB 文件路径</param>
        /// <returns>幻灯片名称列表</returns>
        public List<string> ReadSlideNames(string filePath)
        {
            List<string> slideNames = new List<string>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // SLB 文件格式:
                    // 文件头: "AutoCAD Slide Library 1.0\r\n\x1a\0"
                    // 然后是目录表，每个条目包含:
                    // - 32 字节: 幻灯片名称 (以 null 结尾的字符串)
                    // - 4 字节: 幻灯片数据在文件中的偏移位置 (小端序 32 位整数)
                    // 目录表以一个空名称条目结束

                    // 获取目录起始位置
                    long directoryStart = GetDirectoryStartPosition(fs, reader);
                    fs.Seek(directoryStart, SeekOrigin.Begin);

                    while (fs.Position < fs.Length)
                    {
                        // 读取幻灯片名称 (32 字节)
                        byte[] nameBytes = reader.ReadBytes(SLIDE_NAME_LENGTH);

                        // 读取偏移量 (4 字节)
                        if (fs.Position + OFFSET_LENGTH > fs.Length)
                        {
                            break; // 文件结束
                        }

                        uint offset = reader.ReadUInt32();

                        // 检查是否是目录终止符：第一个字节为0x00表示名称为空
                        if (nameBytes.Length == 0 || nameBytes[0] == 0x00)
                        {
                            break;
                        }

                        // 将字节数组转换为字符串，去除 null 字符
                        string slideName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0').Trim();

                        // 如果处理后的名称不为空，添加到列表
                        if (!string.IsNullOrEmpty(slideName))
                        {
                            slideNames.Add(slideName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取 SLB 文件时出错: {ex.Message}", ex);
            }

            return slideNames;
        }

        /// <summary>
        /// 获取 SLB 文件的详细信息
        /// </summary>
        /// <param name="filePath">SLB 文件路径</param>
        /// <returns>包含幻灯片名称和偏移量的字典</returns>
        public Dictionary<string, uint> ReadSlideDetails(string filePath)
        {
            Dictionary<string, uint> slideDetails = new Dictionary<string, uint>();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件不存在: {filePath}");
            }

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // 获取目录起始位置
                    long directoryStart = GetDirectoryStartPosition(fs, reader);
                    fs.Seek(directoryStart, SeekOrigin.Begin);

                    while (fs.Position < fs.Length)
                    {
                        // 读取幻灯片名称
                        byte[] nameBytes = reader.ReadBytes(SLIDE_NAME_LENGTH);

                        // 读取偏移量
                        if (fs.Position + OFFSET_LENGTH > fs.Length)
                        {
                            break;
                        }

                        uint offset = reader.ReadUInt32();

                        // 检查是否是目录终止符：第一个字节为0x00表示名称为空
                        if (nameBytes.Length == 0 || nameBytes[0] == 0x00)
                        {
                            break;
                        }

                        // 将字节数组转换为字符串，去除 null 字符
                        string slideName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0').Trim();

                        // 如果处理后的名称不为空，添加到字典
                        if (!string.IsNullOrEmpty(slideName))
                        {
                            slideDetails[slideName] = offset;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取 SLB 文件详细信息时出错: {ex.Message}", ex);
            }

            return slideDetails;
        }

        /// <summary>
        /// 读取指定幻灯片的数据
        /// </summary>
        /// <param name="filePath">SLB 文件路径</param>
        /// <param name="slideName">幻灯片名称</param>
        /// <returns>幻灯片的原始数据字节数组</returns>
        public byte[]? ReadSlideData(string filePath, string slideName, out string debugInfo)
        {
            debugInfo = "";
            var slideDetails = ReadSlideDetails(filePath);

            if (!slideDetails.ContainsKey(slideName))
            {
                debugInfo = "幻灯片不在目录中";
                return null;
            }

            uint offset = slideDetails[slideName];
            debugInfo += $"偏移量: {offset}\n";

            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    debugInfo += $"文件大小: {fs.Length}\n";

                    // 找到下一个幻灯片的偏移量以确定当前幻灯片的大小
                    uint nextOffset = uint.MaxValue;
                    foreach (var kvp in slideDetails)
                    {
                        if (kvp.Value > offset && kvp.Value < nextOffset)
                        {
                            nextOffset = kvp.Value;
                        }
                    }

                    debugInfo += $"下一个偏移量: {(nextOffset == uint.MaxValue ? "文件结束" : nextOffset.ToString())}\n";

                    // 计算数据大小
                    long dataSize;
                    if (nextOffset == uint.MaxValue)
                    {
                        dataSize = fs.Length - offset;
                    }
                    else
                    {
                        dataSize = nextOffset - offset;
                    }

                    debugInfo += $"数据大小: {dataSize}\n";

                    // 检查数据大小是否合理
                    if (dataSize <= 0)
                    {
                        debugInfo += "数据大小 <= 0\n";
                        return null;
                    }

                    if (dataSize > 10 * 1024 * 1024)
                    {
                        debugInfo += "数据大小超过10MB限制\n";
                        return null;
                    }

                    // 跳转到幻灯片数据位置
                    fs.Seek(offset, SeekOrigin.Begin);

                    // 读取所有幻灯片数据
                    byte[] slideData = reader.ReadBytes((int)dataSize);

                    debugInfo += $"成功读取 {slideData.Length} 字节\n";

                    return slideData;
                }
            }
            catch (Exception ex)
            {
                debugInfo += $"异常: {ex.Message}\n";
                throw new Exception($"读取幻灯片数据时出错: {ex.Message}", ex);
            }
        }

        // 保持向后兼容的重载方法
        public byte[]? ReadSlideData(string filePath, string slideName)
        {
            return ReadSlideData(filePath, slideName, out _);
        }
    }
}
