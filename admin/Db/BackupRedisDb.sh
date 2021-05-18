#!/bin/bash
# RedisDb is backed up at 8:05 on every Sunday on Linux, and at 10:00 on every Thursday on Win
DATE=$(date +'%F %H:%M:%S')
echo "Starting RedisBackup...time: $DATE" >>/home/ubuntu/_admin/BackupRedisDb.log

today=$(date +"%Y-%m-%d")
# the copy of the file is still owned by the user sq-core-redis
sudo -u sq-core-redis cp /var/lib/redis/7045/dump-7045.rdb /home/ubuntu/redis-backup/dump-7045.${today}.rdb
# the 7z file is owned by user ubuntu. File size went from 2.5K to 2.3K (only a 10% saving because it is a small file)
7z a /home/ubuntu/redis-backup/dump-7045.${today}.rdb.7z /home/ubuntu/redis-backup/dump-7045.${today}.rdb
# rm -f force delete without the prompt for 'remove write-protected regular file?'
rm /home/ubuntu/redis-backup/dump-7045.${today}.rdb -f

DATE=$(date +'%F %H:%M:%S')
echo "End of RedisBackup...time: $DATE" >>/home/ubuntu/_admin/BackupRedisDb.log

# Then either call this SH script from the command line on demand (or from Windows backup script)
# Or add this to 'sudo crontab -e'
# Backup Redis DB 8:05 on every Sunday. Once per week is fine to avoid 360 backups per year
# 5 8 * * Sun /home/ubuntu/_admin/BackupRedisDb.sh