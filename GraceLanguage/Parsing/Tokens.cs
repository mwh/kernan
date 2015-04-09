using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grace.Parsing
{
    internal abstract class Token
    {
        public string module;
        public int line;
        public int column;
        public Token(string module, int line, int column)
        {
            this.module = module;
            this.line = line;
            this.column = column;
        }

        public override string ToString()
        {
            return "<Token:" + module + ":" + line + ":" + column
                + "::" + describe() + ">";
        }

        abstract protected string describe();

        public string Module
        {
            get
            {
                return module;
            }
        }
    }

    class IdentifierToken : Token
    {
        public string name;
        public IdentifierToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            name = val;
        }

        protected override string describe()
        {
            return "Identifier:" + name;
        }
    }

    class AsToken : IdentifierToken
    {
        public AsToken(string module, int line, int column)
            : base(module, line, column, "as")
        {
        }

        protected override string describe()
        {
            return "As (contextual keyword 'as')";
        }
    }

    class StringToken : Token
    {
        public string value;
        public string raw;
        public bool beginsInterpolation = false;

        public StringToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            value = val;
        }

        public StringToken(string module, int line, int column, string val,
                bool interp)
            : base(module, line, column)
        {
            value = val;
            beginsInterpolation = interp;
        }

        public StringToken(string module, int line, int column, string val,
                string raw)
            : base(module, line, column)
        {
            value = val;
            this.raw = raw;
        }

        public StringToken(string module, int line, int column, string val,
                string raw,
                bool interp)
            : base(module, line, column)
        {
            value = val;
            this.raw = raw;
            beginsInterpolation = interp;
        }

        protected override string describe()
        {
            return "String:" + value;
        }
    }

    class NumberToken : Token
    {
        public int _base;
        public string digits;

        public NumberToken(string module, int line, int column, int b,
                string digits)
            : base(module, line, column)
        {
            _base = b;
            this.digits = digits;
        }

        protected override string describe()
        {
            string ret = "Number:";
            if (_base == 10)
                ret += digits;
            else if (_base == 16)
                ret += "0x" + digits;
            else
                ret += _base + "x" + digits;
            return ret;
        }
    }

    class OperatorToken : Token
    {
        public string name;
        public bool spaceBefore;
        public bool spaceAfter;
        public OperatorToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            name = val;
        }

        public void SetSpacing(bool before, bool after)
        {
            spaceBefore = before;
            spaceAfter = after;
        }

        protected override string describe()
        {
            return "Operator:" + name;
        }
    }

    class CommentToken : Token
    {
        public string value;
        public bool beginsInterpolation = false;

        public CommentToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            value = val;
        }

        protected override string describe()
        {
            return "Comment:" + value;
        }
    }

    class SpaceToken : Token
    {
        public int size;
        public SpaceToken(string module, int line, int column, int size)
            : base(module, line, column)
        {
            this.size = size;
        }

        protected override string describe()
        {
            return "Space:" + size;
        }
    }

    class LParenToken : Token
    {
        public LParenToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "LParen";
        }
    }

    class RParenToken : Token
    {
        public RParenToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "RParen";
        }
    }

    class LBraceToken : Token
    {
        public LBraceToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "LBrace";
        }
    }

    class RBraceToken : Token
    {
        public RBraceToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "RBrace";
        }
    }

    class LGenericToken : Token
    {
        public LGenericToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "LGeneric";
        }
    }

    class RGenericToken : Token
    {
        public RGenericToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "RGeneric";
        }
    }

    class KeywordToken : Token
    {
        public KeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Keyword";
        }
    }

    class ObjectKeywordToken : KeywordToken
    {
        public ObjectKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "ObjectKeyword";
        }
    }

    class VarKeywordToken : KeywordToken
    {
        public VarKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "VarKeyword";
        }
    }

    class DefKeywordToken : KeywordToken
    {
        public DefKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "DefKeyword";
        }
    }

    class MethodKeywordToken : KeywordToken
    {
        public MethodKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "MethodKeyword";
        }
    }

    class ClassKeywordToken : KeywordToken
    {
        public ClassKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "ClassKeyword";
        }
    }

    class InheritsKeywordToken : KeywordToken
    {
        public InheritsKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "InheritsKeyword";
        }
    }

    class DialectKeywordToken : KeywordToken
    {
        public DialectKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "DialectKeyword";
        }
    }

    class ImportKeywordToken : KeywordToken
    {
        public ImportKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "ImportKeyword";
        }
    }

    class TypeKeywordToken : KeywordToken
    {
        public TypeKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "TypeKeyword";
        }
    }

    class ReturnKeywordToken : KeywordToken
    {
        public ReturnKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "ReturnKeyword";
        }
    }

    class IsKeywordToken : KeywordToken
    {
        public IsKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "IsKeyword";
        }
    }

    class WhereKeywordToken : KeywordToken
    {
        public WhereKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "WhereKeyword";
        }
    }

    class SemicolonToken : Token
    {
        public SemicolonToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Semicolon";
        }
    }

    class CommaToken : Token
    {
        public CommaToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Comma";
        }
    }

    class DotToken : Token
    {
        public DotToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Dot";
        }
    }

    class BindToken : Token
    {
        public BindToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Bind";
        }
    }

    class ColonToken : Token
    {
        public ColonToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Colon";
        }
    }

    class ArrowToken : Token
    {
        public ArrowToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Arrow";
        }
    }

    class SingleEqualsToken : Token
    {
        public SingleEqualsToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "SingleEquals";
        }
    }

    class NewLineToken : Token
    {
        public NewLineToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "NewLine";
        }
    }

    class EndToken : Token
    {
        public EndToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "End";
        }
    }

    class UnknownToken : Token
    {
        public UnknownToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "Unknown";
        }
    }

}
