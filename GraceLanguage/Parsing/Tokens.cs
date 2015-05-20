using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using Grace.Unicode;

namespace Grace.Parsing
{
    /// <summary>A token of Grace source</summary>
    public abstract class Token
    {
        /// <summary>Module this token was found in</summary>
        public string module;

        /// <summary>Line this token was found at</summary>
        public int line;

        /// <summary>Column this token was found at</summary>
        public int column;

        /// <param name="module">Module this token was found in</param>
        /// <param name="line">Line this token was found at</param>
        /// <param name="column">Column this token was found at</param>
        public Token(string module, int line, int column)
        {
            this.module = module;
            this.line = line;
            this.column = column;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "<Token:" + module + ":" + line + ":" + column
                + "::" + describe() + ">";
        }

        /// <summary>
        /// Subclass-specific description of the value of this token.
        /// </summary>
        abstract protected string describe();

        /// <summary>
        /// Module this token was found in.
        /// </summary>
        public string Module
        {
            get
            {
                return module;
            }
        }

        /// <summary>
        /// Give a string description of a token class, suitable for
        /// use in an error message.
        /// </summary>
        /// <typeparam name="T">Class to describe</typeparam>
        public static string DescribeSubclass<T>() where T : Token
        {
            if (typeof(ArrowToken).Equals(typeof(T)))
                return "->";
            if (typeof(RParenToken).Equals(typeof(T)))
                return ")";
            return typeof(T).Name;
        }
    }

    class IdentifierToken : Token
    {
        private string _name;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        public IdentifierToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            _name = val;
        }

        protected override string describe()
        {
            return "Identifier:" + _name;
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
        private string _value;

        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }
        private string _raw;

        public string Raw
        {
            get { return _raw; }
            set { _raw = value; }
        }
        private bool _beginsInterpolation = false;

        public bool BeginsInterpolation
        {
            get { return _beginsInterpolation; }
            set { _beginsInterpolation = value; }
        }

        public StringToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            _value = val;
        }

        public StringToken(string module, int line, int column, string val,
                bool interp)
            : base(module, line, column)
        {
            _value = val;
            _beginsInterpolation = interp;
        }

        public StringToken(string module, int line, int column, string val,
                string raw)
            : base(module, line, column)
        {
            _value = val;
            this._raw = raw;
        }

        public StringToken(string module, int line, int column, string val,
                string raw,
                bool interp)
            : base(module, line, column)
        {
            _value = val;
            this._raw = raw;
            _beginsInterpolation = interp;
        }

        protected override string describe()
        {
            return "String:" + _value;
        }
    }

    class NumberToken : Token
    {
        private int _base;

        public int NumericBase
        {
            get { return _base; }
            set { _base = value; }
        }
        private string _digits;

        public string Digits
        {
            get { return _digits; }
            set { _digits = value; }
        }

        public NumberToken(string module, int line, int column, int b,
                string digits)
            : base(module, line, column)
        {
            _base = b;
            this._digits = digits;
        }

        protected override string describe()
        {
            string ret = "Number:";
            if (_base == 10)
                ret += _digits;
            else if (_base == 16)
                ret += "0x" + _digits;
            else
                ret += _base + "x" + _digits;
            return ret;
        }
    }

    class OperatorToken : Token
    {
        private string _name;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        private bool _spaceBefore;

        public bool SpaceBefore
        {
            get { return _spaceBefore; }
            set { _spaceBefore = value; }
        }
        private bool _spaceAfter;

        public bool SpaceAfter
        {
            get { return _spaceAfter; }
            set { _spaceAfter = value; }
        }

        public OperatorToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            _name = val;
        }

        /// <summary>Set whether spaces were found before and after
        /// this operator symbol</summary>
        public void SetSpacing(bool before, bool after)
        {
            _spaceBefore = before;
            _spaceAfter = after;
        }

        protected override string describe()
        {
            return "Operator:" + _name;
        }
    }

    class BracketToken : Token
    {
        private string _name;
        private string _other;

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public bool Opening { get; protected set; }
        public bool Closing { get; protected set; }
        public string Other {
            get {
                if (_other == null)
                {
                    _other = computeOther();
                }
                return _other;
            }
        }

        public BracketToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            _name = val;
        }

        private string computeOther()
        {
            var sb = new StringBuilder();
            int[] graphemeIndices = StringInfo.ParseCombiningCharacters(_name);
            for (int i = graphemeIndices.Length - 1; i >= 0; i--)
            {
                string c = StringInfo.GetNextTextElement(_name, i);
                if (UnicodeLookup.MirroredBrackets.ContainsKey(c))
                    sb.Append(UnicodeLookup.MirroredBrackets[c]);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        protected override string describe()
        {
            return "Bracket:" + _name;
        }
    }

    class OpenBracketToken : BracketToken
    {
        public OpenBracketToken(string module, int line, int column,
                string val)
            : base(module, line, column, val)
        {
            Opening = true;
        }

        protected override string describe()
        {
            return "OpenBracket:" + Name;
        }
    }

    class CloseBracketToken : BracketToken
    {
        public CloseBracketToken(string module, int line, int column,
                string val)
            : base(module, line, column, val)
        {
            Closing = true;
        }

        protected override string describe()
        {
            return "CloseBracket:" + Name;
        }
    }

    class CommentToken : Token
    {
        private string _value;

        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }

        public CommentToken(string module, int line, int column, string val)
            : base(module, line, column)
        {
            _value = val;
        }

        protected override string describe()
        {
            return "Comment:" + _value;
        }
    }

    class SpaceToken : Token
    {
        private int _size;

        public int Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public SpaceToken(string module, int line, int column, int size)
            : base(module, line, column)
        {
            this._size = size;
        }

        protected override string describe()
        {
            return "Space:" + _size;
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

    class OuterKeywordToken : KeywordToken
    {
        public OuterKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "OuterKeyword";
        }
    }

    class SelfKeywordToken : KeywordToken
    {
        public SelfKeywordToken(string module, int line, int column)
            : base(module, line, column)
        { }

        protected override string describe()
        {
            return "SelfKeyword";
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

        public override string ToString()
        {
            return "end of file";
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
