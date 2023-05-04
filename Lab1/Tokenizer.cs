using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;

namespace lab
{

    public class TokenizeException : Exception
    {
        public TokenizeException(string reason) : base(reason) { }
    }

    public class Token
    {
        public string sym;
        public string lexeme;
        public int line;
        public int col;

        public Token(string sym, string lexeme, int line, int col)
        {
            this.sym = sym;
            this.lexeme = lexeme;
            this.line = line;
            this.col = col;
        }
        public override string ToString()
        {
            return $@"{{ ""sym"": {this.sym}, ""lexeme"": {this.lexeme}, ""line"": {this.line} ""col"": {this.col} }}";
        }
    }

    //This is a simplified Tokenizer that doesn't support implicit EOS
    public class Tokenizer
    {
        int idx;
        string input;
        public int line;
        public int column;

        Grammar grammar;

        public Tokenizer(Grammar g)
        {
            this.grammar = g;
        }

        public void setInput(string input)
        {
            this.idx = 0;
            this.input = input;
            this.line = 1;
            this.column = 0;
        }

        public string peek()
        {
            int oldidx = this.idx;
            int oldline = this.line;
            int oldcol = this.column;
            var tmp = this.next();
            this.idx = oldidx;
            this.line = oldline;
            this.column = oldcol;
            return tmp.sym;
        }
        public Token next()
        {

            //scan for match that starts at index idx
            //if more than one regex matches, prefer
            //the longest match. If two matches are the
            //same length, prefer the first one found
            string bestSym = null;
            string bestLexeme = null;

            //we've reached end of file
            if (this.idx >= this.input.Length)
            {
                return new Token("$", "", this.line, this.column);
            }

            //check each terminal; keep the one with the longest match length.
            //in case of a tie, prefer the first terminal found.
            foreach (var t in this.grammar.terminals)
            {
                var M = t.rex.Match(this.input, this.idx);
                if (M.Success)
                {
                    if (bestLexeme == null || bestLexeme.Length < M.Groups[0].Value.Length)
                    {
                        bestSym = t.sym;
                        bestLexeme = M.Groups[0].Value;
                    }
                }
            }

            //if no match found: Input was syntactically incorrect.
            if (bestSym == null)
            {
                int column = 0;
                for (int j = this.idx; j >= 0; j--)
                {
                    if (this.input[j] == '\n')
                        break;
                    column++;
                }
                throw new TokenizeException($"Cannot tokenize at line {this.line}, column {column}");
            }

            //If a zero-length regex was the best match,
            //we have an erroneous grammar: This will lead to
            //an infinite loop.
            if (bestLexeme.Length == 0)
            {
                throw new Exception($"Zero length match for symbol {bestSym}; this is an error in the grammar");
            }

            //save the line number so we can create a new token with the correct line number
            int tokenline = this.line;
            int tokencol = this.column;

            //advance line counter according to how many newlines are in
            //the matched lexeme
            for (int i = 0; i < bestLexeme.Length; ++i)
            {
                this.column++;
                if (bestLexeme[i] == '\n')
                {
                    this.line++;
                    this.column = 0;
                }
            }

            //move index up by the number of characters in the match
            this.idx += bestLexeme.Length;

            if (bestSym == "WHITESPACE" || bestSym == "COMMENT")
                return this.next();
            return new Token(bestSym, bestLexeme, tokenline, tokencol);
        }
    }

}   //namespace

