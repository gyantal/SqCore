import os
import subprocess
import platform
import sys
from pathlib import Path
import fileinput
import shutil

print("SqBuild: Python ver: " + platform.python_version() + " (" + platform.architecture()[0] + "), CWD:'" + os. getcwd() + "'")
if (os.getcwd().endswith("SqCore")) : # VsCode's context menu 'Run Python file in Terminal' runs it from the workspace folder. VsCode F5 runs it from the project folder. We change it to the project folder
    os.chdir(os.getcwd() + "/src/WebServer/SqCoreWeb")

# 1. Basic checks: Ensure Node.js is installed. If node_modules folder is empty or package.json changed since last nodeTouchFile, it should restore Npm packages.
nodeTouchFile = os.getcwd() + "/node_modules/.install-stamp"
nodeJsInstallNeeded = False
if os.path.isfile(nodeTouchFile):
    print ("SqBuild: /node_modules/ exist")
    nodeTouchModificationTimestamp = os.path.getmtime(nodeTouchFile)
    packageJsonModificationTimestamp = os.path.getmtime(os.getcwd() + "/package.json")
    if packageJsonModificationTimestamp > nodeTouchModificationTimestamp :
        nodeJsInstallNeeded = True
else:
    nodeJsInstallNeeded = True

if nodeJsInstallNeeded:
    nodeRetCode = os.system("node --version")   # don't want to run 'node --version' all the times.
    if (nodeRetCode != 0) :
        sys.exit("SqBuild: Node.js is required to build and run this project. To continue, please install Node.js from https://nodejs.org/")
    os.system("npm install")    # Install the dependencies in the local node_modules folder.  # -f for suppressing warning of optional SKIPPING OPTIONAL DEPENDENCY: fsevents
    #os.system("npm install fsevents@latest -f --save-optional")    # Do it once if fsevents (Mac file system) warning appear "npm WARN optional SKIPPING OPTIONAL DEPENDENCY: fsevents" 
    angularRetCode = os.system("ng --version") # requires the local node_modules folder, otherwise raises unhandled exception
    if (angularRetCode != 0) :
        sys.exit("SqBuild: NodeJs's AngularCLI is required to build and run this project. To continue, please install 'npm install -g @angular/cli'")
    Path(nodeTouchFile).touch()

# 2.1. Non-Webpack webapps in ./wwwroot/webapps should be transpiled from TS to JS
print("\nSqBuild: Executing 'tsc.exe'")
os.system("tsc")    # works like normal, loads ./tsconfig.json, which contains "include": ["wwwroot"]. 

# 2.2. Webpack webapps in ./webapps should be packed (TS, CSS, HTML)
# npm install -D clean-webpack-plugin css-loader html-webpack-plugin mini-css-extract-plugin ts-loader typescript webpack webpack-cli
# Webpack: 'Multiple output files' are not possible and out of scope of webpack. You can use a build system.
print("\nSqBuild: Executing 'npx webpack --mode=production'")
os.system("npx webpack --config webapps/Example/ExampleJsClientGet/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/Example/ExampleCsServerPushInRealtime/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/ContangoVisualizer/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/WithdrawalSimulator/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/LiveStrategy/Sin/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/LiveStrategy/RenewedUber/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/LiveStrategy/UberTaa/webpack.config.js --mode=production")
os.system("npx webpack --config webapps/VolatilityVisualizer/webpack.config.js --mode=production")

# 2.3. Angular webapps in  ./Angular should be built
print("\nSqBuild: Executing Angular 'ng build...'")
os.system("ng build MarketDashboard --configuration production --output-path=wwwroot/webapps/MarketDashboard --base-href ./")
os.system("ng build HealthMonitor --configuration production --output-path=wwwroot/webapps/HealthMonitor --base-href ./")
os.system("ng build PortfolioViewer --configuration production --output-path=wwwroot/webapps/PortfolioViewer --base-href ./")
os.system("ng build ChartGenerator --configuration production --output-path=wwwroot/webapps/ChartGenerator --base-href ./")
os.system("ng build TechnicalAnalyzer --configuration production --output-path=wwwroot/webapps/TechnicalAnalyzer --base-href ./")

