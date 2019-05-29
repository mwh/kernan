using System;
using System.Collections.Generic;
using System.Linq;
using Grace.Execution;
using Grace.Parsing;

namespace Grace.Execution
{
    /// <summary>Translates a tree of ParseNodes into Nodes</summary>
    public class ExecutionTreeTranslator : ParseNodeVisitor<Node>
    {

        private ObjectConstructorNode module;

        /// <summary>Translate a tree rooted at a parse node for an
        /// object into the corresponding Node tree</summary>
        /// <param name="obj">Root of the tree</param>
        public Node Translate(ObjectParseNode obj)
        {
            return obj.Visit(this);
        }

        /// <summary>Default visit, which reports an error</summary>
        /// <inheritdoc />
        public Node Visit(ParseNode pn)
        {
            throw new Exception("No ParseNodeVisitor override provided for " + pn);
        }

        /// <inheritdoc />
        public Node Visit(ObjectParseNode obj)
        {
            var ret = new ObjectConstructorNode(obj.Token, obj);
            if (module == null)
                module = ret;
            foreach (ParseNode p in obj.Body)
            {
                var n = p.Visit<Node>(this);
                if (!(p is CommentParseNode))
                    ret.Add(n);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(NumberParseNode n)
        {
            return new NumberLiteralNode(n.Token, n);
        }

        /// <inheritdoc />
        public Node Visit(StringLiteralParseNode n)
        {
            return new StringLiteralNode(n.Token, n);
        }

        /// <inheritdoc />
        public Node Visit(InterpolatedStringParseNode n)
        {
            Node ret = null;
            foreach (ParseNode part in n.Parts)
            {
                if (ret == null)
                {
                    ret = part.Visit(this);
                }
                else
                {
                    var errn = new ExplicitReceiverRequestNode(n.Token, n,
                            ret);
                    var args = new List<Node>();
                    if (!(part is StringLiteralParseNode))
                    {
                        var rpnAS = new RequestPartNode("asString",
                                new List<Node>(), new List<Node>());
                        var errnAS = new ExplicitReceiverRequestNode(n.Token,
                                n, part.Visit(this));
                        errnAS.AddPart(rpnAS);
                        args.Add(errnAS);
                    }
                    else
                    {
                        args.Add(part.Visit(this));
                    }
                    var rpn = new RequestPartNode("++",
                            new List<Node>(), args);
                    errn.AddPart(rpn);
                    ret = errn;
                }
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(SignatureParseNode spn)
        {
            var ret = new SignatureNode(spn.Token, spn);
            foreach (var part in spn.Parts)
            {
                ret.AddPart((SignaturePartNode)part.Visit(this));
            }
            if (spn.ReturnType != null)
                ret.ReturnType = spn.ReturnType.Visit(this);
            addAnnotations(spn.Annotations, ret.Annotations);
            return ret;
        }

        private void addAnnotations(AnnotationsParseNode source,
                AnnotationsNode dest)
        {
            if (source != null)
                dest.AddAnnotations(from x in source.Annotations
                        select x.Visit(this));
        }

        /// <inheritdoc />
        public Node Visit(OrdinarySignaturePartParseNode osppn)
        {
            var parameters = new List<ParameterNode>();
            foreach (var p in osppn.Parameters)
            {
                // f
                var id = p as IdentifierParseNode;
                var tppn = p as TypedParameterParseNode;
                var vappn = p as VarArgsParameterParseNode;
                if (id != null)
                    parameters.Add(new ParameterNode(id.Token, id));
                else if (tppn != null)
                {
                    parameters.Add(new ParameterNode(tppn.Token,
                                tppn.Name as IdentifierParseNode,
                                tppn.Type.Visit(this)));
                }
                else if (vappn != null)
                {
                    // Inside could be either an identifier or a
                    // TypedParameterParseNode - check for both.
                    var inIPN = vappn.Name as IdentifierParseNode;
                    var inTPPN = vappn.Name as TypedParameterParseNode;
                    if (inIPN != null)
                        parameters.Add(new ParameterNode(inIPN.Token,
                                    inIPN,
                                    true // Variadic
                                    ));
                    else if (inTPPN != null)
                        parameters.Add(new ParameterNode(inTPPN.Token,
                                    inTPPN.Name as IdentifierParseNode,
                                    true, // Variadic
                                    inTPPN.Type.Visit(this)
                                    ));
                }
                else
                {
                    throw new Exception("unimplemented - unusual parameters");
                }
            }
            var generics = new List<Node>();
            foreach (var p in osppn.GenericParameters)
            {
                var id = p as IdentifierParseNode;
                if (id != null)
                {
                    generics.Add(new ParameterNode(id.Token, id));
                }
                else
                {
                    throw new Exception("unimplemented - bad generic parameters");
                }
            }
            if (osppn.Name.StartsWith("circumfix", StringComparison.Ordinal))
                return new OrdinarySignaturePartNode(osppn.Token, osppn,
                        parameters, generics, false);
            return new OrdinarySignaturePartNode(osppn.Token, osppn,
                    parameters, generics);
        }

        /// <inheritdoc />
        public Node Visit(MethodDeclarationParseNode d)
        {
            var ret = new MethodNode(d.Token, d);
            var sig = (SignatureNode)d.Signature.Visit(this);
            ret.Signature = sig;
            ret.Annotations = sig.Annotations;
            string name = sig.Name;
            ret.Confidential = (d.Signature.Annotations != null
                    && d.Signature.Annotations.HasAnnotation("confidential"));
            ret.Abstract = (d.Signature.Annotations != null
                    && d.Signature.Annotations.HasAnnotation("abstract"));
            // Indicate whether this method returns a fresh object
            ret.Fresh = (d.Body.Count > 0 && d.Body[d.Body.Count - 1] is
                    ObjectParseNode);
            // For each parameter, check against any given parameter types
            // Only run if this method does not return a fresh object, as
            // the try-finally decoration breaks it.
            var hasType = false;
            List<Tuple<string, Node>> matchResults = null;
            if (!ret.Fresh)
            {
                hasType = sig.ReturnType != null;
                matchResults = new List<Tuple<string, Node>>();
                foreach (var part in sig.Parts)
                {
                    var ospn = part as OrdinarySignaturePartNode;
                    foreach (var param in ospn.Parameters)
                    {
                        if (param.Type == null)
                            continue;
                        hasType = true;
                        var patName = "pattern " + param.Name;
                        var mrName = "match result " + param.Name;
                        var tmpName = "temp " + param.Name;
                        // def `pattern x` = String
                        var patDef = new DefDeclarationNode(d.Token, patName,
                            param.Type
                            );
                        var paramReq = new ImplicitReceiverRequestNode(d.Token, null);
                        paramReq.AddPart(new RequestPartNode(param.Name, new List<Node>(),
                            new List<Node>()));

                        var tmpReq = new ImplicitReceiverRequestNode(d.Token, null);
                        tmpReq.AddPart(new RequestPartNode(tmpName, new List<Node>(),
                            new List<Node>()));
                        var mrReq = new ImplicitReceiverRequestNode(d.Token, null);
                        mrReq.AddPart(new RequestPartNode(mrName, new List<Node>(),
                            new List<Node>()));
                        var mr = new ExplicitReceiverRequestNode(d.Token, null, param.Type);
                        mr.AddPart(new RequestPartNode("match", new List<Node>(),
                            new List<Node> { paramReq }));
                        // def `match result x` = `pattern x`.match(x)
                        var mrDef = new DefDeclarationNode(d.Token, mrName,
                            mr
                            );
                        // `match result x`.ifFalse { ArgumentTypeError... }
                        var pars = String.Join(", ",
                                from p in ospn.Parameters select p.Name);
                        var partDesc = ((OrdinarySignaturePartParseNode)part.Origin).Name + "(" + pars + ")";
                        var argumentTypeError = new ImplicitReceiverRequestNode(d.Token, null);
                        argumentTypeError.AddPart(
                                new RequestPartNode("ArgumentTypeError",
                                    new List<Node>(),
                                    new List<Node>()));
                        var raise = new ExplicitReceiverRequestNode(d.Token,
                                null, argumentTypeError);
                        // Error string
                        var pp = new ExplicitReceiverRequestNode(d.Token,
                                null,
                                new StringLiteralNode(d.Token,
                                        "argument for parameter "
                                        + param.Name
                                        + " of part "
                                        + partDesc
                                        + " does not meet type ")
                                );
                        pp.AddPart(new RequestPartNode("++",
                                    new List<Node>(),
                                    new List<Node> {
                                        new PrettyPrintNode(d.Token,
                                                param.Type)
                                    }
                                ));
                        pp = new ExplicitReceiverRequestNode(d.Token, null, pp);
                        pp.AddPart(new RequestPartNode("++",
                                    new List<Node>(),
                                    new List<Node> {
                                        new StringLiteralNode(d.Token,
                                                ": ")
                                    }
                                ));
                        var asString = new ExplicitReceiverRequestNode(d.Token,
                                null, paramReq);
                        asString.AddPart(new RequestPartNode("asString",
                                    new List<Node>(), new List<Node>()));
                        pp = new ExplicitReceiverRequestNode(d.Token, null, pp);
                        pp.AddPart(new RequestPartNode("++",
                                    new List<Node>(),
                                    new List<Node> {
                                        asString
                                    }
                                ));
                        // Actually reaise the error
                        raise.AddPart(new RequestPartNode("raise",
                                    new List<Node>(),
                                    new List<Node> {
                                        pp
                                    }));
                        var ifFalse = new ExplicitReceiverRequestNode(d.Token,
                                null, mrReq);
                        var falseBlock = new BlockNode(d.Token, null,
                                new List<Node>(), new List<Node> {
                                    raise
                                }, null);
                        ifFalse.AddPart(new RequestPartNode("ifFalse",
                                    new List<Node>(),
                                    new List<Node> {falseBlock}));
                        // def x = `match result x`.result
                        var resultReq = new ExplicitReceiverRequestNode(d.Token, null, mrReq);
                        resultReq.AddPart(new RequestPartNode("result", new List<Node>(),
                            new List<Node>()));
                        var replaceDef = new DefDeclarationNode(d.Token, param.Name,
                            resultReq
                            );
                        var testDef = new DefDeclarationNode(d.Token, "test" + param.Name,
                            resultReq
                            );
                        ret.Add(patDef);
                        ret.Add(mrDef);
                        ret.Add(ifFalse);
                        ret.Add(replaceDef);
                        ret.Add(testDef);
                        matchResults.Add(Tuple.Create(param.Name, (Node)mrReq));
                    }
                }
            }
            if (!hasType)
            {
                foreach (ParseNode p in d.Body)
                    if (!(p is CommentParseNode))
                        ret.Add(p.Visit(this));
            }
            else
            {
                // This means 1) not a fresh method, 2) at least one parameter had a type
                var body = new List<Node>();
                foreach (ParseNode p in d.Body)
                    if (!(p is CommentParseNode))
                        body.Add(p.Visit(this));
                if (sig.ReturnType != null && body.Count > 0)
                {
                    var last = body.Last();
                    body.RemoveAt(body.Count - 1);
                    var retDef = new DefDeclarationNode(last.Origin.Token, "return value", last);
                    var retReq = new ImplicitReceiverRequestNode(d.Token, null);
                    retReq.AddPart(new RequestPartNode("return value", new List<Node>(),
                        new List<Node>()));
                    var retMR = new ExplicitReceiverRequestNode(last.Origin.Token, null,
                        sig.ReturnType);
                    retMR.AddPart(new RequestPartNode("match", new List<Node>(),
                        new List<Node> { retReq }));
                    var mrDef = new DefDeclarationNode(last.Origin.Token,
                        "match result return value",
                        retMR
                    );
                    var mrReq = new ImplicitReceiverRequestNode(last.Origin.Token, null);
                    mrReq.AddPart(new RequestPartNode("match result return value",
                        new List<Node>(), new List<Node>()));
                    body.Add(retDef);
                    body.Add(mrDef);
                    var resultReq = new ExplicitReceiverRequestNode(last.Origin.Token,
                        null, mrReq);
                    resultReq.AddPart(new RequestPartNode("result", new List<Node>(),
                        new List<Node>()));
                    body.Add(resultReq);
                }
                var cleanupBody = new List<Node>();
                foreach (var tup in matchResults)
                {
                    var paramName = tup.Item1;
                    var mrReq = tup.Item2;
                    var cleanupReq = new ExplicitReceiverRequestNode(d.Token, null, mrReq);
                    cleanupReq.AddPart(new RequestPartNode("cleanup", new List<Node>(),
                        new List<Node>()));
                    cleanupBody.Add(cleanupReq);
                }
                var tryFinally = new ImplicitReceiverRequestNode(d.Token, null);
                tryFinally.AddPart(new RequestPartNode("try", new List<Node>(),
                    new List<Node> {
                    new BlockNode(d.Token, null, new List<Node>(), body, null)
                    }));
                tryFinally.AddPart(new RequestPartNode("finally", new List<Node>(),
                    new List<Node>
                    {
                    new BlockNode(d.Token, null, new List<Node>(), cleanupBody, null)
                    }));
                ret.Add(tryFinally);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(IdentifierParseNode i)
        {
            var ret = new ImplicitReceiverRequestNode(i.Token, i);
            var rpn = new RequestPartNode(i.Name, new List<Node>(), new List<Node>());
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ImplicitReceiverRequestParseNode irrpn)
        {
            ImplicitReceiverRequestNode ret = null;
            if (irrpn.Name == "if then")
            {
                ret = new IfThenRequestNode(irrpn.Token, irrpn);
            }
            else if (irrpn.Name == "for do")
            {
                ret = new ForDoRequestNode(irrpn.Token, irrpn);
            }
            else
            {
                ret = new ImplicitReceiverRequestNode(irrpn.Token, irrpn);
            }
            for (int i = 0; i < irrpn.Arguments.Count; i++)
            {
                var rpn = new RequestPartNode(
                        ((IdentifierParseNode)irrpn.NameParts[i]).Name,
                        map(irrpn.GenericArguments[i]),
                        map(irrpn.Arguments[i]));
                ret.AddPart(rpn);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ExplicitReceiverRequestParseNode irrpn)
        {
            var ret = new ExplicitReceiverRequestNode(irrpn.Token, irrpn,
                irrpn.Receiver.Visit(this));
            for (int i = 0; i < irrpn.Arguments.Count; i++)
            {
                var rpn = new RequestPartNode(
                    ((IdentifierParseNode)irrpn.NameParts[i]).Name,
                    map(irrpn.GenericArguments[i]),
                    map(irrpn.Arguments[i]));
                ret.AddPart(rpn);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(PrefixOperatorParseNode popn)
        {
            var ret = new ExplicitReceiverRequestNode(popn.Token, popn,
                popn.Receiver.Visit(this));
            var rpn = new RequestPartNode("prefix" + popn.Name,
                    new List<Node>(),
                    new List<Node>());
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(OperatorParseNode opn)
        {
            var ret = new ExplicitReceiverRequestNode(opn.Token, opn, opn.Left.Visit(this));
            var args = new List<Node>();
            args.Add(opn.Right.Visit(this));
            var rpn = new RequestPartNode(opn.name, new List<Node>(), args);
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(VarDeclarationParseNode vdpn)
        {
            Node val = null;
            Node type = null;
            if (vdpn.Value != null)
                val = vdpn.Value.Visit(this);
            if (vdpn.Type != null)
                type = vdpn.Type.Visit(this);
            var ret = new VarDeclarationNode(vdpn.Token, vdpn,
                    val, type);
            addAnnotations(vdpn.Annotations, ret.Annotations);
            if (vdpn.Annotations != null
                    && vdpn.Annotations.HasAnnotation("public"))
            {
                ret.Readable = true;
                ret.Writable = true;
            }
            else
            {
                ret.Readable = (vdpn.Annotations != null
                        && vdpn.Annotations.HasAnnotation("readable"));
                ret.Writable = (vdpn.Annotations != null
                        && vdpn.Annotations.HasAnnotation("writable"));
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(DefDeclarationParseNode vdpn)
        {
            Node val = null;
            Node type = null;
            if (vdpn.Value != null)
                val = vdpn.Value.Visit(this);
            if (vdpn.Type != null)
                type = vdpn.Type.Visit(this);
            var ret = new DefDeclarationNode(vdpn.Token, vdpn,
                    val, type);
            addAnnotations(vdpn.Annotations, ret.Annotations);
            if (vdpn.Annotations != null
                    && vdpn.Annotations.HasAnnotation("public"))
                ret.Public = true;
            ret.Public = (vdpn.Annotations != null
                    && (vdpn.Annotations.HasAnnotation("public")
                        || vdpn.Annotations.HasAnnotation("readable")));
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(BindParseNode bpn)
        {
            var ret = bpn.Left.Visit(this);
            var right = bpn.Right.Visit(this);
            var lrrn = ret as RequestNode;
            if (lrrn != null)
            {
                lrrn.MakeBind(right);
                if (bpn.Left is OperatorParseNode ||
                        bpn.Left is InterpolatedStringParseNode)
                    lrrn = null;
            }
            if (lrrn == null)
            {
                var name = ret.GetType().Name;
                name = name.Substring(0, name.Length - 4);
                if (bpn.Left is OperatorParseNode)
                    name = "Operator";
                if (bpn.Left is InterpolatedStringParseNode)
                    name = "StringLiteral";
                ErrorReporting.ReportStaticError(bpn.Token.Module,
                        bpn.Line, "P1044",
                        new Dictionary<string, string> {
                            { "lhs", name }
                        },
                        "Cannot assign to " + name);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(BlockParseNode d)
        {
            var parameters = new List<Node>();
            Node forcedPattern = null;
            foreach (ParseNode p in d.Parameters)
            {
                var id = p as IdentifierParseNode;
                var tppn = p as TypedParameterParseNode;
                var vappn = p as VarArgsParameterParseNode;
                if (id != null)
                    parameters.Add(new ParameterNode(id.Token, id));
                else if (tppn != null)
                {
                    parameters.Add(
                            new ParameterNode(tppn.Token,
                                tppn.Name as IdentifierParseNode,
                                tppn.Type.Visit(this)));
                }
                else if (vappn != null)
                {
                    // Inside could be either an identifier or a
                    // TypedParameterParseNode - check for both.
                    var inIPN = vappn.Name as IdentifierParseNode;
                    var inTPPN = vappn.Name as TypedParameterParseNode;
                    if (inIPN != null)
                        parameters.Add(new ParameterNode(inIPN.Token,
                                    inIPN,
                                    true // Variadic
                                    ));
                    else if (inTPPN != null)
                        parameters.Add(new ParameterNode(inTPPN.Token,
                                    inTPPN.Name as IdentifierParseNode,
                                    true, // Variadic
                                    inTPPN.Type.Visit(this)
                                    ));
                }
                else if (p is NumberParseNode || p is StringLiteralParseNode
                        || p is OperatorParseNode)
                {
                    parameters.Add(p.Visit(this));
                }
                else if (p is ParenthesisedParseNode)
                {
                    var tok = p.Token;
                    var it = new IdentifierToken(tok.module, tok.line,
                            tok.column, "_");
                    id = new IdentifierParseNode(it);
                    parameters.Add(new ParameterNode(tok, id, p.Visit(this)));
                }
                else
                {
                    throw new Exception("unimplemented - unusual parameters");
                }
            }
            var ret = new BlockNode(d.Token, d,
                    parameters,
                    map(d.Body),
                    forcedPattern);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ClassDeclarationParseNode d)
        {
            var constructor = new MethodDeclarationParseNode(d.Token);
            constructor.Signature = d.Signature;
            var instanceObj = new ObjectParseNode(d.Token);
            instanceObj.Body = d.Body;
            constructor.Body.Add(instanceObj);
            var ret = (MethodNode)constructor.Visit(this);
            // Classes are public by default.
            // The next line makes them public always; it is not
            // possible to have a confidential class. It is unclear
            // whether that should be permitted or not.
            ret.Confidential = false;
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(TraitDeclarationParseNode d)
        {
            var constructor = new MethodDeclarationParseNode(d.Token);
            constructor.Signature = d.Signature;
            var instanceObj = new ObjectParseNode(d.Token);
            instanceObj.Body = d.Body;
            constructor.Body.Add(instanceObj);
            var ret = (MethodNode)constructor.Visit(this);
            // Traits are public by default.
            // The next line makes them public always; it is not
            // possible to have a confidential trait. It is unclear
            // whether that should be permitted or not.
            ret.Confidential = false;
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ReturnParseNode rpn)
        {
            if (rpn.ReturnValue == null)
                return new ReturnNode(rpn.Token, rpn,
                        null);
            return new ReturnNode(rpn.Token, rpn,
                    rpn.ReturnValue.Visit(this));
        }

        /// <inheritdoc />
        public Node Visit(CommentParseNode cpn)
        {
            return new NoopNode(cpn.Token, cpn);
        }

        /// <inheritdoc />
        public Node Visit(TypeStatementParseNode tspn)
        {
            var meth = new MethodDeclarationParseNode(tspn.Token);
            var spn = new SignatureParseNode(tspn.Token);
            var spp = new OrdinarySignaturePartParseNode((IdentifierParseNode)tspn.BaseName);
            spp.GenericParameters = tspn.GenericParameters;
            spn.AddPart(spp);
            meth.Signature = spn;
            var tpn = tspn.Body as InterfaceParseNode;
            if (tpn != null)
            {
                tpn.Name = ((IdentifierParseNode)tspn.BaseName).Name;
            }
            meth.Body.Add(tspn.Body);
            return meth.Visit(this);
        }

        /// <inheritdoc />
        public Node Visit(InterfaceParseNode tpn)
        {
            var ret = new InterfaceNode(tpn.Token, tpn);
            if (tpn.Name != null)
                ret.Name = tpn.Name;
            foreach (var p in tpn.Body)
                ret.Body.Add((SignatureNode)p.Visit(this));
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ImportParseNode ipn)
        {
            Node type = null;
            if (ipn.Type != null)
                type = ipn.Type.Visit(this);
            return new ImportNode(ipn.Token, ipn, type);
        }

        /// <inheritdoc />
        public Node Visit(DialectParseNode dpn)
        {
            return new DialectNode(dpn.Token, dpn, module);
        }

        /// <inheritdoc />
        public Node Visit(InheritsParseNode ipn)
        {
            var frm = ipn.From.Visit(this);
            if (!(frm is RequestNode))
                ErrorReporting.ReportStaticError(ipn.From.Token.Module,
                        ipn.From.Line, "P1045",
                        new Dictionary<string, string> {
                        },
                        "Can only inherit from method requests" );
            return new InheritsNode(ipn.Token, ipn, frm,
                    from x in ipn.Aliases select
                        new KeyValuePair<string, SignatureNode>(
                            ((SignatureNode)x.NewName.Visit(this)).Name,
                            (SignatureNode)x.OldName.Visit(this)),
                    from x in ipn.Excludes select
                        ((SignatureNode)x.Name.Visit(this)).Name);
        }

        /// <inheritdoc />
        public Node Visit(UsesParseNode upn)
        {
            var frm = upn.From.Visit(this);
            if (!(frm is RequestNode))
                ErrorReporting.ReportStaticError(upn.From.Token.Module,
                        upn.From.Line, "P1045",
                        new Dictionary<string, string> {
                        },
                        "Can only inherit from method requests" );
            return new InheritsNode(upn.Token, upn, frm,
                    from x in upn.Aliases select
                        new KeyValuePair<string, SignatureNode>(x.NewName.Name,
                            (SignatureNode)x.OldName.Visit(this)),
                    from x in upn.Excludes select
                        ((SignatureNode)x.Name.Visit(this)).Name);
        }

        /// <inheritdoc/>
        public Node Visit(AliasParseNode ipn)
        {
            return null;
        }

        /// <inheritdoc/>
        public Node Visit(ExcludeParseNode ipn)
        {
            return null;
        }

        /// <inheritdoc />
        public Node Visit(ParenthesisedParseNode ppn)
        {
            return ppn.Expression.Visit(this);
        }

        /// <inheritdoc />
        public Node Visit(TypedParameterParseNode tppn)
        {
            ErrorReporting.ReportStaticError(tppn.Token.Module,
                    tppn.Line, "P1023",
                    new Dictionary<string, string> {
                        { "token", "" + tppn.Token }
                    },
                    "Unexpected ':' in argument list" );
            return null;
        }

        /// <inheritdoc />
        public Node Visit(ImplicitBracketRequestParseNode ibrpn)
        {
            ImplicitReceiverRequestNode ret = new ImplicitReceiverRequestNode(ibrpn.Token, ibrpn);
            RequestPartNode rpn = new RequestPartNode("circumfix" + ibrpn.Name,
                    new List<Node>(),
                    map(ibrpn.Arguments),
                    false);
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ExplicitBracketRequestParseNode ebrpn)
        {
            ExplicitReceiverRequestNode ret = new ExplicitReceiverRequestNode(ebrpn.Token, ebrpn,
                ebrpn.Receiver.Visit(this));
            RequestPartNode rpn = new RequestPartNode(ebrpn.Name,
                    new List<Node>(),
                    map(ebrpn.Arguments));
            ret.AddPart(rpn);
            return ret;
        }

        /// <summary>Transforms a list of ParseNodes into a list of the
        /// corresponding Nodes</summary>
        private List<Node> map(IEnumerable<ParseNode> l)
        {
            var ret = new List<Node>();
            foreach (ParseNode p in l)
                if (!(p is CommentParseNode))
                    ret.Add(p.Visit(this));
            return ret;
        }
    }

}
