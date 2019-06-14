using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Grace.Utility;

namespace Grace.Parsing
{
    /// <summary>Tokeniser for Grace code</summary>
    class Lexer
    {
        private string code;
        private int index = 0;
        private int line = 1;
        private int column = 1;
        private int lineStart = -1;
        private bool allowShebang = true;
        public string moduleName;
        public Token current;
        public Token previous;

        /// <param name="module">Module of this code</param>
        /// <param name="code">Code of this module as a string</param>
        public Lexer(string module, string code)
        {
            this.code = code;
            this.moduleName = module;
            if (code[code.Length - 1] != '\n')
                this.code += "\n";
            NextToken();
        }

        /// <param name="code">Code of this module as a string</param>
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

        /// <summary>Behave as though the source code at this point
        /// were immediately after the opening quote of a string
        /// literal</summary>
        public void TreatAsString()
        {
            column = index - lineStart;
            current = lexStringRemainder();
            return;
        }

        /// <summary>Look at what the next token would be, without
        /// changing the state of the lexer</summary>
        public Token Peek()
        {
            int startIndex = index;
            Token startCurrent = current;
            int startLine = line;
            var startPrevious = previous;
            Token ret = NextToken();
            index = startIndex;
            line = startLine;
            current = startCurrent;
            previous = startPrevious;
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

        /// <summary>Get the next token from the stream and
        /// advance the lexer</summary>
        public Token NextToken()
        {
            previous = current;
            if (index >= code.Length)
            {
                current = new EndToken(moduleName, line, column);
                return current;
            }
            char c = code[index];
            column = index - lineStart;
            Token ret = null;
            UnicodeCategory cat = validateChar();
            string cStr = StringInfo.GetNextTextElement(code, index);
            if (isIdentifierStartCharacter(c, cat))
                ret = lexIdentifier();
            if (isOperatorCharacter(c, cat))
                ret = lexOperator();
            if (isNumberStartCharacter(c))
                ret = lexNumber();
            if (c == ' ')
            {
                skipSpaces();
                return NextToken();
            }
            if (c == '#' && allowShebang && column == 1)
            {
                // Eat the rest of the line, ignoring its
                // contents entirely.
                while (code[index] != '\n' && code[index] != '\u2028')
                {
                    index++;
                }
                line++;
                lineStart = index;
                advanceIndex();
                return NextToken();
            }
            else if (column == 1)
                allowShebang = false;
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
            if (ret == null && UnicodeLookup.OpenBrackets.Contains(cStr))
                ret = lexOpenBracket();
            if (ret == null && UnicodeLookup.CloseBrackets.Contains(cStr))
                ret = lexCloseBracket();
            //if (UnicodeLookup.CloseBrackets.Contains(cStr))
            //    ret = lexCloseBracket();
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
            if ("trait" == ident)
                return new TraitKeywordToken(moduleName, line, column);
            if ("inherit" == ident)
                return new InheritsKeywordToken(moduleName, line, column);
            if ("use" == ident)
                return new UsesKeywordToken(moduleName, line, column);
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
            if ("interface" == ident)
                return new InterfaceKeywordToken(moduleName, line, column);
            if ("is" == ident)
                return new IsKeywordToken(moduleName, line, column);
            if ("where" == ident)
                return new WhereKeywordToken(moduleName, line, column);
            if ("outer" == ident)
                return new OuterKeywordToken(moduleName, line, column);
            if ("self" == ident)
                return new SelfKeywordToken(moduleName, line, column);
            if ("alias" == ident)
                return new AliasKeywordToken(moduleName, line, column);
            if ("exclude" == ident)
                return new ExcludeKeywordToken(moduleName, line, column);
            return new IdentifierToken(moduleName, line, column,
                    ident.Normalize());
        }

        /// <summary>
        /// Check whether a given string is an identifier.
        /// </summary>
        /// <param name="s">String to check</param>
        public static bool IsIdentifier(string s)
        {
            var first = StringInfo.GetNextTextElement(s);
            var cat = UnicodeLookup.GetUnicodeCategory(s, 0);
            if (!isIdentifierStartCharacter(first[0], cat))
                return false;
            var en = StringInfo.GetTextElementEnumerator(s);
            while (en.MoveNext())
            {
                var el = (string)en.Current;
                cat = UnicodeLookup.GetUnicodeCategory(el, 0);
                if (!isIdentifierCharacter(el[0], cat))
                    return false;
            }
            return true;
        }

        private static bool isIdentifierStartCharacter(char c,
                UnicodeCategory cat)
        {
            return (cat == UnicodeCategory.LowercaseLetter
                    || cat == UnicodeCategory.UppercaseLetter
                    || cat == UnicodeCategory.TitlecaseLetter
                    || cat == UnicodeCategory.ModifierLetter
                    || cat == UnicodeCategory.OtherLetter
                    || c == '_'
                    || (!isOperatorCharacter(c, cat) &&
                        (cat == UnicodeCategory.OtherSymbol)
                        )
                    );
        }

        private static bool isIdentifierCharacter(char c, UnicodeCategory cat)
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
                    || (!isOperatorCharacter(c, cat) &&
                        (cat == UnicodeCategory.OtherSymbol)
                        )
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
            while (isOperatorCharacter(code[index], cat)
                    || cat == UnicodeCategory.NonSpacingMark
                    || cat == UnicodeCategory.SpacingCombiningMark
                    || cat == UnicodeCategory.EnclosingMark
                    )
            {
                advanceIndex();
                cat = validateChar();
            }
            string op = code.Substring(start, index - start);
            if (op.StartsWith("//"))
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
            if (":".Equals(op))
            {
                return new ColonToken(moduleName, line, column);
            }
            if (":=".Equals(op))
            {
                return new BindToken(moduleName, line, column);
            }
            if (index < code.Length && (code[index] == ' '
                        || code[index] == '\r' || code[index] == '\n'
                        || code[index] == '\u2028'))
                spaceAfter = true;
            if (op.StartsWith(">") && !spaceBefore)
            {
                // This is a closing generic followed by some other
                // operator, rather than a single operator, so we
                // need to "un-lex" some codepoints.
                index -= (op.Length - 1);
                return new RGenericToken(moduleName, line, column);
            }
            OperatorToken ret = new OperatorToken(moduleName, line, column,
                    op.Normalize());
            ret.SetSpacing(spaceBefore, spaceAfter);
            return ret;
        }