# 3. Brotli-ing text (HTML, JS, CSS) files in wwwroot.
print ("\nSqBuild: !!!!!!!!!!!! Brotli-ing text files... (brotli(x64).exe runs only in Python x64) !!!!!!!!!!")
for dir in os.walk("wwwroot"):
    # print("directory: " + dir[0])
    for file in dir[2]:
        fileName = os.path.splitext(file)
        fileExt = fileName[1]
        # normal (non-debug) user should not downoald TS, MAP files, so don't increase the footprint by brotling them.
        if (fileExt == ".html" or fileExt == ".js" or fileExt == ".css" or fileExt == ".json" or fileExt == ".xml" or fileExt == ".txt"):
            print("SqBuild: Brotli-ing: " + dir[0] + "\\" + fileName[0] + fileExt)
            os.system("c:/windows/system32/brotli.exe " +dir[0]+ "/" + file + ' --best --force --verbose')

# 4. DotNet (C#) build RELEASE and Publish
print("\nSqBuild: Executing 'dotnet publish...'")

# 2024-09-19: We have to use the "-r" flag, because there is a strange bug in .Net 8 Linux, that disappears if we target Linux with the -r runtime flag.
# !22|ERROR|Sq: LegacyDb Error. Init_WT() exception: Could not load file or assembly 'Microsoft.Data.SqlClient, Version=5.0.0.0, Culture=neutral, PublicKeyToken=23ec7fc2d6eaa4a5'. The system cannot find the file specified." 
# https://github.com/dotnet/SqlClient/issues/2146
# https://github.com/dotnet/SqlClient/issues/1945
publishRuntimeTarget = "linux-x64"  # use empty string ("") for not specifying target runtime. In that case App will only run on target machine if the .Net8 runtime is installed before.
if (publishRuntimeTarget == ""):
    publishTargetFolder = "bin/Release/net8.0/publish"
    nLogConfigPath = "bin/Release/net8.0/publish/NLog.config"
else:
    publishTargetFolder = "bin/Release/net8.0/" + publishRuntimeTarget
    nLogConfigPath = "bin/Release/net8.0/" + publishRuntimeTarget + "/publish/NLog.config"

if os.path.exists(publishTargetFolder):
    shutil.rmtree(publishTargetFolder)    # platform independent way of deleting a folder recursively

# dotnet publish has an "-r" flag, to specify the target runtime
# A. If you don't specify the -r flag when using dotnet publish, it publishes a framework-dependent application, meaning it relies on the .NET runtime being installed on the target machine. (2024: 114MB deployment)
# B. using the -r flag creates a self-contained deployment, bundling the .NET runtime with the app, so it can run independently on the target platform. (2024: 108MB deployment, Linux specific)
if (publishRuntimeTarget == ""):
    os.system("dotnet publish --configuration Release SqCoreWeb.csproj /property:GenerateFullPaths=true") # target folder: '/bin/Release/netX.0/publish/'
else:
    os.system("dotnet publish --configuration Release SqCoreWeb.csproj /property:GenerateFullPaths=true -r " + publishRuntimeTarget) # target folder: '/bin/Release/netX.0/linux-x64/publish/'

# 5. Postprocess the published folder. (before deploying to Linux)
# NLog.config: fileName="${basedir}/../../../../../../logs/SqCoreWeb.${date:format=yyyy-MM-dd}.sqlog" should be changed to fileName="${basedir}/../logs/SqCoreWeb.${date:format=yyyy-MM-dd}.sqlog"
# This should not be done in the local Debug or Release folders, only the Publish folder.
print("\nSqBuild: Modifying NLog.config for Linux logs folder.")
with fileinput.FileInput(nLogConfigPath, inplace=True, backup='.bak') as file:
    for line in file:
        print(line.replace("{basedir}/../../../../../../logs", "{basedir}/../logs"), end='')

print("\nScroll up to check that all build parts were succesful! We pause to prevent VsCode tasks.json to close the CMD.")
os.system("pause")  # To prevent tasks.json to close the CMD window. This will generate a pause and will ask user to press any key to continue.