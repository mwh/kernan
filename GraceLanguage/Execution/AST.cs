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
            var rirq = receiver as ImplicitReceiverRequestNode;
            GraceObject rec;
            if (rirq != null)
            {
                if (rirq.Name == "self")
                {
                    if (req == null)
                        return null;
                    req.IsInterior = true;
                    rec = receiver.Evaluate(ctx);
                }
                else if (rirq.Name == "outer")
                {
                    if (req == null)
                        return null;
                    req.IsInterior = true;
                    rec = ctx.FindReceiver(req, 1);
                    if (rec == null)
                    {
                        NestRequest(ctx, req);
                        ErrorReporting.RaiseError(ctx, "R2002",
                                new Dictionary<string, string> {
                                    { "method", Name },
                                    { "found", "" },
                                    { "bind", "no" }
                                },
                                "LookupError: No receiver found for ${method}"
                            );
                    }
                }
                else
                {
                    rec = receiver.Evaluate(ctx);
                }
            }
            else
            {
                rec = receiver.Evaluate(ctx);
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
            if (req == null)
                return null;
            GraceObject rec = ctx.FindReceiver(req);
            if (rec == null)
            {
                NestRequest(ctx, req);
                if (req[req.Count - 1].Name == ":=")
                {
                    var req2 = new MethodRequest();
                    for (int i = 0; i < req.Count() - 1; i++)
                        req2.AddPart(req[i]);
                    var rec2 = ctx.FindReceiver(req2);
                    if (rec2 != null)
                    {
                        ErrorReporting.RaiseError(ctx, "R2002",
                                new Dictionary<string, string> {
                                    { "method", Name },
                                    { "found", req2.Name },
                                    { "bind", "yes" }
                                },
                                "LookupError: No receiver found for ${method}"
                        );
                    }
                }
                ctx.DebugScopes();
                ErrorReporting.RaiseError(ctx, "R2002",
                        new Dictionary<string, string> {
                            { "method", Name },
                            { "found", "" },
                            { "bind", "no" }
                        },
                        "LookupError: No receiver found for ${method}"
                );
            }
            req.IsInterior = true;
            return rec;
        }
    }

    /// <summary>A method request on the inbuilt prelude</summary>
    public class PreludeRequestNode : RequestNode
    {
        internal PreludeRequestNode(Token location, ParseNode source)
            : base(location, source)
        {

        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "PreludeRequest: " + Name);
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
            return ctx.Prelude;
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
            var rpn = new RequestPartNode(":=", new List<Node>(),
                    new List<Node> { val });
            AddPart(rpn);
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

        /// <summary>
        /// Make a MethodRequest representing this request.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        protected MethodRequest createRequest(EvaluationContext ctx)
        {
            MethodRequest req = new MethodRequest(name);
            foreach (RequestPartNode rpn in this)
            {
                var generics = rpn.GenericArguments.Count > 0
                    ? new List<GraceObject>()
                    : RequestPart.EmptyList;
                var arguments = rpn.Arguments.Count > 0
                    ? new List<GraceObject>()
                    : RequestPart.EmptyList;
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
            GraceObject rec = GetReceiver(ctx, null);
            var req = createRequest(ctx);
            if (rec == null)
                rec = GetReceiver(ctx, req);
            return performRequest(ctx, rec, req);
        }

        /// <summary>
        /// Notify interpreter of this request for backtrace purposes.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="req">Request being made</param>
        /// <returns>Depth of method nesting</returns>
        protected int NestRequest(EvaluationContext ctx, MethodRequest req)
        {
            string m = "";
            int l = 0;
            if (Location != null)
            {
                m = Location.Module;
                l = Location.line;
            }
            return ctx.NestRequest(m, l, req.Name);
        }

        /// <summary>
        /// Perform the request described by the parameters.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="rec">Receiver of the method request</param>
        /// <param name="req">Request being made</param>
        protected GraceObject performRequest(EvaluationContext ctx,
                GraceObject rec, MethodRequest req)
        {
            int start = NestRequest(ctx, req);
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
        /// <param name="inheritor">Part-object immediately inheriting this one
        /// </param>
        /// <param name="parentName">"as" name of inherits clause</param>
        /// <param name="self">Binding of self</param>
        public virtual GraceObject Inherit(EvaluationContext ctx,
                GraceObject inheritor, string parentName, GraceObject self)
        {
            var req = createRequest(ctx);
            req.IsInherits = true;
            req.InheritingObject = inheritor;
            req.InheritingName = parentName;
            req.InheritingSelf = self;
            var rec = GetReceiver(ctx, req);
            return performRequest(ctx, rec, req);
        }

    }

    /// <summary>Specialisation for if-then requests</summary>
    public class IfThenRequestNode : ImplicitReceiverRequestNode
    {
        private bool defer;
        private bool found;
        private bool needsScope;

        /// <inheritdoc />
        internal IfThenRequestNode(Token location, ParseNode source)
            : base(location, source) {}

        /// <inheritdoc />
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            if (defer)
                return base.Evaluate(ctx);
            var block = parts[1].Arguments[0] as BlockNode;
            if (block == null)
            {
                defer = true;
                return base.Evaluate(ctx);
            }
            if (!found)
            {
                var req = createRequest(ctx);
                var r = GetReceiver(ctx, req);
                if (r == ctx.Prelude)
                {
                    found = true;
                    foreach (var n in block.Body)
                    {
                        if (n is VarDeclarationNode
                                || n is DefDeclarationNode)
                        {
                            needsScope = true;
                        }
                    }
                }
                else
                    defer = true;
                return base.performRequest(ctx, r, req);
            }
            var test = parts[0].Arguments[0].Evaluate(ctx);
            var b = test as GraceBoolean;
            if (b == null)
            {
                defer = true;
                // FIXME This will reevaluate the condition!
                return base.Evaluate(ctx);
            }
            if (b == GraceBoolean.True)
            {
                if (needsScope)
                {
                    var myScope = new LocalScope();
                    ctx.Extend(myScope);
                    foreach (var n in block.Body)
                        n.Evaluate(ctx);
                    ctx.Unextend(myScope);
                    return GraceObject.Done;
                }
                foreach (var n in block.Body)
                    n.Evaluate(ctx);
            }
            return GraceObject.Done;
        }

        /// <inheritdoc />
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "IfThenRequest: " + Name);
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

    }

    /// <summary>Specialisation for for-do requests</summary>
    public class ForDoRequestNode : ImplicitReceiverRequestNode
    {
        private bool defer;
        private bool found;

        /// <inheritdoc />
        internal ForDoRequestNode(Token location, ParseNode source)
            : base(location, source) {}

        /// <inheritdoc />
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            if (defer)
                return base.Evaluate(ctx);
            var block = parts[1].Arguments[0] as BlockNode;
            if (block == null || block.Parameters.Count != 1)
            {
                defer = true;
                return base.Evaluate(ctx);
            }
            if (!found)
            {
                var req = createRequest(ctx);
                var r = GetReceiver(ctx, req);
                if (r == ctx.Prelude)
                {
                    found = true;
                }
                else
                    defer = true;
                return base.performRequest(ctx, r, req);
            }
            var iterable = parts[0].Arguments[0].Evaluate(ctx);
            var gr = iterable as GraceRange;
            if (gr != null)
            {
                var p = block.Parameters[0];
                var i = p as IdentifierNode;
                if (i == null)
                    goto end;
                if (gr.Step < 0)
                    goto end;
                string name = i.Name;
                for (double v = gr.Start; v <= gr.End; v += gr.Step)
                {
                    LocalScope l = new LocalScope();
                    l.AddLocalDef(name, GraceNumber.Create(v));
                    ctx.Extend(l);
                    foreach (var n in block.Body)
                        n.Evaluate(ctx);
                    ctx.Unextend(l);
                }
                return GraceObject.Done;
            }
end:
            var doReq = MethodRequest.Single("do", block.Evaluate(ctx));
            iterable.Request(ctx, doReq);
            return GraceObject.Done;
        }

        /// <inheritdoc />
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "ForDoRequest: " + Name);
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
        private bool containsInheritance;
        private List<InheritsNode> inheritsStatements =
            new List<InheritsNode>();
        private List<DefDeclarationNode> defs = new List<DefDeclarationNode>();
        private List<VarDeclarationNode> vars = new List<VarDeclarationNode>();

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
            var i = node as InheritsNode;
            var d = node as DefDeclarationNode;
            var v = node as VarDeclarationNode;
            if (i != null)
            {
                containsInheritance = true;
                inheritsStatements.Add(i);
            }
            if (d != null)
                defs.Add(d);
            if (v != null)
                vars.Add(v);
            if (meth == null)
                body.Add(node);
            else
            {
                methods[meth.Name] = meth;
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

        /// <summary>The methods of this object constructor</summary>
        /// <value>This property gets the value of the field methods</value>
        public Dictionary<string, MethodNode> Methods
        {
            get
            {
                return methods;
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

        /// <summary>Evaluate this object as the return of a
        /// fresh method being inherited</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="inheritor">Part-object immediately inheriting this one
        /// </param>
        /// <param name="parentName">"as" name of inherits clause</param>
        /// <param name="self">Binding of self</param>
        public GraceObject BeInherited(EvaluationContext ctx,
                GraceObject inheritor,
                string parentName,
                GraceObject self)
        {
            LocalScope local = new LocalScope("object-inner");
            GraceObject ret = new GraceObject(local, true);
            inheritor.AddParent(parentName, ret);
            ret.SetFlag(GraceObject.Flags.UserspaceObject);
            ctx.Extend(ret);
            ret.Identity = self;
            local.AddLocalDef("self", self);
            local.RedirectSurrounding = self;
            return basicEvaluate(ctx, ret, local, self);
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            LocalScope local = new LocalScope("object-inner");
            GraceObject ret = new GraceObject(local, true);
            ret.SetFlag(GraceObject.Flags.UserspaceObject);
            ctx.Extend(ret);
            local.AddLocalDef("self", ret);
            return basicEvaluate(ctx, ret, local, ret);
        }

        private GraceObject basicEvaluate(EvaluationContext ctx,
                GraceObject ret, LocalScope local,
                GraceObject self)
        {
            if (!containsInheritance)
                ret.AddParent("super", GraceObject.DefaultMethods);
            else
            {
                foreach (InheritsNode i in inheritsStatements)
                    if (i.As != null)
                        local.AddLocalDef(i.As, GraceObject.UninheritedParent);
            }
            foreach (var d in defs)
                ret.AddLocalDef(d.Name, GraceObject.Uninitialised);
            foreach (var v in vars)
                ret.AddLocalVar(v.Name, GraceObject.Uninitialised);
            ctx.ExtendMinor(local);
            ret.RememberScope(ctx);
            foreach (MethodNode m in methods.Values)
                ret.AddMethod(m);
            foreach (Node n in body)
            {
                if (n is InheritsNode)
                {
                    var i = (InheritsNode)n;
                    i.Inherit(ctx, ret, self);
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

    }

    /// <summary>A method declaration</summary>
    public class MethodNode : Node
    {
        private List<Node> body = new List<Node>();

        /// <summary>Signature of this method</summary>
        public SignatureNode Signature { get; set; }

        /// <summary>Whether this method is confidential or not</summary>
        public bool Confidential { get; set; }

        /// <summary>Whether this method returns a fresh object or not</summary>
        public bool Fresh { get; set; }

        /// <summary>
        /// Whether this method should be given the user-facing receiver
        /// (true) or the concrete part-object on which it was found
        /// (false)
        /// </summary>
        public bool UseRealReceiver { get; set; }

        internal MethodNode(Token token, ParseNode source)
            : base(token, source)
        {

        }

        /// <summary>The name of this method</summary>
        /// <value>This property gets the value of the field name</value>
        public string Name
        {
            get
            {
                return Signature.Name;
            }
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
            tw.WriteLine(prefix + "  Signature:");
            Signature.DebugPrint(tw, prefix + "    ");
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

        /// <summary>
        /// Check that the number of arguments provided is satisfactory,
        /// and report a runtime error if not.
        /// </summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="methodName">Name of method</param>
        /// <param name="partName">Name of part</param>
        /// <param name="need">Number of parameters of method</param>
        /// <param name="variadic">Whether this method is variadic</param>
        /// <param name="got">Number of arguments provided</param>
        public static void CheckArgCount(EvaluationContext ctx,
                string methodName, string partName, int need, bool variadic,
                int got)
        {
            if (!variadic && got > need)
                ErrorReporting.RaiseError(ctx, "R2006",
                        new Dictionary<string, string> {
                            { "method", methodName },
                            { "part",  partName },
                            { "need", need.ToString()},
                            { "have", got.ToString() }
                        },
                        "SurplusArgumentsError: Too many arguments for method"
                );
            if (got < need - (variadic ? 1 : 0))
                ErrorReporting.RaiseError(ctx, "R2004",
                        new Dictionary<string, string> {
                            { "method", methodName },
                            { "part",  partName },
                            { "need", variadic ? (need - 1) + "+"
                                               : need.ToString() },
                            { "have", got.ToString() }
                        },
                        "InsufficientArgumentsError: Not enough arguments for method"
                );
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
            foreach (var pp in Signature.Zip(req, (dp, rp) => new { mine = dp, req = rp }))
            {
                if (!(pp.mine is OrdinarySignaturePartNode))
                    throw new Exception("unimplemented - non-ordinary parts");
                var sigPart = (OrdinarySignaturePartNode)pp.mine;
                bool hadVariadic = false;
                foreach (var arg in sigPart.Parameters.Zip(pp.req.Arguments, (a, b) => new { name = a, val = b }))
                {
                    var idNode = (ParameterNode)arg.name;
                    string name = idNode.Name;
                    if (idNode.Variadic)
                    {
                        hadVariadic = true;
                        var gvl = new GraceVariadicList();
                        for (var i = sigPart.Parameters.Count - 1;
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
                if (!hadVariadic && sigPart.Parameters.Count > 0)
                {
                    var p = sigPart.Parameters.Last() as ParameterNode;
                    if (p != null)
                        hadVariadic |= p.Variadic;
                }
                CheckArgCount(ctx, req.Name, sigPart.Name,
                        sigPart.Parameters.Count, hadVariadic,
                        pp.req.Arguments.Count);
                if (sigPart.Parameters.Count > pp.req.Arguments.Count)
                {
                    // Variadic parameter with no arguments provided
                    // for it - fill with an empty list.
                    var arg = sigPart.Parameters[sigPart.Parameters.Count - 1];
                    var idNode = arg as ParameterNode;
                    string name = idNode.Name;
                    if (idNode.Variadic)
                    {
                        var gvl = new GraceVariadicList();
                        myScope.AddLocalDef(name, gvl);
                    }
                }
                for (var i = 0; i < sigPart.GenericParameters.Count; i++)
                {
                    var g = sigPart.GenericParameters[i];
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
                foreach (var arg in sigPart.GenericParameters.Zip(pp.req.GenericArguments, (a, b) => new { name = a, val = b }))
                {
                    string name = (arg.name as IdentifierNode).Name;
                    myScope.AddLocalDef(name, arg.val);
                }
            }
            ctx.Extend(myScope);
            try
            {
                if (req.IsInherits)
                {
                    for (var i=0; i<body.Count - 1; i++)
                    {
                        var n = body[i];
                        ret = n.Evaluate(ctx);
                    }
                    var last = body[body.Count - 1] as ObjectConstructorNode;
                    ret = last.BeInherited(ctx, req.InheritingObject,
                            req.InheritingName, req.InheritingSelf);
                }
                else
                {
                    foreach (Node n in body)
                    {
                        ret = n.Evaluate(ctx);
                    }
                }
            }
            catch (ReturnException re)
            {
                if (!re.IsFromScope(myScope))
                    throw;
                myScope.Complete();
                ctx.Unextend(myScope);
                ctx.RestoreExactly(memo);
                return re.Value;
            }
            myScope.Complete();
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
        private Node _forcedPattern;
        private bool variadic;

        internal BlockNode(Token token, ParseNode source,
                List<Node> parameters,
                List<Node> body,
                Node forcedPattern)
            : base(token, source)
        {
            this.parameters = parameters;
            this.body = body;
            _forcedPattern = forcedPattern;
            foreach (var p in parameters)
            {
                var param = p as ParameterNode;
                if (param != null)
                    variadic |= param.Variadic;
            }
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
            if (_forcedPattern != null)
            {
                tw.WriteLine(prefix + "  Pattern:");
                _forcedPattern.DebugPrint(tw, prefix + "    ");
            }
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
            if (_forcedPattern != null)
            {
                ret.ForcePattern(_forcedPattern.Evaluate(ctx));
            }
            ret.Variadic = variadic;
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
            return GraceObject.Done;
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
            return GraceObject.Done;
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
            if (Value != null)
                ms.Return(ctx, Value.Evaluate(ctx), this);
            else
                ms.Return(ctx, GraceObject.Done, this);
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
        private List<SignatureNode> body = new List<SignatureNode>();

        /// <summary>The name of this type literal for debugging</summary>
        public string Name { get; set; }

        internal TypeNode(Token token, ParseNode source)
            : base(token, source)
        {
            Name = "Anonymous";
        }

        /// <summary>The body of this type literal</summary>
        /// <value>This property gets the value of the field body</value>
        public List<SignatureNode> Body
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
        public string As { get; set; }

        internal InheritsNode(Token location, InheritsParseNode source,
                Node from)
            : base(location, source)
        {
            From = from;
            if (source.As != null)
                As = source.As.Name;
        }

        /// <summary>Inherit this request into an object</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="partObject">Inheriting object</param>
        /// <param name="self">Object identity</param>
        public GraceObject Inherit(EvaluationContext ctx,
                GraceObject partObject,
                GraceObject self)
        {
            var f = From as RequestNode;
            return f.Inherit(ctx, partObject, As, self);
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

    /// <summary>A method signature</summary>
    public class SignatureNode : Node, IEnumerable<SignaturePartNode>
    {

        /// <summary>Name of the method</summary>
        public string Name { get; private set; }

        /// <summary>Parts of the method name</summary>
        public IList<SignaturePartNode> Parts { get; private set; }

        /// <summary>
        /// True if this signature is an exact list of literal parts.
        /// </summary>
        public bool Linear = true;

        internal SignatureNode(Token location, SignatureParseNode source)
            : base(location, source)
        {
            Name = source.Name;
            Parts = new List<SignaturePartNode>();
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Signature: " + Name);
            foreach (var p in Parts)
                p.DebugPrint(tw, prefix + "    ");
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }

        /// <summary>Add a part to this method name</summary>
        public void AddPart(SignaturePartNode spn)
        {
            Parts.Add(spn);
            if (!(spn is OrdinarySignaturePartNode))
                Linear = false;
        }

        /// <summary>
        /// Get an enumerator giving each part of this signature in turn.
        /// </summary>
        public IEnumerator<SignaturePartNode> GetEnumerator()
        {
            foreach (var p in Parts)
            {
                yield return p;
            }
        }

        /// <summary>
        /// Get an enumerator giving each part of this signature in turn.
        /// </summary>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }


    /// <summary>
    /// A component of a method signature.
    /// </summary>
    public abstract class SignaturePartNode : Node
    {
        /// <summary>Name of the part</summary>
        public abstract string Name { get; }

        internal SignaturePartNode(Token location,
                SignaturePartParseNode source)
            : base(location, source)
        {
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "SignaturePart: " + Name);
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }
    /// <summary>A literal method signature part</summary>
    public class OrdinarySignaturePartNode : SignaturePartNode
    {

        private string _name;
        /// <summary>Name of the part</summary>
        public override string Name {
            get { return _name; }
        }

        /// <summary>Generic parameters of this part</summary>
        public IList<Node> GenericParameters { get; private set; }

        /// <summary>Ordinary parameters of this part</summary>
        public IList<Node> Parameters { get; private set; }

        internal OrdinarySignaturePartNode(Token location,
                OrdinarySignaturePartParseNode source,
                IList<Node> parameters,
                IList<Node> genericParameters)
            : base(location, source)
        {
            _name = source.Name;
            Parameters = parameters;
            GenericParameters = genericParameters;
        }

        /// <inheritdoc/>
        public override void DebugPrint(System.IO.TextWriter tw, string prefix)
        {
            tw.WriteLine(prefix + "Part: " + Name);
            foreach (var p in Parameters)
                p.DebugPrint(tw, prefix + "    ");
        }

        /// <inheritdoc/>
        public override GraceObject Evaluate(EvaluationContext ctx)
        {
            return null;
        }
    }

}
