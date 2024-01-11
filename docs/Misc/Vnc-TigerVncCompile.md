<!-- To view properly formatted MD (Markdown) file in VS Code, install VS Code extension: "Markdown Preview Enhanced" - https://www.youtube.com/watch?v=4QzFVQsD-9I -->
<!-- Right click on the markdown file and select option -> "Markdown Preview Enhanced: Open preview to the side"( shortcut-ctrl+KV)  -->
# TigerVNCViewer Build Steps

## Step 1: Install Required Software

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

### Notes:

- When linking files and folders, provide libraries, not directories. Example: For libturbo-Jpeg, specify like this:

## Troubleshooting FLTK Installation:

If facing issues with FLTK installation, refer to this video tutorial: [FLTK Installation Guide](insert_link_here)

## Issue while running vnviewer.exe:

If encountering the error "exception on worker thread: wrong jpeg library version: library is 62, caller expects 80?", it might be due to the libturbo-jpeg version. Use "libjpeg-turbo-2.1.5.1-gcc64" to resolve the issue.

## Steps to Build TigerVNCViewer:

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

## Change the userInputDailog.cxx file password section
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