
import os
import os.path
import sys
import configparser
import tkinter as tk
import tkinter.ttk as ttk
import tkinter.messagebox
import zipfile
import threading
import json
import queue
import shlex
import subprocess
import enum
import collections
import time
import re
import getopt

TestCase = collections.namedtuple("TestCase", "name meta inputfilename inputfilecontents")


#metadata: Missing = don't care
#   returns: integer                [missing=don't care]
#   syntaxok: True|False            [missing=True]
#   loops: True|False               [missing=False]
#   input: Program input, on stdin  [missing=none]
#   output: Expected output         [missing=don't care]
#   inputfiles : [{
#       name: filename
#       content: data
#   }]
#   outputfiles : [{
#       name: filename
#       content: data
#   }]
#   bonus: groupName


MessageType = enum.Enum("MessageType",
    "ASMOUTPUT CONSOLEOUTPUT STARTSPINNER STOPSPINNER CLEAR_ASMOUTPUT SET_STATUSBAR"
)

Message = collections.namedtuple("Message",
    field_names="type data style",
    defaults   =[     None,None]
)


#failed = compile, assemble, or link failed
#stopped = manually stopped
#timeout = timed out
#ok = ran to completion
ExitStatus = enum.Enum("ExitStatus",
    "FAILED TIMEOUT OK STOPPED"
)



#newline symbol
NEWLINE = "\u23ce";

def main():

    config = parseConfig()

    mainwin = MainWindow(config)

    mainwin.loadTests()

    if config["oneshot"] == "yes":
        mainwin.runAllTestsAndQuit()

    mainwin.mainloop()


def parseConfig():

    #set defaults for all items.
    #blank = unspecified as of yet
    config={
        "launcher":"",
        "exe":"",
        "assembler":"",
        "linker":"",
        "assemblerargs":"",
        "linkerargs": "",
        "inputfile":"inp.txt",
        "compilerargs":"inp.txt out.asm",
        "asmfile":"out.asm",
        "objfile":"out.o",
        "exefile":"out.exe",
        "gluefile":"",
        "timeout":"1.5",
        "oneshot":"",
        "chdir":"",
        "tests":os.path.join( os.path.dirname(__file__),"tests")
    }


    configfile = os.path.join(os.path.dirname(__file__),"config.ini")
    if os.path.exists(configfile):
        # ~ tkinter.messagebox.showerror(title="Error",
            # ~ message="No config file: The file config.ini must "
                # ~ "be in the same folder as the test harness.")
        # ~ return

        configp = configparser.ConfigParser(interpolation=configparser.ExtendedInterpolation())
        configp.read(configfile)
        configX = configp["config"]


        #override defaults with config file
        for key in configX:
            config[key] = configX[key]

        #command line overrides everything
        opts,args = getopt.gnu_getopt(
            sys.argv[1:],
            "",
            [q+"=" for q in config.keys()]
        )
        for o,a in opts:
            assert o.startswith("--")
            o=o[2:]
            if o in config:
                config[o] = a
            else:
                assert 0


    def setIfNeeded(key,value):
        if key not in config or not config[key]:
            config[key]=value

    if "win" in sys.platform.lower():
        setIfNeeded("objformat","win64")
    elif "lin" in sys.platform.lower():
        setIfNeeded("objformat","elf64")


    setIfNeeded("assembler","nasm")

    setIfNeeded("assemblerargs",
        f'-f {config["objformat"]} -Werror -o "{config["objfile"]}" "{config["asmfile"]}"'
    )

    #set items that haven't been specified yet
    if "win" in sys.platform:
        setIfNeeded( "gluefile", "kernel32.lib" )
        setIfNeeded( "linker", "lld-link.exe")
        setIfNeeded("linkerargs",
            f'/entry:_start /subsystem:console "/out:{config["exefile"]}" "{config["gluefile"]}" "{config["objfile"]}"'
        )
    elif "linux" in sys.platform:
        setIfNeeded("gluefile","stubs.o")
        #setIfNeeded("launcher","dotnet")
        setIfNeeded("linker","ld.lld")
        setIfNeeded("linkerargs",
            f'--entry=_start -o "{config["exefile"]}" "{config["objfile"]}" "{config["gluefile"]}"'
        )


    if "chdir" in config and config["chdir"]:
        os.chdir(config["chdir"])

    return config


