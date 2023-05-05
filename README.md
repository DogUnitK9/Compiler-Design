# Compiler-Design
The idea behind this is to take human readable code and be able to convert it into executable code. This involves making your own programming language and having it execute code based on what you specify. A prime example would be the Chef programming language made by David Morgan-Mar which simulates a baking recipe to output functional code.

But a compilers works as a multistage process where it scans a text, puts the text into a compiler where it can generate the assembly code, then it sends the assembly code into an object file where the linker file will combine everything into one executable. Example Image of process from https://www.geeksforgeeks.org/introduction-of-compiler-design/

![compilerP](https://user-images.githubusercontent.com/59978662/236352445-3b728568-fcbb-4c6d-b11a-25050cc9cdf1.jpg)


The compiler here is a very simple one, where we were writing our own version of a simple programming language adapting a style that was taking aspects that the class enjoyed about certain languages. It all starts by reading the grammar file that was created which contains a context free grammar that the program will parse through and seperate the terminals and nonterminals where they can be added to a list of tokens for the program to use.
```
public Grammar(string data)
        {
            //handle line continuation: Any line that begins with
            //at least four spaces is combined with the previous line
            data = data.Replace("\n    ", " ");

            bool foundWhitespace = false;

            //process each line one at a time
            foreach (var line in data.Split('\n'))
            {
                //ignore empty line
                if (line.Length == 0)
                    continue;
                //ignore comment line
                if (line.StartsWith("#"))
                    continue;
                //split line into two parts at the double colon
                int i = line.IndexOf("::");
                if (i == -1)
                    throw new Exception("Line is missing ::  ->  " + line);
                //left and right hand sides
                string lhs = line.Substring(0, i).Trim();
                string rhs = line.Substring(i + 2).Trim();
                //...
                //check to see that the lhs is not the start terminal or S
                //...                
                else
                {
                    this.terminals.Add(new Terminal(lhs, new Regex("\\G(" + rhs + ")")));
                }
```
After that is all tokenized, it is parsed into a TreeNode function where it will use everything later to walk through the grammar file and assert the types and parameters that the input files provide.
Finally we enter a walk into the TreeNode where we will be creating the assembly code from the provided grammar
Where something from the CFG such as: addexpr :: addexpr ADDOP mulexpr,
will be turned into:
```
 w.registerPost(
                new[]{
                    "addexpr :: addexpr ADDOP mulexpr"
                },
                (TreeNode n) => {
                    string opcode;
                    switch (n["ADDOP"].token.lexeme)
                    {
                        case "+": opcode = "add"; break;
                        case "-": opcode = "sub"; break;
                        default: ICE(); break;
                    }
                    if (n["addexpr"].type == VarType.INT)
                    {
                        n.code = new ASM(
                            //...
                            //generated assembly code
                            //...
                        );
                    }
                    if (n["addexpr"].type == VarType.FLOAT)
                    {
                        if (opcode == "add")
                        {
                            n.code = new ASM(
                                //...
                                //generated assembly code
                                //...
                            );
                        }
                        else
                        {
                            n.code = new ASM(
                                //...
                                //generated assembly code
                                //...
                            );
                        }
                    }
                    TreeNode.assertType(n.children[0], VarType.INT, VarType.FLOAT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    n.type = n.children[0].type;
                }
            );
```
so this addexpr would be taken from a Addtion Expresion such as 4 + 7.
The process for 4 would be as follows:
addexpr -> mulexpr 
mulexpr -> powexpr
powexpr -> unaryexpr
unaryexpr -> ppexpr
ppexpr -> itemselection
itemselection -> constant
constant -> INUM
and INUM would contain numbers 0-9
To actually run the assembly language generated, it will be outputed to a .asm file that will contain specific instructions to run the language provided.
So for a simple function such as
return 10 + 20, the assembly language would look like:
```
    mov rax, 10                                
    push rax
    mov rax, 20                                     
    push rax
    pop rbx                                           
    pop rax                                        
    add rax,rbx
    push rax
    pop rax                                              
    ret
```
rax and rbx was registers or temporary storage locations since the CPU often can’t work with values in RAM, and each one is 64 bits wide. There also exists xmm0-15 for floats since these registers only work with ints.

After the assembly language has been generated, it will be put into the test harness to run, and the test harness was a program provided by the instructor to be able to run our program multiple times without having to change the args constantly in the main program to check our provided tests.
![image](https://user-images.githubusercontent.com/59978662/236357983-cf871b5f-b2d2-4307-8e48-54bf2bd6d89f.png)
The top left box is what output you want your source code to be, the bottom left is where you can provide the code to be run. The right boxes are used for debugged where the top right shows what assembly code was produced from the provided function and the bottom right is a console output.
The code here is far from perfect, very wet in some areas and far from handling all types of coding aspects such as globals, locals, or complex functions. It also has not been optimized which means it is not working up to possible speeds it can achieve, but this is something that I may work on later.
