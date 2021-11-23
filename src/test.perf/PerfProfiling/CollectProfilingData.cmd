@echo off
REM see for info: https://github.com/dotnet/diagnostics/blob/master/documentation/dotnet-trace-instructions.md
REM 1. Run app separately
REM 2. Find out the process identifier (pid) of the .NET Core 3.0 application, with the command: 'dotnet-trace list-processes'

@echo on
REM 1. Run your app and find out the process identifier (pid) with this:
dotnet-trace list-processes

REM 2. Then use 'dotnet-trace collect --process-id <10264> --providers Microsoft-Windows-DotNETRuntime' to sample data for some seconds and use PerfView
