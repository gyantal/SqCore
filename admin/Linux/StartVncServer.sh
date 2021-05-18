#!/bin/bash
DATE=$(date +'%F %H:%M:%S')
echo "Starting VNC Server...time: $DATE" >>/home/sq-vnc-client/_admin/StartVncServer2.log

# Sleep for 10 seconds: intention to avoid grey-screen, which happened randomly; maybe leave time for X to initialize properly
# Create first (:1) display for Trader Users
sleep 10s
tightvncserver -geometry 1920x1080 -localhost :1

# Create first (:2) display for Devs
sleep 2s
tightvncserver -geometry 1920x1080 -localhost :2

DATE=$(date +'%F %H:%M:%S')
echo "Alive VNC Server...time: $DATE" >>/home/sq-vnc-client/_admin/StartVncServer2.log

# .config/autostart runs twice, because TightVncServer runs it on :0 and on :1 as well. Buy. >https://www.raspberrypi.org/forums/viewtopic.php?t=59285  
# so instead of that, run startups only once after tightnvserver is up. With a bit delay.
# Later it turned out  /etc/rc.local scripts were already executed twice after upgrading to Ubuntu20.4 in 2021
# So, this is not necessary here. Going back to .config/autostart method.
#sleep 5s
#DISPLAY=:1.0  lxterminal -e "/home/sq-vnc-client/SQ/Server/VirtualBroker/startDelayed.py" &
#DATE=$(date +'%F %H:%M:%S')
#echo "Started VirtualBroker.PY...time: $DATE" >>/home/sq-vnc-client/_admin/StartVncServer2.log

