using System.Text.RegularExpressions;

namespace lab{

public class Terminal {
    public string sym;
    public Regex rex;
    public Terminal(string sym, Regex rex){
        this.sym=sym;
        this.rex=rex;
    }
}

}