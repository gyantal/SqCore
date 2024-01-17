<!-- To view properly formatted MD (Markdown) file in VS Code, install VS Code extension: "Markdown Preview Enhanced" - https://www.youtube.com/watch?v=4QzFVQsD-9I -->
<!-- Right click on the markdown file and select option -> "Markdown Preview Enhanced: Open preview to the side"( shortcut-ctrl+KV)  -->
# TigerVNCViewer Build Steps

Compiling TigerVncViewer is very complicated, mostly because very few professionals compile it on Windows. End users only use the installers. Professional people compile it on Linux mostly. Therefore, the Windows installer make-files are quite abandoned. Not tested too much and not up-to-date..

There are 2 building guides we present here. Daya did a heroic task to make it comile first. So, we saw that it is possible. However, George couldn't reproduce it, because the process was so complicated and taking 2 months that the exact detailed steps of the history is forgotten. So, George produced another approch for compilation, which hopefully will be able to reproduced more easily on any computer in the future.


#<span style="color:blue">Build process, Version 1 (George)</span>

# Preliminary notes

- In this version, we use the MSYS2 built-in native package manager for the platform, the PacMan (PackageManager)

- Previously, we had tried to compile sub-projects (zlib, libjpeg) separately by downloading each of their source codes manually from GitHub. But even Zlib requires 3 dependencies. Too many problems.

