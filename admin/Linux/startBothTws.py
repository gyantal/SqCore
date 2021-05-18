#!/usr/bin/python3
from platform import python_version
import subprocess
import time
import logging
import argparse

def xstr(s):
    return '' if s is None else str(s)

numeric_log_level = logging.INFO
parser = argparse.ArgumentParser(description='Optional app description')
parser.add_argument('--log', help='DEBUG or INFO or WARNING or ERROR or CRITICAL')
args = parser.parse_args()
print("commandline --log parameter: '" + xstr(args.log) + "'")
if isinstance(args.log, str):
	numeric_log_level = getattr(logging, args.log.upper(), None)
	if not isinstance(numeric_log_level, int):
		numeric_log_level = logging.INFO

#print(numeric_log_level)
logging.basicConfig(format='%(asctime)s %(levelname)s %(message)s', level=numeric_log_level,
	handlers=[logging.FileHandler("startBothTws.py.log"),
			logging.StreamHandler()])

logging.info("***** START *****")
logging.info("Python version: " + python_version())

logging.info("DcMain TWS: starting in a parallel subprocess.");
subprocess.call(['/home/sq-vnc-client/opt/ibc/twsstartDcMain.sh'])
logging.info("DcMain TWS: started. Now sleep 100sec (50s may not enough)...")
time.sleep(100)

logging.info("DeBlanzac TWS: starting in a parallel subprocess.");
subprocess.call(['/home/sq-vnc-client/opt/ibc/twsstartDeBlanzac.sh'])
logging.info("***** END *****");
