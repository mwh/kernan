using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Parsing;
using Grace.Runtime;

namespace Grace.Execution
{

    /// <summary>An abstract executable representation of a piece of
    /// source code</summary>
    public abstract class Node
    {
        /// <summary>The original source code location whence this
        /// Node originate</summary>
        internal Token Location;
        private ParseNode parseNode;
        /// <summary>The ParseNode whence this Node originated</summary>
        /// <value>This property gets the value of the field parseNode</value>
        public ParseNode Origin
        {
            get
            {
                return parseNode;
            }
        }

        /// <param name="location">Token spawning this node</param>
        /// <param name="source">ParseNode spawning this node</param>
        internal Node(Token location, ParseNode source)
        {
            this.Location = location;
            this.parseNode = source;
        }

        /// <param name="source">ParseNode spawning this node</param>
        internal Node(ParseNode source)
        {
            this.parseNode = source;
        }

        /// <summary>Execute this node and return the resulting value</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <returns>Result of evaluating node in the given context</returns>
        public abstract GraceObject Evaluate(EvaluationContext ctx);
        /// <summary>Writes a textual debugging representation of this node
        /// </summary>
        /// <param name="tw">Destination for debugging string</param>
        /// <param name="prefix">String to prepend to each line</param>
        public abstract void DebugPrint(System.IO.TextWriter tw, string prefix);
    }

    /// <summary>A dialect statement</summary>
    public class DialectNode : Node
    {

        private DialectParseNode origin;
        internal DialectNode(Token location, DialectParseNode source)
            : base(location, source)
        {
            origin = source;
        }

        /// <summary>Module path</summary>
        /// <value>This property gets the string value of the
        /// path field of the originating parse node</value>
        public string Path
        {
            get
            {
                return (origin.Path as StringLiteralParseNode).Value;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Dialect:");
            tw.WriteLine(prefix + "    " + Path);
        }
    }

    /// <summary>An import statement</summary>
    public class ImportNode : Node
    {

        private Node type;
        private ImportParseNode origin;

        /// <summary>Type annotation of the import statement</summary>
        /// <value>This property gets the value of the type field</value>
        public Node Type { get { return type; } }

        internal ImportNode(Token location, ImportParseNode source,
                Node type)
            : base(location, source)
        {
            this.type = type;
            this.origin = source;
        }

        /// <summary>Module path</summary>
        /// <value>This property gets the string value of the
        /// path field of the originating parse node</value>
        public string Path
        {
            get
            {
                return (origin.Path as StringLiteralParseNode).Value;
            }
        }

        /// <summary>Bound name</summary>
        /// <value>This property gets the string value of the
        /// name field of the originating parse node</value>
        public string Name
        {
            get
            {
                return (origin.Name as IdentifierParseNode).Name;
            }
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var mod = ctx.LoadModule(Path);
            ctx.AddDef(Name, mod);
            return GraceObject.Done;
        }

        /// <inheritdoc/>
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

    /// <summary>A method request with a syntactic receiver</summary>
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
        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override GraceObject GetReceiver(EvaluationContext ctx,
                MethodRequest req)
        {
            GraceObject rec = receiver.Evaluate(ctx);
            var rirq = receiver as ImplicitReceiverRequestNode;
            if (rirq != null)
            {
                if (rirq.Name == "self")
                {
                    req.IsInterior = true;
                }
            }
            return rec;
        }

    }

