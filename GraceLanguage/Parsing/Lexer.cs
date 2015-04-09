using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Grace.Unicode;

namespace Grace.Parsing
{
    class Lexer
    {
        private string code;
        private int index = 0;
        private int line = 1;
        private int column = 1;
        private int lineStart = -1;
        public string moduleName;
        public Token current;
        public Lexer(string module, string code)
        {
            this.code = code;
            this.moduleName = module;
            if (code[code.Length - 1] != '\n')
                this.code += "\n";
            NextToken();
        }

        public Lexer(string code)
        {
            this.code = code;
            if (code[code.Length - 1] != '\n')
                this.code += "\n";
            NextToken();
        }

        private void reportError(string code, Dictionary<string, string> vars,
                string localDescription)
        {
            ErrorReporting.ReportStaticError(moduleName, line,
                    code, vars, localDescription);
        }

        private void reportError(string code, string localDescription)
        {
            ErrorReporting.ReportStaticError(moduleName, line,
                    code, localDescription);
        }


        public bool Done()
        {
            return index == code.Length;
        }

        public void TreatAsString()
        {
            column = index - lineStart;
            current = lexStringRemainder();
            return;
        }

        public Token Peek()
        {
            int startIndex = index;
            Token startCurrent = current;
            int startLine = line;
            Token ret = NextToken();
            index = startIndex;
            line = startLine;
            current = startCurrent;
            return ret;
        }

        private string formatCodepoint(int cp)
        {
            return "U+" + cp.ToString("X4");
        }

        private UnicodeCategory validateChar()
        {
            char c = code[index];
            if (c >= 0xD800 && c <= 0xDBFF)
            {
                // Leading surrogate
                char c2 = code[index + 1];
                if (c2 >= 0xDC00 && c2 <= 0xDFFF)
                {
                    // Trailing surrogate - ignore for now
                }
                else
                {
                    reportError("L0007", "Illegal lone surrogate");
                }
            }
            else if (c >= 0xDC00 && c <= 0xDFFF)
            {
                // Trailing surrogate
                reportError("L0007", "Illegal lone surrogate");
            }
            UnicodeCategory cat = UnicodeLookup.GetUnicodeCategory(code, index);
            if (c == '\t')
                reportError("L0001", "Tab characters are not permitted.");
            if (cat == UnicodeCategory.ParagraphSeparator
                    || cat == UnicodeCategory.SpaceSeparator)
            {
                if (c != ' ' && c != '\u2028')
                    reportError("L0002", new Dictionary<string, string>()
                            {
                                { "codepoint", formatCodepoint(Char.ConvertToUtf32(code, index)) },
                                { "name", UnicodeLookup.GetCodepointName(Char.ConvertToUtf32(code, index)) }
                            },
                            "Illegal whitespace.");
            }
            else if ((cat == UnicodeCategory.Control
                  || cat == UnicodeCategory.Format
                  || cat == UnicodeCategory.Surrogate
                  )
                  && c != '\n' && c != '\r')
            {
                reportError("L0003", new Dictionary<string, string>()
                            {
                                { "codepoint", formatCodepoint(Char.ConvertToUtf32(code, index)) },
                                { "name", UnicodeLookup.GetCodepointName(Char.ConvertToUtf32(code, index)) }
                            },
                            "Illegal control character. ");
            }
            return cat;
        }

        private void advanceIndex()
        {
            if (code[index] >= 0xD800 && code[index] <= 0xDBFF)
            {
                // Leading surrogate - skip the trailing one
                index++;
            }
            index++;
        }

