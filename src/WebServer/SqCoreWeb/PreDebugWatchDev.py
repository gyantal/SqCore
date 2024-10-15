import os
import platform
from threading import Thread

import sys
import socket
import psutil

print("SqBuild:PreDebugWatchDev.py Python ver: " + platform.python_version() + " (" + platform.architecture()[0] + "), CWD:'" + os. getcwd() + "'")
print("In VsCode, this should run in a separate CMD window, not in VsCode.Terminal. If not, manually run once this PY") # That 'magically' fixed the VsCode in 2023-01.
if (os.getcwd().endswith("SqCore")) : # VsCode's context menu 'Run Python file in Terminal' runs it from the workspace folder. VsCode F5 runs it from the project folder. We change it to the project folder
    os.chdir(os.getcwd() + "/src/WebServer/SqCoreWeb")

# 1. Basic checks: Ensure Node.js is installed. If node_modules folder is empty, it should restore Npm packages.
nodeTouchFile = os.getcwd() + "/node_modules/.install-stamp"
if os.path.isfile(nodeTouchFile):
    print ("SqBuild: /node_modules/ exist")
else:
    nodeRetCode = os.system("node --version")   # don't want to run 'node --version' all the times. If stamp file exists, assume node.exe is installed
    if (nodeRetCode != 0) :
        sys.exit("Node.js is required to build and run this project. To continue, please install Node.js from https://nodejs.org/")
    sys.exit("SqBuild: PreDebugBuildDev.py checks /node_modules in parallel. And we don't want that both processes start to download that huge folder. Exit now. It only happens once per year.")
    # os.system("npm install")    # Install the dependencies in the local node_modules folder.
    angularRetCode = os.system("ng --version") # requires the local node_modules folder, otherwise raises unhandled exception
    if (angularRetCode != 0) :
        sys.exit("SqBuild: NodeJs's AngularCLI is required to build and run this project. To continue, please install 'npm install -g @angular/cli'")
    # Path(nodeTouchFile).touch()


# 2. What can Debug user watch: wwwrootGeneral (NonWebpack), ExampleCsServerPushInRealtime (Webpack), HealthMonitor (Angular), MarketDashboard (Angular)
def runCommandThread(commandStr):
     print("SqBuild: Executing in an in-process separate thread: " + commandStr)
     # Don't run them in a separate process CMD window, because then it is more difficult to kill them. Just run in inprocess threads, which will PIPE their output StdOut to this the parent process.
     os.system(commandStr) # run the command in-process, not as a separate process
     # os.system("cmd /k " + commandStr)  # 2023-01: it seemed it was a fix, but didn't change a thing.
     # os.system("start /wait cmd /k " + commandStr) # This would creates a separate CMD/WT window, the 'start' creates as a separate process, so when I kill this running process, that extra process is not terminated. Can be worked out that we kill those processes too, but not too easy.
     # processObj = subprocess.run("tsc --watch", shell=True, stdout=subprocess.PIPE)  # An alternative to os.system(). This will run the command and return any output into process.output

# we run os.system() commands 
def startThreadForSubprocess(pCommandStr):
    thread = Thread(target = runCommandThread, daemon = True, args = (pCommandStr,))
    thread.start()  #thread1.join()

# 2.1 Non-Webpack webapps in ./wwwroot/webapps should be transpiled from TS to JS
# os.system("tsc --watch")    # works like normal, loads ./tsconfig.json, which contains "include": ["wwwroot"]. 
# subprocess.run(["tsc", "--watch"], stdout=subprocess.PIPE) # This will run the command and return any output
# subprocess.run("tsc --watch", shell=True, stdout=subprocess.PIPE)
# subprocess.run("tsc --watch", shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)  # This will run the command and return any output
# thread1 = Thread(target = runCommandThread, args = ("tsc --watch",))
# thread1.setDaemon(True)  # daemon = true didn't help. Main thread exited, but watchers were left alive.
# thread1.start()  #thread1.join()
startThreadForSubprocess("tsc --watch --preserveWatchOutput")


