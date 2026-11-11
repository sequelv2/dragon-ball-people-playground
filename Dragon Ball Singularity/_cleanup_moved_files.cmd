@echo off
rem Removes the empty placeholder files left at the old script paths by the Singularity restructure.
rem Only files whose first line starts with "// SINGULARITY-MOVED" are deleted.
cd /d "%~dp0Assets\scripts"
set COUNT=0
for %%f in (*.cs) do (
  findstr /b /c:"// SINGULARITY-MOVED" "%%f" >nul && (del "%%f" & set /a COUNT+=1)
)
echo Removed placeholder files from Assets\scripts.
pause
