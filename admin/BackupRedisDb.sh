#!/bin/bash
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