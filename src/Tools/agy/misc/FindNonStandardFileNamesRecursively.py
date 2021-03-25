import platform
print("Python version: " + platform.python_version() + " (" + platform.architecture()[0] + ")")

import os        # listdir, isfile
import string

def isEnglish(s):
    return all(c in string.printable for c in s)
    #return s.translate(None, string.punctuation).isalnum()

# Parameters to change:
#rootDir = "g:/work/Archi-data/_backup/BackupDVD_2_bySumi_2006-04-29/docs/3DClick 1_4/Content Development";
rootDir = "g:/work";

import sys
sys.stdout = open('g:/FindNonStandardFileNames.txt','wt')  # redirect stdout into a file "output.txt":

for root, dirs, files in os.walk(rootDir, topdown=True):
    curRelPathWin = os.path.relpath(root, rootDir);
    #print("Inspecting folder: " + curRelPathWin);

    nonEnglishDirs = [d for d in dirs if not isEnglish(d)];
    for d in nonEnglishDirs:
        print("NonEnglish dir: " + root + "/" + d);
    
    nonEnglishFiles = [f for f in files if not isEnglish(f)];
    for f in nonEnglishFiles:
        print("NonEnglish file: " + root + "/" + f);


