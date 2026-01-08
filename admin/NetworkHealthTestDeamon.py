#!/usr/bin/python3
import sys
import re
import time
import speedtest # don't install library 'speedtest', but 'pip3 install speedtest-cli'
import win32api # pip3 install pypiwin32
from tcping import Ping # pip3 install tcping
from io import StringIO
import datetime



# https://stackoverflow.com/questions/2953462/pinging-servers-in-python
# "For python3 there's a very simple and convenient python module ping3: (pip install ping3, needs root privileges)."
# "Oh, need root prvilege not only for install, but also for execution: ping("example.com") "
# "Programmatic ICMP ping is complicated due to the elevated privileges required to send raw ICMP packets, and calling ping binary is ugly. For server monitoring, you can achieve the same result using a technique called TCP ping:"
# "Internally, this simply establishes a TCP connection to the target server and drops it immediately, measuring time elapsed. "

def testSpeedtest():
    spTstNum = 0
    try:
        st = speedtest.Speedtest()  # https://www.speedtest.net/ in browser gives 540Mbit/s, because excellent Chrome C++ implementation, but Python implementation is much slower, equivalent to 250Mbit/s
        spTstNum = st.download() / 1024 / 1024  # in bit per second, convert it to Mbit/s by dividing 1024*1024  "225.8859956876999 Mbit/s." format to 2 decimals
        spTstResult = format(spTstNum, '.2f')
        print('Speedtest is: ' + spTstResult + ' Mbit/s.')
    except Exception as e:
        print('Speedtest Failed: ' + e)
    return spTstNum


def speedtest_recursion(thresholdMbit, k, logfile):
    spTstNum = testSpeedtest()
    if spTstNum <= 0:
        spTstNumStr = 'FAIL'
    else:
        spTstNumStr = format(spTstNum, '.2f')
    logfile.write(datetime.datetime.now(datetime.UTC).strftime('%Y-%m-%d %H:%M:%S') + ',Speedtest,' + spTstNumStr + '\n')  # '2013-09-18 11:16:32'
    if spTstNum >= thresholdMbit or k <= 0:  # when Speedtest >= thresholdMbit Mbit, accept it as OK.
        return spTstNum
    else:   # when Speedtest < 90Mbit AND k > 0 => try again
        time.sleep(120)  # wait for 120? seconds = every 2 minutes
        return speedtest_recursion(thresholdMbit, k - 1, logfile)

nCycles = 0
# https://stackoverflow.com/questions/23704615/is-there-anything-wrong-with-a-python-infinite-loop-and-time-sleep
while True: # daemon function runs forever with sleep(), with a keyboard interrupt Ctrl-C, we can stop the infinite loop if necessary.
    print('Testing (' + str(nCycles) + ')...' )
    with open("NetworkHealthTest.sqlog.csv", "a") as logfile:  # When you are writing to a file in text mode (the default), it does newline translation as it writes; that is, each "\n" in the output will be translated to "\r\n" in the resulting file.
        # 1. TESTING PING
        print('Testing TcPing')
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
        logfile.write(datetime.datetime.now(datetime.UTC).strftime('%Y-%m-%d %H:%M:%S') + ',Ping,' + pingResult + '\n')  # '2013-09-18 11:16:32'

        # 2. SPEEDTEST
        if nCycles % 10 == 0:   # runs every 10 cycles, which is usually 10 minutes
            print('Testing Speedtest')
            thresholdMbit = 90
            spTstNum = speedtest_recursion(thresholdMbit, 3, logfile)
            if spTstNum < thresholdMbit:
                if spTstNum <= 0:
                    spTstNumStr = 'FAIL'
                else:
                    spTstNumStr = format(spTstNum, '.2f')
                win32api.MessageBox(0, 'Checking second time. Speedtest download speed is only ' + spTstNumStr + ' Mbit/s.' , 'SqCore:NetworkHealthTestDeamon', 0x00001000) # https://stackoverflow.com/questions/177287/alert-boxes-in-python
    nCycles += 1
    time.sleep(60)  # wait for 60? seconds = every 1 minute