        public Token NextToken()
        {
            if (index >= code.Length)
            {
                current = new EndToken(moduleName, line, column);
                return current;
            }
            char c = code[index];
            column = index - lineStart;
            Token ret = null;
            UnicodeCategory cat = validateChar();
            if (isIdentifierStartCharacter(c, cat))
                ret = lexIdentifier();
            if (isOperatorCharacter(c, cat))
                ret = lexOperator();
            if (c == ':')
                ret = lexColon();
            if (isNumberStartCharacter(c))
                ret = lexNumber();
            if (c == ' ')
            {
                skipSpaces();
                return NextToken();
            }
            if (c == '#')
                ret = lexComment();
            if (c == '"')
                ret = lexString();
            if (c == '(')
                ret = lexLParen();
            if (c == ')')
                ret = lexRParen();
            if (c == '{')
                ret = lexLBrace();
            if (c == '}')
                ret = lexRBrace();
            if (c == ',')
                ret = lexComma();
            if (c == ';')
                ret = lexSemicolon();
            if (c == '\n' || c == '\u2028' || c == '\r')
            {
                ret = new NewLineToken(moduleName, line, column);
                lineStart = index;
                line++;
                advanceIndex();
                if (c == '\r' && index < code.Length && code[index] == '\n')
                {
                    advanceIndex();
                    lineStart++;
                }
            }
            if (ret == null)
            {
                reportError("L0000", new Dictionary<string, string>()
                            {
                                { "codepoint", formatCodepoint(Char.ConvertToUtf32(code, index)) },
                                { "name", UnicodeLookup.GetCodepointName(Char.ConvertToUtf32(code, index)) }
                            },
                            "Character '" + c + "' may not appear here");
                ret = new UnknownToken(moduleName, line, index - 1);
            }
            current = ret;
            return ret;
        }

        private Token lexIdentifier()
        {
            int start = index;
            advanceIndex();
            UnicodeCategory cat = validateChar();
            while (isIdentifierCharacter(code[index], cat))
            {
                advanceIndex();
                cat = validateChar();
            }
            string ident = code.Substring(start, index - start);
            if ("object" == ident)
                return new ObjectKeywordToken(moduleName, line, column);
            if ("var" == ident)
                return new VarKeywordToken(moduleName, line, column);
            if ("def" == ident)
                return new DefKeywordToken(moduleName, line, column);
            if ("method" == ident)
                return new MethodKeywordToken(moduleName, line, column);
            if ("class" == ident)
                return new ClassKeywordToken(moduleName, line, column);
            if ("inherits" == ident)
                return new InheritsKeywordToken(moduleName, line, column);
            if ("import" == ident)
                return new ImportKeywordToken(moduleName, line, column);
            if ("as" == ident)
                return new AsToken(moduleName, line, column);
            if ("dialect" == ident)
                return new DialectKeywordToken(moduleName, line, column);
            if ("return" == ident)
                return new ReturnKeywordToken(moduleName, line, column);
            if ("type" == ident)
                return new TypeKeywordToken(moduleName, line, column);
            if ("is" == ident)
                return new IsKeywordToken(moduleName, line, column);
            if ("where" == ident)
                return new WhereKeywordToken(moduleName, line, column);
            return new IdentifierToken(moduleName, line, column, ident);
        }

        private bool isIdentifierStartCharacter(char c, UnicodeCategory cat)
        {
            return (cat == UnicodeCategory.LowercaseLetter
                    || cat == UnicodeCategory.UppercaseLetter
                    || cat == UnicodeCategory.TitlecaseLetter
                    || cat == UnicodeCategory.ModifierLetter
                    || cat == UnicodeCategory.OtherLetter
                    || c == '_');
        }

        private bool isIdentifierCharacter(char c, UnicodeCategory cat)
        {
            return (cat == UnicodeCategory.LowercaseLetter
                    || cat == UnicodeCategory.UppercaseLetter
                    || cat == UnicodeCategory.TitlecaseLetter
                    || cat == UnicodeCategory.ModifierLetter
                    || cat == UnicodeCategory.OtherLetter
                    || cat == UnicodeCategory.DecimalDigitNumber
                    || cat == UnicodeCategory.LetterNumber
                    || cat == UnicodeCategory.OtherNumber
                    || cat == UnicodeCategory.NonSpacingMark
                    || cat == UnicodeCategory.SpacingCombiningMark
                    || cat == UnicodeCategory.EnclosingMark
                    || c == '\'' || c == '_');
        }

