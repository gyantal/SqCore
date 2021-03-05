import platform
print("Python version: " + platform.python_version() + " (" + platform.architecture()[0] + ")")

import os        # listdir, isfile
import paramiko  # for sftp
import colorama  # for colourful print
import subprocess
from stat import S_ISDIR
from colorama import Fore, Back, Style
import time

use7zip = True

start_time = time.time()
# Parameters to change:
# 1. LocalPC params
runningEnvironmentComputerName = platform.node()    # 'gyantal-PC' or Balazs
if runningEnvironmentComputerName == 'gyantal-PC':
    rootLocalDir = "g:/work/Archi-data/GitHubRepos/SqCore/src"        #os.walk() gives back in a way that the last character is not slash, so do that way
    serverRsaKeyFile = "g:/agy/Google Drive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/cert/AwsSqCore/AwsSqCore,sq-vnc-client.pem"
else:   # TODO: Laci, Balazs, you have to add your IF here (based on the 'name' of your PC)
    rootLocalDir = "d:/GitHub/SqCore/src"       #os.walk() gives back in a way that the last character is not slash, so do that way
    serverRsaKeyFile = "d:/<fill it to user>/Google Drive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/cert/AwsSqCore/AwsSqCore,sq-vnc-client.pem"
acceptedSubTreeRoots = ["Tools\\RedisManager", "Common\\SqCommon", "Common\\DbCommon"]        # everything under these relPaths is traversed: files or folders too
zipExeWithPath = "c:/Program Files/7-Zip/7z.exe"

# 2. Server params
serverHost = "ec2-34-251-1-119.eu-west-1.compute.amazonaws.com"         # SqCore server
serverPort = 122    # on MTraderServer, port 22 bandwidth throttled, because of VNC viewer usage, a secondary SSH port 122 has no bandwith limit
serverUser = "sq-vnc-client"
rootRemoteDir = "/home/" + serverUser + "/SQ/Tools/RedisManager/src"

zipFileNameWithoutPath = "deploy.7z"
zipFileRemoteName = rootRemoteDir + "/" + zipFileNameWithoutPath
zipListFileName = rootLocalDir + "/" + "deployList.txt"
zipFileName = rootLocalDir + "/" + zipFileNameWithoutPath

excludeDirs = set(["bin", "obj", ".vs", "artifacts", "Properties", "node_modules"])
excludeFileExts = set(["sln", "xproj", "log", "sqlog", "ps1", "sh", "user", "md"])
excludeFiles = set(["Deploy.py"]) # send *.py files in general, except Deploy.py, start.py to not clutter the root folder on Server

# "mkdir -p" means Create intermediate directories as required. 
# http://stackoverflow.com/questions/14819681/upload-files-using-sftp-in-python-but-create-directories-if-path-doesnt-exist
def mkdir_p(sftp, remote_directory):        
    """Change to this directory, recursively making new folders if needed.     Returns True if any folders were created."""
    if remote_directory == '/':
        # absolute path so change directory to root
        sftp.chdir('/')
        return
    if remote_directory == '':
        # top-level relative directory must exist
        return
    try:
        sftp.chdir(remote_directory) # sub-directory exists
    except IOError:
        dirname, basename = os.path.split(remote_directory.rstrip('/'))
        mkdir_p(sftp, dirname) # make parent directories
        sftp.mkdir(basename) # sub-directory missing, so created it
        sftp.chdir(basename)
        return True

# remove directory recursively
# http://stackoverflow.com/questions/20507055/recursive-remove-directory-using-sftp
# http://stackoverflow.com/questions/3406734/how-to-delete-all-files-in-directory-on-remote-server-in-python
def isdir(path):
    try:
        return S_ISDIR(sftp.stat(path).st_mode)
    except IOError:
        return False

# remove the root folder too
def rm(sftp, path): 
    files = sftp.listdir(path=path)
    for f in files:
        #filepath = os.path.join(path, f)       # it adds '\\', but my server is Linux
        filepath = path + "/" + f 
        if isdir(filepath):
            print("Removing: " + filepath)
            rm(sftp, filepath)
        else:
            sftp.remove(filepath)
    sftp.rmdir(path)

# do not remove the root folder, only the subfolders recursively
def rm_onlySubdirectories(sftp, path):
    files = sftp.listdir(path=path)
    for f in files:
        #filepath = os.path.join(path, f)       # it adds '\\', but my server is Linux
        filepath = path + "/" + f 
        if isdir(filepath):
            print("Removing: " + filepath)
            rm_onlySubdirectories(sftp, filepath)
            sftp.rmdir(filepath)
        else:
            sftp.remove(filepath)    

# script START
colorama.init()
print(Fore.MAGENTA + Style.BRIGHT  +  "Start deploying '" + acceptedSubTreeRoots[0] + "' ...")

if os.path.isfile(zipFileName):
    os.remove(zipFileName)  #remove old zip list file if exists
if os.path.isfile(zipListFileName):
    os.remove(zipListFileName)  #remove old zip file if exists

