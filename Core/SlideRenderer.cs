using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SlbViewer
{
    /// <summary>
    /// 幻灯片渲染器
    /// 将解析后的幻灯片绘制到图像
    /// </summary>
    public class SlideRenderer
    {
        private SlideParser parser;
        private Color backgroundColor = Color.Black;
        private Color foregroundColor = Color.White;

        public Color BackgroundColor
        {
            get => backgroundColor;
            set => backgroundColor = value;
        }

        public Color ForegroundColor
        {
            get => foregroundColor;
            set => foregroundColor = value;
        }

        private float dpiScale = 1.0f;

        /// <summary>
        /// 屏幕物理 DPI 缩放比例（默认 1.0，如 125%=1.25，200%=2.0）
        /// </summary>
        public float DpiScale
        {
            get => dpiScale;
            set => dpiScale = Math.Max(0.5f, Math.Min(value, 5.0f));
        }
        public SlideRenderer(SlideParser parser)
        {
            this.parser = parser;
        }

        public SlideRenderer(SlideParser parser, Color backgroundColor, Color foregroundColor)
        {
            this.parser = parser;
            this.backgroundColor = backgroundColor;
            this.foregroundColor = foregroundColor;
        }

        /// <summary>
        /// 渲染幻灯片到位图
        /// </summary>
        public Bitmap Render(int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);
            float physicalDpi = 96.0f * dpiScale;
            bitmap.SetResolution(physicalDpi, physicalDpi);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // 设置高质量渲染
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                // 填充背景
                g.Clear(backgroundColor);

                if (parser.Commands.Count == 0)
                {
                    DrawNoContentMessage(g, width, height);
                    return bitmap;
                }

                // 计算所有点的边界框
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                foreach (var cmd in parser.Commands)
                {
                    foreach (var point in cmd.Points)
                    {
                        if (point.X < minX) minX = point.X;
                        if (point.X > maxX) maxX = point.X;
                        if (point.Y < minY) minY = point.Y;
                        if (point.Y > maxY) maxY = point.Y;
                    }
                }

                // 如果边界无效，使用默认值
                if (minX == float.MaxValue || maxX == float.MinValue)
                {
                    minX = 0; maxX = parser.Header.Width;
                    minY = 0; maxY = parser.Header.Height;
                }

                float dataWidth = maxX - minX;
                float dataHeight = maxY - minY;

                // 避免除以零
                if (dataWidth < 1) dataWidth = 1;
                if (dataHeight < 1) dataHeight = 1;

                // 计算缩放比例，留出边距
                float margin = 20;
                float scaleX = (width - 2 * margin) / dataWidth;
                float scaleY = (height - 2 * margin) / dataHeight;
                float scale = Math.Min(scaleX, scaleY);

                // 计算偏移以居中显示
                float offsetX = margin + (width - 2 * margin - dataWidth * scale) / 2 - minX * scale;
                float offsetY = margin + (height - 2 * margin - dataHeight * scale) / 2 - minY * scale;

                // 应用变换
                g.TranslateTransform(offsetX, offsetY);
                g.ScaleTransform(scale, -scale); // Y轴翻转（AutoCAD坐标系）
                g.TranslateTransform(0, -maxY - minY); // 调整Y偏移

                // 绘制所有命令
                // 笔触粗细根据 DPI 自适应补偿，确保在高分屏下矢量线条清晰饱满
                float strokeFactor = dpiScale > 1.1f ? (float)Math.Pow(dpiScale, 0.35) : 1.0f;
                using (Pen pen = new Pen(foregroundColor, strokeFactor / scale))
                {
                    foreach (var cmd in parser.Commands)
                    {
                        DrawCommand(g, pen, cmd, scale);
                    }
                }
            }

            return bitmap;
        }

        private void DrawCommand(Graphics g, Pen pen, SlideParser.DrawCommand cmd, float scale)
        {
            // 设置颜色
            pen.Color = GetColorFromIndex(cmd.ColorIndex);

            switch (cmd.Type)
            {
                case SlideParser.DrawCommand.CommandType.Line:
                    if (cmd.Points.Count >= 2)
                    {
                        g.DrawLine(pen, cmd.Points[0], cmd.Points[1]);
                    }
                    break;

                case SlideParser.DrawCommand.CommandType.Point:
                    if (cmd.Points.Count >= 1)
                    {
                        // 点的大小根据缩放比例与 DPI 自适应调整
                        float strokeFactor = dpiScale > 1.1f ? (float)Math.Pow(dpiScale, 0.35) : 1.0f;
                        float pointSize = (3.0f * strokeFactor) / scale;
                        float halfSize = pointSize / 2;
                        g.FillEllipse(new SolidBrush(pen.Color),
                            cmd.Points[0].X - halfSize, cmd.Points[0].Y - halfSize,
                            pointSize, pointSize);
                    }
                    break;

                case SlideParser.DrawCommand.CommandType.Vector:
                    if (cmd.Points.Count >= 2)
                    {
                        g.DrawLines(pen, cmd.Points.ToArray());
                    }
                    break;

                case SlideParser.DrawCommand.CommandType.SolidFill:
                    // 实体填充：根据收集到的顶点绘制填充多边形
                    if (cmd.Points.Count >= 3)
                    {
                        using (SolidBrush brush = new SolidBrush(pen.Color))
                        {
                            g.FillPolygon(brush, cmd.Points.ToArray());
                        }
                    }
                    else if (cmd.Points.Count == 1)
                    {
                        // 单点填充记录，可能是填充序列的一部分
                        // 在这种情况下，我们简单地画一个点
                        float strokeFactor = dpiScale > 1.1f ? (float)Math.Pow(dpiScale, 0.35) : 1.0f;
                        float pointSize = (3.0f * strokeFactor) / scale;
                        float halfSize = pointSize / 2;
                        g.FillEllipse(new SolidBrush(pen.Color),
                            cmd.Points[0].X - halfSize, cmd.Points[0].Y - halfSize,
                            pointSize, pointSize);
                    }
                    break;
            }
        }

        private Color GetColorFromIndex(int index)
        {
            Color color = AutoCADColors.GetColor(index);
            // 亮暗背景自适应（AutoCAD 7号色及对比色调整）
            bool isLightBackground = (backgroundColor.R * 0.299 + backgroundColor.G * 0.587 + backgroundColor.B * 0.114) > 128;
            if (isLightBackground)
            {
                if (color.R > 210 && color.G > 210 && color.B > 210)
                {
                    return foregroundColor; // 浅色在浅底转深色
                }
            }
            else
            {
                if (color.R < 45 && color.G < 45 && color.B < 45)
                {
                    return foregroundColor; // 深色在深底转浅色
                }
            }
            return color;
        }
        private void DrawNoContentMessage(Graphics g, int width, int height)
        {
            string message = "幻灯片数据格式无法识别\n或内容为空";
            using (Font font = new Font("Microsoft YaHei UI", 14, FontStyle.Regular))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                g.DrawString(message, font, Brushes.Gray,
                    new RectangleF(0, 0, width, height), sf);
            }
        }

        /// <summary>
        /// 创建缩略图
        /// </summary>
        public Bitmap RenderThumbnail(int size)
        {
            return Render(size, size);
        }
    }
}