class ScrolledText(tk.Frame):
    def __init__(self,owner,showPosition,**kw):
        super().__init__(owner,)

        if "echo" in kw:
            self.echo = kw["echo"]
            del kw["echo"]
        else:
            self.echo=False

        self.text = tk.Text(self,**kw)
        self.vbar = ttk.Scrollbar(self,orient=tk.VERTICAL)
        self.hbar = ttk.Scrollbar(self,orient=tk.HORIZONTAL)
        self.grid_rowconfigure(0,weight=1)
        self.grid_rowconfigure(1,weight=0)
        self.grid_rowconfigure(2,weight=0)
        self.grid_columnconfigure(0,weight=1)
        self.grid_columnconfigure(1,weight=0)
        self.text.grid(row=0,column=0,sticky="nesw")
        self.vbar.grid(row=0,column=1,sticky="ns")
        self.hbar.grid(row=1,column=0,sticky="ew")
        self.vbar.configure(command=self.text.yview)
        self.hbar.configure(command=self.text.xview)
        self.text.configure(xscrollcommand=self.hbar.set)
        self.text.configure(yscrollcommand=self.vbar.set)

        #dummy
        self.normalTag = self.text.tag_configure("normal", elide=False )
        self.errorTag = self.text.tag_configure("error", foreground="red")
        self.infoTag = self.text.tag_configure("info", foreground="grey")
        self.cmdTag = self.text.tag_configure("cmd", foreground="blue")
        self.warningTag = self.text.tag_configure("warning", foreground="#808000")
        self.successTag = self.text.tag_configure("success", foreground="#00a000")
        self.buildTag = self.text.tag_configure("buildoutput", foreground="#800080")
        self.progTag = self.text.tag_configure("progoutput", foreground="#ff00ff")


        def event(*args):
            tmp = self.text.index(tk.INSERT)
            tmp = tmp.split(".")
            self.poslabel.configure(text="Line {} Col {}".format(tmp[0],tmp[1]))

        if showPosition:
            self.poslabel=tk.Label(self)
            self.poslabel.grid(row=2,column=0,sticky="ew")
            for ev in ["<Key>","<KeyRelease>","<ButtonRelease-1>",
                "<ButtonRelease-2>", "<ButtonRelease-3>",
                "<Button-1>", "<Button-2>", "<Button-3>"]:
                self.text.bind(ev,event)
    def print(self,*args,**kw):
        end=kw.get("end","\n")
        tag=kw.get("style","normal")
        tmp = [str(q) for q in args]
        tmp = " ".join(tmp)
        if self.echo:
            print(tmp)
        oldstate = self.text.cget("state")
        self.text.configure(state="normal")
        self.text.insert( "end", tmp+end, [tag] )
        self.text.configure(state=oldstate)
        self.text.see("end")
    def get(self):
        txt = self.text.get("1.0","end")
        return txt
    def clear(self):
        oldstate = self.text.cget("state")
        self.text.delete("1.0","end")
        self.text.configure(state=oldstate)
        self.text.see("end")