# 2.2 Webpack webapps in ./webapps should be packed (TS, CSS, HTML)
# Webpack: 'Multiple output files' are not possible and out of scope of webpack. You can use a build system.
# thread2 = Thread(target = runCommandThread, args = ("npx webpack --config webapps/Example/ExampleCsServerPushInRealtime/webpack.config.js --mode=development --watch",))
# thread2.setDaemon(True)
# thread2.start()  #thread2.join()
# startShellCallingThread("npx webpack --config webapps/Example/ExampleCsServerPushInRealtime/webpack.config.js --mode=development --watch")  # leave this, so we are aware of compiling errors in development
# startShellCallingThread("npx webpack --config webapps/Example/ExampleJsClientGet/webpack.config.js --mode=development --watch")
# startShellCallingThread("npx webpack --config webapps/ContangoVisualizer/webpack.config.js --mode=development --watch")   # commented out, so less resources is used in Development
# startShellCallingThread("npx webpack --config webapps/WithdrawalSimulator/webpack.config.js --mode=development --watch") # <AFTER DEVELOPMENT IS FINISHED> commented out, so less resources is used in Development
# startShellCallingThread("npx webpack --config webapps/LiveStrategy/Sin/webpack.config.js --mode=development --watch")
# startShellCallingThread("npx webpack --config webapps/LiveStrategy/RenewedUber/webpack.config.js --mode=development --watch")
# startShellCallingThread("npx webpack --config webapps/LiveStrategy/UberTaa/webpack.config.js --mode=development --watch")
# startShellCallingThread("npx webpack --config webapps/VolatilityVisualizer/webpack.config.js --mode=development --watch")
# startShellCallingThread("npx webpack --config webapps/SeekingAlphaHelper/webpack.config.js --mode=development --watch")

# 2.3 Angular webapps in the project folder should be served on different ports. If an Angular app is not developed any more, comment it out to save resources
# ng serve doesn't create anything into --output-path=wwwroot/webapps/ (it keeps its files temp, maybe in RAM)
# to create files into wwwroot/weapps, at publish run 'ng build HealthMonitor --prod --output-path=wwwroot/webapps/HealthMonitor --base-href ./'
# startShellCallingThread("ng serve --ssl --ssl-key DevTools/AngularLocalServeHttpsCert/localhost.key  --ssl-cert DevTools/AngularLocalServeHttpsCert/localhost.crt --proxy-config angular.watch.proxy.conf.js HelloAngular --port 4201")
startThreadForSubprocess("ng serve --ssl --ssl-key DevTools/AngularLocalServeHttpsCert/localhost.key  --ssl-cert DevTools/AngularLocalServeHttpsCert/localhost.crt --proxy-config angular.watch.proxy.conf.js MarketDashboard --host 127.0.0.1 --port 4202")
# startShellCallingThread("ng serve --ssl --ssl-key DevTools/AngularLocalServeHttpsCert/localhost.key  --ssl-cert DevTools/AngularLocalServeHttpsCert/localhost.crt --proxy-config angular.watch.proxy.conf.js HealthMonitor --host 127.0.0.1 --port 4203")
# startThreadForSubprocess("ng serve --ssl --ssl-key DevTools/AngularLocalServeHttpsCert/localhost.key  --ssl-cert DevTools/AngularLocalServeHttpsCert/localhost.crt --proxy-config angular.watch.proxy.conf.js PortfolioViewer --host 127.0.0.1 --port 4204")
# startThreadForSubprocess("ng serve --ssl --ssl-key DevTools/AngularLocalServeHttpsCert/localhost.key  --ssl-cert DevTools/AngularLocalServeHttpsCert/localhost.crt --proxy-config angular.watch.proxy.conf.js ChartGenerator --host 127.0.0.1 --port 4205")
# startThreadForSubprocess("ng serve --ssl --ssl-key DevTools/AngularLocalServeHttpsCert/localhost.key  --ssl-cert DevTools/AngularLocalServeHttpsCert/localhost.crt --proxy-config angular.watch.proxy.conf.js TechnicalAnalyzer --host 127.0.0.1 --port 4206")

