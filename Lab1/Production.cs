
using System.Collections.Generic;

namespace lab{


public class Production {
    public string lhs;
    public string[] rhs;
    public Production(string lhs, string[] rhs){
        this.lhs=lhs;
        this.rhs=rhs;
    }
    public override bool Equals(object o){
        if( o == null )
            return false;
        Production p2 = (o as Production);
        if( p2 == null )
            return false;
        if( this.lhs != p2.lhs )
            return false;
        if( this.rhs.Length != p2.rhs.Length )
            return false;
        for(int i=0;i<this.rhs.Length;++i){
            if( this.rhs[i] != p2.rhs[i] )
                return false;
        }
        return true;
    }
    public override int GetHashCode(){
        int h = this.lhs.GetHashCode();
        foreach(var s in this.rhs){
            h ^= s.GetHashCode();
        }
        return h;
    }
}

}