- Then we tried to use [VcPkg](https://vcpkg.io/en/) (Visual C Package Manager), which is Windows Only, maintained by Microsoft, but the community hates it, therefore FLTK, and GNUTLS both failed with the VcPkg approach at the end. VcPkg downloads the published versioned source code and compiles it locally for the target architecture. 
By default VcPkg installs packages as "x64-windows" (*.LIB), that is not "mingw" (*.so.a) libraries. Works well on Windows if it is compiled with msbuild, but MinGW uses GCC instead.
vcpkg install zlib // installs "x64-windows" (*.LIB)
vcpkg install zlib --triplet=x64-mingw-dynamic --host-triplet=x64-mingw-dynamic // installs "x64-mingw" (*.so.a)
At package install you can simply use one of the mingw triplets, and it will try to compile that architecture (x64|x86)-mingw-(dynamic|static)
I almost went through this VcPkg way until 90%, but at the end I faced that vcpkg install had problems with FLTK and GNUTLS compile and installation.
A dead end with VcPkg.

- What are the problems with 'Build process, Version 2 (Daya)'?
1. We couldn't reproduce how Daya achieved the result. So, after 2 weeks George still couldn't compile on his side.
2. Ugly. Using specific folders such as "c:\fltk-12.3\", it is not generic. Ugly to see we created package Folders everywhere on C:\root.
3. Too much work. Many dependent libraries are custom compiled manually locally. Instead of using general framework (a package manager) for compiling them the same way.
4. That solution is fragile. Even if we could reproduce it, it will break when the next FLTK, ZLIB, etc. version comes, as those are just snapshot momentary GitHub clones. These are not the stable libraries that are in usual published Packages.
Maybe sometimes, that Brute Force suffering and putting Libraries into c:\fltk-12.3\ folders will be useful. But it is very non-general.
Not repeatable in half a year, when other versions will be on the market.
This Version1 is likely reproducible for years.
It only uses PacMan for compiling dependent libraries. There is no need to download dependent source code from GitHub and compile them manually.

# Building process

We try to keep in mind that the tigervnc/BUILDING.TXT contains the following:
```
If building the Windows version of TigerVNC on a Windows build system, use the following procedure.
  cd {build_directory}
  cmake -G "MSYS Makefiles" [additional CMake flags] {source_directory}
  make
```
So, it is not using msbuild (*.LIB) libraries. And instead of MinGW it prefers its base, the MSYS. (Although it actually works as MinGW, because both uses the GCC compiler)

## Step 1: Install Required Compiling Framework

One of the most common ways to get GCC on Windows is through MinGW (Minimalist GNU for Windows) or MSYS2. 
Another option is to use Cygwin, which provides a large collection of GNU and Open Source tools for Windows. (ZLib source code is better compiled that way.)

- [MSYS2](https://www.msys2.org/) The Building Platform for Windows. Its package manager is 'PacMan'. Then in it, you can install MinGW, the GCC compiler environment with 
"pacman -S mingw-w64-ucrt-x86_64-gcc" or/and
"pacman -S mingw-w64-x86_64-toolchain"
Or install MinGW64 in any other way. You can google what is the best way.
! Important. Make sure the PATH environment variable is set to its BIN folder. (e.g. PATH += 'C:\opt\msys64\mingw64\bin;')
Check that from any Terminal, "GCC --version" works.
In MSYS2 you can use various building environments, such as GCC or CLang. The 3rd option is the Microsoft MSVC compiler with 'nmake *.msc' makefiles.

- [CMake](http://www.cmake.org) should be installed.
! Important. Make sure the PATH environment variable is set to its BIN folder. (e.g. PATH += 'C:\Program Files\CMake\bin;')
Check that from any Terminal, "cmake --version" works.
There is a difference when we use CMAKE with different Generators.
cmake -G "MSYS Makefiles"
cmake -G "MinGW Makefiles"

Note the [CMake Default make programs](https://cmake.org/cmake/help/latest/guide/user-interaction/index.html#command-line-g-option) for the generators:
Generator          Default make program
MinGW Makefiles     mingw32-make
MSYS Makefiles      make
Ninja               ninja
Visual Studio       msbuild

## Step 2: Build process
1. **Download TigerVNC from GitHub, and Enter the build directory**
```
git clone https://github.com/TigerVNC/tigervnc.git
cd tigervnc
mkdir build
cd build
```

2. **Install MinGW packages with PacMan**
```
pacman -Q // List all packages installed. Package search here at the top: https://packages.msys2.org/queue

Note that "i686" means 32-bit, so you have to use the "-x86_64" packages to install. E.g.
	  For 64-bit Windows, enter:  pacman -S mingw-w64-x86_64-fltk 
    For 32-bit Windows, enter:  pacman -S mingw-w64-i686-fltk

pacman -S zlib
pacman -S mingw-w64-pixman // this is the base package, but we also install the subpackage mingw-w64-x86_64-pixman
pacman -Sy mingw-w64-x86_64-pixman // first it was complaining about PGP keys, but after Googling the problem and do that, now it works.
pacman -S mingw-w64-x86_64-libjpeg-turbo

pacman -S mingw-w64-x86_64-fltk	// pacman -S mingw-w64-fltk doesn't work.

// this doesn't helped: pacman -S gettext // without NLS (Native Language Support), undeclared identifier 'LC_MESSAGES' ERROR at compile confirmed in GitHub issues.
pacman -S mingw-w64-x86_64-gettext // this is actually: libintl.h and libintl.dll.a such as Lib-International for NLS

pacman -S mingw-w64-x86_64-libiconv // This is the Library. but it didn't help. so install the next too:
pacman -S mingw-w64-x86_64-iconv // This is the Utility. Maybe we need both.
pacman -S libiconv // still, doesn't help: -- ICONV_FOUND = blank.
```

3. **Change 1 line in tigervnc's CMakeList.txt.**

The Windows compiling process is not well maintained. Somebody should fix this in the future. It is under the radar.
The check_library_exists() doesn't find the dgettext() function in the libintl.dll.a or libintl.a.
So, we have to force it, saying that it is there.

In CMakeList.txt:
check_library_exists(\${LIBINTL_LIBRARY} "dgettext" "" LIBINTL_HAS_DGETTEXT)
message(STATUS "LIBINTL_HAS_DGETTEXT = ${LIBINTL_HAS_DGETTEXT}") # show that this in not-defined blank, so it couldn't find it.
set(LIBINTL_HAS_DGETTEXT TRUE) # Insert this line: Force this to TRUE, because check_library_exists() cannot find dgettext(), although it is there.

4. **CMAKE it in a 2-step process.**
Run CMAKE with the specified configurations to configure the project and generate a native build system:
-G: Generator
-S: Source folder
-B: Build (Target) folder

__There are 2 kinds of libraries. Static libraries (e.g. libintl.a) that are like OBJ files and link fully into the final EXE, or dynamic libraries (e.g. libintl.dll.a) that needs additonal *.DLLs to run the target EXE.__ There is a fundamental reason to prefer static libraries, because of no DLL-hell (versioning) problem, but the old practice is that dynamic libraries are the dominant, and most of the time people test build system with the dynamic version of the library.

  ```
  cmake -G "MSYS Makefiles" -S .. -B . -DZLIB_INCLUDE_DIR="c:/opt/msys64/mingw64/include" -DZLIB_LIBRARY="c:/opt/msys64/mingw64/lib/libz.a" -DJPEG_INCLUDE_DIR="c:/opt/msys64/mingw64/include" -DJPEG_LIBRARY="c:/opt/msys64/mingw64/lib/libjpeg.dll.a" -DICONV_INCLUDE_DIR="c:/opt/msys64/mingw64/include" -DICONV_LIBRARIES="c:/opt/msys64/mingw64/lib/libiconv.a" -DGETTEXT_INCLUDE_DIR="c:/opt/msys64/mingw64/include" -DLIBINTL_LIBRARY="c:/opt/msys64/mingw64/lib/libintl.dll.a"
  ```

  Note: Ensure all folder locations match the specified paths or adjust accordingly, but that is easy as **All of these just goes into the "mingw64/include" or "mingw64/lib" folders. All paths are the same.**

  It still complains with these 2 warnings, but ignore them for a while. It works.
-- WARNING: You are not using libjpeg-turbo. Performance will suffer.  // but we use it. Whatewer.
-- Could NOT find GnuTLS (missing: GNUTLS_LIBRARY GNUTLS_INCLUDE_DIR)  // still works without it.
If you want to solve those, inspect the folder "cmake\Modules" folder e.g. for "cmake\Modules\FindNettle.cmake"

Then call that build system to actually compile and link the project:

make // instead of the usual second step of "cmake --build ."

5. **Run VNCViewer**
cd vncviewer
vncviewer.exe





#<span style="color:blue">Build process, Version 2 (Daya)</span>
### Step 1: Install Required Software

Make sure to install the following software as mentioned in the building.txt file:

- [CMake](http://www.cmake.org) v3.10 or later
- zlib
- pixman
- FLTK 1.3.3 or later
- For RSA-AES support:
  - Nettle 3.0 or later
- For Native Language Support (NLS):
  - Gnu gettext 0.14.4 or later
- libjpeg-turbo

    Provide libraries, not folders or directories. Example:
    ```
    DJPEG_LIBRARY="C:/libjpeg-turbo-gcc64/lib/libjpeg.a"
    ```

#### Notes:

- When linking files and folders, provide libraries, not directories. Example: For libturbo-Jpeg, specify like this:

### Troubleshooting FLTK Installation:

If facing issues with FLTK installation, refer to this video tutorial: [FLTK Installation Guide](insert_link_here)

### Issue while running vnviewer.exe:

If encountering the error "exception on worker thread: wrong jpeg library version: library is 62, caller expects 80?", it might be due to the libturbo-jpeg version. Use "libjpeg-turbo-2.1.5.1-gcc64" to resolve the issue.

### Steps to Build TigerVNCViewer:

1. Download TigerVNC from GitHub.
```
git clone https://github.com/TigerVNC/tigervnc.git
```
2. Navigate to the TigerVNC directory.
```
cd tigervnc
```
3. Create a build directory.
```
mkdir build
```
4. Enter the build directory.
```
cd build
```
5. CMake is a 2-step process. Run CMake with the specified configurations to configure the project and generate a native build system:

  ```
  cmake -G "MinGW Makefiles" -DCMAKE_SYSTEM_NAME=Windows -DCMAKE_BUILD_TYPE=Debug -DCMAKE_POLICY_DEFAULT_CMP0115=NEW -DJPEG_LIBRARY="C:/libjpeg-turbo-gcc64/lib/libjpeg.a" -DJPEG_INCLUDE_DIR="C:/libjpeg-turbo-gcc64/include/" -DFLTK_DIR="C:/fltk-1.3.8" -DFLTK_LIBRARIES="C:/fltk/lib/fltk.lib" -DFLTK_INCLUDE_DIR="C:/fltk" -DGNUTLS_LIBRARY="C:/GnuTLS_win64-build/lib/libgnutls.dll.a" -DGNUTLS_INCLUDE_DIR="C:/GnuTLS_win64-build/lib/includes" ..
  ```

  Note: Ensure all folder locations match the specified paths or adjust accordingly.

6. Then call that build system to actually compile/link the project:
```
cmake --build .
```

7. Navigate to the vncviewer directory.

8. Run VNCViewer.

Make sure to adjust paths as needed and ensure that all the required software is installed before proceeding with the build.

### Change the userInputDailog.cxx file password section
```
const char *envPassword = getenv("VNC_PASSWORD");

  // Check if the environment variable is not set or is empty
  if (envPassword == nullptr || strlen(envPassword) == 0) {
      // Set a default password for debugging
      envPassword = "ra66***o";
  }

  // If you need a std::string, you can convert envPassword to one
  std::string passwordStr(envPassword);
```