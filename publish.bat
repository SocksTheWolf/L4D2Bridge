@echo off
rm -f Release.zip
rem Move to publish folder
cd "L4D2Bridge.Desktop\publish\"
rem do some cleanup
rm -f *.pdb
if exist L4D2Bridge.Desktop.exe (
rm -f L4D2Bridge.exe
rem rename binary
rename L4D2Bridge.Desktop.exe L4D2Bridge.exe
)

rem make zip
7z a ..\..\Release.zip *.dll *.exe ..\..\README.md ..\..\LICENSE
@echo on