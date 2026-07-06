@echo off
chcp 65001 >nul
echo ========================================
echo   JTLP 发布打包脚本
echo ========================================
echo.

set PROJECT_DIR=%~dp0src\JTLP
set OUTPUT_DIR=%~dp0dist
set PUBLISH_DIR=%OUTPUT_DIR%\JTLP

:: 清理旧的发布目录
if exist "%OUTPUT_DIR%" (
    echo [1/6] 清理旧发布目录...
    rmdir /s /q "%OUTPUT_DIR%"
)

:: 发布程序（独立 exe）
echo [2/6] 正在发布程序...
cd /d "%PROJECT_DIR%"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%PUBLISH_DIR%" 2>&1
if errorlevel 1 (
    echo 发布失败！
    pause
    exit /b 1
)

:: 复制 FFmpeg
echo [3/6] 复制 FFmpeg...
set FFMPEG_SRC=%~dp0tools\ffmpeg\ffmpeg.exe
if exist "%FFMPEG_SRC%" (
    mkdir "%PUBLISH_DIR%\tools\ffmpeg" 2>nul
    copy "%FFMPEG_SRC%" "%PUBLISH_DIR%\tools\ffmpeg\ffmpeg.exe" >nul
    echo   FFmpeg 已复制
) else (
    echo   警告: FFmpeg 未找到，请手动复制 ffmpeg.exe 到 tools\ffmpeg\ 目录
)

:: 复制说明文件
echo [4/6] 复制说明文件...
copy "%~dp0dist-README.md" "%PUBLISH_DIR%\" >nul

:: 创建 ZIP
echo [5/6] 打包 ZIP...
cd /d "%OUTPUT_DIR%"
powershell -Command "Compress-Archive -Path 'JTLP' -DestinationPath 'JTLP.zip' -Force"
if errorlevel 1 (
    echo ZIP 打包失败
) else (
    echo   ZIP 已创建: %OUTPUT_DIR%\JTLP.zip
)

:: 创建安装包（如果安装了 Inno Setup）
echo [6/6] 创建安装包...
set INNO_COMPILER=
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set INNO_COMPILER=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
)
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set INNO_COMPILER=C:\Program Files\Inno Setup 6\ISCC.exe
)

if defined INNO_COMPILER (
    mkdir "%~dp0installer" 2>nul
    "%INNO_COMPILER%" "%~dp0installer.iss"
    if errorlevel 1 (
        echo 安装包创建失败
    ) else (
        echo   安装包已创建: %~dp0installer\JTLP_Setup.exe
    )
) else (
    echo   跳过（未安装 Inno Setup 6）
    echo   如需创建安装包，请安装 Inno Setup: https://jrsoftware.org/isdl.php
)

echo.
echo ========================================
echo   发布完成！
echo.
echo   文件夹: %PUBLISH_DIR%
echo   ZIP包:  %OUTPUT_DIR%\JTLP.zip
if exist "%~dp0installer\JTLP_Setup.exe" (
    echo   安装包: %~dp0installer\JTLP_Setup.exe
)
echo ========================================
echo.
pause
