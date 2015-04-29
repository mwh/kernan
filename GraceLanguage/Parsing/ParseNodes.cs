using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grace.Parsing
{
    /// <summary>A concrete syntax node</summary>
    public abstract class ParseNode
    {
        private int _line;

        /// <summary>Line number this node began on</summary>
        public int Line
        {
            get { return _line; }
            set { _line = value; }
        }

        private int _column;

        /// <summary>Column number this node began at</summary>
        public int Column
        {
            get { return _column; }
            set { _column = value; }
        }

        private ParseNode _comment;

        /// <summary>Comment on this node, if any</summary>
        public ParseNode Comment
        {
            get { return _comment; }
            set { _comment = value; }
        }

        internal Token Token { get; set; }

        /// <param name="tok">Token that gave rise to this node</param>
        internal ParseNode(Token tok)
        {
            this._line = tok.line;
            this._column = tok.column;
            Token = tok;
        }

        /// <param name="basis">ParseNode that gave rise to this node</param>
        internal ParseNode(ParseNode basis)
        {
            Token = basis.Token;
            this._line = basis._line;
            this._column = basis._column;
        }

        /// <summary>Write a human-readable description of this node
        /// and its children to a given sink</summary>
        /// <param name="tw">Sink to write output into</param>
        /// <param name="prefix">Prefix string to print before each line</param>
        public abstract void DebugPrint(System.IO.TextWriter tw, string prefix);

        /// <summary>Write out this node's comment to a stream, if any</summary>
        /// <param name="tw">Sink to write output into</param>
        /// <param name="prefix">Prefix string to print before each line</param>
        public void writeComment(System.IO.TextWriter tw, string prefix)
        {
            if (this._comment != null)
            {
                tw.WriteLine(prefix + "  Comment:");
                this._comment.DebugPrint(tw, prefix + "    ");
            }
        }

        /// <summary>Double-dispatch visitor for parse nodes</summary>
        /// <param name="visitor">Visitor to double-dispatch to</param>
        /// <typeparam name="T">Return type of visitor</typeparam>
        public virtual T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    /// <summary>Parse node for an Object</summary>
    public class ObjectParseNode : ParseNode
    {
        private List<ParseNode> _body;

        /// <summary>Body of the object</summary>
        public List<ParseNode> Body
        {
            get { return _body; }
            set { _body = value; }
        }

        internal ObjectParseNode(Token tok)
            : base(tok)
        {
            _body = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Object:");
            foreach (ParseNode n in _body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }
        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Combined ordinary and generic parameters of
    /// a method name part</summary>
    public struct PartParameters
    {
        private List<ParseNode> _generics;

        /// <summary>Generic parameters</summary>
        public List<ParseNode> Generics
        {
            get { return _generics; }
            set { _generics = value; }
        }

        private List<ParseNode> _ordinary;

        /// <summary>Ordinary parameters</summary>
        public List<ParseNode> Ordinary
        {
            get { return _ordinary; }
            set { _ordinary = value; }
        }

        /// <param name="g">Generic parameters</param>
        /// <param name="o">Ordinary parameters</param>
        public PartParameters(List<ParseNode> g, List<ParseNode> o)
        {
            _generics = g;
            _ordinary = o;
        }
    }

    /// <summary>Shared interface of classes with the behaviour of
    /// method headers</summary>
    public interface MethodHeader
    {
        /// <summary>Add a part to this method header</summary>
        /// <param name="id">Identifier naming the part to add</param>
        PartParameters AddPart(ParseNode id);

        /// <summary>Return type of this method</summary>
        ParseNode ReturnType { get; set; }

        /// <summary>Annotations of this method</summary>
        AnnotationsParseNode Annotations { get; set; }
    }

    /// <summary>Parse node for a method declaration</summary>
    public class MethodDeclarationParseNode : ParseNode, MethodHeader
    {
        private List<ParseNode> _nameParts;

        /// <summary>Parts of this method</summary>
        public List<ParseNode> NameParts
        {
            get { return _nameParts; }
            set { _nameParts = value; }
        }

        private List<List<ParseNode>> _parameters;

        /// <summary>Parameter lists of each part</summary>
        public List<List<ParseNode>> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }

        private List<List<ParseNode>> _generics;

        /// <summary>Generic parameter lists of each part</summary>
        public List<List<ParseNode>> Generics
        {
            get { return _generics; }
            set { _generics = value; }
        }

        private List<ParseNode> _body;

        /// <summary>Body of this method</summary>
        public List<ParseNode> Body
        {
            get { return _body; }
            set { _body = value; }
        }

        /// <inheritdoc/>
        public ParseNode ReturnType { get; set; }

        /// <inheritdoc/>
        public AnnotationsParseNode Annotations { get; set; }

        internal MethodDeclarationParseNode(Token tok)
            : base(tok)
        {
            _nameParts = new List<ParseNode>();
            _parameters = new List<List<ParseNode>>();
            _generics = new List<List<ParseNode>>();
            _body = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public PartParameters AddPart(ParseNode id)
        {
            _nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            _parameters.Add(ordinaries);
            _generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in _nameParts)
            {
                name += (n as IdentifierParseNode).Name + " ";
            }
            tw.WriteLine(prefix + "MethodDeclaration: " + name);
            if (ReturnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                ReturnType.DebugPrint(tw, prefix + "    ");
            }
            if (Annotations != null)
            {
                tw.WriteLine(prefix + "  Annotations:");
                Annotations.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < _nameParts.Count; i++)
            {
                ParseNode partName = _nameParts[i];
                List<ParseNode> ps = _parameters[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (ParseNode arg in ps)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (ParseNode n in _body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }
        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a class declaration</summary>
    public class ClassDeclarationParseNode : ParseNode, MethodHeader
    {
        private ParseNode _baseName;

        /// <summary>Name of this class</summary>
        public ParseNode BaseName
        {
            get { return _baseName; }
            set { _baseName = value; }
        }

        private List<ParseNode> _nameParts;

        /// <summary>Parts of the constructor of this class</summary>
        public List<ParseNode> NameParts
        {
            get { return _nameParts; }
            set { _nameParts = value; }
        }

        private List<List<ParseNode>> _parameters;

        /// <summary>Parameter lists of each part</summary>
        public List<List<ParseNode>> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }

        private List<List<ParseNode>> _generics;

        /// <summary>Generic parameter lists of each part</summary>
        public List<List<ParseNode>> Generics
        {
            get { return _generics; }
            set { _generics = value; }
        }

        private List<ParseNode> _body;

        /// <summary>Body of this class</summary>
        public List<ParseNode> Body
        {
            get { return _body; }
            set { _body = value; }
        }

        /// <inheritdoc/>
        public ParseNode ReturnType { get; set; }

        /// <inheritdoc/>
        public AnnotationsParseNode Annotations { get; set; }

        internal ClassDeclarationParseNode(Token tok, ParseNode baseName)
            : base(tok)
        {
            this._baseName = baseName;
            _nameParts = new List<ParseNode>();
            _parameters = new List<List<ParseNode>>();
            _generics = new List<List<ParseNode>>();
            _body = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public PartParameters AddPart(ParseNode id)
        {
            _nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            _parameters.Add(ordinaries);
            _generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            IdentifierParseNode b = _baseName as IdentifierParseNode;
            string name = b.Name + ".";
            foreach (ParseNode n in _nameParts)
            {
                name += (n as IdentifierParseNode).Name + " ";
            }
            tw.WriteLine(prefix + "ClassDeclaration: " + name);
            if (ReturnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                ReturnType.DebugPrint(tw, prefix + "    ");
            }
            if (Annotations != null)
            {
                tw.WriteLine(prefix + "  Anontations:");
                Annotations.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < _nameParts.Count; i++)
            {
                ParseNode partName = _nameParts[i];
                List<ParseNode> ps = _parameters[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (ParseNode arg in ps)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (ParseNode n in _body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }
        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a type statement</summary>
    public class TypeStatementParseNode : ParseNode
    {
        private ParseNode _baseName;

        /// <summary>Name of this type</summary>
        public ParseNode BaseName
        {
            get { return _baseName; }
            set { _baseName = value; }
        }

        private ParseNode _body;

        /// <summary>Value of this type declaration</summary>
        public ParseNode Body
        {
            get { return _body; }
            set { _body = value; }
        }

        /// <summary>Generic parameters of this type</summary>
        public List<ParseNode> genericParameters;

        internal TypeStatementParseNode(Token tok, ParseNode baseName,
                ParseNode body, List<ParseNode> generics)
            : base(tok)
        {
            this._baseName = baseName;
            this._body = body;
            this.genericParameters = generics;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "TypeStatement:");
            tw.WriteLine(prefix + "  Name:");
            _baseName.DebugPrint(tw, prefix + "    ");
            if (genericParameters.Count > 0)
            {
                tw.WriteLine(prefix + "  Generic parameters:");
                foreach (ParseNode n in genericParameters)
                    n.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Body:");
            _body.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a type</summary>
    public class TypeParseNode : ParseNode
    {
        private List<ParseNode> _body;

        /// <summary>Body of this type</summary>
        public List<ParseNode> Body
        {
            get { return _body; }
            set { _body = value; }
        }

        /// <summary>Name of this type for debugging</summary>
        public string Name { get; set; }

        internal TypeParseNode(Token tok, List<ParseNode> body)
            : base(tok)
        {
            this._body = body;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Type:");
            foreach (ParseNode n in _body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    /// <summary>Parse node for a type method</summary>
    public class TypeMethodParseNode : ParseNode, MethodHeader
    {
        private List<ParseNode> _nameParts;

        /// <summary>Parts of the method</summary>
        public List<ParseNode> NameParts
        {
            get { return _nameParts; }
            set { _nameParts = value; }
        }

        private List<List<ParseNode>> _parameters;

        /// <summary>Parameter lists of each part</summary>
        public List<List<ParseNode>> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }

        private List<List<ParseNode>> _generics;

        /// <summary>Generic parameter lists of each part</summary>
        public List<List<ParseNode>> Generics
        {
            get { return _generics; }
            set { _generics = value; }
        }

        /// <inheritdoc/>
        public ParseNode ReturnType { get; set; }

        /// <inheritdoc/>
        public AnnotationsParseNode Annotations { get; set; }

        internal TypeMethodParseNode(Token tok)
            : base(tok)
        {
            _nameParts = new List<ParseNode>();
            _parameters = new List<List<ParseNode>>();
            _generics = new List<List<ParseNode>>();
        }

        /// <inheritdoc/>
        public PartParameters AddPart(ParseNode id)
        {
            _nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            _parameters.Add(ordinaries);
            _generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in _nameParts)
            {
                name += (n as IdentifierParseNode).Name + " ";
            }
            tw.WriteLine(prefix + "TypeMethod: " + name);
            if (ReturnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                ReturnType.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < _nameParts.Count; i++)
            {
                ParseNode partName = _nameParts[i];
                List<ParseNode> ps = _parameters[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (ParseNode arg in ps)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            writeComment(tw, prefix);
        }
        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a block</summary>
    public class BlockParseNode : ParseNode
    {
        private List<ParseNode> _parameters;

        /// <summary>Parameters of the block</summary>
        public List<ParseNode> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }

        private List<ParseNode> _body;

        /// <summary>Body of the block</summary>
        public List<ParseNode> Body
        {
            get { return _body; }
            set { _body = value; }
        }

        internal BlockParseNode(Token tok)
            : base(tok)
        {
            _body = new List<ParseNode>();
            _parameters = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Block:");
            tw.WriteLine(prefix + "  Parameters:");
            foreach (ParseNode n in _parameters)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (ParseNode n in _body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a varargs parameter</summary>
    public class VarArgsParameterParseNode : ParseNode
    {
        private ParseNode _name;

        /// <summary>Name of the parameter</summary>
        public ParseNode Name
        {
            get { return _name; }
            set { _name = value; }
        }

        internal VarArgsParameterParseNode(ParseNode name)
            : base(name)
        {
            this._name = name;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "VarArgsParameter:");
            tw.WriteLine(prefix + "  Name:");
            _name.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a typed parameter</summary>
    public class TypedParameterParseNode : ParseNode
    {
        private ParseNode _name;

        /// <summary>Name of the parameter</summary>
        public ParseNode Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private ParseNode _type;

        /// <summary>Type of the parameter</summary>
        public ParseNode Type
        {
            get { return _type; }
            set { _type = value; }
        }

        internal TypedParameterParseNode(ParseNode name, ParseNode type)
            : base(name)
        {
            this._name = name;
            this._type = type;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "TypedParameter:");
            tw.WriteLine(prefix + "  Name:");
            _name.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Type:");
            _type.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a var declaration</summary>
    public class VarDeclarationParseNode : ParseNode
    {
        private ParseNode _name;

        /// <summary>Name of the var</summary>
        public ParseNode Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private ParseNode _val;

        /// <summary>Initial value of the var</summary>
        public ParseNode Value
        {
            get { return _val; }
            set { _val = value; }
        }

        private ParseNode _type;

        /// <summary>Type of the var, if any</summary>
        public ParseNode Type
        {
            get { return _type; }
            set { _type = value; }
        }

        private AnnotationsParseNode _annotations;

        /// <summary>Annotations of the var, if any</summary>
        public AnnotationsParseNode Annotations
        {
            get { return _annotations; }
            set { _annotations = value; }
        }

        internal VarDeclarationParseNode(Token tok, ParseNode name, ParseNode val,
                ParseNode type, AnnotationsParseNode annotations)
            : base(tok)
        {
            this._name = name;
            this._val = val;
            this._type = type;
            this._annotations = annotations;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "VarDeclaration:");
            tw.WriteLine(prefix + "  Name:");
            _name.DebugPrint(tw, prefix + "    ");
            if (_type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                _type.DebugPrint(tw, prefix + "    ");
            }
            if (_annotations != null)
            {
                tw.WriteLine(prefix + "  Annotations:");
                _annotations.DebugPrint(tw, prefix + "    ");
            }
            if (_val != null)
            {
                tw.WriteLine(prefix + "  Value:");
                _val.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a def declaration</summary>
    public class DefDeclarationParseNode : ParseNode
    {
        private ParseNode _name;

        /// <summary>Name of the def</summary>
        public ParseNode Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private ParseNode _val;

        /// <summary>Value of the def</summary>
        public ParseNode Value
        {
            get { return _val; }
            set { _val = value; }
        }

        private ParseNode _type;

        /// <summary>Type of the def, if any</summary>
        public ParseNode Type
        {
            get { return _type; }
            set { _type = value; }
        }

        private AnnotationsParseNode _annotations;

        /// <summary>Annotations of the def, if any</summary>
        public AnnotationsParseNode Annotations
        {
            get { return _annotations; }
            set { _annotations = value; }
        }

        internal DefDeclarationParseNode(Token tok, ParseNode name, ParseNode val,
                ParseNode type, AnnotationsParseNode annotations)
            : base(tok)
        {
            this._name = name;
            this._val = val;
            this._type = type;
            this._annotations = annotations;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "DefDeclaration:");
            tw.WriteLine(prefix + "  Name:");
            _name.DebugPrint(tw, prefix + "    ");
            if (_type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                _type.DebugPrint(tw, prefix + "    ");
            }
            if (_annotations != null)
            {
                tw.WriteLine(prefix + "  Anontations:");
                _annotations.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Value:");
            _val.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a list of annotations</summary>
    public class AnnotationsParseNode : ParseNode
    {
        List<ParseNode> _annotations = new List<ParseNode>();

        /// <summary>The annotations in this collection</summary>
        public List<ParseNode> Annotations
        {
            get { return _annotations; }
            set { _annotations = value; }
        }

        internal AnnotationsParseNode(Token tok)
            : base(tok)
        {
        }

        /// <summary>Add an annotation to this collection</summary>
        /// <param name="ann">Annotation to add</param>
        public void AddAnnotation(ParseNode ann)
        {
            _annotations.Add(ann);
        }

        /// <summary>Check for a named annotation</summary>
        /// <param name="name">Annotation to search for</param>
        public bool HasAnnotation(string name)
        {
            foreach (ParseNode p in _annotations)
            {
                IdentifierParseNode aid = p as IdentifierParseNode;
                if (aid != null)
                {
                    if (aid.Name == name)
                        return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Annotations:");
            foreach (ParseNode ann in _annotations)
                ann.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for an operator</summary>
    public class OperatorParseNode : ParseNode
    {
        private ParseNode _left;

        /// <summary>LHS of the operator</summary>
        public ParseNode Left
        {
            get { return _left; }
            set { _left = value; }
        }

        private ParseNode _right;

        /// <summary>RHS of the operator</summary>
        public ParseNode Right
        {
            get { return _right; }
            set { _right = value; }
        }

        /// <summary>The name (symbol) of the operator</summary>
        public string name;

        internal OperatorParseNode(Token tok, string name, ParseNode l,
                ParseNode r)
            : base(tok)
        {
            this.name = name;
            _left = l;
            _right = r;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Operator: " + name);
            _left.DebugPrint(tw, prefix + "    ");
            _right.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }


    /// <summary>Parse node for a prefix operator</summary>
    public class PrefixOperatorParseNode : ParseNode
    {
        private string _name;

        /// <summary>Name (symbol) of the operator</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private ParseNode _receiver;

        /// <summary>Receiver of the operator request</summary>
        public ParseNode Receiver
        {
            get { return _receiver; }
            set { _receiver = value; }
        }

        internal PrefixOperatorParseNode(OperatorToken tok, ParseNode expr)
            : base(tok)
        {
            this._name = tok.Name;
            this._receiver = expr;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "PrefixOperator: " + _name);
            _receiver.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a bind :=</summary>
    public class BindParseNode : ParseNode
    {
        private ParseNode _left;

        /// <summary>LHS of :=</summary>
        public ParseNode Left
        {
            get { return _left; }
            set { _left = value; }
        }

        private ParseNode _right;

        /// <summary>RHS of :=</summary>
        public ParseNode Right
        {
            get { return _right; }
            set { _right = value; }
        }

        internal BindParseNode(Token tok, ParseNode l, ParseNode r)
            : base(tok)
        {
            _left = l;
            _right = r;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Bind:");
            _left.DebugPrint(tw, prefix + "    ");
            _right.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a number</summary>
    public class NumberParseNode : ParseNode
    {
        private int _base;

        /// <summary>Base of the number</summary>
        public int NumericBase
        {
            get { return _base; }
            set { _base = value; }
        }

        private string _digits;

        /// <summary>Digits of the number in its base</summary>
        public string Digits
        {
            get { return _digits; }
            set { _digits = value; }
        }

        internal NumberParseNode(Token tok)
            : base(tok)
        {
            NumberToken it = tok as NumberToken;
            _base = it.NumericBase;
            _digits = it.Digits;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string desc = "";
            if (_base == 10)
                desc += _digits;
            else if (_base == 16)
                desc += "0x" + _digits;
            else
                desc += _base + "x" + _digits;
            tw.WriteLine(prefix + "Number: " + desc);
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for an identifier</summary>
    public class IdentifierParseNode : ParseNode
    {
        private string _name;

        /// <summary>Name of this identifier</summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        internal IdentifierParseNode(Token tok)
            : base(tok)
        {
            IdentifierToken it = tok as IdentifierToken;
            _name = it.Name;
        }

        internal IdentifierParseNode(OperatorToken tok)
            : base(tok)
        {
            _name = tok.Name;
        }

        internal IdentifierParseNode(OpenBracketToken tok)
            : base(tok)
        {
            _name = tok.Name + tok.Other;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Identifier: " + _name);
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a string literal</summary>
    public class StringLiteralParseNode : ParseNode
    {
        private string _value;

        /// <summary>String value after escape processing</summary>
        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }

        /// <summary>Literal string as written, without
        /// escape processing</summary>
        public string raw;

        internal StringLiteralParseNode(Token tok)
            : base(tok)
        {
            StringToken comm = tok as StringToken;
            _value = comm.Value;
            raw = comm.Raw;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "StringLiteral: " + raw);
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a interpolated string</summary>
    public class InterpolatedStringParseNode : ParseNode
    {
        private List<ParseNode> _parts;

        /// <summary>List of component strings and stringifiables</summary>
        public List<ParseNode> Parts
        {
            get { return _parts; }
            set { _parts = value; }
        }

        internal InterpolatedStringParseNode(Token tok)
            : base(tok)
        {
            _parts = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "InterpolatedString:");
            foreach (ParseNode n in _parts)
                n.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for an implicit-receiver bracket request</summary>
    public class ImplicitBracketRequestParseNode : ParseNode
    {
        /// <summary>Name of this method</summary>
        public string Name { get; private set; }

        /// <summary>Arguments to this request</summary>
        public List<ParseNode> Arguments { get; private set; }

        internal ImplicitBracketRequestParseNode(Token start, string name,
                List<ParseNode> arguments)
            : base(start)
        {
            Name = name;
            Arguments = arguments;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "ImplicitBracketRequest: " + Name);
            tw.WriteLine(prefix + "  Parts:");
            foreach (ParseNode arg in Arguments)
                arg.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for an explicit-receiver bracket request</summary>
    public class ExplicitBracketRequestParseNode : ParseNode
    {
        /// <summary>Name of this method</summary>
        public string Name { get; private set; }

        /// <summary>Receiver  ofthis request</summary>
        public ParseNode Receiver { get; private set; }

        /// <summary>Arguments to this request</summary>
        public List<ParseNode> Arguments { get; private set; }

        internal ExplicitBracketRequestParseNode(Token start, string name,
                ParseNode receiver,
                List<ParseNode> arguments)
            : base(start)
        {
            Name = name;
            Receiver = receiver;
            Arguments = arguments;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "ExplicitBracketRequest: " + Name);
            tw.WriteLine(prefix + "  Receiver:");
            Receiver.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Arguments:");
            foreach (ParseNode arg in Arguments)
                arg.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a implicit-receiver request</summary>
    public class ImplicitReceiverRequestParseNode : ParseNode
    {
        private List<ParseNode> _nameParts;

        /// <summary>Parts of this method</summary>
        public List<ParseNode> NameParts
        {
            get { return _nameParts; }
            set { _nameParts = value; }
        }

        private List<List<ParseNode>> _arguments;

        /// <summary>Argument lists of each part</summary>
        public List<List<ParseNode>> Arguments
        {
            get { return _arguments; }
            set { _arguments = value; }
        }

        private List<List<ParseNode>> _genericArguments;

        /// <summary>Generic argument lists of each part</summary>
        public List<List<ParseNode>> GenericArguments
        {
            get { return _genericArguments; }
            set { _genericArguments = value; }
        }

        internal ImplicitReceiverRequestParseNode(ParseNode id)
            : base(id)
        {
            _nameParts = new List<ParseNode>();
            _arguments = new List<List<ParseNode>>();
            _genericArguments = new List<List<ParseNode>>();
            AddPart(id);
        }

        /// <summary>Add a part to the method requested here</summary>
        public void AddPart(ParseNode id)
        {
            _nameParts.Add(id);
            _arguments.Add(new List<ParseNode>());
            _genericArguments.Add(new List<ParseNode>());
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in _nameParts)
            {
                name += (n as IdentifierParseNode).Name + " ";
            }
            tw.WriteLine(prefix + "ImplicitReceiverRequest: " + name);
            tw.WriteLine(prefix + "  Arguments:");
            for (int i = 0; i < _nameParts.Count; i++)
            {
                ParseNode partName = _nameParts[i];
                List<ParseNode> args = _arguments[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Generic arguments:");
                foreach (ParseNode arg in _genericArguments[i])
                    arg.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Arguments:");
                foreach (ParseNode arg in args)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a explicit-receiver request</summary>
    public class ExplicitReceiverRequestParseNode : ParseNode
    {
        private ParseNode _receiver;

        /// <summary>Receiver of this request</summary>
        public ParseNode Receiver
        {
            get { return _receiver; }
            set { _receiver = value; }
        }

        private List<ParseNode> _nameParts;

        /// <summary>Parts of this method</summary>
        public List<ParseNode> NameParts
        {
            get { return _nameParts; }
            set { _nameParts = value; }
        }

        private List<List<ParseNode>> _arguments;

        /// <summary>Argument lists of each part</summary>
        public List<List<ParseNode>> Arguments
        {
            get { return _arguments; }
            set { _arguments = value; }
        }

        private List<List<ParseNode>> _genericArguments;

        /// <summary>Generic argument lists of each part</summary>
        public List<List<ParseNode>> GenericArguments
        {
            get { return _genericArguments; }
            set { _genericArguments = value; }
        }


        internal ExplicitReceiverRequestParseNode(ParseNode receiver)
            : base(receiver)
        {
            this._receiver = receiver;
            _nameParts = new List<ParseNode>();
            _arguments = new List<List<ParseNode>>();
            _genericArguments = new List<List<ParseNode>>();
        }

        /// <summary>Add a part to the method requested here</summary>
        public void AddPart(ParseNode id)
        {
            _nameParts.Add(id);
            _arguments.Add(new List<ParseNode>());
            _genericArguments.Add(new List<ParseNode>());
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in _nameParts)
            {
                name += (n as IdentifierParseNode).Name + " ";
            }
            tw.WriteLine(prefix + "ExplicitReceiverRequest: " + name);
            tw.WriteLine(prefix + "  Receiver:");
            _receiver.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < _nameParts.Count; i++)
            {
                ParseNode partName = _nameParts[i];
                List<ParseNode> args = _arguments[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Generic arguments:");
                foreach (ParseNode arg in _genericArguments[i])
                    arg.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Arguments:");
                foreach (ParseNode arg in args)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for an inherits clause</summary>
    public class InheritsParseNode : ParseNode
    {
        private ParseNode _from;
        private IdentifierParseNode _as;

        /// <summary>RHS of the inherits clause</summary>
        public ParseNode From
        {
            get { return _from; }
            set { _from = value; }
        }

        /// <summary>As clause of of inherits</summary>
        public IdentifierParseNode As
        {
            get { return _as; }
        }

        internal InheritsParseNode(Token tok, ParseNode expr,
                IdentifierParseNode asExpr)
            : base(tok)
        {
            _from = expr;
            _as = asExpr;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Inherits:");
            _from.DebugPrint(tw, prefix + "    ");
            if (_as != null)
            {
                tw.WriteLine(prefix + "  As:");
                _as.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for an import</summary>
    public class ImportParseNode : ParseNode
    {
        private ParseNode _path;

        /// <summary>Given import path in the syntax</summary>
        public ParseNode Path
        {
            get { return _path; }
            set { _path = value; }
        }

        private ParseNode _name;

        /// <summary>Given "as name" in the syntax</summary>
        public ParseNode Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private ParseNode _type;

        /// <summary>Given ": type", if provided</summary>
        public ParseNode Type
        {
            get { return _type; }
            set { _type = value; }
        }

        internal ImportParseNode(Token tok, ParseNode path, ParseNode name,
                ParseNode type)
            : base(tok)
        {
            this._path = path;
            this._name = name;
            this._type = type;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Import:");
            tw.WriteLine(prefix + "  Path:");
            _path.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  As:");
            _name.DebugPrint(tw, prefix + "    ");
            if (_type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                _type.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a dialect declaration</summary>
    public class DialectParseNode : ParseNode
    {
        private ParseNode _path;

        /// <summary>Given import path in the syntax</summary>
        public ParseNode Path
        {
            get { return _path; }
            set { _path = value; }
        }

        internal DialectParseNode(Token tok, ParseNode path)
            : base(tok)
        {
            this._path = path;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Dialect:");
            _path.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a return statement</summary>
    public class ReturnParseNode : ParseNode
    {
        private ParseNode _returnValue;

        /// <summary>Expression returned, if any</summary>
        public ParseNode ReturnValue
        {
            get { return _returnValue; }
            set { _returnValue = value; }
        }

        internal ReturnParseNode(Token tok, ParseNode val)
            : base(tok)
        {
            _returnValue = val;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Return:");
            if (_returnValue == null)
                tw.WriteLine(prefix + "    (nothing)");
            else
                _returnValue.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a parenthesised expression</summary>
    public class ParenthesisedParseNode : ParseNode
    {
        private ParseNode _expr;

        /// <summary>Expression in parentheses</summary>
        public ParseNode Expression
        {
            get { return _expr; }
            set { _expr = value; }
        }

        internal ParenthesisedParseNode(Token tok, ParseNode expr)
            : base(tok)
        {
            _expr = expr;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Parenthesised:");
            _expr.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    /// <summary>Parse node for a comment</summary>
    public class CommentParseNode : ParseNode
    {
        private string _value;

        /// <summary>String body of comment</summary>
        public string Value
        {
            get { return _value; }
            set { _value = value; }
        }

        internal CommentParseNode(Token tok)
            : base(tok)
        {
            CommentToken comm = tok as CommentToken;
            _value = comm.Value;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Comment: " + _value);
            if (this.Comment != null)
                this.Comment.DebugPrint(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

}
