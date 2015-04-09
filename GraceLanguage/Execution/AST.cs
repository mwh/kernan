using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Parsing;
using Grace.Runtime;

namespace Grace.Execution
{

    public abstract class Node
    {
        internal Token Location;
        private ParseNode parseNode;
        public ParseNode Origin
        {
            get
            {
                return parseNode;
            }
        }

        internal Node(Token location, ParseNode source)
        {
            this.Location = location;
            this.parseNode = source;
        }

        internal Node(ParseNode source)
        {
            this.parseNode = source;
        }

        public abstract GraceObject Evaluate(EvaluationContext ctx);
        public abstract void DebugPrint(System.IO.TextWriter tw, string prefix);
    }

    public class DialectNode : Node
    {

        private DialectParseNode origin;
        internal DialectNode(Token location, DialectParseNode source)
            : base(location, source)
        {
            origin = source;
        }

        public string Path
        {
            get
            {
                return (origin.path as StringLiteralParseNode).value;
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var mod = ctx.LoadModule(Path);
            ctx.InsertOuter(mod);
            ctx.AddMinorDef("dialect", mod);
            var atModuleStart = MethodRequest.Single("atModuleStart", GraceString.Create(""));
            if (mod.RespondsTo(atModuleStart))
                mod.Request(ctx, atModuleStart);
            var selfReq = MethodRequest.Nullary("self");
            var self = ctx.FindReceiver(selfReq).Request(ctx, selfReq);
            var atModuleEnd = MethodRequest.Single("atModuleEnd", self);
            if (mod.RespondsTo(atModuleEnd))
                self.SetFlag(GraceObject.Flags.RunAtModuleEnd);
            return GraceObject.Done;
        }
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Dialect:");
            tw.WriteLine(prefix + "    " + Path);
        }
    }

    public class ImportNode : Node
    {

        private Node type;
        private ImportParseNode origin;

        public Node Type { get { return type; } }

        internal ImportNode(Token location, ImportParseNode source,
                Node type)
            : base(location, source)
        {
            this.type = type;
            this.origin = source;
        }

        public string Path
        {
            get
            {
                return (origin.path as StringLiteralParseNode).value;
            }
        }

        public string Name
        {
            get
            {
                return (origin.name as IdentifierParseNode).name;
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var mod = ctx.LoadModule(Path);
            var meth = ctx.AddDef(Name, mod);
            return GraceObject.Done;
        }
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Import:");
            tw.WriteLine(prefix + "  Path:");
            tw.WriteLine(prefix + "    " + Path);
            tw.WriteLine(prefix + "  As:");
            tw.WriteLine(prefix + "    " + Name);
            if (type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                type.DebugPrint(tw, prefix + "    ");
            }
        }
    }

