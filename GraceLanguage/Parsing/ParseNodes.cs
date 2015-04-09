using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grace.Parsing
{
    public abstract class ParseNode
    {
        public int line;
        public int column;
        public ParseNode comment;

        internal Token Token { get; set; }

        internal ParseNode(Token tok)
        {
            this.line = tok.line;
            this.column = tok.column;
            Token = tok;
        }

        internal ParseNode(ParseNode basis)
        {
            Token = basis.Token;
            this.line = basis.line;
            this.column = basis.column;
        }

        public abstract void DebugPrint(System.IO.TextWriter tw, string prefix);

        public void writeComment(System.IO.TextWriter tw, string prefix)
        {
            if (this.comment != null)
            {
                tw.WriteLine(prefix + "  Comment:");
                this.comment.DebugPrint(tw, prefix + "    ");
            }
        }

        public virtual T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class ObjectParseNode : ParseNode
    {
        public List<ParseNode> body;

        internal ObjectParseNode(Token tok)
            : base(tok)
        {
            body = new List<ParseNode>();
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Object:");
            foreach (ParseNode n in body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public struct PartParameters
    {
        public List<ParseNode> Generics;
        public List<ParseNode> Ordinary;
        public PartParameters(List<ParseNode> g, List<ParseNode> o)
        {
            Generics = g;
            Ordinary = o;
        }
    }

    public interface MethodHeader
    {
        PartParameters AddPart(ParseNode id);
        ParseNode returnType { get; set; }
        AnnotationsParseNode annotations { get; set; }
    }

    public class MethodDeclarationParseNode : ParseNode, MethodHeader
    {
        public List<ParseNode> nameParts;
        public List<List<ParseNode>> parameters;
        public List<List<ParseNode>> generics;
        public List<ParseNode> body;
        public ParseNode returnType { get; set; }
        public AnnotationsParseNode annotations { get; set; }

        internal MethodDeclarationParseNode(Token tok)
            : base(tok)
        {
            nameParts = new List<ParseNode>();
            parameters = new List<List<ParseNode>>();
            generics = new List<List<ParseNode>>();
            body = new List<ParseNode>();
        }

        public PartParameters AddPart(ParseNode id)
        {
            nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            parameters.Add(ordinaries);
            generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

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
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class ClassDeclarationParseNode : ParseNode, MethodHeader
    {
        public ParseNode baseName;
        public List<ParseNode> nameParts;
        public List<List<ParseNode>> parameters;
        public List<List<ParseNode>> generics;
        public List<ParseNode> body;
        public ParseNode returnType { get; set; }
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

        public PartParameters AddPart(ParseNode id)
        {
            nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            parameters.Add(ordinaries);
            generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

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
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class TypeStatementParseNode : ParseNode
    {
        public ParseNode baseName;
        public ParseNode body;
        public List<ParseNode> genericParameters;

        internal TypeStatementParseNode(Token tok, ParseNode baseName,
                ParseNode body, List<ParseNode> generics)
            : base(tok)
        {
            this.baseName = baseName;
            this.body = body;
            this.genericParameters = generics;
        }

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
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class TypeParseNode : ParseNode
    {
        public List<ParseNode> body;

        public string Name { get; set; }

        internal TypeParseNode(Token tok, List<ParseNode> body)
            : base(tok)
        {
            this.body = body;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Type:");
            foreach (ParseNode n in body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class TypeMethodParseNode : ParseNode, MethodHeader
    {
        public List<ParseNode> nameParts;
        public List<List<ParseNode>> parameters;
        public List<List<ParseNode>> generics;
        public ParseNode returnType { get; set; }
        public AnnotationsParseNode annotations { get; set; }

        internal TypeMethodParseNode(Token tok)
            : base(tok)
        {
            nameParts = new List<ParseNode>();
            parameters = new List<List<ParseNode>>();
            generics = new List<List<ParseNode>>();
        }

        public PartParameters AddPart(ParseNode id)
        {
            nameParts.Add(id);
            List<ParseNode> ordinaries = new List<ParseNode>();
            List<ParseNode> gens = new List<ParseNode>();
            parameters.Add(ordinaries);
            generics.Add(gens);
            return new PartParameters(gens, ordinaries);
        }

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
        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class BlockParseNode : ParseNode
    {
        public List<ParseNode> parameters;
        public List<ParseNode> body;

        internal BlockParseNode(Token tok)
            : base(tok)
        {
            body = new List<ParseNode>();
            parameters = new List<ParseNode>();
        }

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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class VarArgsParameterParseNode : ParseNode
    {
        public ParseNode name;

        internal VarArgsParameterParseNode(ParseNode name)
            : base(name)
        {
            this.name = name;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "VarArgsParameter:");
            tw.WriteLine(prefix + "  Name:");
            name.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class TypedParameterParseNode : ParseNode
    {
        public ParseNode name;
        public ParseNode type;

        internal TypedParameterParseNode(ParseNode name, ParseNode type)
            : base(name)
        {
            this.name = name;
            this.type = type;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "TypedParameter:");
            tw.WriteLine(prefix + "  Name:");
            name.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Type:");
            type.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class VarDeclarationParseNode : ParseNode
    {
        public ParseNode name;
        public ParseNode val;
        public ParseNode type;
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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class DefDeclarationParseNode : ParseNode
    {
        public ParseNode name;
        public ParseNode val;
        public ParseNode type;
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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class AnnotationsParseNode : ParseNode
    {
        List<ParseNode> annotations = new List<ParseNode>();
        internal AnnotationsParseNode(Token tok)
            : base(tok)
        {
        }

        public void AddAnnotation(ParseNode ann)
        {
            annotations.Add(ann);
        }

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

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Annotations:");
            foreach (ParseNode ann in annotations)
                ann.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class OperatorParseNode : ParseNode
    {
        public ParseNode left;
        public ParseNode right;
        public string name;

        internal OperatorParseNode(Token tok, string name, ParseNode l,
                ParseNode r)
            : base(tok)
        {
            this.name = name;
            left = l;
            right = r;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Operator: " + name);
            left.DebugPrint(tw, prefix + "    ");
            right.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }


    public class PrefixOperatorParseNode : ParseNode
    {
        public string name;
        public ParseNode receiver;

        internal PrefixOperatorParseNode(OperatorToken tok, ParseNode expr)
            : base(tok)
        {
            this.name = tok.name;
            this.receiver = expr;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "PrefixOperator: " + name);
            receiver.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class BindParseNode : ParseNode
    {
        public ParseNode left;
        public ParseNode right;

        internal BindParseNode(Token tok, ParseNode l, ParseNode r)
            : base(tok)
        {
            left = l;
            right = r;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Bind:");
            left.DebugPrint(tw, prefix + "    ");
            right.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class NumberParseNode : ParseNode
    {
        public int _base;
        public string digits;
        internal NumberParseNode(Token tok)
            : base(tok)
        {
            NumberToken it = tok as NumberToken;
            _base = it._base;
            digits = it.digits;
        }

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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class IdentifierParseNode : ParseNode
    {
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


        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Identifier: " + name);
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class StringLiteralParseNode : ParseNode
    {
        public string value;
        public string raw;
        internal StringLiteralParseNode(Token tok)
            : base(tok)
        {
            StringToken comm = tok as StringToken;
            value = comm.value;
            raw = comm.raw;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "StringLiteral: " + raw);
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class InterpolatedStringParseNode : ParseNode
    {
        public List<ParseNode> parts;

        internal InterpolatedStringParseNode(Token tok)
            : base(tok)
        {
            parts = new List<ParseNode>();
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "InterpolatedString:");
            foreach (ParseNode n in parts)
                n.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class ImplicitReceiverRequestParseNode : ParseNode
    {
        public List<ParseNode> nameParts;
        public List<List<ParseNode>> arguments;
        public List<List<ParseNode>> genericArguments;

        internal ImplicitReceiverRequestParseNode(ParseNode id)
            : base(id)
        {
            nameParts = new List<ParseNode>();
            arguments = new List<List<ParseNode>>();
            genericArguments = new List<List<ParseNode>>();
            AddPart(id);
        }

        public void AddPart(ParseNode id)
        {
            nameParts.Add(id);
            arguments.Add(new List<ParseNode>());
            genericArguments.Add(new List<ParseNode>());
        }

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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class ExplicitReceiverRequestParseNode : ParseNode
    {
        public ParseNode receiver;
        public List<ParseNode> nameParts;
        public List<List<ParseNode>> arguments;
        public List<List<ParseNode>> genericArguments;

        internal ExplicitReceiverRequestParseNode(ParseNode receiver)
            : base(receiver)
        {
            this.receiver = receiver;
            nameParts = new List<ParseNode>();
            arguments = new List<List<ParseNode>>();
            genericArguments = new List<List<ParseNode>>();
        }

        public void AddPart(ParseNode id)
        {
            nameParts.Add(id);
            arguments.Add(new List<ParseNode>());
            genericArguments.Add(new List<ParseNode>());
        }

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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class InheritsParseNode : ParseNode
    {
        public ParseNode from;
        internal InheritsParseNode(Token tok, ParseNode expr)
            : base(tok)
        {
            from = expr;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Inherits:");
            from.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class ImportParseNode : ParseNode
    {
        public ParseNode path;
        public ParseNode name;
        public ParseNode type;

        internal ImportParseNode(Token tok, ParseNode path, ParseNode name,
                ParseNode type)
            : base(tok)
        {
            this.path = path;
            this.name = name;
            this.type = type;
        }

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

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class DialectParseNode : ParseNode
    {
        public ParseNode path;
        public ParseNode name;
        internal DialectParseNode(Token tok, ParseNode path)
            : base(tok)
        {
            this.path = path;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Dialect:");
            path.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class ReturnParseNode : ParseNode
    {
        public ParseNode returnValue;
        internal ReturnParseNode(Token tok, ParseNode val)
            : base(tok)
        {
            returnValue = val;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Return:");
            if (returnValue == null)
                tw.WriteLine(prefix + "    (nothing)");
            else
                returnValue.DebugPrint(tw, prefix + "    ");
            writeComment(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

    public class CommentParseNode : ParseNode
    {
        public string value;
        internal CommentParseNode(Token tok)
            : base(tok)
        {
            CommentToken comm = tok as CommentToken;
            value = comm.value;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Comment: " + value);
            if (this.comment != null)
                this.comment.DebugPrint(tw, prefix);
        }

        public override T Visit<T>(ParseNodeVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }

    }

}
