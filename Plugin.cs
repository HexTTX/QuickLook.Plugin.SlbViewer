using System;
using System.IO;
using System.Linq;
using System.Windows;
using QuickLook.Common.Plugin;

namespace QuickLook.Plugin.SlbViewer
{
    public class Plugin : IViewer
    {
        private static readonly string[] SupportedExtensions = { ".slb", ".sld" };
        private SlbViewerControl? _control;

        public int Priority => 10;

        public void Init()
        {
        }

        public bool CanHandle(string path)
        {
            if (string.IsNullOrEmpty(path) || Directory.Exists(path))
                return false;

            string ext = Path.GetExtension(path);
            if (!SupportedExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)))
                return false;

            return ValidateMagicBytes(path, ext);
        }

        private static bool ValidateMagicBytes(string path, string ext)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists || fi.Length < 31)
                    return false;

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs))
                {
                    if (string.Equals(ext, ".sld", StringComparison.OrdinalIgnoreCase))
                    {
                        // AutoCAD Slide header: 必须以 "AutoCAD Slide" 开头
                        if (fs.Length < 31) return false;
                        byte[] idBytes = reader.ReadBytes(17);
                        if (idBytes.Length < 17) return false;
                        string idStr = System.Text.Encoding.ASCII.GetString(idBytes);
                        return idStr.StartsWith("AutoCAD Slide");
                    }
                    else if (string.Equals(ext, ".slb", StringComparison.OrdinalIgnoreCase))
                    {
                        // 标准 AutoCAD 幻灯片库头（32字节）: "AutoCAD Slide Library 1.0\r\n\x1a\0"
                        if (fs.Length < 36) return false;
                        byte[] headerBytes = reader.ReadBytes(32);
                        if (headerBytes.Length < 32) return false;
                        string headerStr = System.Text.Encoding.ASCII.GetString(headerBytes);
                        if (headerStr.StartsWith("AutoCAD Slide Library"))
                            return true;

                        // 兼容早期无头目录结构 (offset 0): 32字节名称 + 4字节偏移量
                        fs.Seek(0, SeekOrigin.Begin);
                        byte[] nameBytes = reader.ReadBytes(32);
                        uint offset = reader.ReadUInt32();
                        if (offset > 0 && offset + 17 <= fs.Length)
                        {
                            fs.Seek(offset, SeekOrigin.Begin);
                            byte[] slideId = reader.ReadBytes(17);
                            string slideIdStr = System.Text.Encoding.ASCII.GetString(slideId);
                            if (slideIdStr.StartsWith("AutoCAD Slide"))
                                return true;
                        }

                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
        public void Prepare(string path, ContextObject context)
        {
            bool isSlb = string.Equals(Path.GetExtension(path), ".slb", StringComparison.OrdinalIgnoreCase);
            var preferredSize = isSlb ? new Size(960, 660) : new Size(860, 620);
            context.SetPreferredSizeFit(preferredSize, 0.85);
            context.CanResize = true;
            context.Title = Path.GetFileName(path);
        }

        public void View(string path, ContextObject context)
        {
            _control = new SlbViewerControl(path, context);
            context.ViewerContent = _control;
            _control.LoadContent();
        }

        public void Cleanup()
        {
            _control?.Dispose();
            _control = null;
        }
    }
}
