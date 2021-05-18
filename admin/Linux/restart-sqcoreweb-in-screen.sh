#!/bin/bash

# 1. Kill a detached screen session if it exists
#https://stackoverflow.com/questions/18517059/run-command-if-screen-exists
#If I use screen -S jenkins_job -X quit on a non-existent job shell will return error code and jenkins script will fail as well.
screen -ls 2>&1 | grep '(Detached)' | grep -o 'SqCoreWeb' | xargs -I{} -n 1 -r screen -r -S {} -X quit

# 2. Create a screen with a name and in detached mode
screen -S "SqCoreWeb" -d -m
echo A new screen 'SqCoreWeb' is created. Sleeping for 1 sec before sending command to start webserver...

# 3. sleep for 1 second, to give screen time to start the session before sending the command. Otherwise, sporadic error with message 'No screen session found.'
sleep 1

# 4. Send the command to be executed on your screen
# The $ before the command is to make the shell parse the \n inside the quotes, and the newline is required to execute the command (like when you press enter).
screen -r "SqCoreWeb" -X stuff $'cd /home/sq-vnc-client/SQ/WebServer/SqCoreWeb/published/publish\ndotnet SqCoreWeb.dll\n'

#this is how to switch to the session
#screen -r "SqCoreWeb"
#this is how to kill the session
#screen -X -S "SqCoreWeb" quit
#
# Then run 'crontab -e' and insert this:
## run SqCore web server 20 sec after reboot
#@reboot sleep 20 && /home/sq-vnc-client/SQ/admin/restart-sqcoreweb-in-screen.sh

