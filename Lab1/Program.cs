using System;
using System.Collections.Generic;

namespace lab
{
    public class MainClass
    {
        static void Main(string[] args)
        {
            string gfile = "grammar-lr.txt";
            string ifile = args[0];
            string ofile = args[1];
            string gramspec;
            using (var r = new System.IO.StreamReader(gfile))
            {
                gramspec = r.ReadToEnd();
            }
            gramspec = gramspec.Replace("\r", "");
            string input;
            using (var r = new System.IO.StreamReader(ifile))
            {
                input = r.ReadToEnd();
            }

            var G = new Grammar(gramspec);
            var tokenizer = new Tokenizer(G);
            tokenizer.setInput(input);
            var parser = new SLR1Parser(G);
            var D = new Dictionary<string, dynamic>();

            TreeNode root = parser.parse(tokenizer);
            void ICE()
            {
                throw new Exception();
            }
            void extractParameters(TreeNode n, List<TreeNode> p)
            {
                if (n.sym != "commaexpr")
                    throw new Exception();
                switch (n.children.Count)
                {
                    case 3:
                        //commaexpr :: commaexpr COMMA assignexpr
                        extractParameters(n.children[0], p);
                        p.Add(n.children[2]);
                        return;
                    case 1:
                        //commaexpr :: assignexpr
                        p.Add(n.children[0]);
                        return;
                    default:
                        ICE();
                        return;

                }
            }
            VarType getTypeFromNodes(TreeNode n, TreeNode array)
            {
                VarType tmp = null;
                switch (n["TYPE"].token.lexeme)
                {
                    case "int":
                        tmp = VarType.INT;
                        break;
                    case "float":
                        tmp = VarType.FLOAT;
                        break;
                    case "string":
                        tmp = VarType.STRING;
                        break;
                    case "void":
                        tmp = VarType.VOID;
                        break;
                    default:
                        ICE();
                        break;
                }
                List<int> L = array.attribs["arraySize"];
                if (L.Count > 0)
                {
                    for (int i = L.Count - 1; i >= 0; i--)
                    {
                        int size = L[i];
                        tmp = new ArrayVarType(tmp, size);
                    }
                }
                return tmp;
            }
            Walker.walkPre(root, (n) =>
            {
                if (!G.nonterminals.ContainsKey(n.sym))
                {
                    n.production = n.sym;   //terminal
                }
                else
                {
                    if (n.children.Count == 0)
                    {
                        n.production = n.sym + " :: lambda";
                    }
                    else
                    {
                        var tmp = new string[n.children.Count];
                        for (int i = 0; i < n.children.Count; ++i)
                        {
                            tmp[i] = n.children[i].sym;
                        }
                        n.production = n.sym + " :: " + string.Join(" ", tmp);
                    };
                }
            });
            var w = new Walker();
            var initwalk = new Walker();
            var symbolTable = new SymbolTable();
            initwalk.registerPost(
                new[]{
                "optionalinout :: IN",
                "optionalinout :: lambda"
                },
                (n) =>
                {
                    n.attribs["in"] = true;
                    n.attribs["out"] = false;
                });
            initwalk.registerPost(
                new[]{
                "optionalinout :: OUT"
                },
                (n) =>
                {
                    n.attribs["in"] = true;
                    n.attribs["out"] = false;
                });
            initwalk.registerPost(
                "optionalinout :: INOOUT",
                (n) => {
                    n.attribs["in"] = true;
                    n.attribs["out"] = true;
                }
                );
            initwalk.registerPost(
                "param :: optionalinout ID COLON type optionalarray",
                (n) => {
                    var type = getTypeFromNodes(n["type"], n["optionalarray"]);
                    var p = new Parameter(
                    type, n["ID"].token.lexeme,
                    n["optionalinout"].attribs["in"],
                    n["optionalinout"].attribs["out"]
                    );
                    n.attribs["param"] = p;
                }
                );
            initwalk.registerPost(
                "paramlist :: param",
                (n) => {
                    var L = new List<Parameter>();
                    L.Add(n["param"].attribs["param"]);
                    n.attribs["parameters"] = L;
                }
                );
            initwalk.registerPost(
                "paramlist :: param COMMA paramlist",
                (n) => {
                    var L = new List<Parameter>();
                    L.Add(n["param"].attribs["param"]);
                    L.AddRange(n["paramlist"].attribs["parameters"]);
                    n.attribs["parameters"] = L;
                }
                );
            initwalk.registerPost(
                "optionalparamlist :: lambda",
                (n) => {
                    n.attribs["parameters"] = new List<Parameter>();
                }
                );
            initwalk.registerPost(
                "optionalparamlist :: paramlist",
                (n) => {
                    n.attribs["parameters"] = n["paramlist"].attribs["parameters"];
                }
                );
            initwalk.registerPost(
                "optionalreturn :: lambda",
                (n) => {
                    n.attribs["returntype"] = VarType.VOID;
                }
                );
            initwalk.registerPost(
                "optionalreturn :: ARROW type optionalarray",
                (n) => {
                    n.attribs["returntype"] = getTypeFromNodes(n["type"], n["optionalarray"]);
                }
                );
            initwalk.registerPost(
                "funcdecl :: FUNC ID LP optionalparamlist RP optionalreturn braceblock",
                (n) => {
                    List<Parameter> parameters = n["optionalparamlist"].attribs["parameters"];
                    VarType returnType = n["optionalreturn"].attribs["returntype"];
                    var ftype = new FuncVarType(parameters, returnType);
                    var vinfo = new VarInfo(ftype, n["ID"].token);
                    symbolTable.declareVariable(n["ID"].token.lexeme, vinfo, n);
                }
                );
            initwalk.registerPost(
                "optionalarray :: lambda",
                (n) => {
                    var L = new List<int>();
                    n.attribs["arraySize"] = L;
                }
                );
            initwalk.registerPost(
                "optionalsize :: lambda",
                (n) => {
                    n.attribs["arraySize"] = -1;
                }
                );
            initwalk.registerPost(
                "optionalsize :: INUM",
                (n) => {
                    n.attribs["arraySize"] = Int32.Parse(n["INUM"].token.lexeme);
                }
                );
            initwalk.registerPost(
                "optionalarray :: LB optionalsize RB optionalarray",
                (n) => {
                    var L = new List<int>();
                    L.Add(n["optionalsize"].attribs["arraySize"]);
                    L.AddRange(n["optionalarray"].attribs["arraySize"]);
                    n.attribs["arraySize"] = L;
                }
                );
            initwalk.walk(root);
            w.registerPre(
                "braceblock :: LBR vardecls stmts RBR",
                (n) => {
                    symbolTable.addScope();
                }
                );
            w.registerPost(
                "braceblock :: LBR vardecls stmts RBR",
                (n) => {
                    symbolTable.removeScope();
                }
                );
            w.registerPost(
                "vardecl :: VAR ID COLON type optionalarray SEMI",
                (n) => {
                    VarType type = getTypeFromNodes(n["type"], n["optionalarray"]);
                    var info = new VarInfo(type, n["ID"].token);
                    symbolTable.declareVariable(n["ID"].token.lexeme, info, n);
                }
                );
            w.registerPost(
                "atom :: ID",
                (n) => {
                    n.type = symbolTable.lookup(n["ID"]).type;
                }
                );
            w.registerPost(
                "param::optionalinout ID COLON type optionalarray",
                (n)  => {
                    var type = getTypeFromNodes(n["type"], n["optionalarray"]);
                    var vinfo = new VarInfo(type, n["ID"].token);
                    symbolTable.declareVariable(n["ID"].token.lexeme, vinfo, n);
                }
                );
            w.registerPre(
                "funcdecl :: FUNC ID LP optionalparamlist RP optionalreturn braceblock",
                (n) => {
                    //List<Parameter> parameters = n["optionalparamlist"].attribs["parameters"];
                    symbolTable.addScope();
                }
                );
            w.registerPost(
                "funcdecl :: FUNC ID LP optionalparamlist RP optionalreturn braceblock",
                (n) => {
                    var funcname = n["ID"].token.lexeme;
                    var funcLoc = new GlobalLocation();
                    symbolTable.lookup(n["ID"]).location = funcLoc;
                    n.code = new ASM(
                        new Instr(funcLoc.label + ":", $"Beginning of {funcname}"),
                        n["braceblock"].code,
                        new Instr("", $"End of {funcname}")
                    );
                    symbolTable.removeScope();
                }
                );
            w.registerPost(
                "atom :: ID LP calllist RP",
                (n) => {
                    var vinfo = symbolTable.lookup(n["ID"]);
                    if (!(vinfo.type is FuncVarType))
                        TreeNode.error("Cannot call a non-function", n);
                    var vtype = (vinfo.type as FuncVarType);
                    var paramNodes = new List<TreeNode>();
                    var calllist = n["calllist"];
                    if (calllist.children.Count == 0)
                    {
                        //calllist :: lambda
                        //nothing to do
                    }
                    else if (calllist.children.Count == 1)
                    {
                        //calllist :: expr
                        //first child of expr is commaexpr
                        var expr = calllist["expr"];
                        var commaexpr = expr["commaexpr"];
                        extractParameters(commaexpr, paramNodes);
                    }
                    else
                    {
                        ICE();
                    }
                    //match types in calllist
                    if (paramNodes.Count != vtype.parameters.Count)
                        TreeNode.error("Bad parameter count for function call", n);
                    for (int i = 0; i < paramNodes.Count; ++i)
                    {
                        if (paramNodes[i].type != vtype.parameters[i].type)
                            TreeNode.error($"Type mismatch for parameter {i + 1}", paramNodes[i]);
                    }
                    n.type = vtype.returnType;
                }
                );
            w.registerPost(
                new[]
                {
                    "atom :: LP expr RP"
                },
                (n) => {
                    n.type = n.children[1].type;
                }
                );
            w.registerPost(
                new[]
                {
                    "expr :: commaexpr",
                    "commaexpr :: assignexpr",
                    "assignexpr :: logicalexpr",
                    "logicalexpr :: relexpr",
                    "relexpr:: shiftexpr",
                    "shiftexpr :: bitexpr",
                    "bitexpr :: addexpr",
                    "addexpr :: mulexpr",
                    "mulexpr :: powexpr",
                    "powexpr:: unaryexpr",
                    "unaryexpr :: ppexpr",
                    "ppexpr :: itemselection",
                    "itemselection :: atom",
                    "atom :: cast",
                    "atom :: constant"
                },
                (n) =>
                {
                    n.type = n.children[0].type;
                }
                );
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
                            n["addexpr"].code,
                            n["mulexpr"].code,
                            new Instr("pop rbx", "second arg for ADDOP"),
                            new Instr("pop rax", "first arg for ADDOP"),
                            new Instr($"{opcode} rax,rbx"),
                            new Instr("push rax")
                        );
                    }
                    if (n["addexpr"].type == VarType.FLOAT)
                    {
                        if (opcode == "add")
                        {
                            n.code = new ASM(
                                n["addexpr"].code,
                                n["mulexpr"].code,
                                new Instr("movq xmm1, [rsp]", "pop arg2"),
                                new Instr("add rsp,8"),
                                new Instr("movq xmm0, [rsp]", "pop arg1"),
                                new Instr("add rsp,8"),
                                new Instr("addsd xmm0,xmm1"),
                                new Instr("sub rsp,8", "push result"),
                                new Instr("movq [rsp], xmm0")
                            );
                        }
                        else
                        {
                            n.code = new ASM(
                                n["addexpr"].code,
                                n["mulexpr"].code,
                                new Instr("movq xmm1, [rsp]", "pop arg2"),
                                new Instr("add rsp,8"),
                                new Instr("movq xmm0, [rsp]", "pop arg1"),
                                new Instr("add rsp,8"),
                                new Instr("subsd xmm0,xmm1"),
                                new Instr("sub rsp,8", "push result"),
                                new Instr("movq [rsp], xmm0")
                            );
                        }
                    }
                    TreeNode.assertType(n.children[0], VarType.INT, VarType.FLOAT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "mulexpr :: mulexpr MULOP powexpr"
                },
                (TreeNode n) => {
                    string opcode;

                    switch (n["MULOP"].token.lexeme)
                    {
                        case "*": opcode = "imul"; break;
                        case "/": opcode = "idiv"; break;
                        case "%": opcode = "mod"; break;
                        default: ICE(); break;
                    }
                    if (n["mulexpr"].type == VarType.INT)
                    {
                        if (opcode == "imul")
                        {
                            n.code = new ASM(
                                n["mulexpr"].code,
                                n["powexpr"].code,
                                new Instr("pop rdx", "second arg for MULOP"),
                                new Instr("pop rax", "first arg for MULOP"),
                                new Instr($"{opcode} rax,rdx"),
                                new Instr("push rax")
                            );
                        }
                        if (opcode == "idiv")
                        {
                            n.code = new ASM(
                                n["mulexpr"].code,
                                n["powexpr"].code,
                                new Instr("xor rdx, rdx", "clear rdx for div"),
                                new Instr("pop rbx", "this is the denominator"),
                                new Instr("pop rax", "this is the numerator"),
                                new Instr($"{opcode} rbx"),
                                new Instr("push rax")
                            );
                        }
                        if (opcode == "mod")
                        {
                            n.code = new ASM(
                                n["mulexpr"].code,
                                n["powexpr"].code,
                                new Instr("xor rdx, rdx", "clear rdx for mod"),
                                new Instr("pop rbx", "this is the second arg"),
                                new Instr("pop rax", "this is the first arg"),
                                new Instr($"{"idiv"} rbx"),
                                new Instr("push rdx")
                            );
                        }
                    }
                    if (n["mulexpr"].type == VarType.FLOAT)
                    {
                        if (opcode == "imul")
                        {
                            n.code = new ASM(
                                n["mulexpr"].code,
                                n["powexpr"].code,
                                new Instr("movq xmm1, [rsp]", "pop arg2"),
                                new Instr("add rsp,8"),
                                new Instr("movq xmm0, [rsp]", "pop arg1"),
                                new Instr("add rsp,8"),
                                new Instr("mulsd xmm0,xmm1"),
                                new Instr("sub rsp,8", "push result"),
                                new Instr("movq [rsp], xmm0")
                            );
                        }
                        if (opcode == "idiv")
                        {
                            n.code = new ASM(
                                n["mulexpr"].code,
                                n["powexpr"].code,
                                new Instr("movq xmm1, [rsp]", "pop arg2"),
                                new Instr("add rsp,8"),
                                new Instr("movq xmm0, [rsp]", "pop arg1"),
                                new Instr("add rsp,8"),
                                new Instr("divsd xmm0,xmm1"),
                                new Instr("sub rsp,8", "push result"),
                                new Instr("movq [rsp], xmm0")
                            );
                        }
                        if (opcode == "mod")
                        {
                            TreeNode.error("Cannot mod with floats", n);
                        }
                    }
                    TreeNode.assertType(n.children[0], VarType.INT, VarType.FLOAT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "ppexpr :: itemselection PLUSPLUS"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.INT, VarType.FLOAT);
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "unaryexpr :: ADDOP unaryexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[1], VarType.INT, VarType.FLOAT);
                    string opcode;
                    switch (n["ADDOP"].token.lexeme)
                    {
                        case "+": opcode = "add"; break;
                        case "-": opcode = "neg"; break;
                        default: ICE(); break;
                    }
                    if (n["unaryexpr"].type == VarType.INT)
                    {
                        if (opcode == "neg")
                        {
                            n.code = new ASM(
                                n["unaryexpr"].code,
                                new Instr("pop rbx", "second arg for ADDOP"),
                                new Instr($"{opcode} rbx"),
                                new Instr("push rbx")
                            );
                        }
                    }
                    if (n["unaryexpr"].type == VarType.FLOAT)
                    {
                        if (opcode == "neg")
                        {
                            n.code = new ASM(
                                n["unaryexpr"].code,
                                new Instr("movq xmm1, [rsp]", "pop value to negate"),
                                new Instr("add rsp,8"),
                                new Instr("xorpd xmm0, xmm0", "set xmm0 to 0"),
                                new Instr("subsd xmm0,xmm1"),
                                new Instr("sub rsp,8", "push result"),
                                new Instr("movq [rsp], xmm0")
                            );
                        }
                    }
                    n.type = n.children[1].type;
                }
            );
            w.registerPost(
                new[]{
                    "logicalexpr :: logicalexpr LOGICOP relexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.INT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    string lbl1 = Label.make();
                    string lbl2 = Label.make();
                    if (n["LOGICOP"].token.lexeme == "or")
                    {
                        n.code = new ASM(
                                n["logicalexpr"].code,
                                new Instr("pop rax", "get operand for logicalexpr"),
                                new Instr("cmp rax,0"),
                                new Instr($"je {lbl1}", "if first operand zero, do second operand"),
                                new Instr("push qword 1", "result was true, push and stop"),
                                new Instr($"jmp {lbl2}", "go to end of logicalexpr"),
                                new Instr($"{lbl1}:", "start of second operand for logicalexpr"),
                                n["relexpr"].code,
                                new Instr("pop rax", "get second operand for logicalexpr"),
                                new Instr("cmp rax,0", "Set to zero or one"),
                                new Instr("setne al"),
                                new Instr("and rax,1", "mask upper bits"),
                                new Instr("push rax", "second op. determines OR's value"),
                                new Instr($"{lbl2}:", "end of or - expression")
                            );
                    }
                    if (n["LOGICOP"].token.lexeme == "and")
                    {
                        n.code = new ASM(
                                n["logicalexpr"].code,
                                new Instr("pop rax", "get operand for logicalexpr"),
                                new Instr("cmp rax,0"),
                                new Instr($"je {lbl1}", "if first operand zero, do second operand"),
                                n["relexpr"].code,
                                new Instr("pop rax", "get second operand for logicalexpr"),
                                new Instr("cmp rax,0", "Set to zero or one"),
                                new Instr($"je {lbl1}", "if first operand zero, do second operand"),
                                new Instr("mov rax, 1"),
                                new Instr($"{lbl1}:", "end of or - expression"),
                                new Instr("push rax")
                            );
                    }
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "shiftexpr :: shiftexpr SHIFTOP bitexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.INT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    string opcode;
                    switch (n["SHIFTOP"].token.lexeme)
                    {
                        case "<<": opcode = "shl"; break;
                        case ">>": opcode = "sar"; break;
                        case ">>>": opcode = "shr"; break;
                        default: ICE(); break;
                    }
                    n.code = new ASM(
                        n["shiftexpr"].code,
                        n["bitexpr"].code,
                        new Instr("pop rcx", "second arg for BITOP"),
                        new Instr("pop rax", "first arg for BITOP"),      
                        new Instr($"{opcode} rax,cl"),
                        new Instr("push rax")
                    );
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "bitexpr :: bitexpr BITOP addexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.INT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    string opcode;
                    switch (n["BITOP"].token.lexeme)
                    {
                        case "&": opcode = "and"; break;
                        case "|": opcode = "or"; break;
                        case "^": opcode = "xor"; break;
                        default: ICE(); break;
                    }
                    n.code = new ASM(
                        n["bitexpr"].code,
                        n["addexpr"].code,
                        new Instr("pop rbx", "second arg for BITOP"),
                        new Instr("pop rax", "first arg for BITOP"),
                        new Instr($"{opcode} rax,rbx"),
                        new Instr("push rax")
                    );  
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "unaryexpr :: BITNOT unaryexpr"
                },
                (TreeNode n) => {
                    string opcode;
                    switch (n["BITNOT"].token.lexeme)
                    {
                        case "~": opcode = "not"; break;
                        default: ICE(); break;
                    }
                    n.code = new ASM(
                        n["unaryexpr"].code,
                        new Instr("pop rbx", "arg for BITNOT"),
                        new Instr($"{opcode} rbx"),
                        new Instr("push rbx")
                    );
                    TreeNode.assertType(n.children[1], VarType.INT);
                    n.type = n.children[1].type;
                }
            );
            w.registerPost(
                new[]{
                    "unaryexpr :: NOTOP unaryexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[1], VarType.INT);
                    var X = Label.make();
                    var Y = Label.make();
                    n.code = new ASM(
                        n["unaryexpr"].code,
                        new Instr("pop rbx", "arg for NOTOP"),
                        new Instr("cmp rbx,0"),
                        new Instr($"je {X}", "skip if false"),
                        new Instr("push 0"),
                        new Instr($"jmp {Y}", "end statement"),
                        new Instr(X + ":", $"else: {TreeNode.getLocation(n)}"),
                        new Instr("push 1"),
                        new Instr(Y + ":", $"end of cond at {TreeNode.getLocation(n)}")
                    ); ;
                    n.type = n.children[1].type;
                }
            );
            w.registerPost(
                "itemselection :: itemselection LB expr RB",
                (n) => {
                    TreeNode.assertType(n.children[2], VarType.INT);
                    var t = n["itemselection"].type;
                    if (!(t is ArrayVarType))
                        TreeNode.error("Cannot use [] on non-array", n);
                    n.type = (t as ArrayVarType).baseType;
                }
                );
            w.registerPost(
                new[]{
                    "powexpr :: unaryexpr POWOP powexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.FLOAT);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "relexpr :: relexpr RELOP shiftexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.INT, VarType.FLOAT, VarType.STRING);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    if (n["relexpr"].type == VarType.INT)
                    {
                        string cc;
                        switch (n["RELOP"].token.lexeme)
                        {
                            case "==": cc = "e"; break;
                            case "!=": cc = "ne"; break;
                            case ">": cc = "g"; break;
                            case ">=": cc = "ge"; break;
                            case "<": cc = "l"; break;
                            case "<=": cc = "le"; break;
                            default: ICE(); break;
                        }
                        n.code = new ASM(
                            n["relexpr"].code,
                            n["shiftexpr"].code,
                            new Instr("xor rcx, rcx", "set rcx to 0"),
                            new Instr("pop rbx", "second arg for RELOP"),
                            new Instr("pop rax", "first arg for RELOP"),
                            new Instr("cmp rax,rbx"),
                            new Instr($"set{cc} cl"),
                            new Instr("push rcx")
                        );
                    }
                    if (n["relexpr"].type == VarType.FLOAT)
                    {
                        int cc;
                        switch (n["RELOP"].token.lexeme)
                        {
                            case "==": cc = 0; break;
                            case "!=": cc = 4; break;
                            case ">": cc = 6; break;
                            case ">=": cc = 5; break;
                            case "<": cc = 1; break;
                            case "<=": cc = 2; break;
                            default: ICE(); break;
                        }
                        n.code = new ASM(
                            n["relexpr"].code,
                            n["shiftexpr"].code,
                            new Instr("movq xmm1, [rsp]", "pop arg2"),
                            new Instr("add rsp,8"),
                            new Instr("movq xmm0, [rsp]", "pop arg1"),
                            new Instr("add rsp,8"),
                            new Instr($"cmpsd xmm0, xmm1, {cc}"),
                            new Instr("movq rax, xmm0"),
                            new Instr("and rax, 1"),
                            new Instr("push rax")
                        ); ;
                    }
                    n.type = VarType.INT;
                }
            );
            w.registerPost(
                new[]{
                    "assignexpr :: assignexpr EQ logicalexpr"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[0], VarType.INT, VarType.FLOAT, VarType.STRING);
                    TreeNode.assertEqualTypes(n.children[0], n.children[2]);
                    n.type = n.children[0].type;
                }
            );
            w.registerPost(
                new[]{
                    "constant :: INUM"
                },
                (TreeNode n) => {
                    n.type = VarType.INT;
                    int value = (int)Int64.Parse(n["INUM"].token.lexeme);
                    n.code = new ASM(
                        new Instr($"mov rax, {value}", $"Constant at {TreeNode.getLocation(n)}"),
                        new Instr("push rax")
                    );
                }
            );
            w.registerPost(
                new[]{
                    "constant :: FNUM"
                },
                (TreeNode n) => {
                    n.type = VarType.FLOAT;
                    var s = n["FNUM"].token.lexeme;
                    double value = Double.Parse(s);
                    string str = value.ToString("F");
                    n.code = new ASM(
                        new Instr($"mov rax, __float64__({str})", $"Constant at {TreeNode.getLocation(n)}"),
                        new Instr("push rax")
                    );
                }
            );
            w.registerPost(
                new[]{
                    "constant :: STRINGCONST"
                },
                (TreeNode n) => {
                    n.type = VarType.STRING;
                }
            );
            w.registerPost(
                new[]{
                    "loop :: WHILE LP expr RP braceblock",
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[2], VarType.INT);
                    n.type = null;
                    string loop = Label.make();
                    string endloop = Label.make();              
                    n.code = new ASM(
                        new Instr("", $"begin cond at {TreeNode.getLocation(n)}"),
                        new Instr(loop + ":", $"continue loop {TreeNode.getLocation(n["expr"])}"),
                        n["expr"].code,         //leaves result on stack
                        new Instr("pop rax", $"if-expr at {TreeNode.getLocation(n["expr"])}"),
                        new Instr("cmp rax,0"),
                        new Instr($"je {endloop}", "skip if false"),
                        n["braceblock"].code,
                        new Instr($"jmp {loop}", "go through loop again"),
                        new Instr(endloop + ":", $"end of cond at {TreeNode.getLocation(n)}")
                    );
                }
            );
            w.registerPost(
                new[]{
                    "switch :: SWITCH LP expr RP LBR cases RBR",
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[2], VarType.INT);
                    n.type = null;
                }
            );
            w.registerPost(
                "cond :: IF LP expr RP braceblock",
                (n) => {
                    TreeNode.assertType(n.children[2], VarType.INT);
                    n.type = null;
                    string endif = Label.make();
                    n.code = new ASM(
                        new Instr("", $"begin cond at {TreeNode.getLocation(n)}"),
                        n["expr"].code,         //leaves result on stack
                        new Instr("pop rax", $"if-expr at {TreeNode.getLocation(n["expr"])}"),
                        new Instr("cmp rax,0"),
                        new Instr($"je {endif}", "skip if false"),
                        n["braceblock"].code,
                        new Instr(endif + ":", $"end of cond at {TreeNode.getLocation(n)}")
                    );
                }
            );
            w.registerPost(
                "cond :: IF LP expr RP braceblock ELSE braceblock",
                (n) => {
                    TreeNode.assertType(n.children[2], VarType.INT);
                    n.type = null;
                    string elselabel = Label.make();
                    string endif = Label.make();
                    n.code = new ASM(
                        new Instr("", $"begin cond at {TreeNode.getLocation(n)}"),
                        n["expr"].code,         //leaves result on stack
                        new Instr("pop rax", $"if-expr at {TreeNode.getLocation(n["expr"])}"),
                        new Instr("cmp rax,0"),
                        new Instr($"je {elselabel}", "go to else statement if false"),
                        n.children[4].code,
                        new Instr($"jmp {endif}", "go to endif"),
                        new Instr(elselabel + ":", $"else cond at {TreeNode.getLocation(n["ELSE"])}"),
                        n.children[6].code,
                        new Instr(endif + ":", $"end of cond at {TreeNode.getLocation(n)}")
                    );
                }
            );
            w.registerPost(
                new[]{
                    "loop :: FOR LP expr SEMI expr SEMI expr RP braceblock",
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[4], VarType.INT);
                    n.type = null;
                }
            );
            w.registerPost(
                new[]{
                    "loop :: DO braceblock WHILE LP expr RP SEMI",
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[4], VarType.INT);
                    n.type = null;
                    string loop = Label.make();
                    string endloop = Label.make();
                    n.code = new ASM(
                        new Instr("", $"begin cond at {TreeNode.getLocation(n)}"),
                        new Instr(loop + ":", $"continue loop {TreeNode.getLocation(n["expr"])}"),
                        n["braceblock"].code,
                        n["expr"].code,         //leaves result on stack
                        new Instr("pop rax", $"if-expr at {TreeNode.getLocation(n["expr"])}"),
                        new Instr("cmp rax,0"),
                        new Instr($"je {endloop}", "skip if false"),
                        new Instr($"jmp {loop}", "go through loop again"),
                        new Instr(endloop + ":", $"end of cond at {TreeNode.getLocation(n)}")
                    );
                }
            );
            w.registerPost(
                new[]{
                    "loop :: REPEAT braceblock UNTIL LP expr RP SEMI",
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[4], VarType.INT);
                    n.type = null;
                    string loop = Label.make();
                    n.code = new ASM(
                        new Instr("", $"begin cond at {TreeNode.getLocation(n)}"),
                        new Instr(loop + ":", $"continue loop {TreeNode.getLocation(n["expr"])}"),
                        n["braceblock"].code,
                        n["expr"].code,         //leaves result on stack
                        new Instr("pop rax", $"if-expr at {TreeNode.getLocation(n["expr"])}"),
                        new Instr("cmp rax,0"),
                        new Instr($"je {loop}", "skip if true")
                    );
                }
            );
            w.registerPost(
                "return :: RETURN",
                (n) => {
                    n.code = new ASM(
                        new Instr("ret", $"return void at {TreeNode.getLocation(n)}")
                    );
                }
            );
            w.registerPost(
                "return :: RETURN expr",
                (n) => {
                    n.code = new ASM(
                        n["expr"].code,
                        new Instr("pop rax", $"return expr at {TreeNode.getLocation(n)}"),
                        new Instr("ret")
                    );
                }
            );
            w.registerPost(
                new[]{
                    "cast :: TYPE LP expr RP"
                },
                (TreeNode n) => {
                    TreeNode.assertType(n.children[2], VarType.INT, VarType.FLOAT, VarType.STRING);
                    if (n.children[0].token.lexeme == "int")
                    {
                        if (n["expr"].type == VarType.FLOAT)
                        {
                            n.code = new ASM(
                                n["expr"].code,
                                new Instr("movq xmm0, [rsp]", "pop arg1"),
                                new Instr("add rsp,8"),
                                new Instr("roundsd xmm0, xmm0, 3", "round float down"),
                                new Instr("cvtsd2si rax, xmm0","convert xmm0 to int and push to rax"),
                                new Instr("push rax")
                                );
                        }
                        n.type = VarType.INT;
                    }
                    if (n.children[0].token.lexeme == "float")
                    {
                        if (n["expr"].type == VarType.INT)
                        {
                            n.code = new ASM(
                                n["expr"].code,
                                new Instr("pop rax", "pop arg1"),
                                new Instr("cvtsi2sd xmm0, rax", "convert rax to float and push to xmm0"),
                                new Instr("sub rsp,8"),      
                                new Instr("movq [rsp], xmm0")
                                );
                        }
                        n.type = VarType.FLOAT;
                    }
                    if (n.children[0].token.lexeme == "string")
                        n.type = VarType.STRING;
                }
            );
            w.registerPost(
                "stmt :: expr SEMI",
                (n) => {
                    n.code = new ASM(
                        n["expr"].code,
                        new Instr("add rsp,8", "discard expr result")
                    );
                }
            );
            w.walk(root);
            if (!symbolTable.scopes[0].vars.ContainsKey("main"))
                TreeNode.error("No main() function", root);
            var mainInfo = symbolTable.scopes[0].vars["main"];
            if (!(mainInfo.type is FuncVarType))
                TreeNode.error("main() must be a function", root);
            var mainLoc = mainInfo.location as GlobalLocation;
            var boilerplate = new ASM(
                new Instr("bits 64", "generate 64 bit code"),
                new Instr("default rel", "relative addresses"),
                new Instr("section .text", "executable code follows"),
                new Instr("extern ExitProcess", "external function declaration"),
                new Instr("global _start", "make symbol visible everywhere"),
                new Instr("_start:", "begin _start() function"),
                new Instr($"call {mainLoc.label}", "call the real main function"),
                new Instr("mov rcx,rax", "ExitProcess expects return code in rcx"),
                new Instr("call ExitProcess", "ask OS to terminate this process")
            );

            //more to be added here later...
            var dataSection = new ASM(
                new Instr("section .data", "beginning of global data")
            );

            var finalCode = boilerplate + root.code + dataSection;
            using(var output = new System.IO.StreamWriter(ofile))
            {
                output.WriteLine(finalCode.ToString());
            }
        }
    }
}
