@echo off

cls

call C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe -out:serial.exe serial.cs

if %errorlevel% neq 0 (
    echo Build failed with error code %errorlevel%.
    pause
) else (
    echo Build succeeded.
    start serial.exe
)