@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /out:bin\FamilyTree.exe ^
  Program.cs MainForm.cs Core\ProcessMonitor.cs Core\PopupTracker.cs ^
  /reference:System.dll /reference:System.Core.dll /reference:System.Management.dll ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll
if errorlevel 1 (echo Build FAILED) else (echo Build OK: bin\FamilyTree.exe)
