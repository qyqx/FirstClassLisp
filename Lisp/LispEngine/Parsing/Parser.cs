﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LispEngine.Datums;
using LispEngine.Lexing;

namespace LispEngine.Parsing
{
    public sealed class Parser : DatumHelpers
    {
        private readonly Scanner s;
        private IEnumerator<Token> tokens;
        private Token next;
        public Parser(Scanner s)
        {
            this.s = s;
            initTokens(s.Scan());
        }

        private void initTokens(IEnumerable<Token> tokenStream)
        {
            // Skip whitespace and comments
            tokens = tokenStream.Where(token => token.Type != TokenType.Space && token.Type != TokenType.Comment).GetEnumerator();            
        }

        private void readNext()
        {
            try
            {
                next = tokens.MoveNext() ? tokens.Current : null;                
            }
            catch (Exception)
            {
                // If an exception is encountered scanning,
                // re-initialize the enumerator (as soon as MoveNext
                // throws an exception the previous enumerator
                // appears to switch to 'EOF').
                // This is to support recovery from typos in the REPL.
                initTokens(s.Recover());
                throw;
            }
        }

        private ParseException fail(String fmt, params object[] args)
        {
            return s.fail(fmt, args);
        }

        private void expectNext(string what)
        {
            readNext();
            if(next == null)
                throw fail("Expected '{0}'", what);
        }

        private Datum symbol()
        {
            if (next.Type == TokenType.Symbol)
                return symbol(next.Contents);
            // read ".Equals" as "(dot Equals)" so that it can be macro
            // expanded if desired.
            if (next.Type == TokenType.DotSymbol)
                return compound(symbol("dot"), nil, symbol(next.Contents.Substring(1)));
            return null;
        }

        private Datum readCdr()
        {
            expectNext(")");
            var cdr = expression();
            expectNext(")");
            if (next.Type != TokenType.Close)
                throw fail("more than one item found after dot (.)");
            return cdr;
        }

        private Datum vectorExpr()
        {
            if (next.Type != TokenType.VectorOpen)
                return null;
            readNext();
            var elements = new List<Datum>();
            while (next.Type != TokenType.Close)
            {
                elements.Add(expression());
                expectNext(")");
            }
            return vector(elements.ToArray());
        }

        private Datum compound()
        {
            if (next.Type != TokenType.Open)
                return null;
            readNext();
            var elements = new List<Datum>();
            var cdr = nil;
            while(next.Type != TokenType.Close)
            {
                if (elements.Count > 0 && next.Type == TokenType.Dot)
                {
                    cdr = readCdr();
                    break;
                }
                elements.Add(expression());
                expectNext(")");
            }
            elements.Reverse();
            var result = elements.Aggregate(cdr, (current, d) => cons(d, current));
            var resultPair = result as Pair;
            if(resultPair != null)
                resultPair.Location = string.Format("{0}:{1}", s.Filename, s.LineNumber);
            return result;
        }

        // Remove the '"' delimiters surrounding the token that came
        // back from the lexer. Also 'unescape' any backslashes.
        private static string unescape(string s)
        {
            // Remove surrounding quotes
            s = s.Substring(1, s.Length - 2);
            // Regex.Unescape solves the problem of converting \n, \t etc
            // for us.
            return System.Text.RegularExpressions.Regex.Unescape(s);
        }

        private Datum atom()
        {
            if (next.Type == TokenType.Integer)
                return atom(int.Parse(next.Contents));
            if(next.Type == TokenType.Boolean)
                return atom(next.Contents.ToLower().Equals("#t"));
            if (next.Type == TokenType.String)
                return atom(unescape(next.Contents));
            return null;
        }


        private Datum quotedExpression()
        {
            if (next.Type != TokenType.Quote)
                return null;
            var symbol = isQuote(next.Contents);
            if(symbol != null)
            {
                var expression = parse();
                return cons(symbol, compound(expression));
            }
            return null;
        }

        // Based on the token that was just read, turn it into an expression
        private Datum expression()
        {
            Datum d;
            if ((d = quotedExpression()) != null)
                return d;
            if ((d = symbol()) != null)
                return d;
            if ((d = atom()) != null)
                return d;
            if ((d = vectorExpr()) != null)
                return d;
            if ((d = compound()) != null)
                return d;
            throw fail("Unexpected token: {0}", next);
        }

        public Datum parse()
        {
            readNext();
            return Eof ? null : expression();
        }

        private bool Eof
        {
            get
            {
                return next == null;
            }
        }
    }
}