# 3. Wait for Python message to terminate all threads.
print("SqBuild: User can break (Control-C, or closing CMD) here manually. Or Wait for socket (TCP port) communication from another Python thread to end this thread.")
# Named pipes are nothing but mechanisms that allow IPC communication through the use of file descriptors associated with special files
# Let's use the be basic socket, because it is platform-independent. (and it is not file based), and we can use it easily in C# interop to Python (even under Linux).
serversocket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)  # versus AF_LOCAL, reliable, bidirectional
serversocket.bind(('localhost', 8389)) # bind the server socket to 'localhost'. On most platforms, this will take a shortcut around a couple of layers of network code and be quite a bit faster.
serversocket.listen(5) # become a server socket, maximum 5 connections, queue up as many as 5 connect requests (the normal max) before refusing outside connections. 
while True:
    connection, address = serversocket.accept()
    buf = connection.recv(64)
    if len(buf) > 0:
        print("SqBuild: Socket received message: " + str(buf))
        break


# 4. Terminate all sub-threads. It not only terminates threads, but 'taskkill current process' will close the CMD window as well, which is perfect.
print("SqBuild: Main thread exits now as it kill the the CMD/terminal window")
# quit(), sys.exit() # they don't kill the started child-threads, even though they are daemons
# taskkill parameters: /F: Force termination. /T: Terminate the specified process tree with child processes

# Method 1: set the Title of the CMD window, and kill the task which has the WindowTitle that was just set. This stopped working in WT (Windows Terminal) with the message 'No tasks running with the specified criteria.', maybe because of many tabpages in WT. The 'title' command now sets only the title of the tabpage, not the title of the whole WT.exe
#uniqueCmdTitle = "PreDebugWatch.py." + str(random.randint(0,99999))
#os.system("title " + uniqueCmdTitle)   # set the title of the CMD
#os.system('taskkill /F /T /FI "WindowTitle eq ' + uniqueCmdTitle + '"')  #kill the task which has the title that was just set.
# Or you can do the same in 1 step, with the & operator:
#os.system('title '+ uniqueCmdTitle + ' & taskkill /F /T /FI "WindowTitle eq ' + uniqueCmdTitle + '"')

# taskkill with WindowTitle stopped working with the message 'No tasks running with the specified criteria.', because there is really no task with that "Window Title:". Maybe because we started to use WT.exe instead of CMD.exe


# Method 2: get the pid of the process and kill it.
# If WT.exe was started from TotalCommander, we have to go back to parent.parent().pid, If it was started from VsCode, we have to go back to parent.pid.
print('SqBuild: process PID ' + str(os.getpid())) # the embedded Python
print('SqBuild: process parent PID ' + str(os.getppid())) # the embeded CMD inside Windows Terminal
parentProcess = psutil.Process(os.getpid()).parent()
if parentProcess.parent() is None:
    os.system('taskkill /F /T /pid ' + str(parentProcess.pid)) # If started in VsCode, we kill the parent process
else:
    os.system('taskkill /F /T /pid ' + str(parentProcess.parent().pid)) # If started in WT.exe that was started from TotalCommander, we kill the parent of the parent

#Keeping for debugging purposes:
#print("parent.parent process of our main python is", parent.parent().pid)
#print("parent.parent.parent (TC.exe) process of our main python is", parent.parent().parent().pid) # Total Commander
#children = parent.parent().parent().children(recursive=True)
#print(children)
#for child in children:
#    print("Child pid is {}".format(child.pid))