class Spinner:
    def __init__(self,owner):
        fr = tk.Frame(owner)
        bg = fr.cget("bg")
        dots=[]
        NR=1
        NC=10
        for i in range(NR):
            dots.append([])
            for j in range(NC):
                f1 = tk.Frame(fr,width=4,height=4)
                f1.grid(row=i,column=j)
                dots[i].append(f1)

        self.fr=fr
        #which dot is currently "on". None = all dots are off
        self.currentDot=None
        #how many ticks until we stop displaying. None=
        #we are already stopped OR we have an infinite
        #time left before stopping.
        self.ticksToStop=None
        #ticks left until we change a dot. Only updated
        #when currentDot != None
        self.dotTime=0
        #the Frame objects for the dots
        self.dots=[]
        #default background color
        self.bg=bg

        if 0:
            #square
            for i in range(N):
                self.dots.append( dots[0][i] )
            for i in range(1,N):
                self.dots.append( dots[i][-1] )
            for i in reversed(range(1,N)):
                self.dots.append( dots[-1][i] )
            for i in reversed(range(1,N)):
                self.dots.append( dots[i][0] )
        elif 1:
            #back and forth
            for i in range(NC):
                self.dots.append( dots[0][i] )
            for i in reversed(range(1,NC)):
                self.dots.append( dots[0][i] )

    def start(self):
        #set current dot to zero and
        #set time left to infinite
        self.ticksToStop=None
        if self.currentDot == None:
            self.currentDot=0

    def stop(self):
        #we don't stop immediately to avoid
        #flicker if there's a sequence of start/stop calls.
        #we wait a couple of ticks.
        #If we already are in a countdown to stopping,
        #leave it as-is. Otherwise, set to a known value.
        if self.ticksToStop == None:
            self.ticksToStop=3

    def update(self):

        if self.ticksToStop != None:
            #in a countdown timer
            self.ticksToStop -= 1
            if self.ticksToStop <= 0:
                self.currentDot=None
                self.ticksToStop=None
                for row in self.dots:
                    for dot in self.dots:
                        dot.configure(bg=self.bg)
                return

        if self.currentDot==None:
            #not displaying
            return

        self.dotTime -= 1
        if self.dotTime <= 0:
            #move to next dot
            self.dots[self.currentDot].configure(bg=self.bg)
            self.currentDot += 1
            self.currentDot %= len(self.dots)
            self.dots[self.currentDot].configure(bg="black")
            self.dotTime=10

    def grid(self,**kw):
        self.fr.grid(**kw)

