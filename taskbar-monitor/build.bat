@echo off
echo Building Router Taskbar Monitor...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:RouterTaskbarMonitor.exe /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.dll,System.Web.Extensions.dll Program.cs
if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo SUCCESS: RouterTaskbarMonitor.exe compiled successfully!
    echo ========================================================
) else (
    echo.
    echo BUILD FAILED with error code %ERRORLEVEL%.
)