#quicker to do one remote command then removing files/folders recursively one by one
#in the future. We can 7-zip locally, upload it by Sftp, unzip it with SSHClient commands. It is about 2 days development, so, later.
#command = "ls " + rootRemoteDir
command = "rm -rf " + rootRemoteDir
print("SSHClient. Executing remote command: " + command)
sshClient = paramiko.SSHClient()
sshClient.set_missing_host_key_policy(paramiko.AutoAddPolicy())
sshClient.connect(serverHost, serverPort, username = serverUser, pkey = paramiko.RSAKey.from_private_key_file(serverRsaKeyFile))
(stdin, stdout, stderr) = sshClient.exec_command(command)
for line in stdout.readlines():
    if line != "\n":
        print(line, end='') # tell print not to add any 'new line', because the input already contains that

print("SFTPClient is connecting...")
transport = paramiko.Transport((serverHost, serverPort))
transport.connect(username = serverUser, pkey = paramiko.RSAKey.from_private_key_file(serverRsaKeyFile))
sftp = paramiko.SFTPClient.from_transport(transport)
#rm_onlySubdirectories(sftp, rootRemoteDir)

fileNamesToDeploy = []
for root, dirs, files in os.walk(rootLocalDir, topdown=True):
    curRelPathWin = os.path.relpath(root, rootLocalDir)
    # we have to visit all subdirectories
    dirs[:] = [d for d in dirs if d not in excludeDirs]     #Modifying dirs in-place will prune the (subsequent) files and directories visited by os.walk

    if curRelPathWin != ".":    # root folder is always traversed
        isFilesTraversed = False
        for aSubTreeRoot in acceptedSubTreeRoots:
            if curRelPathWin.startswith(aSubTreeRoot):
                isFilesTraversed = True   
                break
        if not isFilesTraversed:
            continue        # if none of the acceptedSubTreeRoots matched, skip to the next loop cycle

    goodFiles = [f for f in files if os.path.splitext(f)[1][1:].strip().lower() not in excludeFileExts and not f.endswith(".lock.json") and f not in excludeFiles]
    for f in goodFiles:
        if curRelPathWin == ".":
            curRelPathLinux = ""
        else:
            curRelPathLinux = curRelPathWin.replace(os.path.sep, '/') + "/"
        remoteDir = rootRemoteDir + "/" +  curRelPathLinux
        print(Fore.CYAN + Style.BRIGHT  + "Processing file: " + remoteDir  + f)
        currFileName = rootLocalDir + "/"+ curRelPathWin.replace(os.path.sep, '/')+  "/"+f
        if os.path.isfile(currFileName):
            fileNamesToDeploy.append((curRelPathWin.replace(os.path.sep, '/') + "/" + f))
            if not use7zip:
                mkdir_p(sftp, remoteDir) 
                ret = sftp.put(root + "/" + f, remoteDir + f, None, True) # Check FileSize after Put() = True
        # print(Style.RESET_ALL + str(ret))

if use7zip:
    # Windows has an 8KB limit on command line length. SqCore Web all files with relative paths are 10KB. We cannot list all the files in the command line. We have to use a @listfile, which can be longer than the command line limit
    zipListFile = open(zipListFileName,"w")
    zipListFile.write('\n'.join(fileNamesToDeploy))         # concatenate them with a CRLF
    zipListFile.close()

    print(Fore.CYAN + Style.BRIGHT  + "Packing all files ...")

    print("working dir before " + os.getcwd())
    os.chdir(rootLocalDir)
    print("working dir after " + os.getcwd())

    cmd = [zipExeWithPath, 'a', zipFileName, '-spf2', '@' + zipListFileName]
    # cmd = [zipExeWithPath, 'a', zipFileName, '-spf2', ' '.join(fileNamesToDeploy]  # file list on command line works only if command line is less than 8KB
    sp = subprocess.Popen(cmd, stderr=subprocess.STDOUT, stdout=subprocess.PIPE).wait()

    print(Fore.CYAN + Style.BRIGHT  + "Creating root directory on the server ...")
    mkdir_p(sftp, rootRemoteDir)

    print(Fore.CYAN + Style.BRIGHT + "Sending packed file ...")
    ret = sftp.put(zipFileName, zipFileRemoteName, None, True)  # Check FileSize after Put() = True

    # unpack files
    print(Fore.CYAN + Style.BRIGHT  + "Unpacking file on the server ...")
    command = "cd " + rootRemoteDir + " && 7z x " + zipFileRemoteName
    (stdin, stdout, stderr) = sshClient.exec_command(command)
    for line in stdout.readlines():
        if line != "\n":
            print(line, end='') # tell print not to add any 'new line', because the input already contains that

sshClient.close()

print(Fore.MAGENTA + Style.BRIGHT  +  "SFTPClient is closing. Deployment '" + acceptedSubTreeRoots[0] + "' is OK.")
sftp.close()
transport.close()

if os.path.isfile(zipFileName):
    os.remove(zipFileName)  # remove zip list file
if os.path.isfile(zipListFileName):
    os.remove(zipListFileName)  # remove zip file

print("--- Deployment of %d files ended in %03.2f seconds ---" % (len(fileNamesToDeploy), time.time() - start_time))    # 183 files: one by one upload: 38sec, 7zip: 4.8sec

# k = input("Press ENTER...")
