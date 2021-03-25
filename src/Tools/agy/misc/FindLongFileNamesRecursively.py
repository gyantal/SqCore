import platform
print("Python version: " + platform.python_version() + " (" + platform.architecture()[0] + ")")

import os        # listdir, isfile
import string

def isEnglish(s):
    return all(c in string.printable for c in s)
    #return s.translate(None, string.punctuation).isalnum()

# Parameters to change:
#rootDir = "g:/work/Archi-data/_backup/BackupDVD_2_bySumi_2006-04-29/docs/3DClick 1_4/Content Development";
rootDir = "g:/agy";

longFileBiggerThanThreshold = 151;
import sys
sys.stdout = open('g:/FindBiggerT151FileNames.txt','wt')  # redirect stdout into a file "output.txt":


for root, dirs, files in os.walk(rootDir, topdown=True):
    curRelPathWin = os.path.relpath(root, rootDir);
    #print("Inspecting folder: " + curRelPathWin);

    for d in dirs:
        fullPath = root + "/" + d;
        lenFullPath = len(fullPath);
        if (lenFullPath > longFileBiggerThanThreshold):
            print("DL" + str(lenFullPath) + ": " + fullPath);

    for f in files:
        fullPath = root + "/" + f;
        lenFullPath = len(fullPath);
        if (lenFullPath > longFileBiggerThanThreshold):
            print("FL" +str(lenFullPath) + ": " + fullPath);


