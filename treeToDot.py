#!/usr/bin/env python3

import json
import sys
import os.path
import tkinter
import tkinter.ttk as ttk
import tkinter.filedialog
import tkinter.messagebox

def main():
    root = tkinter.Tk()
    infiles=sys.argv[1:]

    if len(infiles) == 0:
        infile = tkinter.filedialog.askopenfilename(
            initialdir=os.path.dirname(__file__),
            filetypes=[ ("JSON files","*.json"), ("All files", "*") ]
        )
        if not infile:
            sys.exit(0)
        infiles=[infile]

    outfiles=[]

    for infile in infiles:
        idx=infile.rfind(".")
        if idx == -1:
            outfile = infile+".dot"
        else:
            outfile = infile[:idx]+".dot"
        with open(infile) as fp:
            data = fp.read()
        J = json.loads(data)
        with open(outfile,"w") as fp:
            walk(J,fp)
        outfiles.append(outfile)

    if len(outfiles) == 1:
        msg=f"Created file {outfiles[0]}"
    else:
        msg=f"Created files {' '.join(outfiles)}"

    if len(sys.argv) == 1:
        tkinter.messagebox.showinfo("Done",msg)
    else:
        print(msg)

    sys.exit(0)

def escape(s):
    s=s.replace('\\','\\\\')
    s=s.replace('"','\\"')
    return s

def walk(node,fp,title=None):
    print("graph g {",file=fp)
    if title != None:
        print(f'label="{escape(title)}";',file=fp)
    print('node [shape=box];',file=fp)
    walk1(node,fp,[0])
    walk2(node,fp)
    print("}",file=fp)

def walk1(node,fp,ctr):
    node["unique__"]=ctr[0]
    lbl = escape(node["sym"])
    if "token" in node and node["token"]:
        lbl += "\\n"
        lbl += escape(node["token"]["lexeme"])
    for attrib in ["type","sibtype"]:
        if attrib in node and node[attrib]:
            ty = node[attrib]
            if type(ty) == dict:
                printableType=[]
                for key in ty:
                    printableType.append(ty[key])
                printableType=" ".join(printableType)
            else:
                printableType = str(ty)
            qq=[]
            lbl += f"\\n[{attrib}={escape(printableType)}]"

    print( f'v{ctr[0]} [label="{lbl}"];', file=fp)
    ctr[0]+=1
    if "children" in node:
        for c in node["children"]:
            walk1(c,fp,ctr)

def walk2(node,fp):
    if node.get("children"):
        for c in node["children"]:
            lbl1 = node["unique__"]
            lbl2 = c["unique__"]
            print(f"v{lbl1} -- v{lbl2};", file=fp)
            walk2(c,fp)

if __name__=="__main__":
    main()