        private bool isNumberStartCharacter(char c)
        {
            return (c >= '0' && c <= '9');
        }

        private bool isDigitInBase(char c, int numBase)
        {
            if (c >= '0' && c <= '9')
                return (c - '0') < numBase;
            if (c >= 'A' && c <= 'Z')
                return (c - 'A' + 10) < numBase;
            if (c >= 'a' && c <= 'z')
                return (c - 'a' + 10) < numBase;
            return false;
        }

        private Token lexOperator()
        {
            bool spaceBefore = false, spaceAfter = false;
            int start = index;
            if (start > 0 && (code[start - 1] == ' '))
                spaceBefore = true;
            advanceIndex();
            UnicodeCategory cat = validateChar();
            while (isOperatorCharacter(code[index], cat))
            {
                advanceIndex();
                cat = validateChar();
            }
            string op = code.Substring(start, index - start);
            if ("//".Equals(op))
            {
                index = start;
                return lexComment();
            }
            if (".".Equals(op))
            {
                return new DotToken(moduleName, line, column);
            }
            if ("=".Equals(op))
            {
                return new SingleEqualsToken(moduleName, line, column);
            }
            if ("->".Equals(op))
            {
                return new ArrowToken(moduleName, line, column);
            }
            if (index < code.Length && (code[index] == ' '
                        || code[index] == '\r' || code[index] == '\n'
                        || code[index] == '\u2028'))
                spaceAfter = true;
            if ("<" == op && !spaceBefore)
                return new LGenericToken(moduleName, line, column);
            if (">" == op && !spaceBefore)
                return new RGenericToken(moduleName, line, column);
            OperatorToken ret = new OperatorToken(moduleName, line, column, op);
            ret.SetSpacing(spaceBefore, spaceAfter);
            return ret;
        }

        private Token lexColon()
        {
            advanceIndex();
            if (index >= code.Length)
                return new ColonToken(moduleName, line, column);
            if (code[index] == '=')
            {
                advanceIndex();
                return new BindToken(moduleName, line, column);
            }
            return new ColonToken(moduleName, line, column);
        }

        private Token lexComment()
        {
            advanceIndex();
            advanceIndex();
            int start = index;
            while (code[index] != '\n')
            {
                validateChar();
                // If this is a leading surrogate, skip over
                // the trailing surrogate too.
                if (code[index] >= '\ud800' && code[index] <= '\udfff')
                    advanceIndex();
                advanceIndex();
            }
            string body = code.Substring(start, index - start);
            return new CommentToken(moduleName, line, column, body);
        }

        private bool isOperatorCharacter(char c, UnicodeCategory cat)
        {
            return (cat == UnicodeCategory.MathSymbol
                    || cat == UnicodeCategory.OtherSymbol
                    || c == '+' || c == '-' || c == '*' || c == '/'
                    || c == '=' || c == '!' || c == '.' || c == '>'
                    || c == '<' || c == '@' || c == '$' || c == '?'
                    || c == '&' || c == '|' || c == '^' || c == '%');
        }

        private void skipSpaces()
        {
            advanceIndex();
            while (' ' == code[index])
            {
                advanceIndex();
            }
            return;// new SpaceToken(moduleName, line, column, index - start);
        }

