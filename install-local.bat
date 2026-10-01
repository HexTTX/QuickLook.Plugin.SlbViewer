@echo off
chcp 65001 >nul
title 安装 QuickLook.Plugin.SlbViewer 插件

echo 正在编译最新 Release 版本...
dotnet build "%~dp0QuickLook.Plugin.SlbViewer.csproj" -c Release

if %ERRORLEVEL% NEQ 0 (
    echo 编译失败！
    pause
    exit /b %ERRORLEVEL%
)

set "TARGET_DIR=%APPDATA%\pooi.moe\QuickLook\QuickLook.Plugin\QuickLook.Plugin.SlbViewer"

echo.
echo 正在安装到: %TARGET_DIR%
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

copy /y "%~dp0bin\Release\QuickLook.Plugin.SlbViewer.dll" "%TARGET_DIR%\" >nul
copy /y "%~dp0bin\Release\QuickLook.Plugin.Metadata.config" "%TARGET_DIR%\" >nul

echo 插件文件已复制完成！
echo.
echo 提示：若 QuickLook 正在运行，需重启 QuickLook 以加载新插件。
set /p RESTART="是否立即重启 QuickLook? (Y/N): "
if /i "%RESTART%"=="Y" (
    echo 正在关闭 QuickLook...
    taskkill /f /im QuickLook.exe >nul 2>nul
    timeout /t 1 /nobreak >nul
    echo 正在启动 QuickLook...
    start "" "D:\Program Files\QuickLook\QuickLook.exe"
    echo QuickLook 已重启！现在可以在资源管理器中选中 .slb 或 .sld 文件按空格键预览了！
)

echo.
pause
