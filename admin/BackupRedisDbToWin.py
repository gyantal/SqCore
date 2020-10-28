#!/usr/bin/python3
# RedisDb is backed up at 8:05 on every Sunday on Linux, and at 10:00 on every Thursday on Win
import platform
import paramiko  # for sftp
from datetime import datetime

serverHost = "ec2-34-251-1-119.eu-west-1.compute.amazonaws.com"         # MTrader server
serverPort = 122    # on MTraderServer, port 22 bandwidth throttled, because of VNC viewer usage, a secondary SSH port 122 has no bandwith limit
serverUser = "ubuntu"
rootRemoteDir = "/home/ubuntu/redis-backup/"

# Parameters to change:
runningEnvironmentComputerName = platform.node()    # 'gyantal-PC' or Balazs
if runningEnvironmentComputerName == "gyantal-PC":
    serverRsaKeyFile = "g:/work/Archi-data/GitHubRepos/HedgeQuant/src/Server/AmazonAWS/AwsMTrader/AwsMTrader.pem"
    backupLocalDir = "g:/work/Archi-data/_backup/2020/SqCoreWeb_RedisDb/"
else:   # TODO: Laci, Balazs, you have to add your IF here (based on the 'name' of your PC)
    serverRsaKeyFile = "d:/SVN/HedgeQuant/src/Server/AmazonAWS/AwsMTrader/AwsMTrader,sq-vnc-client.pem"
    backupLocalDir = ":<Fill this up later>"

# 1. Run Linux shell script to create 'dump-7045.2020-10-20.rdb.7z' into /home/ubuntu/redis-backup/
command = "/home/ubuntu/_admin/BackupRedisDb.sh"
print("Weekly Backup RedisDb To Win (Thursdays, 10am)\n")
print("1. Executing remote command: " + command)

# input('Press ENTER to continue...')

sshClient = paramiko.SSHClient()
sshClient.set_missing_host_key_policy(paramiko.AutoAddPolicy())
sshClient.connect(serverHost, serverPort, username = serverUser, pkey = paramiko.RSAKey.from_private_key_file(serverRsaKeyFile))
(stdin, stdout, stderr) = sshClient.exec_command(command)
for line in stdout.readlines():
    if line != "\n":
        print(line, end='') # tell print not to add any 'new line', because the input already contains that
sshClient.close()

# 2. SFTP.Get 'dump-7045.2020-10-20.rdb.7z' from /home/ubuntu/redis-backup/
print("\n2. SFTPClient is connecting to get 7zip file...")
transport = paramiko.Transport((serverHost, serverPort))
transport.connect(username = serverUser, pkey = paramiko.RSAKey.from_private_key_file(serverRsaKeyFile))
sftp = paramiko.SFTPClient.from_transport(transport)
filename = "dump-7045." + datetime.utcnow().strftime('%Y-%m-%d') + ".rdb.7z"
sftp.get(rootRemoteDir + filename, backupLocalDir + filename)
print("Backup file created: " + backupLocalDir + filename)
sftp.close()
transport.close()

input('\nPress ENTER to end...<Requiring user keystroke is intentional for checking backup happened without error>')

