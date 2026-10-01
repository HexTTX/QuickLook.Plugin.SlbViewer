@echo off
chcp 65001 >nul
title 打包 QuickLook.Plugin.SlbViewer

echo 正在编译并打包 QuickLook.Plugin.SlbViewer...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack.ps1" -Configuration Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo 打包完成！生成文件位于本目录：
    echo QuickLook.Plugin.SlbViewer.qlplugin
    echo ========================================================
) else (
    echo.
    echo 打包失败，请检查错误输出。
)
pause
