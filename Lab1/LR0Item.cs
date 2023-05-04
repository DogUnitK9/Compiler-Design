
using System.Collections.Generic;

namespace lab{

public class LR0Item{
    public Production production;
    public int dpos;
    public LR0Item(Production p, int dp){
        this.production=p;
        this.dpos=dp;
    }
   public override int GetHashCode(){
        return (production.GetHashCode()<<16) ^ dpos;
    }
    public override bool Equals( object o ){
        if(o==null || !(o is LR0Item))
            return false;
        var L = (o as LR0Item);
        return (this.production == L.production) &&
               (this.dpos == L.dpos);
    }
}

}