class MainWindow:
    def __init__(self,config):

        self.config=config
        self.root = tkinter.Tk()
        self.root.geometry("700x480+10+10")
        self.makeUI()

        #list of TestCase objects
        self.tests=[]

        def periodic():
            self.periodicTask()
            self.root.after(5,periodic)
        self.root.after( 5, periodic)

        #tests that will be started by after()
        #used for batch tests only, not immediate one-shots
        self.queuedTests=[]

        self.quitOnTestsComplete=False

        self.C=threading.Condition()
        self.messages = queue.Queue()
        self.backgroundThread=None

    def runAllTestsAndQuit(self):
        self.quitOnTestsComplete=True
        self.testAll()


    def makeUI(self):
        def pane():
            return tk.PanedWindow(root,
                showhandle=True,sashwidth=4,
                sashrelief="groove")

        root = self.root

        root.grid_rowconfigure(0,weight=1)
        root.grid_columnconfigure(0,weight=1)

        pane1 = pane()
        pane1.configure(orient=tk.HORIZONTAL)
        pane1.grid(row=0,column=0,rowspan=1,columnspan=1,
            sticky="nesw")

        pane2 = pane()
        pane2.configure(orient=tk.VERTICAL)

        pane3 = tk.Frame()
        pane3.grid_rowconfigure(0,weight=0)
        pane3.grid_rowconfigure(1,weight=1)
        pane3.grid_columnconfigure(0,weight=1)


        northeastframe = ttk.Frame(pane2)
        southeastframe = ttk.Frame(pane2)
        northwestframe = ttk.Frame(pane3)
        southwestframe = ttk.Frame(pane3)

        pane1.add(pane3,width=300)
        pane1.add(pane2,width=300)
        pane2.add(northeastframe,height=200)
        pane2.add(southeastframe,height=200)
        #pane3.add(northwestframe,height=100)
        #pane3.add(southwestframe,height=300)
        northwestframe.grid(row=0,column=0,sticky="ew")
        southwestframe.grid(row=1,column=0,sticky="nesw")


        self.statusbar = tk.Frame(root)
        self.statusbar.grid_columnconfigure(0,weight=0)
        self.statusbar.grid_columnconfigure(1,weight=1)
        self.statusbar.grid_columnconfigure(2,weight=0)
        self.statusbar.grid(row=1,column=0,sticky="ew")

        self.statuslabel = ttk.Label(self.statusbar,text="Ready.")
        self.statuslabel.grid(row=0,column=0)

        self.spinner = Spinner(self.statusbar)
        self.spinner.grid(row=0,column=2)

        self.makeTestSelection(northwestframe)
        self.makeSourceCodeInput(southwestframe)
        self.makeAssemblyOutput(northeastframe)
        self.makeConsoleOutput(southeastframe)
        self.makeMenubar(root)


    def loadTests(self):
        # ~ rex=re.compile(r"^(i\d+)\.txt")
        # ~ crex=re.compile(r"(?s)/\*\*(.*?)\*\*/")

        lst=[]
        # ~ self.grammarfile=None

        testfolder = self.config["tests"]

        for dirpath,dirs,files in os.walk( testfolder ):
            for fn in files:
                if fn == "grammar.txt":
                    pass
                    #self.grammarfile = os.path.join(dirpath,fn)
                elif fn.endswith(".txt"):
                    inputfilename = os.path.join(dirpath,fn)
                    # ~ treefile = os.path.join(dirpath,M.group(1)+"-tree.txt")
                    # ~ if not os.path.exists(treefile):
                        # ~ treefile = os.path.join(dirpath,M.group(1)+".tree")
                    with open(inputfilename) as fp:
                        inputfilecontents = fp.read()

                    if inputfilecontents.startswith("/**"):
                        i = inputfilecontents.find("**/")
                        metadata = inputfilecontents[3:i]
                        inputfilecontents = inputfilecontents[i+3:]
                    else:
                        metadata=""

                    try:
                        J = json.loads(metadata)
                    except json.decoder.JSONDecodeError as e:
                        print()
                        print()
                        print()
                        print("JSON error in file",inputfilename)
                        print(str(e))
                        print()
                        print()
                        sys.exit(1)
                    lst.append( TestCase(
                        name=fn,
                        inputfilename=inputfilename,
                        inputfilecontents=inputfilecontents,
                        meta=J
                    ))

        # ~ if not self.grammarfile:
            # ~ tkinter.messagebox.showerror(title="Error",
                # ~ message="No grammar file: Check the tests folder")
            # ~ sys.exit(1)

        lst.sort(key=lambda q: q.name)
        self.tests=lst

        if not self.tests:
            tkinter.messagebox.showerror(title="Error",
                message=f"No tests found in {testfolder}")
            sys.exit(1)

        names = [q.name for q in self.tests]
        self.testSelectionBox.configure(values=names)
        self.testSelectionBox.current(0)
        self.sourceChangedCallback(0)

    def sourceChangedCallback(self,idx):
        info = self.tests[idx]
        self.sourcetext.clear()
        data = info.inputfilecontents   #has leading comments removed
        self.sourcetext.text.insert("1.0", data )
        self.metainfo.clear()
        meta = info.meta
        if "returns" in meta:
            self.metainfo.print("Should return",meta["returns"])
        if meta.get("loops",False):
            self.metainfo.print("Note: Should loop forever")
        if not meta.get("syntaxok",True):
            self.metainfo.print("Note: Should not compile")
        if meta.get("input"):
            self.metainfo.print("Provided input:", meta["input"].replace("\n",NEWLINE) )
        if meta.get("output"):
            self.metainfo.print("Expected output:",meta["output"].replace("\n",NEWLINE))

    def makeTestSelection(self,frame):

        frame.grid_rowconfigure(0,weight=0)
        frame.grid_rowconfigure(1,weight=1)
        frame.grid_columnconfigure(0,weight=1)

        cbox = ttk.Combobox(frame,state="readonly")
        cbox.grid(row=0,column=0,sticky="ew")

        def callback(ev):
            idx = cbox.current()
            self.sourceChangedCallback(idx)
            #self.asmoutput.clear()
            #self.consoleoutput.clear()

        cbox.bind("<<ComboboxSelected>>", callback )
        self.testSelectionBox = cbox

        metainfo = ScrolledText(frame,False,height=4,wrap="none")
        metainfo.grid(row=1,column=0,sticky="nesw")
        metainfo.print("Metadata will go here")
        self.metainfo = metainfo

    def makeSourceCodeInput(self,frame):

        frame.grid_rowconfigure(0,weight=0)
        frame.grid_rowconfigure(1,weight=1)
        frame.grid_rowconfigure(2,weight=0)
        frame.grid_columnconfigure(0,weight=1)

        lbl = ttk.Label(frame,text="Source Code")
        lbl.grid(row=0,column=0,sticky="ew")
        sourcetext = ScrolledText(frame,True,wrap="none")
        sourcetext.grid(row=1,column=0,sticky="nesw")
        sourcetext.text.insert(tk.END,"If you had source code,\nit would appear here")
        self.sourcetext=sourcetext

    def makeAssemblyOutput(self,frame):

        frame.grid_rowconfigure(0,weight=0)
        frame.grid_columnconfigure(0,weight=1)
        frame.grid_rowconfigure(1,weight=1)

        lbl = ttk.Label(frame,text="Assembly Output")
        lbl.grid(row=0,column=0,sticky="ew")

        asmoutput = ScrolledText(frame,True,wrap="none")
        asmoutput.grid(row=1,column=0,sticky="nesw")
        asmoutput.text.insert(tk.END,"Assembly code will appear here.")
        self.asmoutput=asmoutput


    def makeConsoleOutput(self,frame):

        frame.grid_rowconfigure(0,weight=0)
        frame.grid_columnconfigure(0,weight=1)
        frame.grid_rowconfigure(1,weight=1)

        lbl = ttk.Label(frame,text="Console Output")
        lbl.grid(row=0,column=0,sticky="ew")

        consoleoutput = ScrolledText(frame,False,wrap="none",echo=True)
        consoleoutput.grid(row=1,column=0,sticky="nesw")
        consoleoutput.text.insert(tk.END,"Console messages will go here.")

        self.consoleoutput=consoleoutput

    def makeMenubar(self,root):
        mbar = tk.Menu(root)
        filemenu = tk.Menu(mbar,tearoff=False)
        filemenu.add_command(label="Run current...",
            accelerator="Ctrl+R",command=self.runCurrent)
        root.bind("<Control-r>",self.runCurrent)
        filemenu.add_command(label="Run all...",command=self.testAll,
            accelerator="Ctrl+Shift+R")
        root.bind("<Control-R>",self.testAll)
        filemenu.add_separator()
        filemenu.add_command(label="Stop tests", command=self.stopEverything)
        filemenu.add_command(label="Quit", command=lambda: sys.exit(0))
        mbar.add_cascade(label="File",menu=filemenu)
        root.config(menu=mbar)

    def flushQueue(self):
        while True:
            try:
                msg = self.messages.get(block=False)
                msgtype = msg[0]
                if msgtype == MessageType.ASMOUTPUT:
                    self.asmoutput.print(msg.data,end="",style=msg.style)
                elif msgtype == MessageType.CLEAR_ASMOUTPUT:
                    self.asmoutput.clear()
                elif msgtype == MessageType.CONSOLEOUTPUT:
                    self.consoleoutput.print(msg.data,end="",style=msg.style)
                elif msgtype == MessageType.STARTSPINNER:
                    self.spinner.start()
                elif msgtype == MessageType.STOPSPINNER:
                    self.spinner.stop()
                elif msgtype == MessageType.SET_STATUSBAR:
                    self.statuslabel.configure(text=msg.data)
                else:
                    print("Bad message",msg)
            except queue.Empty:
                break

    def clearOutput(self):
        self.consoleoutput.clear()
        self.asmoutput.clear()

    def runCurrent(self,*args):

        self.queuedTests = []

        if self.backgroundThread:
            #blocks until done
            self.backgroundThread.stopNow()
            self.backgroundThread=None

        self.flushQueue()
        self.clearOutput()

        curr = self.testSelectionBox.current()
        info = self.tests[curr]
        self.sourcetext.clear()
        data = info.inputfilecontents
        self.sourcetext.text.insert("1.0", data )

        self.executeTest(
            inputfilecontents=info.inputfilecontents,
            meta=info.meta
        )

    def testAll(self,*args):
        if self.backgroundThread:
            #blocks until done
            self.backgroundThread.stopNow()
            self.backgroundThread=None

        self.flushQueue()
        self.clearOutput()
        self.queuedTests = list(range(len(self.tests)))
        #periodicTask will pick these up later

    def periodicTask(self):
        #periodically poll the message queue
        #also see if we need to dispatch another test
        #and update the spinner
        self.flushQueue()
        self.runQueuedTest()
        self.spinner.update()

    def checkIt( self, metadata, exitStatus, exitValue, stdout ):
        if exitStatus == ExitStatus.FAILED:
            if False == metadata.get("syntaxok",True):
                #no point checking other stuff
                #since it didn't compile
                return True

        #if we get here, it compiled

        if False == metadata.get("syntaxok",True):
            self.consoleoutput.print("Program was syntactically invalid, but it compiled OK", style="error")
            return False

        if exitStatus == ExitStatus.TIMEOUT:
            if metadata.get("loops",False):
                #program loops forever (correctly). Nothing else to check.
                return True
            else:
                self.consoleoutput.print("Timeout!",style="error")
                return False

        if metadata.get("loops",False):
            #should have looped, but it didn't
            self.consoleoutput.print("Program should have looped forever, but it didn't",style="error")
            return False

        if exitStatus == ExitStatus.FAILED:
            return False

        if "returns" in metadata and metadata["returns"] != None:
            if exitValue != metadata["returns"]:
                self.consoleoutput.print(f'Return value mismatch: Expected {metadata["returns"]} but got {exitValue}',style="error")
                return False

        if "output" in metadata:
            if stdout != metadata["output"]:
                self.consoleoutput.print("Output mismatch",style="error")
                return False

        return True

    def stopEverything(self):
        if self.backgroundThread:
            self.backgroundThread.stopNow()
            self.backgroundThread=None
        self.flushQueue()
        self.queuedTests=[]


    def runQueuedTest(self):
        #items are not removed from the queue until
        #the test is completely done
        if len(self.queuedTests) == 0:
            return

        def failed():
            self.consoleoutput.print("Stopping.",style="warning")
            self.queuedTests=[]

        if self.backgroundThread:

            #still working
            if self.backgroundThread.is_alive():
                return

            #should return immediately
            self.backgroundThread.join()

            exitStatus = self.backgroundThread.exitStatus
            exitValue =  self.backgroundThread.exitValue
            stdout = self.backgroundThread.stdout

            self.backgroundThread=None

            #probably superfluous, but not harmful
            self.flushQueue()

            idx = self.queuedTests[0]
            testdata = self.tests[idx]
            metadata = testdata.meta

            if not self.checkIt( metadata, exitStatus, exitValue, stdout ):
                failed()
                return

            self.consoleoutput.print(testdata.name,"OK",style="success")
            self.queuedTests.pop(0)
            if len(self.queuedTests) == 0:
                self.consoleoutput.print("\U0001f601 \U0001f601 \U0001f601 All tests OK \U0001f601 \U0001f601 \U0001f601",style="success")
                if self.quitOnTestsComplete:
                    sys.exit(0)

        if len(self.queuedTests) == 0:
            return
        #if we get here, there's no active
        #background thread, and we need to spawn one
        #to do the next test
        idx = self.queuedTests[0]
        testdata = self.tests[idx]
        self.consoleoutput.print("="*40,style="info")
        self.consoleoutput.print("Test:",testdata.name,style="info")

        self.testSelectionBox.current(idx)
        self.sourceChangedCallback(idx)

        self.executeTest(
            inputfilecontents=testdata.inputfilecontents,
            meta=testdata.meta
        )

    def executeTest(self,*,inputfilecontents,meta):

        assert inputfilecontents != None

        if self.backgroundThread:
            #blocks until thread exits.
            #This should never happend because the caller ensures
            #that there's no thread, but just to be safe,
            #we do this
            self.backgroundThread.stopNow()
            self.backgroundThread=None

        #flush any old output
        self.flushQueue()

        self.backgroundThread = BackgroundThread(
            config=self.config,
            messages=self.messages,
            inputfilecontents=inputfilecontents,
            meta=meta)
        self.backgroundThread.start()

    def mainloop(self):
        self.root.mainloop()

