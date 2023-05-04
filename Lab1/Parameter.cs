using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class Parameter
    {
        public VarType type;
        public string formalName;
        public bool in_;
        public bool out_;
        public Parameter(VarType type, string formalName, bool in_, bool out_)
        {
            this.type = type;
            this.formalName = formalName;
            this.in_ = in_;
            this.out_ = out_;
        }
    }
}
