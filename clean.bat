@echo off
setlocal
cd /d "%~dp0"
if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist publish rmdir /s /q publish
echo Build artifacts removed.
