#!/usr/bin/python3
import sys
import re
import time
import speedtest # pip3 install speedtest
import win32api # pip3 install pypiwin32
from tcping import Ping # pip3 install speedtest-cli
from io import StringIO
from datetime import datetime



# https://stackoverflow.com/questions/2953462/pinging-servers-in-python
# "For python3 there's a very simple and convenient python module ping3: (pip install ping3, needs root privileges)."
# "Oh, need root prvilege not only for install, but also for execution: ping("example.com") "
# "Programmatic ICMP ping is complicated due to the elevated privileges required to send raw ICMP packets, and calling ping binary is ugly. For server monitoring, you can achieve the same result using a technique called TCP ping:"
# "Internally, this simply establishes a TCP connection to the target server and drops it immediately, measuring time elapsed. "


nCycles = 0
# https://stackoverflow.com/questions/23704615/is-there-anything-wrong-with-a-python-infinite-loop-and-time-sleep
while True: # daemon function runs forever with sleep(), with a keyboard interrupt Ctrl-C, we can stop the infinite loop if necessary.
    print('Testing (' + str(nCycles) + ')...' )
    with open("NetworkHealthTest.sqlog.csv", "a") as logfile:  # When you are writing to a file in text mode (the default), it does newline translation as it writes; that is, each "\n" in the output will be translated to "\r\n" in the resulting file.
        # 1. TESTING PING
        print('Testing Ping')
        pingResult = 'FAIL'
        sys.stdout = mystdout = StringIO()
        try:
            # Ping(host, port, timeout)  or ping www.wikipedia.org:443
            tcpPing = Ping('www.google.com', 443, 10)
            tcpPing.ping(1)
            # Connected to www.googleXXX.com[:443]: seq=1 time out!
            # Connected to www.google.com[:443]: seq=1 time=48.68 ms
        except Exception as e:
            print(e) # [WinError 10060] A connection attempt failed because the connected party did not properly respond
        consoleStr = mystdout.getvalue()
        sys.stdout = sys.__stdout__
        # print(consoleStr)

        regex = re.compile(r'\=([0-9\.]+?)\sms')
        match = regex.search(consoleStr) # match() checks only for matches at the beginning of the string while re.search() will match a pattern anywhere in string
        if match:
            pingResult = match.group(1)  # '48.68' as ms
            print('Yes! Ping is OK: ' + pingResult + ' ms.')
        else:
            print('No! Ping Failed.')
        logfile.write(datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S') + ',Ping,' + pingResult + '\n')  # '2013-09-18 11:16:32'

        # 2. SPEEDTEST
        if nCycles % 10 == 0:   # runs every 10 cycles, which is usually 10 minutes
            print('Testing Speedtest')
            spTstResult = 'FAIL'
            spTstNum = 0
            try:
                st = speedtest.Speedtest() # https://www.speedtest.net/ in browser gives 540Mbit/s, because excellent Chrome C++ implementation, but Python implementation is much slower, equivalent to 250Mbit/s
                spTstNum = st.download() / 1024 / 1024
                spTstResult = format(spTstNum, '.2f')  # in bit per second, convert it to Mbit/s by dividing 1024*1024  "225.8859956876999 Mbit/s." format to 2 decimals
                print('Yes! Speedtest is OK: ' + spTstResult + ' Mbit/s.')
            except Exception as e:
                print(e)
                print('No! Speedtest Failed.')
            logfile.write(datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S') + ',Speedtest,' + spTstResult + '\n')  # '2013-09-18 11:16:32'
            if spTstNum < 100:
                win32api.MessageBox(0, 'Speedtest download speed is only ' + spTstResult + ' Mbit/s.' , 'SqCore:NetworkHealthTestDeamon', 0x00001000) # https://stackoverflow.com/questions/177287/alert-boxes-in-python

    nCycles += 1
    time.sleep(60)  # wait for 60? seconds = every 1 minute