    /// <summary>A method request with no syntactic receiver</summary>
    public class ImplicitReceiverRequestNode : RequestNode
    {
        internal ImplicitReceiverRequestNode(Token location, ParseNode source)
            : base(location, source)
        {

        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override GraceObject GetReceiver(EvaluationContext ctx,
                MethodRequest req)
        {
            GraceObject rec = ctx.FindReceiver(req);
            req.IsInterior = true;
            return rec;
        }
        /// <inheritdoc/>
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

    /// <summary>A method request, either explicit or implicit</summary>
    public abstract class RequestNode : Node, IEnumerable<RequestPartNode>
    {

        private string name = "";
        /// <summary>The name parts making up this request</summary>
        protected List<RequestPartNode> parts;

        internal RequestNode(Token location,
                ParseNode source)
            : base(location, source)
        {
            this.parts = new List<RequestPartNode>();
        }

        /// <summary>Make this request into a := bind request</summary>
        /// <param name="val">Value to assign</param>
        public void MakeBind(Node val)
        {
            name += ":=";
            parts[0].MakeBind();
            parts[0].Arguments.Add(val);
        }

        /// <summary>Add another part to this request</summary>
        /// <param name="part">Part to append</param>
        public void AddPart(RequestPartNode part)
        {
            parts.Add(part);
            if (name.Length > 0)
                name += " ";
            name += part.Name;
        }

        /// <summary>The name of the method being requested</summary>
        /// <value>This property gets the value of the field name</value>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>Get an enumerator giving each part of this request
        /// in turn</summary>
        public IEnumerator<RequestPartNode> GetEnumerator()
        {
            foreach (RequestPartNode p in parts)
            {
                yield return p;
            }
        }

        /// <summary>Get an enumerator giving each part of this request
        /// in turn</summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Get the dynamic receiver for this request</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Dynamic request under construction</param>
        protected abstract GraceObject GetReceiver(EvaluationContext ctx,
                MethodRequest req);

        private MethodRequest createRequest(EvaluationContext ctx)
        {
            MethodRequest req = new MethodRequest();
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
            return req;
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var req = createRequest(ctx);
            GraceObject rec = GetReceiver(ctx, req);
            return performRequest(ctx, rec, req);
        }

        private GraceObject performRequest(EvaluationContext ctx,
                GraceObject rec, MethodRequest req)
        {
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

        /// <summary>
        /// Make this request as the target of an inherits clause
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="inheritor">Object inheriting from this request</param>
        public virtual GraceObject Inherit(EvaluationContext ctx,
                GraceObject inheritor)
        {
            var req = createRequest(ctx);
            req.IsInherits = true;
            req.InheritingObject = inheritor;
            var rec = GetReceiver(ctx, req);
            return performRequest(ctx, rec, req);
        }

    }

    /// <summary>A part of a method name and its arguments</summary>
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

        /// <summary>Make this part into a := bind request part</summary>
        public void MakeBind()
        {
            name += ":=";
        }

        /// <summary>The name of this part</summary>
        /// <value>This property gets the string field name</value>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>Generic arguments to this part</summary>
        /// <value>This property gets the field generics</value>
        public List<Node> GenericArguments
        {
            get
            {
                return generics;
            }
        }

        /// <summary>Ordinary arguments to this part</summary>
        /// <value>This property gets the field arguments</value>
        public List<Node> Arguments
        {
            get
            {
                return arguments;
            }
        }

    }

    /// <summary>An object constructor expression</summary>
    public class ObjectConstructorNode : Node
    {
        private List<Node> body = new List<Node>();
        private Dictionary<string, MethodNode> methods = new Dictionary<string, MethodNode>();

        internal ObjectConstructorNode(Token token, ParseNode source)
            : base(token, source)
        {

        }

        /// <summary>Add a new method or statement to the body of this
        /// object</summary>
        /// <param name="node">Node to add</param>
        public void Add(Node node)
        {
            MethodNode meth = node as MethodNode;
            if (meth == null)
                body.Add(node);
            else
            {
                methods[meth.Name] = meth;
                if (meth.Fresh)
                {
                    methods[meth.Name + " object"] = meth;
                }
            }
        }

        /// <summary>The body of this object constructor</summary>
        /// <value>This property gets the value of the field body</value>
        public List<Node> Body
        {
            get
            {
                return body;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
                if (n is InheritsNode)
                {
                    var i = (InheritsNode)n;
                    inherit(i.As, i.Inherit(ctx, ret), ret);
                }
                else {
                    n.Evaluate(ctx);
                }
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

        private void inherit(string name, GraceObject partObject,
                GraceObject ret)
        {
            ret.AddParent(name, partObject);
        }
    }

    /// <summary>A method declaration</summary>
    public class MethodNode : Node, IEnumerable<RequestPartNode>
    {
        private List<RequestPartNode> parts = new List<RequestPartNode>();
        private List<Node> body = new List<Node>();
        private string name = "";
        private Node returnType;

        /// <summary>Whether this method is confidential or not</summary>
        public bool Confidential { get; set; }

        /// <summary>Whether this method returns a fresh object or not</summary>
        public bool Fresh { get; set; }

        internal MethodNode(Token token, ParseNode source)
            : base(token, source)
        {

        }

        /// <summary>Add a part to this declaration</summary>
        /// <param name="part">Part to add</param>
        public void AddPart(RequestPartNode part)
        {
            parts.Add(part);
            if (name.Length > 0)
                name += " ";
            name += part.Name;
        }

        /// <summary>The name of this method</summary>
        /// <value>This property gets the value of the field name</value>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>Get an enumerator giving each part of this method
        /// in turn</summary>
        public IEnumerator<RequestPartNode> GetEnumerator()
        {
            foreach (RequestPartNode p in parts)
            {
                yield return p;
            }
        }

        /// <summary>Get an enumerator giving each part of this method
        /// in turn</summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>Add a node to the body of this method</summary>
        /// <param name="node">Node to add</param>
        public void Add(Node node)
        {
            body.Add(node);
        }

        /// <summary>The body of this method</summary>
        /// <value>This property gets the value of the field body</value>
        public List<Node> Body
        {
            get
            {
                return body;
            }
        }

        /// <inheritdoc/>
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
            if (Fresh)
            {
                tw.WriteLine(prefix + "  Fresh: Yes");
            }
            else
            {
                tw.WriteLine(prefix + "  Fresh: No");
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

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }

        /// <summary>Confirm that this method can be accessed through
        /// the given request in this context</summary>
        /// <remarks>If this method is confidential and the request is
        /// not an interior one with privileged access, this method
        /// will raise a Grace exception reporting an accessibility
        /// violation.</remarks>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request to check</param>
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

        /// <summary>Respond to a given request with a given binding of the
        /// receiver</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Receiver of the request</param>
        /// <param name="req">Request that accessed this method</param>
        /// <returns>The return value of this method within
        /// this context and with these arguments.</returns>
        public virtual GraceObject Respond(EvaluationContext ctx,
                GraceObject self, MethodRequest req)
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

    /// <summary>A block expression</summary>
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

        /// <summary>The parameters of this block</summary>
        /// <value>This property gets the value of the field parameters</value>
        public List<Node> Parameters
        {
            get
            {
                return parameters;
            }
        }

        /// <summary>The body of this block</summary>
        /// <value>This property gets the value of the field body</value>
        public List<Node> Body
        {
            get
            {
                return body;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            GraceBlock ret = GraceBlock.Create(ctx, parameters, body);
            return ret;
        }

    }

    /// <summary>A numeric literal</summary>
    public class NumberNode : Node
    {

        private NumberParseNode origin;
        int numbase = 10;
        double val;

        internal NumberNode(Token location, NumberParseNode source)
            : base(location, source)
        {
            origin = source;
            numbase = origin.NumericBase;
            if (numbase == 10)
                val = double.Parse(origin.Digits);
            else
            {
                int integral = 0;
                double fractional = 0;
                double size = 1.0;
                bool frac = false;
                foreach (char c in origin.Digits)
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

        /// <summary>The value of this literal as a double</summary>
        /// <value>This property gets the value of the field val</value>
        public double Value
        {
            get
            {
                return val;
            }
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            string desc = "";
            if (origin.NumericBase == 10)
                desc += origin.Digits;
            else if (origin.NumericBase == 16)
                desc += "0x" + origin.Digits;
            else
                desc += origin.NumericBase + "x" + origin.Digits;
            tw.WriteLine(prefix + "Number: " + desc + " (" + Value + ")");
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            //return new GraceObjectProxy(Value);
            return GraceNumber.Create(Value);
        }
    }

    /// <summary>A string literal</summary>
    public class StringLiteralNode : Node
    {

        private StringLiteralParseNode origin;
        internal StringLiteralNode(Token location, StringLiteralParseNode source)
            : base(location, source)
        {
            origin = source;
        }

        /// <summary>The string value of this literal</summary>
        /// <value>This property gets the value field of the
        /// originating parse node</value>
        public string Value
        {
            get
            {
                return origin.Value;
            }
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "String: " + Value);
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return GraceString.Create(Value);
        }
    }

    /// <summary>A bare identifier</summary>
    public class IdentifierNode : Node
    {

        private IdentifierParseNode origin;
        internal IdentifierNode(Token location, IdentifierParseNode source)
            : base(location, source)
        {
            origin = source;
        }

        /// <summary>The name of this identifier</summary>
        /// <value>This property gets the name field of the originating
        /// parse node</value>
        public string Name
        {
            get
            {
                return origin.Name;
            }
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Identifier: " + Name);
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }

    /// <summary>A var declaration</summary>
    public class VarDeclarationNode : Node
    {

        private Node type;
        private VarDeclarationParseNode origin;

        /// <summary>The type given to this var declaration</summary>
        /// <value>This property gets the value of the field type</value>
        public Node Type { get { return type; } }

        /// <summary>Whether this var is annotated readable</summary>
        public bool Readable { get; set; }

        /// <summary>Whether this var is annotated writable</summary>
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

        /// <summary>The initial value given in this var declaration</summary>
        public Node Value { get; set; }

        /// <summary>The name of this var declaration</summary>
        /// <value>This property accesses the name field of the originating
        /// parse node</value>
        public string Name
        {
            get
            {
                return (origin.Name as IdentifierParseNode).Name;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

    /// <summary>A def declaration</summary>
    public class DefDeclarationNode : Node
    {

        private Node type;
        private DefDeclarationParseNode origin;

        /// <summary>The type given to this def declaration</summary>
        /// <value>This property gets the value of the field type</value>
        public Node Type { get { return type; } }

        /// <summary>Whether this def is annotated public</summary>
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

        /// <summary>The initial value given in this def declaration</summary>
        public Node Value { get; set; }

        /// <summary>The name of this def declaration</summary>
        /// <value>This property accesses the name field of the originating
        /// parse node</value>
        public string Name
        {
            get
            {
                return (origin.Name as IdentifierParseNode).Name;
            }
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var meth = ctx.AddDef(Name, Value.Evaluate(ctx));
            if (Public)
                meth.Confidential = false;
            return GraceObject.Uninitialised;
        }

        /// <inheritdoc/>
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

    /// <summary>A return statement</summary>
    public class ReturnNode : Node
    {

        internal ReturnNode(Token location,
                ReturnParseNode source,
                Node val)
            : base(location, source)
        {
            Value = val;
        }

        /// <summary>The returned expression</summary>
        public Node Value { get; set; }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            MethodScope ms = ctx.FindNearestMethod();
            ms.Return(Value.Evaluate(ctx));
            return GraceObject.Done;
        }

        /// <inheritdoc/>
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

    /// <summary>A placeholder node with no effect</summary>
    public class NoopNode : Node
    {

        internal NoopNode(Token location,
                ParseNode source)
            : base(location, source)
        {
        }


        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return GraceObject.Done;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Noop");
        }
    }

    /// <summary>A type literal</summary>
    public class TypeNode : Node
    {
        private List<MethodTypeNode> body = new List<MethodTypeNode>();

        /// <summary>The name of this type literal for debugging</summary>
        public string Name { get; set; }

        internal TypeNode(Token token, ParseNode source)
            : base(token, source)
        {
            Name = "Anonymous";
        }

        /// <summary>The body of this type literal</summary>
        /// <value>This property gets the value of the field body</value>
        public List<MethodTypeNode> Body
        {
            get
            {
                return body;
            }
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Type:");
            tw.WriteLine(prefix + "  Methods:");
            foreach (var meth in body)
            {
                meth.DebugPrint(tw, prefix + "    ");
            }
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            var ret = new GraceType(Name);
            foreach (var n in body)
                ret.Add(n);
            return ret;
        }
    }

    /// <summary>A method type given in a type literal</summary>
    public class MethodTypeNode : Node, IEnumerable<RequestPartNode>
    {
        private List<RequestPartNode> parts = new List<RequestPartNode>();

        /// <summary>Declared return type of this method</summary>
        public Node Returns { get; set; }

        private string name = "";

        internal MethodTypeNode(Token token, ParseNode source)
            : base(token, source)
        {
        }

        /// <inheritdoc/>
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

        /// <summary>Add a part to this method name</summary>
        /// <param name="part">Part to add</param>
        public void AddPart(RequestPartNode part)
        {
            parts.Add(part);
            if (name.Length > 0)
                name += " ";
            name += part.Name;
        }

        /// <summary>Name of this method</summary>
        /// <value>This property gets the value of the field name</value>
        public string Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>Get an enumerator giving each part of this method
        /// in turn</summary>
        public IEnumerator<RequestPartNode> GetEnumerator()
        {
            foreach (RequestPartNode p in parts)
            {
                yield return p;
            }
        }

        /// <summary>Get an enumerator giving each part of this method
        /// in turn</summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return GraceObject.Done;
        }
    }

    /// <summary>A parameter a : b</summary>
    public class ParameterNode : IdentifierNode
    {

        /// <summary>The declared type on this parameter</summary>
        public Node Type { get; set; }

        /// <summary>Whether this parameter is variadic *x or not</summary>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }

    /// <summary>An inherits clause</summary>
    public class InheritsNode : Node
    {

        /// <summary>The request that is being inherited</summary>
        public Node From { get; private set; }

        /// <summary>Name given in an "as" clause</summary>
        public string As { get; private set; }

        internal InheritsNode(Token location, InheritsParseNode source,
                Node from)
            : base(location, source)
        {
            From = from;
            As = "super";
        }

        /// <summary>Inherit this request into an object</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="self">Inheriting object</param>
        public GraceObject Inherit(EvaluationContext ctx, GraceObject self)
        {
            var f = From as RequestNode;
            return f.Inherit(ctx, self);
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Inherits: ");
            From.DebugPrint(tw, prefix + "    ");
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }

}