        private Token lexComment()
        {
            advanceIndex();
            advanceIndex();
            int start = index;
            while (code[index] != '\n' && code[index] != '\u2028')
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

        private static bool isOperatorCharacter(char c, UnicodeCategory cat)
        {
            return c != '"' && c != ',' && c != ';'
                &&
                (
                    // Standard ASCII keyboard operators
                    c == '+' || c == '-' || c == '*' || c == '/'
                    || c == '=' || c == '!' || c == '.' || c == '>'
                    || c == '<' || c == '@' || c == '$' || c == '?'
                    || c == '&' || c == '|' || c == '^' || c == '%'
                    || c == '#' || c == '~'
                    || c == ':' || c == '\\'
                    // Additional individual operator codepoints
                    // From Latin-1
                    || c == '¬' || c == '±' || c == '×' || c == '÷'
                    || c == '¡' || c == '¢' || c == '£' || c == '¤'
                    || c == '¥' || c == '§' || c == '¿'
                    // From General Punctuation
                    || c == '‽' || c == '⁂'
                    // Block: Mathematical Operators
                    || (c >= 0x2200 && c <= 0x22ff)
                    // Block: Supplemental Mathematical Operators
                    || (c >= 0x2a00 && c <= 0x2aff)
                    // Block: Miscellaneous Mathematical Symbols-A
                    || (c >= 0x27c0 && c <= 0x27ef)
                    // Block: Miscellaneous Mathematical Symbols-B
                    || (c >= 0x2980 && c <= 0x29ff)
                    // Block: Miscellaneous Symbols and Arrows
                    || (c >= 0x2b00 && c <= 0x2bff)
                    // Block: Arrows
                    || (c >= 0x2190 && c <= 0x21ff)
                    // Block: Supplemental Arrows-A
                    || (c >= 0x27f0 && c <= 0x27ff)
                    // Block: Supplemental Arrows-B
                    || (c >= 0x2900 && c <= 0x297f)
                    // Block: Supplemental Technical
                    || (c >= 0x2300 && c <= 0x23ff)
                    // Block: Currency Symbols
                    || (c >= 0x20a0 && c <= 0x20cf)
                    // Block: Geometric Shapes
                    || (c >= 0x25a0 && c <= 0x25ff)
                    );
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
            if (numbase == 1 || numbase > 36)
                reportError("L0012", new Dictionary<string, string> {
                        { "base", "" + numbase }
                    },
                    "Invalid base ${base}.");
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

        private void appendHexEscape(StringBuilder b, string hex)
        {
            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                if (!((c >= '0' && c <= '9')
                        || (c >= 'a' && c <= 'f')
                        || (c >= 'A' && c <= 'F')))
                {
                    var error = "" + c;
                    // We don't want to cut a surrogate pair in half,
                    // but also don't want to consume combining marks
                    // here as GetNextTextElement would.
                    if (Char.IsHighSurrogate(c))
                        error += hex[i + 1];
                    reportError("L0013",
                            new Dictionary<string, string>
                            {
                                { "length", "" + hex.Length },
                                { "u", hex.Length == 4 ? "u" : "U" },
                                { "error", "" + error }
                            },
                            "Invalid unicode escape");
                }
            }
            int cp = Convert.ToInt32(hex, 16);
            b.Append(Char.ConvertFromUtf32(cp));
        }

        private Token lexStringRemainder()
        {
            int start = index;
            StringBuilder b = new StringBuilder();
            bool escaped = false;
            while (index < code.Length && ('"' != code[index] || escaped))
            {
                validateChar();
                if (!escaped && code[index] == '\\')
                    escaped = true;
                else if (code[index] == '{' && !escaped)
                {
                    return new StringToken(moduleName, line, column, b.ToString(),
                            code.Substring(start, index - start),
                            true);
                }
                else if ((code[index] == '\n' || code[index] == '\r')
                        && index < code.Length - 1 )
                {
                    reportError("L0011", "String literal contains line break.");
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
                    else if (c == 'e')
                        b.Append('\x1b');
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
                        if (index + 4 > code.Length)
                        {
                            reportError("L0013",
                                    new Dictionary<string, string>
                                    {
                                        { "length", "4" },
                                        { "u", "u" },
                                        { "error", "end of file" }
                                    },
                                    "Invalid unicode escape");
                        }
                        var hex = code.Substring(index, 4);
                        appendHexEscape(b, hex);
                        advanceIndex();
                        advanceIndex();
                        advanceIndex();
                    }
                    else if (c == 'U')
                    {
                        // Six-character Unicode escape
                        advanceIndex();
                        if (index + 6 > code.Length)
                        {
                            reportError("L0013",
                                    new Dictionary<string, string>
                                    {
                                        { "length", "6" },
                                        { "u", "U" },
                                        { "error", "end of file" }

                                    },
                                    "Invalid unicode escape");
                        }
                        var hex = code.Substring(index, 6);
                        appendHexEscape(b, hex);
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

        private Token lexOpenBracket()
        {
            int start = index;
            advanceIndex();
            UnicodeCategory cat = validateChar();
            char c = code[index];
            string cStr = StringInfo.GetNextTextElement(code, index);
            while (UnicodeLookup.OpenBrackets.Contains(cStr)
                    || isOperatorCharacter(c, cat))
            {
                advanceIndex();
                cat = validateChar();
                c = code[index];
                cStr = StringInfo.GetNextTextElement(code, index);
                if (code.Substring(start, index - start) == "[[")
                    return new LGenericToken(moduleName, line, column);
            }
            string bracket = code.Substring(start, index - start);
            int[] indices = StringInfo.ParseCombiningCharacters(bracket);
            int lastIndex = indices[indices.Length - 1];
            string l = StringInfo.GetNextTextElement(bracket, lastIndex);
            if (!UnicodeLookup.OpenBrackets.Contains(l))
                reportError("L0009",
                        new Dictionary<string, string>
                            {
                                { "char", l }
                            },
                        "Invalid character at end of bracket sequence");
            if (l == "(" || l == "{")
                reportError("L0010",
                        new Dictionary<string, string>
                            {
                                { "char", l }
                            },
                        "Invalid character at end of bracket sequence");
            return new OpenBracketToken(moduleName, line, column, bracket);
        }

        private Token lexCloseBracket()
        {
            int start = index;
            advanceIndex();
            UnicodeCategory cat = validateChar();
            char c = code[index];
            string cStr = StringInfo.GetNextTextElement(code, index);
            while (UnicodeLookup.CloseBrackets.Contains(cStr)
                    || isOperatorCharacter(c, cat))
            {
                advanceIndex();
                cat = validateChar();
                c = code[index];
                cStr = StringInfo.GetNextTextElement(code, index);
                if (code.Substring(start, index - start) == "]]")
                    return new RGenericToken(moduleName, line, column);
            }
            string bracket = code.Substring(start, index - start);
            int[] indices = StringInfo.ParseCombiningCharacters(bracket);
            int lastIndex = indices[indices.Length - 1];
            string l = StringInfo.GetNextTextElement(bracket, lastIndex);
            // For ease, any ) characters at the end of a closing bracket
            // are removed from the token.
            int sub = 0;
            int blen = bracket.Length;
            int graphemeOffset = indices.Length - 1;
            while (l == ")" || l == ".")
            {
                sub++;
                graphemeOffset--;
                lastIndex = indices[graphemeOffset];
                l = StringInfo.GetNextTextElement(bracket, lastIndex);
            }
            if (sub > 0)
            {
                // Reset index to before the )s so that they will be
                // lexed as the following tokens.
                index -= sub;
                bracket = bracket.Substring(0, bracket.Length - sub);
            }
            if (!UnicodeLookup.CloseBrackets.Contains(l))
                reportError("L0009",
                        new Dictionary<string, string>
                            {
                                { "char", l }
                            },
                        "Invalid character at end of bracket sequence");
            return new CloseBracketToken(moduleName, line, column, bracket);
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
