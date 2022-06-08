
This example code calls C++ from C# code.
Furthermore, it calls Assembly (ASM) code from C++ code.
These might be useful if we need really fast hardware level execution.
This code works on Windows. Not tested on Linux.
On Linux, modifications are needed. We didn't use the Microsoft MSVC compiler. 
But we used the platform independent "g++" (part of GNU's "gcc"), which is the most popular compiler on Linux as well.
So, the general framework and steps should work similarly on Linux.

maxofthree.asm uses the Intel syntax, not the AT&T syntax. That will work on Linux too.

The C++ code part can be built as an EXE too (not only a DLL), so it can be debugged in VsCode.
Can be disassembled (right click in CPP file, select "disassembled View"). Can see CPU registers.

----------------------------------
Make.bat candidate. These building steps can be done from Console.

cd cpp
nasm -fwin64 maxofthree.asm
g++ -g helloworld.cpp maxofthree.obj -o CppLib.dll   // or it can be built to EXE
// gendef - CppLib.dll // optional: to check the exported symbols with Mingw-w64 tool gendef.

cd ../CS
dotnet build
copy ..\CPP\CppLib.dll .\bin\Debug\net6.0
dotnet run