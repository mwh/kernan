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
        /// <summary>Line number this node began on</summary>
        public int line;

        /// <summary>Column number this node began at</summary>
        public int column;

        /// <summary>Comment on this node, if any</summary>
        public ParseNode comment;

        internal Token Token { get; set; }

        /// <param name="tok">Token that gave rise to this node</param>
        internal ParseNode(Token tok)
        {
            this.line = tok.line;
            this.column = tok.column;
            Token = tok;
        }

        /// <param name="basis">ParseNode that gave rise to this node</param>
        internal ParseNode(ParseNode basis)
        {
            Token = basis.Token;
            this.line = basis.line;
            this.column = basis.column;
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
            if (this.comment != null)
            {
                tw.WriteLine(prefix + "  Comment:");
                this.comment.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Body of the object</summary>
        public List<ParseNode> body;

        internal ObjectParseNode(Token tok)
            : base(tok)
        {
            body = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Object:");
            foreach (ParseNode n in body)
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
        /// <summary>Generic parameters</summary>
        public List<ParseNode> Generics;
        /// <summary>Ordinary parameters</summary>
        public List<ParseNode> Ordinary;

        /// <param name="g">Generic parameters</param>
        /// <param name="o">Ordinary parameters</param>
        public PartParameters(List<ParseNode> g, List<ParseNode> o)
        {
            Generics = g;
            Ordinary = o;
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
        ParseNode returnType { get; set; }

        /// <summary>Annotations of this method</summary>
        AnnotationsParseNode annotations { get; set; }
    }

    /// <summary>Parse node for a method declaration</summary>
    public class MethodDeclarationParseNode : ParseNode, MethodHeader
    {
        /// <summary>Parts of this method</summary>
        public List<ParseNode> nameParts;

        /// <summary>Parameter lists of each part</summary>
        public List<List<ParseNode>> parameters;

        /// <summary>Generic parameter lists of each part</summary>
        public List<List<ParseNode>> generics;

        /// <summary>Body of this method</summary>
        public List<ParseNode> body;

        /// <inheritdoc/>
        public ParseNode returnType { get; set; }

        /// <inheritdoc/>
        public AnnotationsParseNode annotations { get; set; }

        internal MethodDeclarationParseNode(Token tok)
            : base(tok)
        {
            nameParts = new List<ParseNode>();
            parameters = new List<List<ParseNode>>();
            generics = new List<List<ParseNode>>();
            body = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public PartParameters AddPart(ParseNode id)
        {
            nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            parameters.Add(ordinaries);
            generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in nameParts)
            {
                name += (n as IdentifierParseNode).name + " ";
            }
            tw.WriteLine(prefix + "MethodDeclaration: " + name);
            if (returnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                returnType.DebugPrint(tw, prefix + "    ");
            }
            if (annotations != null)
            {
                tw.WriteLine(prefix + "  Annotations:");
                annotations.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < nameParts.Count; i++)
            {
                ParseNode partName = nameParts[i];
                List<ParseNode> ps = parameters[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (ParseNode arg in ps)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (ParseNode n in body)
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
        /// <summary>Name of this class</summary>
        public ParseNode baseName;

        /// <summary>Parts of the constructor of this class</summary>
        public List<ParseNode> nameParts;

        /// <summary>Parameter lists of each part</summary>
        public List<List<ParseNode>> parameters;

        /// <summary>Generic parameter lists of each part</summary>
        public List<List<ParseNode>> generics;

        /// <summary>Body of this class</summary>
        public List<ParseNode> body;

        /// <inheritdoc/>
        public ParseNode returnType { get; set; }

        /// <inheritdoc/>
        public AnnotationsParseNode annotations { get; set; }

        internal ClassDeclarationParseNode(Token tok, ParseNode baseName)
            : base(tok)
        {
            this.baseName = baseName;
            nameParts = new List<ParseNode>();
            parameters = new List<List<ParseNode>>();
            generics = new List<List<ParseNode>>();
            body = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public PartParameters AddPart(ParseNode id)
        {
            nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            parameters.Add(ordinaries);
            generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            IdentifierParseNode b = baseName as IdentifierParseNode;
            string name = b.name + ".";
            foreach (ParseNode n in nameParts)
            {
                name += (n as IdentifierParseNode).name + " ";
            }
            tw.WriteLine(prefix + "ClassDeclaration: " + name);
            if (returnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                returnType.DebugPrint(tw, prefix + "    ");
            }
            if (annotations != null)
            {
                tw.WriteLine(prefix + "  Anontations:");
                annotations.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < nameParts.Count; i++)
            {
                ParseNode partName = nameParts[i];
                List<ParseNode> ps = parameters[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (ParseNode arg in ps)
                    arg.DebugPrint(tw, prefix + "        ");
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (ParseNode n in body)
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
        /// <summary>Name of this type</summary>
        public ParseNode baseName;

        /// <summary>Value of this type declaration</summary>
        public ParseNode body;

        /// <summary>Generic parameters of this type</summary>
        public List<ParseNode> genericParameters;

        internal TypeStatementParseNode(Token tok, ParseNode baseName,
                ParseNode body, List<ParseNode> generics)
            : base(tok)
        {
            this.baseName = baseName;
            this.body = body;
            this.genericParameters = generics;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "TypeStatement:");
            tw.WriteLine(prefix + "  Name:");
            baseName.DebugPrint(tw, prefix + "    ");
            if (genericParameters.Count > 0)
            {
                tw.WriteLine(prefix + "  Generic parameters:");
                foreach (ParseNode n in genericParameters)
                    n.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Body:");
            body.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Body of this type</summary>
        public List<ParseNode> body;

        /// <summary>Name of this type for debugging</summary>
        public string Name { get; set; }

        internal TypeParseNode(Token tok, List<ParseNode> body)
            : base(tok)
        {
            this.body = body;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Type:");
            foreach (ParseNode n in body)
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
        /// <summary>Parts of the method</summary>
        public List<ParseNode> nameParts;

        /// <summary>Parameter lists of each part</summary>
        public List<List<ParseNode>> parameters;

        /// <summary>Generic parameter lists of each part</summary>
        public List<List<ParseNode>> generics;

        /// <inheritdoc/>
        public ParseNode returnType { get; set; }

        /// <inheritdoc/>
        public AnnotationsParseNode annotations { get; set; }

        internal TypeMethodParseNode(Token tok)
            : base(tok)
        {
            nameParts = new List<ParseNode>();
            parameters = new List<List<ParseNode>>();
            generics = new List<List<ParseNode>>();
        }

        /// <inheritdoc/>
        public PartParameters AddPart(ParseNode id)
        {
            nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            parameters.Add(ordinaries);
            generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in nameParts)
            {
                name += (n as IdentifierParseNode).name + " ";
            }
            tw.WriteLine(prefix + "TypeMethod: " + name);
            if (returnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                returnType.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < nameParts.Count; i++)
            {
                ParseNode partName = nameParts[i];
                List<ParseNode> ps = parameters[i];
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
        /// <summary>Parameters of the block</summary>
        public List<ParseNode> parameters;

        /// <summary>Body of the block</summary>
        public List<ParseNode> body;

        internal BlockParseNode(Token tok)
            : base(tok)
        {
            body = new List<ParseNode>();
            parameters = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Block:");
            tw.WriteLine(prefix + "  Parameters:");
            foreach (ParseNode n in parameters)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (ParseNode n in body)
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
        /// <summary>Name of the parameter</summary>
        public ParseNode name;

        internal VarArgsParameterParseNode(ParseNode name)
            : base(name)
        {
            this.name = name;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "VarArgsParameter:");
            tw.WriteLine(prefix + "  Name:");
            name.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Name of the parameter</summary>
        public ParseNode name;

        /// <summary>Type of the parameter</summary>
        public ParseNode type;

        internal TypedParameterParseNode(ParseNode name, ParseNode type)
            : base(name)
        {
            this.name = name;
            this.type = type;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "TypedParameter:");
            tw.WriteLine(prefix + "  Name:");
            name.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Type:");
            type.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Name of the var</summary>
        public ParseNode name;

        /// <summary>Initial value of the var</summary>
        public ParseNode val;

        /// <summary>Type of the var, if any</summary>
        public ParseNode type;

        /// <summary>Annotations of the var, if any</summary>

        public AnnotationsParseNode annotations;

        internal VarDeclarationParseNode(Token tok, ParseNode name, ParseNode val,
                ParseNode type, AnnotationsParseNode annotations)
            : base(tok)
        {
            this.name = name;
            this.val = val;
            this.type = type;
            this.annotations = annotations;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "VarDeclaration:");
            tw.WriteLine(prefix + "  Name:");
            name.DebugPrint(tw, prefix + "    ");
            if (type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                type.DebugPrint(tw, prefix + "    ");
            }
            if (annotations != null)
            {
                tw.WriteLine(prefix + "  Annotations:");
                annotations.DebugPrint(tw, prefix + "    ");
            }
            if (val != null)
            {
                tw.WriteLine(prefix + "  Value:");
                val.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Name of the def</summary>
        public ParseNode name;

        /// <summary>Value of the def</summary>
        public ParseNode val;

        /// <summary>Type of the def, if any</summary>
        public ParseNode type;

        /// <summary>Annotations of the def, if any</summary>
        public AnnotationsParseNode annotations;

        internal DefDeclarationParseNode(Token tok, ParseNode name, ParseNode val,
                ParseNode type, AnnotationsParseNode annotations)
            : base(tok)
        {
            this.name = name;
            this.val = val;
            this.type = type;
            this.annotations = annotations;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "DefDeclaration:");
            tw.WriteLine(prefix + "  Name:");
            name.DebugPrint(tw, prefix + "    ");
            if (type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                type.DebugPrint(tw, prefix + "    ");
            }
            if (annotations != null)
            {
                tw.WriteLine(prefix + "  Anontations:");
                annotations.DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Value:");
            val.DebugPrint(tw, prefix + "    ");
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
        /// <summary>The annotations in this collection</summary>
        List<ParseNode> annotations = new List<ParseNode>();
        internal AnnotationsParseNode(Token tok)
            : base(tok)
        {
        }

        /// <summary>Add an annotation to this collection</summary>
        /// <param name="ann">Annotation to add</param>
        public void AddAnnotation(ParseNode ann)
        {
            annotations.Add(ann);
        }

        /// <summary>Check for a named annotation</summary>
        /// <param name="name">Annotation to search for</param>
        public bool HasAnnotation(string name)
        {
            foreach (ParseNode p in annotations)
            {
                IdentifierParseNode aid = p as IdentifierParseNode;
                if (aid != null)
                {
                    if (aid.name == name)
                        return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Annotations:");
            foreach (ParseNode ann in annotations)
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
        /// <summary>LHS of the operator</summary>
        public ParseNode left;

        /// <summary>RHS of the operator</summary>
        public ParseNode right;

        /// <summary>The name (symbol) of the operator</summary>
        public string name;

        internal OperatorParseNode(Token tok, string name, ParseNode l,
                ParseNode r)
            : base(tok)
        {
            this.name = name;
            left = l;
            right = r;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Operator: " + name);
            left.DebugPrint(tw, prefix + "    ");
            right.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Name (symbol) of the operator</summary>
        public string name;

        /// <summary>Receiver of the operator request</summary>
        public ParseNode receiver;

        internal PrefixOperatorParseNode(OperatorToken tok, ParseNode expr)
            : base(tok)
        {
            this.name = tok.name;
            this.receiver = expr;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "PrefixOperator: " + name);
            receiver.DebugPrint(tw, prefix + "    ");
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
        /// <summary>LHS of :=</summary>
        public ParseNode left;

        /// <summary>RHS of :=</summary>
        public ParseNode right;

        internal BindParseNode(Token tok, ParseNode l, ParseNode r)
            : base(tok)
        {
            left = l;
            right = r;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Bind:");
            left.DebugPrint(tw, prefix + "    ");
            right.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Base of the number</summary>
        public int _base;

        /// <summary>Digits of the number in its base</summary>
        public string digits;

        internal NumberParseNode(Token tok)
            : base(tok)
        {
            NumberToken it = tok as NumberToken;
            _base = it._base;
            digits = it.digits;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string desc = "";
            if (_base == 10)
                desc += digits;
            else if (_base == 16)
                desc += "0x" + digits;
            else
                desc += _base + "x" + digits;
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
        /// <summary>Name of this identifier</summary>
        public string name;

        internal IdentifierParseNode(Token tok)
            : base(tok)
        {
            IdentifierToken it = tok as IdentifierToken;
            name = it.name;
        }

        internal IdentifierParseNode(OperatorToken tok)
            : base(tok)
        {
            name = tok.name;
        }


        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Identifier: " + name);
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
        /// <summary>String value after escape processing</summary>
        public string value;

        /// <summary>Literal string as written, without
        /// escape processing</summary>
        public string raw;

        internal StringLiteralParseNode(Token tok)
            : base(tok)
        {
            StringToken comm = tok as StringToken;
            value = comm.value;
            raw = comm.raw;
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
        /// <summary>List of component strings and stringifiables</summary>
        public List<ParseNode> parts;

        internal InterpolatedStringParseNode(Token tok)
            : base(tok)
        {
            parts = new List<ParseNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "InterpolatedString:");
            foreach (ParseNode n in parts)
                n.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Parts of this method</summary>
        public List<ParseNode> nameParts;

        /// <summary>Argument lists of each part</summary>
        public List<List<ParseNode>> arguments;

        /// <summary>Generic argument lists of each part</summary>
        public List<List<ParseNode>> genericArguments;

        internal ImplicitReceiverRequestParseNode(ParseNode id)
            : base(id)
        {
            nameParts = new List<ParseNode>();
            arguments = new List<List<ParseNode>>();
            genericArguments = new List<List<ParseNode>>();
            AddPart(id);
        }

        /// <summary>Add a part to the method requested here</summary>
        public void AddPart(ParseNode id)
        {
            nameParts.Add(id);
            arguments.Add(new List<ParseNode>());
            genericArguments.Add(new List<ParseNode>());
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in nameParts)
            {
                name += (n as IdentifierParseNode).name + " ";
            }
            tw.WriteLine(prefix + "ImplicitReceiverRequest: " + name);
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < nameParts.Count; i++)
            {
                ParseNode partName = nameParts[i];
                List<ParseNode> args = arguments[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Generic arguments:");
                foreach (ParseNode arg in genericArguments[i])
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
        /// <summary>Receiver of this request</summary>
        public ParseNode receiver;

        /// <summary>Parts of this method</summary>
        public List<ParseNode> nameParts;

        /// <summary>Argument lists of each part</summary>
        public List<List<ParseNode>> arguments;

        /// <summary>Generic argument lists of each part</summary>
        public List<List<ParseNode>> genericArguments;


        internal ExplicitReceiverRequestParseNode(ParseNode receiver)
            : base(receiver)
        {
            this.receiver = receiver;
            nameParts = new List<ParseNode>();
            arguments = new List<List<ParseNode>>();
            genericArguments = new List<List<ParseNode>>();
        }

        /// <summary>Add a part to the method requested here</summary>
        public void AddPart(ParseNode id)
        {
            nameParts.Add(id);
            arguments.Add(new List<ParseNode>());
            genericArguments.Add(new List<ParseNode>());
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string name = "";
            foreach (ParseNode n in nameParts)
            {
                name += (n as IdentifierParseNode).name + " ";
            }
            tw.WriteLine(prefix + "ExplicitReceiverRequest: " + name);
            tw.WriteLine(prefix + "  Receiver:");
            receiver.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Parts:");
            for (int i = 0; i < nameParts.Count; i++)
            {
                ParseNode partName = nameParts[i];
                List<ParseNode> args = arguments[i];
                tw.WriteLine(prefix + "    Part " + (i + 1) + ": ");
                tw.WriteLine(prefix + "      Name:");
                partName.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Generic arguments:");
                foreach (ParseNode arg in genericArguments[i])
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
        /// <summary>RHS of the inherits clause</summary>
        public ParseNode from;
        internal InheritsParseNode(Token tok, ParseNode expr)
            : base(tok)
        {
            from = expr;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Inherits:");
            from.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Given import path in the syntax</summary>
        public ParseNode path;

        /// <summary>Given "as name" in the syntax</summary>
        public ParseNode name;

        /// <summary>Given ": type", if provided</summary>
        public ParseNode type;

        internal ImportParseNode(Token tok, ParseNode path, ParseNode name,
                ParseNode type)
            : base(tok)
        {
            this.path = path;
            this.name = name;
            this.type = type;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Import:");
            tw.WriteLine(prefix + "  Path:");
            path.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  As:");
            name.DebugPrint(tw, prefix + "    ");
            if (type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                type.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Given import path in the syntax</summary>
        public ParseNode path;

        internal DialectParseNode(Token tok, ParseNode path)
            : base(tok)
        {
            this.path = path;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Dialect:");
            path.DebugPrint(tw, prefix + "    ");
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
        /// <summary>Expression returned, if any</summary>
        public ParseNode returnValue;

        internal ReturnParseNode(Token tok, ParseNode val)
            : base(tok)
        {
            returnValue = val;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Return:");
            if (returnValue == null)
                tw.WriteLine(prefix + "    (nothing)");
            else
                returnValue.DebugPrint(tw, prefix + "    ");
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
        /// <summary>String body of comment</summary>
        public string value;

        internal CommentParseNode(Token tok)
            : base(tok)
        {
            CommentToken comm = tok as CommentToken;
            value = comm.value;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Comment: " + value);
            if (this.comment != null)
                this.comment.DebugPrint(tw, prefix);
        }

        /// <inheritdoc/>
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

}
