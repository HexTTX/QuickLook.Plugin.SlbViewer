using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace SlbViewer
{
    /// <summary>
    /// AutoCAD 幻灯片解析器
    /// 解析 .sld 格式的幻灯片数据
    ///
    /// 根据 AutoCAD 幻灯片文件格式规范实现：
    /// - 文件头部：31字节，包含ID字符串、尺寸、宽高比等信息
    /// - 数据记录：每个记录开头为双字节字段，其高位字节表示记录类型
    /// </summary>
    public class SlideParser
    {
        public class SlideHeader
        {
            public string Type { get; set; } = "";
            public int Width { get; set; }
            public int Height { get; set; }
            public double AspectRatio { get; set; }
            public bool IsLittleEndian { get; set; } = true;
            public bool IsVMLFormat { get; set; } = false; // 标识是否为V M L格式
        }

        public class DrawCommand
        {
            public enum CommandType
            {
                Line,
                Point,
                Vector,
                SolidFill,
                EndOfFile
            }

            public CommandType Type { get; set; }
            public List<PointF> Points { get; set; } = new List<PointF>();
            public int ColorIndex { get; set; }
        }

        public SlideHeader Header { get; private set; } = new SlideHeader();
        public List<DrawCommand> Commands { get; private set; } = new List<DrawCommand>();
        public List<string> DebugInfo { get; private set; } = new List<string>();

        /// <summary>
        /// 解析幻灯片数据
        /// </summary>
        public bool Parse(byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    // 读取文件头
                    if (!ParseHeader(reader))
                    {
                        // 尝试解析为旧格式
                        ms.Seek(0, SeekOrigin.Begin);
                        ParseOldFormat(reader);
                    }

                    // 读取绘图命令
                    ParseCommands(reader);

                    return Commands.Count > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool ParseHeader(BinaryReader reader)
        {
            try
            {
                if (reader.BaseStream.Length < 31)
                {
                    return false;
                }

                // 读取整个文件头的原始字节（至少31字节，可能是34字节）
                long startPos = reader.BaseStream.Position;
                byte[] headerBytes = reader.ReadBytes(Math.Min(34, (int)(reader.BaseStream.Length - startPos)));

                if (headerBytes.Length < 31)
                    return false;

                // 1. 检查ID字符串（17字节）
                string idStr = System.Text.Encoding.ASCII.GetString(headerBytes, 0, 17).TrimEnd('\0');

                if (!idStr.StartsWith("AutoCAD Slide"))
                {
                    return false;
                }

                // 检测是否为V M L格式
                if (idStr.Contains("V") && idStr.Contains("M") && idStr.Contains("L"))
                {
                    Header.IsVMLFormat = true;
                    Header.Type = "AutoCAD Slide V M L";
                }
                else
                {
                    Header.Type = "AutoCAD Slide";
                }

                // 2. 类型标识符（1字节，位置17）
                byte typeId = headerBytes[17];

                // 3. 层指示器（1字节，位置18）
                byte levelIndicator = headerBytes[18];

                // 先判断是旧格式还是新格式，因为这决定了头部长度和字节序判断方式
                bool isOldFormat = (levelIndicator == 1);
                bool isLittleEndian = true; // 默认小端序
                int headerLength = 31; // 默认新格式

                if (isOldFormat)
                {
                    // 旧格式：34字节头，没有测试数字，假定小端序
                    headerLength = 34;
                    isLittleEndian = true;
                    Header.Type += " (旧格式)";
                }
                else
                {
                    // 新格式：31字节头，有测试数字（位置29-30）
                    headerLength = 31;

                    // 先用小端序读取测试数字
                    short testNumberLE = (short)(headerBytes[29] | (headerBytes[30] << 8));

                    // 判断字节序：0x1234表示小端序，0x3412表示大端序
                    if (testNumberLE == 0x1234)
                    {
                        isLittleEndian = true;
                    }
                    else if (testNumberLE == 0x3412)
                    {
                        isLittleEndian = false;
                    }
                    else
                    {
                        // 测试数字不匹配，尝试小端序
                        isLittleEndian = true;
                    }

                    Header.Type += " (新格式)";
                }

                Header.IsLittleEndian = isLittleEndian;

                // 现在根据字节序读取宽度和高度（位置19-22）
                if (isLittleEndian)
                {
                    Header.Width = (short)(headerBytes[19] | (headerBytes[20] << 8));
                    Header.Height = (short)(headerBytes[21] | (headerBytes[22] << 8));
                }
                else
                {
                    Header.Width = (short)((headerBytes[19] << 8) | headerBytes[20]);
                    Header.Height = (short)((headerBytes[21] << 8) | headerBytes[22]);
                }

                // 读取宽高比
                if (isOldFormat)
                {
                    // 旧格式：8字节double（位置23-30）
                    long aspectBits;
                    if (isLittleEndian)
                    {
                        aspectBits = headerBytes[23] | ((long)headerBytes[24] << 8) | ((long)headerBytes[25] << 16) |
                                   ((long)headerBytes[26] << 24) | ((long)headerBytes[27] << 32) | ((long)headerBytes[28] << 40) |
                                   ((long)headerBytes[29] << 48) | ((long)headerBytes[30] << 56);
                    }
                    else
                    {
                        aspectBits = ((long)headerBytes[23] << 56) | ((long)headerBytes[24] << 48) | ((long)headerBytes[25] << 40) |
                                   ((long)headerBytes[26] << 32) | ((long)headerBytes[27] << 24) | ((long)headerBytes[28] << 16) |
                                   ((long)headerBytes[29] << 8) | headerBytes[30];
                    }
                    Header.AspectRatio = BitConverter.Int64BitsToDouble(aspectBits);
                }
                else
                {
                    // 新格式：4字节int（位置23-26）
                    int aspectRatioScaled;
                    if (isLittleEndian)
                    {
                        aspectRatioScaled = headerBytes[23] | (headerBytes[24] << 8) | (headerBytes[25] << 16) | (headerBytes[26] << 24);
                    }
                    else
                    {
                        aspectRatioScaled = (headerBytes[23] << 24) | (headerBytes[24] << 16) | (headerBytes[25] << 8) | headerBytes[26];
                    }
                    Header.AspectRatio = aspectRatioScaled / 10000000.0;
                }

                // 设置默认值如果无效
                if (Header.Width <= 0) Header.Width = 800;
                if (Header.Height <= 0) Header.Height = 600;
                if (Header.AspectRatio <= 0) Header.AspectRatio = (double)Header.Width / Header.Height;

                // 调整reader位置到文件头结束
                reader.BaseStream.Position = startPos + headerLength;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ParseOldFormat(BinaryReader reader)
        {
            // 旧格式幻灯片
            Header.Type = "Old Format";
            Header.Width = 800;
            Header.Height = 600;
            Header.AspectRatio = 4.0 / 3.0;
            Header.IsLittleEndian = true;
        }

        /// <summary>
        /// 根据文件字节序读取Int16
        /// </summary>
        private short ReadInt16(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(2);
            if (Header.IsLittleEndian)
            {
                return (short)(bytes[0] | (bytes[1] << 8));
            }
            else
            {
                return (short)((bytes[0] << 8) | bytes[1]);
            }
        }

        /// <summary>
        /// 根据文件字节序读取UInt32
        /// </summary>
        private uint ReadUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (Header.IsLittleEndian)
            {
                return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
            }
            else
            {
                return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
            }
        }

        private void ParseCommands(BinaryReader reader)
        {
            int maxCommands = 50000;
            int commandCount = 0;

            // 跟踪上一点（用于偏移矢量和通用端点矢量）
            PointF lastPoint = new PointF(0, 0);

            // 当前颜色索引
            int currentColor = 7; // 默认白色

            // 调试统计
            Dictionary<byte, int> recordTypeCount = new Dictionary<byte, int>();
            // 不要清除DebugInfo，保留头部解析的信息
            // DebugInfo.Clear();
            DebugInfo.Add($"开始解析命令，数据长度: {reader.BaseStream.Length} 字节，从位置: {reader.BaseStream.Position}");
            DebugInfo.Add($"文件格式: {Header.Type}，字节序: {(Header.IsLittleEndian ? "小端" : "大端")}");
            DebugInfo.Add($"幻灯片尺寸: {Header.Width}x{Header.Height}");

            try
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length - 1 && commandCount < maxCommands)
                {
                    long currentPos = reader.BaseStream.Position;

                    // 检查是否有足够的字节
                    if (reader.BaseStream.Length - currentPos < 2)
                        break;

                    // 读取2字节（包含类型码）- 使用原始字节并根据字节序转换
                    byte[] wordBytes = reader.ReadBytes(2);
                    short firstWord;
                    byte recordType;
                    byte lowByte;

                    if (Header.IsLittleEndian)
                    {
                        // 小端序：低字节在前，高字节在后
                        firstWord = (short)(wordBytes[0] | (wordBytes[1] << 8));
                        lowByte = wordBytes[0];
                        recordType = wordBytes[1];
                    }
                    else
                    {
                        // 大端序：高字节在前，低字节在后
                        firstWord = (short)((wordBytes[0] << 8) | wordBytes[1]);
                        recordType = wordBytes[0];
                        lowByte = wordBytes[1];
                    }

                    // 统计记录类型
                    if (!recordTypeCount.ContainsKey(recordType))
                        recordTypeCount[recordType] = 0;
                    recordTypeCount[recordType]++;

                    // 输出前10个命令的详细信息
                    if (commandCount < 10)
                    {
                        DebugInfo.Add($"命令 {commandCount}: 位置={currentPos}, 类型=0x{recordType:X2}, firstWord=0x{firstWord:X4}, lowByte=0x{lowByte:X2}");
                    }

                    // 根据记录类型处理
                    if (recordType >= 0x00 && recordType <= 0x7F)
                    {
                        // 00-7F: 标准普通矢量 (8字节总计，已读2字节)
                        // firstWord 本身就是 from-X
                        // 然后是 from-Y, to-X, to-Y
                        if (reader.BaseStream.Length - reader.BaseStream.Position >= 6)
                        {
                            short fromX = firstWord;
                            short fromY = ReadInt16(reader);
                            short toX = ReadInt16(reader);
                            short toY = ReadInt16(reader);

                            // 检测是否为点（起点和终点相同）
                            if (fromX == toX && fromY == toY)
                            {
                                DrawCommand pointCmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.Point,
                                    ColorIndex = currentColor
                                };
                                pointCmd.Points.Add(new PointF(fromX, fromY));
                                Commands.Add(pointCmd);
                            }
                            else
                            {
                                DrawCommand cmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.Line,
                                    ColorIndex = currentColor
                                };
                                cmd.Points.Add(new PointF(fromX, fromY));
                                cmd.Points.Add(new PointF(toX, toY));
                                Commands.Add(cmd);
                            }

                            lastPoint = new PointF(fromX, fromY);
                        }
                    }
                    else if (recordType == 0xFB)
                    {
                        // FB: 偏移矢量 (5字节总计，已读2字节)
                        // lowByte 和后续3字节是偏移量
                        if (reader.BaseStream.Length - reader.BaseStream.Position >= 3)
                        {
                            sbyte offsetFromX = (sbyte)lowByte;
                            sbyte offsetFromY = (sbyte)reader.ReadByte();
                            sbyte offsetToX = (sbyte)reader.ReadByte();
                            sbyte offsetToY = (sbyte)reader.ReadByte();

                            float fromX = lastPoint.X + offsetFromX;
                            float fromY = lastPoint.Y + offsetFromY;
                            float toX = lastPoint.X + offsetToX;
                            float toY = lastPoint.Y + offsetToY;

                            // 检测是否为点（起点和终点相同）
                            if (Math.Abs(fromX - toX) < 0.001f && Math.Abs(fromY - toY) < 0.001f)
                            {
                                DrawCommand pointCmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.Point,
                                    ColorIndex = currentColor
                                };
                                pointCmd.Points.Add(new PointF(fromX, fromY));
                                Commands.Add(pointCmd);
                            }
                            else
                            {
                                DrawCommand cmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.Line,
                                    ColorIndex = currentColor
                                };
                                cmd.Points.Add(new PointF(fromX, fromY));
                                cmd.Points.Add(new PointF(toX, toY));
                                Commands.Add(cmd);
                            }

                            lastPoint = new PointF(fromX, fromY);
                        }
                    }
                    else if (recordType == 0xFC)
                    {
                        // FC: 文件结尾 (2字节总计，已全部读取)
                        if (lowByte == 0x00)
                        {
                            return; // 正常结束
                        }
                    }
                    else if (recordType == 0xFD)
                    {
                        // FD: 实体填充 (6字节总计，已读2字节)
                        // lowByte 应该是0，然后是X(2字节), Y(2字节)
                        if (reader.BaseStream.Length - reader.BaseStream.Position >= 4)
                        {
                            short x = ReadInt16(reader);
                            short y = ReadInt16(reader);

                            // Y为负表示填充序列的开始
                            if (y < 0)
                            {
                                DrawCommand fillCmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.SolidFill,
                                    ColorIndex = currentColor
                                };

                                int vertexCount = x;
                                for (int i = 0; i < vertexCount && i < 10; i++)
                                {
                                    if (reader.BaseStream.Length - reader.BaseStream.Position >= 6)
                                    {
                                        short nextWord = ReadInt16(reader);
                                        byte nextType = Header.IsLittleEndian ?
                                            (byte)((nextWord >> 8) & 0xFF) :
                                            (byte)((nextWord >> 8) & 0xFF);

                                        if (nextType == 0xFD)
                                        {
                                            short vx = ReadInt16(reader);
                                            short vy = ReadInt16(reader);
                                            fillCmd.Points.Add(new PointF(vx, Math.Abs(vy)));
                                        }
                                        else
                                        {
                                            reader.BaseStream.Seek(-2, SeekOrigin.Current);
                                            break;
                                        }
                                    }
                                }

                                if (fillCmd.Points.Count >= 3)
                                {
                                    Commands.Add(fillCmd);
                                }
                            }
                            else
                            {
                                DrawCommand cmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.SolidFill,
                                    ColorIndex = currentColor
                                };
                                cmd.Points.Add(new PointF(x, y));
                                Commands.Add(cmd);
                            }
                        }
                    }
                    else if (recordType == 0xFE)
                    {
                        // FE: 通用端点矢量 (3字节总计，已读2字节)
                        // lowByte 是 offsetToX，然后是 offsetToY
                        if (reader.BaseStream.Length - reader.BaseStream.Position >= 1)
                        {
                            sbyte offsetToX = (sbyte)lowByte;
                            sbyte offsetToY = (sbyte)reader.ReadByte();

                            float toX = lastPoint.X + offsetToX;
                            float toY = lastPoint.Y + offsetToY;

                            // 检测是否为点（偏移量都为0）
                            if (offsetToX == 0 && offsetToY == 0)
                            {
                                DrawCommand pointCmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.Point,
                                    ColorIndex = currentColor
                                };
                                pointCmd.Points.Add(lastPoint);
                                Commands.Add(pointCmd);
                                // lastPoint保持不变
                            }
                            else
                            {
                                DrawCommand cmd = new DrawCommand
                                {
                                    Type = DrawCommand.CommandType.Line,
                                    ColorIndex = currentColor
                                };
                                cmd.Points.Add(lastPoint);
                                cmd.Points.Add(new PointF(toX, toY));
                                Commands.Add(cmd);

                                lastPoint = new PointF(toX, toY);
                            }
                        }
                    }
                    else if (recordType == 0xFF)
                    {
                        // FF: 新建颜色 (2字节总计，已全部读取)
                        // lowByte 是颜色索引
                        currentColor = lowByte;
                    }
                    else
                    {
                        // 未知的记录类型，跳过以避免解析错误
                        // 根据AutoCAD Slide格式，大多数未知记录至少是2字节
                        // 已经读取了2字节(firstWord)，所以位置已经移动了
                        System.Diagnostics.Debug.WriteLine($"未知的记录类型: 0x{recordType:X2} at position {currentPos}");

                        // 对于0x80-0xFA范围的记录，尝试根据类型推断长度
                        // 如果无法确定，跳过以避免卡住
                        if (recordType >= 0x80 && recordType <= 0xFA)
                        {
                            // 尝试跳过一些字节，避免卡住
                            // 这是保守的做法，可能需要根据实际格式调整
                            if (reader.BaseStream.Position < reader.BaseStream.Length - 2)
                            {
                                reader.ReadBytes(2); // 跳过额外的2字节
                            }
                        }
                    }

                    commandCount++;

                    // 防止无限循环
                    if (reader.BaseStream.Position == currentPos)
                    {
                        System.Diagnostics.Debug.WriteLine($"检测到无限循环，终止解析。位置: {currentPos}, 记录类型: 0x{recordType:X2}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析命令时出错: {ex.Message}");
                DebugInfo.Add($"解析出错: {ex.Message}");
            }

            // 输出统计信息
            DebugInfo.Add($"\n解析完成，共解析 {commandCount} 条记录");
            DebugInfo.Add($"生成 {Commands.Count} 条绘图命令");
            DebugInfo.Add("\n记录类型统计:");
            foreach (var kvp in recordTypeCount.OrderBy(x => x.Key))
            {
                string typeName = GetRecordTypeName(kvp.Key);
                DebugInfo.Add($"  0x{kvp.Key:X2} ({typeName}): {kvp.Value} 次");
            }

            // 如果没有解析到任何命令，创建一个默认显示
            if (Commands.Count == 0)
            {
                CreateDefaultDisplay();
            }
        }

        private string GetRecordTypeName(byte recordType)
        {
            if (recordType >= 0x00 && recordType <= 0x7F)
                return "普通矢量";
            else if (recordType == 0xFB)
                return "偏移矢量";
            else if (recordType == 0xFC)
                return "文件结尾";
            else if (recordType == 0xFD)
                return "实体填充";
            else if (recordType == 0xFE)
                return "通用端点矢量";
            else if (recordType == 0xFF)
                return "颜色改变";
            else
                return "未知";
        }

        private void CreateDefaultDisplay()
        {
            // 创建一个边框作为默认显示
            DrawCommand border = new DrawCommand
            {
                Type = DrawCommand.CommandType.Line,
                ColorIndex = 7
            };

            border.Points.Add(new PointF(100, 100));
            border.Points.Add(new PointF(700, 100));
            Commands.Add(border);

            border = new DrawCommand
            {
                Type = DrawCommand.CommandType.Line,
                ColorIndex = 7
            };
            border.Points.Add(new PointF(700, 100));
            border.Points.Add(new PointF(700, 500));
            Commands.Add(border);

            border = new DrawCommand
            {
                Type = DrawCommand.CommandType.Line,
                ColorIndex = 7
            };
            border.Points.Add(new PointF(700, 500));
            border.Points.Add(new PointF(100, 500));
            Commands.Add(border);

            border = new DrawCommand
            {
                Type = DrawCommand.CommandType.Line,
                ColorIndex = 7
            };
            border.Points.Add(new PointF(100, 500));
            border.Points.Add(new PointF(100, 100));
            Commands.Add(border);
        }
    }
}