    public class ExplicitReceiverRequestNode : RequestNode
    {
        private Node receiver;
        internal ExplicitReceiverRequestNode(Token location,
          ParseNode source,
          Node receiver)
            : base(location, source)
        {
            this.receiver = receiver;
        }
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "ExplicitReceiverRequest: " + Name);
            tw.WriteLine(prefix + "  Receiver:");
            receiver.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Parts:");
            int i = 1;
            foreach (RequestPartNode p in parts)
            {
                string partName = p.Name;
                tw.WriteLine(prefix + "    Part " + i + ": ");
                tw.WriteLine(prefix + "      Name: " + p.Name);
                if (p.GenericArguments.Count != 0)
                {
                    tw.WriteLine(prefix + "      Generic arguments:");
                    foreach (Node arg in p.GenericArguments)
                        arg.DebugPrint(tw, prefix + "        ");
                }
                if (p.Arguments.Count != 0)
                {
                    tw.WriteLine(prefix + "      Arguments:");
                    foreach (Node arg in p.Arguments)
                        arg.DebugPrint(tw, prefix + "        ");
                }
                i++;
            }
        }
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            GraceObject rec = receiver.Evaluate(ctx);
            MethodRequest req = new MethodRequest();
            var rirq = receiver as ImplicitReceiverRequestNode;
            if (rirq != null)
            {
                if (rirq.Name == "self")
                {
                    req.IsInterior = true;
                }
            }
            foreach (RequestPartNode rpn in this)
            {
                List<GraceObject> generics = new List<GraceObject>();
                List<GraceObject> arguments = new List<GraceObject>();
                foreach (Node n in rpn.GenericArguments)
                    generics.Add(n.Evaluate(ctx));
                foreach (Node n in rpn.Arguments)
                    arguments.Add(n.Evaluate(ctx));
                RequestPart rp = new RequestPart(rpn.Name, generics, arguments);
                req.AddPart(rp);
            }
            string m = "";
            int l = 0;
            if (Location != null)
            {
                m = Location.Module;
                l = Location.line;
            }
            int start = ctx.NestRequest(m, l, req.Name);
            try
            {
                return rec.Request(ctx, req);
            }
            finally
            {
                ctx.PopCallStackTo(start);
            }
        }
    }

    public class ImplicitReceiverRequestNode : RequestNode
    {
        internal ImplicitReceiverRequestNode(Token location, ParseNode source)
            : base(location, source)
        {

        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "ImplicitReceiverRequest: " + Name);
            if (parts.Count == 1)
            {
                if (parts[0].Arguments.Count == 0
                    && parts[0].GenericArguments.Count == 0)
                    return;
            }
            tw.WriteLine(prefix + "  Parts:");
            int i = 1;
            foreach (RequestPartNode p in parts)
            {
                string partName = p.Name;
                tw.WriteLine(prefix + "    Part " + i + ": ");
                tw.WriteLine(prefix + "      Name: " + p.Name);
                if (p.GenericArguments.Count != 0)
                {
                    tw.WriteLine(prefix + "      Generic arguments:");
                    foreach (Node arg in p.GenericArguments)
                        arg.DebugPrint(tw, prefix + "        ");
                }
                if (p.Arguments.Count != 0)
                {
                    tw.WriteLine(prefix + "      Arguments:");
                    foreach (Node arg in p.Arguments)
                        arg.DebugPrint(tw, prefix + "        ");
                }
                i++;
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            MethodRequest req = new MethodRequest();
            req.IsInterior = true;
            foreach (RequestPartNode rpn in this)
            {
                List<GraceObject> generics = new List<GraceObject>();
                List<GraceObject> arguments = new List<GraceObject>();
                foreach (Node n in rpn.GenericArguments)
                    generics.Add(n.Evaluate(ctx));
                foreach (Node n in rpn.Arguments)
                    arguments.Add(n.Evaluate(ctx));
                RequestPart rp = new RequestPart(rpn.Name, generics, arguments);
                req.AddPart(rp);
            }
            GraceObject rec = ctx.FindReceiver(req);
            if (rec == null)
            {
                ctx.DebugScopes();
                ErrorReporting.RaiseError(ctx, "R2002",
                        new Dictionary<string, string>() {
                            { "method", Name }
                        },
                        "LookupError: No receiver found for ${method}"
                );
            }
            string m = "";
            int l = 0;
            if (Location != null)
            {
                m = Location.Module;
                l = Location.line;
            }
            int start = ctx.NestRequest(m, l, req.Name);
            try
            {
                return rec.Request(ctx, req);
            }
            finally
            {
                ctx.PopCallStackTo(start);
            }
        }
    }

    public abstract class RequestNode : Node, IEnumerable<RequestPartNode>
    {

        private string name = "";
        protected List<RequestPartNode> parts;

        internal RequestNode(Token location,
                ParseNode source)
            : base(location, source)
        {
            this.parts = new List<RequestPartNode>();
        }

        public void MakeBind(Node val)
        {
            name += ":=";
            parts[0].MakeBind();
            parts[0].Arguments.Add(val);
        }

        public void AddPart(RequestPartNode part)
        {
            parts.Add(part);
            if (name.Length > 0)
                name += " ";
            name += part.Name;
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public IEnumerator<RequestPartNode> GetEnumerator()
        {
            foreach (RequestPartNode p in parts)
            {
                yield return p;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }

    public class RequestPartNode
    {
        private string name;
        private List<Node> generics;
        private List<Node> arguments;

        internal RequestPartNode(string name, List<Node> generics, List<Node> arguments)
        {
            this.name = name;
            this.generics = generics;
            this.arguments = arguments;
        }

        public void MakeBind()
        {
            name += ":=";
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public List<Node> GenericArguments
        {
            get
            {
                return generics;
            }
        }

        public List<Node> Arguments
        {
            get
            {
                return arguments;
            }
        }

    }

    public class ObjectConstructorNode : Node
    {
        private List<Node> body = new List<Node>();
        private Dictionary<string, MethodNode> methods = new Dictionary<string, MethodNode>();

        internal ObjectConstructorNode(Token token, ParseNode source)
            : base(token, source)
        {

        }

        public void Add(Node node)
        {
            MethodNode meth = node as MethodNode;
            if (meth == null)
                body.Add(node);
            else
            {
                methods[meth.Name] = meth;
            }
        }

        public List<Node> Body
        {
            get
            {
                return body;
            }
        }
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "ObjectConstructor:");
            tw.WriteLine(prefix + "  Methods:");
            foreach (string mn in methods.Keys)
            {
                methods[mn].DebugPrint(tw, prefix + "    ");
            }
            tw.WriteLine(prefix + "  Initialisation code:");
            foreach (Node n in body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            GraceObject ret = new GraceObject();
            ret.SetFlag(GraceObject.Flags.UserspaceObject);
            ctx.Extend(ret);
            LocalScope local = new LocalScope("object-inner");
            ctx.ExtendMinor(local);
            local.AddLocalDef("self", ret);
            ret.RememberScope(ctx);
            foreach (MethodNode m in methods.Values)
                ret.AddMethod(m);
            foreach (Node n in body)
            {
                n.Evaluate(ctx);
            }
            if (ret.HasFlag(GraceObject.Flags.RunAtModuleEnd))
            {
                var dialectReq = MethodRequest.Nullary("dialect");
                var dialect = ctx.FindReceiver(dialectReq).Request(ctx, dialectReq);
                var atModuleEnd = MethodRequest.Single("atModuleEnd", ret);
                dialect.Request(ctx, atModuleEnd);
            }
            ctx.Unextend(local);
            ctx.Unextend(ret);
            return ret;
        }
    }

    public class MethodNode : Node, IEnumerable<RequestPartNode>
    {
        private List<RequestPartNode> parts = new List<RequestPartNode>();
        private List<Node> body = new List<Node>();
        private string name = "";
        private Node returnType;
        public bool Confidential { get; set; }

        internal MethodNode(Token token, ParseNode source)
            : base(token, source)
        {

        }

        public void AddPart(RequestPartNode part)
        {
            parts.Add(part);
            if (name.Length > 0)
                name += " ";
            name += part.Name;
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public IEnumerator<RequestPartNode> GetEnumerator()
        {
            foreach (RequestPartNode p in parts)
            {
                yield return p;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(Node node)
        {
            body.Add(node);
        }

        public List<Node> Body
        {
            get
            {
                return body;
            }
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Method: " + Name);
            if (returnType != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                returnType.DebugPrint(tw, prefix + "    ");
            }
            if (Confidential)
            {
                tw.WriteLine(prefix + "  Is: Confidential");
            }
            else
            {
                tw.WriteLine(prefix + "  Is: Public");
            }
            tw.WriteLine(prefix + "  Parts:");
            int i = 1;
            foreach (RequestPartNode p in parts)
            {
                string partName = p.Name;
                tw.WriteLine(prefix + "    Part " + i + ": ");
                tw.WriteLine(prefix + "      Name: " + partName);
                tw.WriteLine(prefix + "      Generic parameters:");
                foreach (Node arg in p.GenericArguments)
                    arg.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (Node arg in p.Arguments)
                    arg.DebugPrint(tw, prefix + "        ");
                i++;
            }
            tw.WriteLine(prefix + "  Body:");
            foreach (Node n in body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }

        protected virtual void checkAccessibility(EvaluationContext ctx,
                MethodRequest req)
        {
            if (Confidential && !req.IsInterior)
            {
                ErrorReporting.RaiseError(ctx, "R2003",
                        new Dictionary<string, string>() {
                            { "method", req.Name }
                        },
                        "AccessibilityError: Method ${method} is confidential"
                );
            }
        }

        public virtual GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            checkAccessibility(ctx, req);
            GraceObject ret = null;
            Interpreter.ScopeMemo memo = ctx.Memorise();
            var myScope = new MethodScope(req.Name);
            foreach (var pp in parts.Zip(req, (dp, rp) => new { mine = dp, req = rp }))
            {
                foreach (var arg in pp.mine.Arguments.Zip(pp.req.Arguments, (a, b) => new { name = a, val = b }))
                {
                    var idNode = arg.name as ParameterNode;
                    string name = idNode.Name;
                    if (idNode.Variadic)
                    {
                        var gvl = new GraceVariadicList();
                        for (var i = pp.mine.Arguments.Count - 1;
                                i < pp.req.Arguments.Count;
                                i++)
                        {
                            gvl.Add(pp.req.Arguments[i]);
                        }
                        myScope.AddLocalDef(name, gvl);
                    }
                    else
                    {
                        myScope.AddLocalDef(name, arg.val);
                    }
                }
                for (var i = 0; i < pp.mine.GenericArguments.Count; i++)
                {
                    var g = pp.mine.GenericArguments[i];
                    GraceObject val;
                    if (i < pp.req.GenericArguments.Count)
                    {
                        var a = pp.req.GenericArguments[i];
                        val = a;
                    }
                    else
                    {
                        val = GraceType.Unknown;
                    }
                    string name = ((IdentifierNode)g).Name;
                    myScope.AddLocalDef(name, val);
                }
                foreach (var arg in pp.mine.GenericArguments.Zip(pp.req.GenericArguments, (a, b) => new { name = a, val = b }))
                {
                    string name = (arg.name as IdentifierNode).Name;
                    myScope.AddLocalDef(name, arg.val);
                }
            }
            ctx.Extend(myScope);
            try
            {
                foreach (Node n in body)
                {
                    ret = n.Evaluate(ctx);
                }
            }
            catch (ReturnException re)
            {
                if (!re.IsFromScope(myScope))
                    throw;
                ctx.Unextend(myScope);
                ctx.RestoreExactly(memo);
                return re.Value;
            }
            ctx.Unextend(myScope);
            ctx.RestoreExactly(memo);
            return ret;
        }
    }

    public class BlockNode : Node
    {
        private List<Node> parameters;
        private List<Node> body;

        internal BlockNode(Token token, ParseNode source,
                List<Node> parameters,
                List<Node> body)
            : base(token, source)
        {
            this.parameters = parameters;
            this.body = body;
        }

        public List<Node> Parameters
        {
            get
            {
                return parameters;
            }
        }

        public List<Node> Body
        {
            get
            {
                return body;
            }
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Block:");
            tw.WriteLine(prefix + "  Parameters:");
            foreach (Node arg in parameters)
                arg.DebugPrint(tw, prefix + "    ");
            tw.WriteLine(prefix + "  Body:");
            foreach (Node n in body)
            {
                n.DebugPrint(tw, prefix + "    ");
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            GraceBlock ret = GraceBlock.Create(ctx, parameters, body);
            return ret;
        }

        public virtual GraceObject Respond(EvaluationContext ctx, GraceObject self, MethodRequest req)
        {
            GraceObject ret = null;
            /*LocalScope myScope = new LocalScope(req.Name);
            foreach (var pp in parts.Zip(req, (dp, rp) => new { mine = dp, req = rp }))
            {
                foreach (var arg in pp.mine.Arguments.Zip(pp.req.Arguments, (a, b) => new { name = a, val = b }))
                {
                    string name = (arg.name as IdentifierNode).Name;
                    myScope.AddLocalDef(name, arg.val);
                }
            }
            ctx.Extend(myScope);
            foreach (Node n in body)
            {
                ret = n.Evaluate(ctx);
            }
            ctx.Unextend(myScope);*/
            return ret;
        }
    }


    public class NumberNode : Node
    {

        private NumberParseNode origin;
        int numbase = 10;
        double val;

        internal NumberNode(Token location, NumberParseNode source)
            : base(location, source)
        {
            origin = source;
            numbase = origin._base;
            if (numbase == 10)
                val = double.Parse(origin.digits);
            else
            {
                int integral = 0;
                double fractional = 0;
                double size = 1.0;
                bool frac = false;
                foreach (char c in origin.digits)
                {
                    if (c == '.')
                        frac = true;
                    else if (!frac)
                    {
                        integral *= numbase;
                        integral += digit(c);
                    }
                    else
                    {
                        size /= numbase;
                        fractional += size * digit(c);
                    }
                }
                val = integral + fractional;
            }
        }

        private static int digit(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'a' && c <= 'z')
            {
                return 10 + c - 'a';
            }
            if (c >= 'A' && c <= 'Z')
            {
                return 10 + c - 'A';
            }
            // Can't happen, checked in the lexer.
            return -1;
        }

        public double Value
        {
            get
            {
                return val;
            }
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string desc = "";
            if (origin._base == 10)
                desc += origin.digits;
            else if (origin._base == 16)
                desc += "0x" + origin.digits;
            else
                desc += origin._base + "x" + origin.digits;
            tw.WriteLine(prefix + "Number: " + desc + " (" + Value + ")");
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            //return new GraceObjectProxy(Value);
            return GraceNumber.Create(Value);
        }
    }

    public class StringLiteralNode : Node
    {

        private StringLiteralParseNode origin;
        internal StringLiteralNode(Token location, StringLiteralParseNode source)
            : base(location, source)
        {
            origin = source;
        }

        public string Value
        {
            get
            {
                return origin.value;
            }
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "String: " + Value);
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return GraceString.Create(Value);
        }
    }

    public class IdentifierNode : Node
    {

        private IdentifierParseNode origin;
        internal IdentifierNode(Token location, IdentifierParseNode source)
            : base(location, source)
        {
            origin = source;
        }

        public string Name
        {
            get
            {
                return origin.name;
            }
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Identifier: " + Name);
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }

    public class VarDeclarationNode : Node
    {

        private Node type;
        private VarDeclarationParseNode origin;

        public Node Type { get { return type; } }
        public bool Readable { get; set; }
        public bool Writable { get; set; }

        internal VarDeclarationNode(Token location,
                VarDeclarationParseNode source,
                Node val,
                Node type)
            : base(location, source)
        {
            this.type = type;
            Value = val;
            this.origin = source;
        }

        public Node Value { get; set; }

        public string Name
        {
            get
            {
                return (origin.name as IdentifierParseNode).name;
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            ReaderWriterPair pair;
            if (Value != null)
                pair = ctx.AddVar(Name, Value.Evaluate(ctx));
            else
                pair = ctx.AddVar(Name, GraceObject.Uninitialised);
            if (Readable)
                pair.Read.Confidential = false;
            if (Writable)
                pair.Write.Confidential = false;
            return GraceObject.Uninitialised;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "VarDeclaration:");
            tw.WriteLine(prefix + "  As:");
            tw.WriteLine(prefix + "    " + Name);
            if (type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                type.DebugPrint(tw, prefix + "    ");
            }
            if (Value != null)
            {
                tw.WriteLine(prefix + "  Value:");
                Value.DebugPrint(tw, prefix + "    ");
            }
        }
    }

    public class DefDeclarationNode : Node
    {

        private Node type;
        private DefDeclarationParseNode origin;

        public Node Type { get { return type; } }
        public bool Public { get; set; }

        internal DefDeclarationNode(Token location,
                DefDeclarationParseNode source,
                Node val,
                Node type)
            : base(location, source)
        {
            this.type = type;
            Value = val;
            this.origin = source;
        }

        public Node Value { get; set; }

        public string Name
        {
            get
            {
                return (origin.name as IdentifierParseNode).name;
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var meth = ctx.AddDef(Name, Value.Evaluate(ctx));
            if (Public)
                meth.Confidential = false;
            return GraceObject.Uninitialised;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "DefDeclaration:");
            tw.WriteLine(prefix + "  Name:");
            tw.WriteLine(prefix + "    " + Name);
            if (type != null)
            {
                tw.WriteLine(prefix + "  Type:");
                type.DebugPrint(tw, prefix + "    ");
            }
            if (Public)
            {
                tw.WriteLine(prefix + "  Public: yes");
            }
            if (Value != null)
            {
                tw.WriteLine(prefix + "  Value:");
                Value.DebugPrint(tw, prefix + "    ");
            }
        }
    }

    public class ReturnNode : Node
    {

        internal ReturnNode(Token location,
                ReturnParseNode source,
                Node val)
            : base(location, source)
        {
            Value = val;
        }

        public Node Value { get; set; }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            MethodScope ms = ctx.FindNearestMethod();
            ms.Return(Value.Evaluate(ctx));
            return GraceObject.Done;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Return:");
            if (Value != null)
            {
                tw.WriteLine(prefix + "  Value:");
                Value.DebugPrint(tw, prefix + "    ");
            }
        }
    }

    public class NoopNode : Node
    {

        internal NoopNode(Token location,
                ParseNode source)
            : base(location, source)
        {
        }


        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return GraceObject.Done;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Noop");
        }
    }

    public class TypeNode : Node
    {
        private List<MethodTypeNode> body = new List<MethodTypeNode>();

        public string Name { get; set; }

        internal TypeNode(Token token, ParseNode source)
            : base(token, source)
        {
            Name = "Anonymous";
        }

        public List<MethodTypeNode> Body
        {
            get
            {
                return body;
            }
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Type:");
            tw.WriteLine(prefix + "  Methods:");
            foreach (var meth in body)
            {
                meth.DebugPrint(tw, prefix + "    ");
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var ret = new GraceType(Name);
            foreach (var n in body)
                ret.Add(n);
            return ret;
        }
    }

    public class MethodTypeNode : Node, IEnumerable<RequestPartNode>
    {
        private List<RequestPartNode> parts = new List<RequestPartNode>();

        public Node Returns { get; set; }
        private string name = "";

        internal MethodTypeNode(Token token, ParseNode source)
            : base(token, source)
        {
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "MethodType: " + name);
            tw.WriteLine(prefix + "  Parts:");
            int i = 1;
            foreach (RequestPartNode p in parts)
            {
                string partName = p.Name;
                tw.WriteLine(prefix + "    Part " + i + ": ");
                tw.WriteLine(prefix + "      Name: " + partName);
                tw.WriteLine(prefix + "      Generic parameters:");
                foreach (Node arg in p.GenericArguments)
                    arg.DebugPrint(tw, prefix + "        ");
                tw.WriteLine(prefix + "      Parameters:");
                foreach (Node arg in p.Arguments)
                    arg.DebugPrint(tw, prefix + "        ");
                i++;
            }
            if (Returns != null)
            {
                tw.WriteLine(prefix + "  Returns:");
                Returns.DebugPrint(tw, prefix + "    ");
            }
        }

        public void AddPart(RequestPartNode part)
        {
            parts.Add(part);
            if (name.Length > 0)
                name += " ";
            name += part.Name;
        }

        public string Name
        {
            get
            {
                return name;
            }
        }

        public IEnumerator<RequestPartNode> GetEnumerator()
        {
            foreach (RequestPartNode p in parts)
            {
                yield return p;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return GraceObject.Done;
        }
    }

    public class ParameterNode : IdentifierNode
    {

        public Node Type { get; set; }
        public bool Variadic { get; private set; }

        internal ParameterNode(Token location, IdentifierParseNode source)
            : base(location, source)
        {
        }

        internal ParameterNode(Token location, IdentifierParseNode source,
                Node type)
            : base(location, source)
        {
            Type = type;
        }

        internal ParameterNode(Token location, IdentifierParseNode source,
                bool variadic,
                Node type)
            : base(location, source)
        {
            Variadic = variadic;
            Type = type;
        }

        internal ParameterNode(Token location, IdentifierParseNode source,
                bool variadic)
            : base(location, source)
        {
            Variadic = variadic;
        }

        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Parameter: " + Name);
            tw.WriteLine(prefix + "  Variadic: " + Variadic);
            if (Type != null)
            {
                tw.WriteLine(prefix + "  Type: ");
                Type.DebugPrint(tw, prefix + "    ");
            }
        }

        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }

}