class MessagePumper(threading.Thread):
    def __init__(self,stdout,Q):
        super().__init__()
        self.stdout=stdout
        self.Q=Q
        self.active=True
        self.L=threading.Lock()
        self.stdoutdata=[]
        self.reachedEnd=False

    def stopNow(self):
        with self.L:
            self.active=False

    def readEverything(self):
        with self.L:
            r = self.reachedEnd
        return r

    def run(self):
        while True:
            with self.L:
                ac = self.active
            if not ac:
                return
            c = self.stdout.read(1)
            if len(c) == 0:
                with self.L:
                    self.reachedEnd=True
                return
            self.stdoutdata.append(c.decode(errors="replace"))
            self.Q.put( Message(MessageType.CONSOLEOUTPUT, c.decode(), "progoutput") )


class MessagePusher(threading.Thread):
    def __init__(self,stdin,data):
        super().__init__()
        self.stdin=stdin
        self.data=data

    def stopNow(self):
        pass

    def run(self):
        if self.data == None:
            return
        self.stdin.write(self.data)
        self.stdin.flush()

class BackgroundThread(threading.Thread):
    def __init__(self,*,config,messages,inputfilecontents,meta):
        super().__init__()
        self.config=config
        self.messages=messages
        self.inputfilecontents=inputfilecontents
        self.meta=meta

        self.exitStatus=None
        self.exitValue=None
        self.stdout=""

        self.C=threading.Condition()
        self.mustStop=False

    def stopNow(self):
        #called from another thread
        #to signal this one to halt immediately
        with self.C:
            self.mustStop=True
        self.join()

    def run(self):
        try:
            self.messages.put( Message(MessageType.STARTSPINNER))
            self.messages.put( Message(MessageType.SET_STATUSBAR, "Running..."))

            config=self.config
            meta = self.meta

            try:
                os.unlink(config["asmfile"])
            except OSError:
                pass
            try:
                os.unlink(config["inputfile"])
            except OSError:
                pass

            rv = doCompile(
                config=self.config,
                messages=self.messages,
                inputfilecontents=self.inputfilecontents
            )

            #display any asm output even if compile failed
            try:
                with open(config["asmfile"]) as fp:
                    asmdata = fp.read()
                self.messages.put( Message(MessageType.CLEAR_ASMOUTPUT, ) )
                self.messages.put( Message(MessageType.ASMOUTPUT,asmdata) )
            except FileNotFoundError:
                pass

            if not rv:
                self.messages.put( Message(
                    MessageType.CONSOLEOUTPUT,
                    "*** Compile failed.\n",
                    "error")
                )
                self.exitStatus=ExitStatus.FAILED
                return

            if not doAssemble(self.config,self.messages):
                self.messages.put( Message(
                    MessageType.CONSOLEOUTPUT,
                    "*** Assemble failed.\n",
                    "error")
                )
                self.exitStatus=ExitStatus.FAILED
                return

            if not doLink(self.config,self.messages):
                self.messages.put( Message(
                    MessageType.CONSOLEOUTPUT,
                    "*** Link failed.\n",
                    "error")
                )
                self.exitStatus=ExitStatus.FAILED
                return

            cmd = [ os.path.join(".",config["exefile"]) ]
            self.messages.put( Message(
                MessageType.CONSOLEOUTPUT,
                cmd[0]+"\n",
                "cmd")
            )

            try:
                P = subprocess.Popen( cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            except OSError as e:
                self.messages.put( Message(MessageType.CONSOLEOUTPUT,
                    f"*** Error: {e}\n",
                    "error")
                )
                self.exitStatus = ExitStatus.FAILED
                return


            timeout = float(config["timeout"])


            if "stdin" in meta:
                stdin = meta["stdin"]
                #expand \n and other escape sequences
                #https://stackoverflow.com/questions/4020539/process-escape-sequences-in-a-string-in-python
                stdin = bytes(stdin,"utf-8").decode("unicode_escape")
            else:
                stdin = None

            messagePusher = MessagePusher(P.stdin,stdin)
            messagePusher.start()
            messagePumper = MessagePumper(P.stdout,self.messages)
            messagePumper.start()
            started = time.time()

            def killP():
                P.terminate()
                try:
                    P.wait(0.5)
                except subprocess.TimeoutExpired:
                    P.kill()

            while True:

                if P.poll() != None:
                    #process has exited
                    while not messagePumper.readEverything():
                        time.sleep(0.01)
                    # ~ messagePumper.stopNow()
                    self.exitStatus = ExitStatus.OK
                    break

                with self.C:
                    if self.mustStop:
                        killP()
                        messagePumper.stopNow()
                        self.exitStatus=ExitStatus.STOPPED
                        self.messages.put( Message(
                            MessageType.CONSOLEOUTPUT,
                            "Manually stopped\n",
                            "warning")
                        )
                        break

                now = time.time()
                if now-started > timeout:
                    killP()
                    messagePumper.stopNow()
                    self.exitStatus=ExitStatus.TIMEOUT
                    self.messages.put( Message(
                        MessageType.CONSOLEOUTPUT,
                        "Timed out (infinite loop?)\n",
                        "warning")
                    )
                    break

                #polling. Should fix this to use conditions/signalling,
                #but that's more complex
                time.sleep(0.05)

            #if we get here, the process must have exited

            self.messages.put( Message(
                MessageType.CONSOLEOUTPUT,
                f"Process returned: {P.returncode}\n",
                "info")
            )

            self.exitValue = P.returncode
            self.stdout = "".join(messagePumper.stdoutdata)

        finally:
            with self.C:
                self.C.notify_all()
            self.messages.put( Message(MessageType.STOPSPINNER, ) )
            self.messages.put( Message(MessageType.SET_STATUSBAR, ""))



def doCompile(*,config,messages,inputfilecontents):

    compilerinput = shlex.split(config["inputfile"])[0]

    with open(compilerinput,"w") as fp:
        fp.write(inputfilecontents)
    launcher=shlex.split(config["launcher"])
    compiler=shlex.split(config["exe"])
    compilerargs=shlex.split(config["compilerargs"])
    cmd=[]
    if launcher:
        cmd += launcher
    cmd += compiler
    cmd += compilerargs
    return doCommand(cmd,messages)

def doAssemble(config,messages):
    asm=shlex.split(config["assembler"])
    asmargs=shlex.split(config["assemblerargs"])
    cmd = asm  + asmargs
    return doCommand(cmd,messages)

def doLink(config,messages):
    link=shlex.split(config["linker"])
    linkerargs=shlex.split(config["linkerargs"])
    cmd = link + linkerargs
    return doCommand(cmd,messages)

def doCommand(cmd,messages):
    messages.put( Message(MessageType.CONSOLEOUTPUT, shlex.join(cmd)+"\n", style="cmd" ) )
    try:
        P = subprocess.Popen(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
        o,e = P.communicate()
        o = o.decode(errors="ignore").strip()
        if len(o) > 0:
            messages.put( Message(MessageType.CONSOLEOUTPUT, o+"\n", style="buildoutput") )
        if P.returncode != 0:
            messages.put( Message(
                MessageType.CONSOLEOUTPUT,
                f"*** ERROR: Command failed with code {P.returncode}\n",
                style="error")
            )
            return False
    except Exception as e:
        messages.put( Message(MessageType.CONSOLEOUTPUT,
            f"*** ERROR: Command execution error {e}\n",
            style="error"),
        )
        return False
    return True

if __name__ == "__main__":
    main()
