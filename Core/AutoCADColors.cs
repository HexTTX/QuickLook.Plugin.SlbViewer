using System.Drawing;

namespace SlbViewer
{
    /// <summary>
    /// AutoCAD 颜色索引（ACI）标准颜色表
    /// 包含完整的256色定义
    /// </summary>
    public static class AutoCADColors
    {
        // AutoCAD标准颜色索引表（前255个颜色的RGB值）
        private static readonly int[][] colorTable = new int[][]
        {
            // 索引 0-9: 基本颜色
            new int[] { 0, 0, 0 },          // 0: ByBlock (黑色)
            new int[] { 255, 0, 0 },        // 1: 红色
            new int[] { 255, 255, 0 },      // 2: 黄色
            new int[] { 0, 255, 0 },        // 3: 绿色
            new int[] { 0, 255, 255 },      // 4: 青色
            new int[] { 0, 0, 255 },        // 5: 蓝色
            new int[] { 255, 0, 255 },      // 6: 洋红色
            new int[] { 255, 255, 255 },    // 7: 白色
            new int[] { 128, 128, 128 },    // 8: 深灰色
            new int[] { 192, 192, 192 },    // 9: 浅灰色

            // 索引 10-19: 红色系
            new int[] { 255, 0, 0 },
            new int[] { 255, 127, 127 },
            new int[] { 165, 0, 0 },
            new int[] { 165, 82, 82 },
            new int[] { 127, 0, 0 },
            new int[] { 127, 63, 63 },
            new int[] { 76, 0, 0 },
            new int[] { 76, 38, 38 },
            new int[] { 38, 0, 0 },
            new int[] { 38, 19, 19 },

            // 索引 20-29: 橙色系
            new int[] { 255, 63, 0 },
            new int[] { 255, 159, 127 },
            new int[] { 165, 41, 0 },
            new int[] { 165, 103, 82 },
            new int[] { 127, 31, 0 },
            new int[] { 127, 79, 63 },
            new int[] { 76, 19, 0 },
            new int[] { 76, 47, 38 },
            new int[] { 38, 9, 0 },
            new int[] { 38, 23, 19 },

            // 索引 30-39: 黄橙色系
            new int[] { 255, 127, 0 },
            new int[] { 255, 191, 127 },
            new int[] { 165, 82, 0 },
            new int[] { 165, 124, 82 },
            new int[] { 127, 63, 0 },
            new int[] { 127, 95, 63 },
            new int[] { 76, 38, 0 },
            new int[] { 76, 57, 38 },
            new int[] { 38, 19, 0 },
            new int[] { 38, 28, 19 },

            // 索引 40-49: 黄色系
            new int[] { 255, 191, 0 },
            new int[] { 255, 223, 127 },
            new int[] { 165, 124, 0 },
            new int[] { 165, 145, 82 },
            new int[] { 127, 95, 0 },
            new int[] { 127, 111, 63 },
            new int[] { 76, 57, 0 },
            new int[] { 76, 66, 38 },
            new int[] { 38, 28, 0 },
            new int[] { 38, 33, 19 },

            // 索引 50-59: 黄绿色系
            new int[] { 255, 255, 0 },
            new int[] { 255, 255, 127 },
            new int[] { 165, 165, 0 },
            new int[] { 165, 165, 82 },
            new int[] { 127, 127, 0 },
            new int[] { 127, 127, 63 },
            new int[] { 76, 76, 0 },
            new int[] { 76, 76, 38 },
            new int[] { 38, 38, 0 },
            new int[] { 38, 38, 19 },

            // 索引 60-69: 浅绿色系
            new int[] { 191, 255, 0 },
            new int[] { 223, 255, 127 },
            new int[] { 124, 165, 0 },
            new int[] { 145, 165, 82 },
            new int[] { 95, 127, 0 },
            new int[] { 111, 127, 63 },
            new int[] { 57, 76, 0 },
            new int[] { 66, 76, 38 },
            new int[] { 28, 38, 0 },
            new int[] { 33, 38, 19 },

            // 索引 70-79: 绿色系
            new int[] { 127, 255, 0 },
            new int[] { 191, 255, 127 },
            new int[] { 82, 165, 0 },
            new int[] { 124, 165, 82 },
            new int[] { 63, 127, 0 },
            new int[] { 95, 127, 63 },
            new int[] { 38, 76, 0 },
            new int[] { 57, 76, 38 },
            new int[] { 19, 38, 0 },
            new int[] { 28, 38, 19 },

            // 索引 80-89: 深绿色系
            new int[] { 63, 255, 0 },
            new int[] { 159, 255, 127 },
            new int[] { 41, 165, 0 },
            new int[] { 103, 165, 82 },
            new int[] { 31, 127, 0 },
            new int[] { 79, 127, 63 },
            new int[] { 19, 76, 0 },
            new int[] { 47, 76, 38 },
            new int[] { 9, 38, 0 },
            new int[] { 23, 38, 19 },

            // 索引 90-99: 青绿色系
            new int[] { 0, 255, 0 },
            new int[] { 127, 255, 127 },
            new int[] { 0, 165, 0 },
            new int[] { 82, 165, 82 },
            new int[] { 0, 127, 0 },
            new int[] { 63, 127, 63 },
            new int[] { 0, 76, 0 },
            new int[] { 38, 76, 38 },
            new int[] { 0, 38, 0 },
            new int[] { 19, 38, 19 },

            // 索引 100-109: 绿青色系
            new int[] { 0, 255, 63 },
            new int[] { 127, 255, 159 },
            new int[] { 0, 165, 41 },
            new int[] { 82, 165, 103 },
            new int[] { 0, 127, 31 },
            new int[] { 63, 127, 79 },
            new int[] { 0, 76, 19 },
            new int[] { 38, 76, 47 },
            new int[] { 0, 38, 9 },
            new int[] { 19, 38, 23 },

            // 索引 110-119: 青色绿系
            new int[] { 0, 255, 127 },
            new int[] { 127, 255, 191 },
            new int[] { 0, 165, 82 },
            new int[] { 82, 165, 124 },
            new int[] { 0, 127, 63 },
            new int[] { 63, 127, 95 },
            new int[] { 0, 76, 38 },
            new int[] { 38, 76, 57 },
            new int[] { 0, 38, 19 },
            new int[] { 19, 38, 28 },

            // 索引 120-129: 浅青色系
            new int[] { 0, 255, 191 },
            new int[] { 127, 255, 223 },
            new int[] { 0, 165, 124 },
            new int[] { 82, 165, 145 },
            new int[] { 0, 127, 95 },
            new int[] { 63, 127, 111 },
            new int[] { 0, 76, 57 },
            new int[] { 38, 76, 66 },
            new int[] { 0, 38, 28 },
            new int[] { 19, 38, 33 },

            // 索引 130-139: 青色系
            new int[] { 0, 255, 255 },
            new int[] { 127, 255, 255 },
            new int[] { 0, 165, 165 },
            new int[] { 82, 165, 165 },
            new int[] { 0, 127, 127 },
            new int[] { 63, 127, 127 },
            new int[] { 0, 76, 76 },
            new int[] { 38, 76, 76 },
            new int[] { 0, 38, 38 },
            new int[] { 19, 38, 38 },

            // 索引 140-149: 青蓝色系
            new int[] { 0, 191, 255 },
            new int[] { 127, 223, 255 },
            new int[] { 0, 124, 165 },
            new int[] { 82, 145, 165 },
            new int[] { 0, 95, 127 },
            new int[] { 63, 111, 127 },
            new int[] { 0, 57, 76 },
            new int[] { 38, 66, 76 },
            new int[] { 0, 28, 38 },
            new int[] { 19, 33, 38 },

            // 索引 150-159: 浅蓝色系
            new int[] { 0, 127, 255 },
            new int[] { 127, 191, 255 },
            new int[] { 0, 82, 165 },
            new int[] { 82, 124, 165 },
            new int[] { 0, 63, 127 },
            new int[] { 63, 95, 127 },
            new int[] { 0, 38, 76 },
            new int[] { 38, 57, 76 },
            new int[] { 0, 19, 38 },
            new int[] { 19, 28, 38 },

            // 索引 160-169: 天蓝色系
            new int[] { 0, 63, 255 },
            new int[] { 127, 159, 255 },
            new int[] { 0, 41, 165 },
            new int[] { 82, 103, 165 },
            new int[] { 0, 31, 127 },
            new int[] { 63, 79, 127 },
            new int[] { 0, 19, 76 },
            new int[] { 38, 47, 76 },
            new int[] { 0, 9, 38 },
            new int[] { 19, 23, 38 },

            // 索引 170-179: 蓝色系
            new int[] { 0, 0, 255 },
            new int[] { 127, 127, 255 },
            new int[] { 0, 0, 165 },
            new int[] { 82, 82, 165 },
            new int[] { 0, 0, 127 },
            new int[] { 63, 63, 127 },
            new int[] { 0, 0, 76 },
            new int[] { 38, 38, 76 },
            new int[] { 0, 0, 38 },
            new int[] { 19, 19, 38 },

            // 索引 180-189: 紫蓝色系
            new int[] { 63, 0, 255 },
            new int[] { 159, 127, 255 },
            new int[] { 41, 0, 165 },
            new int[] { 103, 82, 165 },
            new int[] { 31, 0, 127 },
            new int[] { 79, 63, 127 },
            new int[] { 19, 0, 76 },
            new int[] { 47, 38, 76 },
            new int[] { 9, 0, 38 },
            new int[] { 23, 19, 38 },

            // 索引 190-199: 紫色系
            new int[] { 127, 0, 255 },
            new int[] { 191, 127, 255 },
            new int[] { 82, 0, 165 },
            new int[] { 124, 82, 165 },
            new int[] { 63, 0, 127 },
            new int[] { 95, 63, 127 },
            new int[] { 38, 0, 76 },
            new int[] { 57, 38, 76 },
            new int[] { 19, 0, 38 },
            new int[] { 28, 19, 38 },

            // 索引 200-209: 洋红紫色系
            new int[] { 191, 0, 255 },
            new int[] { 223, 127, 255 },
            new int[] { 124, 0, 165 },
            new int[] { 145, 82, 165 },
            new int[] { 95, 0, 127 },
            new int[] { 111, 63, 127 },
            new int[] { 57, 0, 76 },
            new int[] { 66, 38, 76 },
            new int[] { 28, 0, 38 },
            new int[] { 33, 19, 38 },

            // 索引 210-219: 洋红色系
            new int[] { 255, 0, 255 },
            new int[] { 255, 127, 255 },
            new int[] { 165, 0, 165 },
            new int[] { 165, 82, 165 },
            new int[] { 127, 0, 127 },
            new int[] { 127, 63, 127 },
            new int[] { 76, 0, 76 },
            new int[] { 76, 38, 76 },
            new int[] { 38, 0, 38 },
            new int[] { 38, 19, 38 },

            // 索引 220-229: 粉红洋红色系
            new int[] { 255, 0, 191 },
            new int[] { 255, 127, 223 },
            new int[] { 165, 0, 124 },
            new int[] { 165, 82, 145 },
            new int[] { 127, 0, 95 },
            new int[] { 127, 63, 111 },
            new int[] { 76, 0, 57 },
            new int[] { 76, 38, 66 },
            new int[] { 38, 0, 28 },
            new int[] { 38, 19, 33 },

            // 索引 230-239: 粉红色系
            new int[] { 255, 0, 127 },
            new int[] { 255, 127, 191 },
            new int[] { 165, 0, 82 },
            new int[] { 165, 82, 124 },
            new int[] { 127, 0, 63 },
            new int[] { 127, 63, 95 },
            new int[] { 76, 0, 38 },
            new int[] { 76, 38, 57 },
            new int[] { 38, 0, 19 },
            new int[] { 38, 19, 28 },

            // 索引 240-249: 红粉色系
            new int[] { 255, 0, 63 },
            new int[] { 255, 127, 159 },
            new int[] { 165, 0, 41 },
            new int[] { 165, 82, 103 },
            new int[] { 127, 0, 31 },
            new int[] { 127, 63, 79 },
            new int[] { 76, 0, 19 },
            new int[] { 76, 38, 47 },
            new int[] { 38, 0, 9 },
            new int[] { 38, 19, 23 },

            // 索引 250-255: 灰度
            new int[] { 51, 51, 51 },
            new int[] { 91, 91, 91 },
            new int[] { 132, 132, 132 },
            new int[] { 173, 173, 173 },
            new int[] { 214, 214, 214 },
            new int[] { 255, 255, 255 }
        };

        /// <summary>
        /// 根据AutoCAD颜色索引获取颜色
        /// </summary>
        /// <param name="index">颜色索引 (0-255)</param>
        /// <returns>对应的颜色</returns>
        public static Color GetColor(int index)
        {
            if (index < 0 || index > 255)
            {
                return Color.White;
            }

            int[] rgb = colorTable[index];
            return Color.FromArgb(rgb[0], rgb[1], rgb[2]);
        }
    }
}
