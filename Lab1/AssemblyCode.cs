using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class AssemblyCode
    {
    }
    public class Instr : AssemblyCode
    {
        public string op;
        public string comment;

        //instruction with no comment
        public Instr(string op) : this(op, "") { }

        //instruction with comment
        public Instr(string op, string comment)
        {
            this.op = op;
            this.comment = comment;
        }
        //auto-convert string to instruction
        public static implicit operator Instr(string op)
        {
            return new Instr(op);
        }
        public override string ToString()
        {
            string instr;

            if (this.op.Length == 0)
            {
                instr = "";
            }
            else
            {
                var tmp = this.op.Split(" ")[0];
                if (tmp.IndexOf(":") != -1)
                {
                    //this is a label; don't indent it
                    instr = this.op;
                }
                else
                {
                    //this is not a label; indent it
                    instr = "    " + this.op;
                }
            }

            if (this.comment.Length > 0)
            {
                while (instr.Length < 60)
                {
                    instr += " ";
                }
                instr += "; " + this.comment;
            }
            return instr;
        }
        public static ASM operator +(Instr i1, Instr i2)
        {
            var a = new ASM();
            a.instructions.Add(i1);
            a.instructions.Add(i2);
            return a;
        }
    }
    public class ASM : AssemblyCode
    {
        public List<Instr> instructions = new List<Instr>();
        public ASM(params AssemblyCode[] items)
        {
            foreach (var v in items)
            {
                if (v is Instr)
                {
                    this.instructions.Add(v as Instr);
                }
                else if (v is ASM)
                {
                    foreach (var i in (v as ASM).instructions)
                    {
                        this.instructions.Add(i);
                    }
                }
                else
                {
                    throw new Exception();
                }
            }
        }
        public static ASM operator +(ASM a, ASM b)
        {
            return new ASM(a, b);
        }

        public override string ToString()
        {
            var tmp = new List<string>();
            foreach (var ins in this.instructions)
            {
                tmp.Add(ins.ToString());
            }
            return string.Join("\n", tmp);
        }
    }
}
