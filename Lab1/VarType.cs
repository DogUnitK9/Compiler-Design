using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace lab
{
    public class VarInfo
    {
        public VarType type;
        public Token decl;
        public VarLocation location;
        public VarInfo(VarType t, Token d)
        {
            this.type = t;
            this.decl = d;
        }
    }
    public class VarType
    {
        public readonly string tag;
        protected VarType(string t)
        {
            this.tag = t;
        }
        public static readonly VarType INT = new IntVarType();
        public static readonly VarType FLOAT = new FloatVarType();
        public static readonly VarType STRING = new StringVarType();
        public static readonly VarType VOID = new VoidVarType();
        public override bool Equals(object? o)
        {
            if (object.ReferenceEquals(o, null))
                return false;
            VarType? v = (o as VarType);
            if (object.ReferenceEquals(v, null))
                return false;
            return this.tag == v.tag;
        }
        public static bool operator ==(VarType? v1, VarType? v2)
        {
            if (object.ReferenceEquals(v1, null))
                return object.ReferenceEquals(v2, null);
            return v1.Equals(v2);
        }
        public static bool operator !=(VarType? v1, VarType? v2)
        {
            return !(v1 == v2);
        }
        public override int GetHashCode()
        {
            return this.tag.GetHashCode();
        }
        public override string ToString()
        {
            return this.tag;
        }        
        public class IntVarType : VarType
        {
            public IntVarType() : base("int")
            {
            }
        }
        public class FloatVarType : VarType
        {
            public FloatVarType() : base("float")
            {
            }
        }
        public class StringVarType : VarType
        {
            public StringVarType() : base("string")
            {
            }
        }
        public class VoidVarType : VarType
        {
            public VoidVarType() : base("void")
            {
            }
        }
    }
    public class ArrayVarType : VarType
    {
        public ArrayVarType(VarType baseType, int size) : base($"{baseType}[{size}]")
        {
            this.size = size;
            this.baseType = baseType;
        }
        public int size;
        public VarType baseType;
    }
    public class FuncVarType : VarType
    {
        public List<Parameter> parameters;
        public VarType returnType;
        public FuncVarType(List<Parameter> parameters, VarType returnType)
        : base(paramListToString(parameters) + "->" + returnType)
        {
            this.parameters = parameters;
            this.returnType = returnType;
        }
        private static string paramListToString(List<Parameter> parameters)
        {
            var tmp = new List<string>();
            foreach (var p in parameters)
            {
                tmp.Add(p.type.ToString());
            }
            return "(" + string.Join(",", tmp) + ")";
        }
    }

}