        private Token lexNumber()
        {
            char base1 = code[index];
            char base2 = '\0';
            char base3 = '\0';
            if (index + 1 < code.Length)
                base2 = code[index + 1];
            if (index + 2 < code.Length)
                base3 = code[index + 2];
            int numbase = 10;
            if (base2 == 'x')
            {
                numbase = base1 - '0';
                if (numbase == 0)
                    numbase = 16;
                index += 2;
            }
            else if (base2 >= '0' && base2 <= '9' && base3 == 'x')
            {
                numbase = (base1 - '0') * 10 + base2 - '0';
                index += 3;
            }
            int start = index;
            while (isDigitInBase(code[index], numbase))
                advanceIndex();
            if (code[index] == '.')
            {
                // Fractional number?
                if (isDigitInBase(code[index + 1], numbase))
                {
                    // Yes!
                    advanceIndex();
                    while (isDigitInBase(code[index], numbase))
                        advanceIndex();
                }

            }
            string digits = code.Substring(start, index - start);
            if (index < code.Length && isDigitInBase(code[index], 36))
                reportError("L0004", new Dictionary<string, string>()
                        {
                            { "base", "" + numbase },
                            { "digit", "" + code[index] }
                        },
                        "Not a valid digit in base " + numbase
                        + ": " + code[index] + ".");
            if (digits.Length == 0)
                reportError("L0005", "No valid digits in number.");
            return new NumberToken(moduleName, line, column, numbase, digits);
        }

        private Token lexString()
        {
            advanceIndex();
            return lexStringRemainder();
        }

        private Token lexStringRemainder()
        {
            int start = index;
            StringBuilder b = new StringBuilder();
            bool escaped = false;
            while (index < code.Length && ('"' != code[index] || escaped))
            {
                validateChar();
                if (code[index] == '\\')
                    escaped = true;
                else if (code[index] == '{' && !escaped)
                {
                    return new StringToken(moduleName, line, column, b.ToString(),
                            code.Substring(start, index - start),
                            true);
                }
                else if (escaped)
                {
                    char c = code[index];
                    if (c == 'n')
                        b.Append('\u2028');
                    else if (c == 't')
                        b.Append('\t');
                    else if (c == 'l')
                        b.Append('\u2028');
                    else if (c == '{')
                        b.Append('{');
                    else if (c == '}')
                        b.Append('}');
                    else if (c == '\\')
                        b.Append('\\');
                    else if (c == '"')
                        b.Append('"');
                    else if (c == 'u')
                    {
                        // Four-character BMP escape
                        advanceIndex();
                        int cp = Convert.ToInt32(code.Substring(index, 4),
                                16);
                        b.Append(Char.ConvertFromUtf32(cp));
                        advanceIndex();
                        advanceIndex();
                        advanceIndex();
                    }
                    else if (c == 'U')
                    {
                        // Six-character Unicode escape
                        advanceIndex();
                        int cp = Convert.ToInt32(code.Substring(index, 6),
                                16);
                        b.Append(Char.ConvertFromUtf32(cp));
                        advanceIndex();
                        advanceIndex();
                        advanceIndex();
                        advanceIndex();
                        advanceIndex();
                    }
                    else
                        reportError("L0008",
                                new Dictionary<string, string>()
                                    {
                                        {"escape", "" + c}
                                    },
                                "Unknown escape sequence");
                    escaped = false;
                }
                else
                {
                    b.Append(code[index]);
                    // If this is a leading surrogate, copy and skip over
                    // the trailing surrogate too (already validated).
                    if (code[index] >= '\ud800' && code[index] <= '\udfff')
                        b.Append(code[++index]);
                    escaped = false;
                }
                advanceIndex();
            }
            if (index == code.Length)
                reportError("L0006", "Unterminated string literal.");
            advanceIndex();
            return new StringToken(moduleName, line, column, b.ToString(),
                    code.Substring(start, index - start - 1));
        }

        private Token lexLParen()
        {
            advanceIndex();
            return new LParenToken(moduleName, line, column);
        }

        private Token lexRParen()
        {
            advanceIndex();
            return new RParenToken(moduleName, line, column);
        }

        private Token lexLBrace()
        {
            advanceIndex();
            return new LBraceToken(moduleName, line, column);
        }

        private Token lexRBrace()
        {
            advanceIndex();
            return new RBraceToken(moduleName, line, column);
        }

        private Token lexSemicolon()
        {
            advanceIndex();
            return new SemicolonToken(moduleName, line, column);
        }

        private Token lexComma()
        {
            advanceIndex();
            return new CommaToken(moduleName, line, column);
        }

    }

}
