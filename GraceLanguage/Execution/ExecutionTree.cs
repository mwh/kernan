using System;
using System.Collections.Generic;
using Grace.Execution;
using Grace.Parsing;

namespace Grace.Execution
{
    /// <summary>Translates a tree of ParseNodes into Nodes</summary>
    public class ExecutionTreeTranslator : ParseNodeVisitor<Node>
    {
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
            InheritsNode parent = null;
            var parentNames = new HashSet<string>();
            var singleParent = true;
            foreach (ParseNode p in obj.Body)
            {
                var n = p.Visit<Node>(this);
                if (!(p is CommentParseNode))
                    ret.Add(n);
                var i = n as InheritsNode;
                if (i != null)
                {
                    singleParent = (parent == null);
                    parent = i;
                    if (i.As != null)
                    {
                        parentNames.Add(i.As);
                    }
                }
            }
            if (singleParent && parent != null && parent.As == null)
            {
                parent.As = "super";
                parentNames.Add("super");
            }
            if (parentNames.Count > 0)
            {
                var checker =
                    new NonReceiverNameCheckingParseNodeVisitor(parentNames);
                foreach (var n in obj.Body)
                    n.Visit(checker);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(NumberParseNode n)
        {
            return new NumberNode(n.Token, n);
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
            string name = ret.Name;
            foreach (var part in spn.Parts)
            {
                ret.AddPart((SignaturePartNode)part.Visit(this));
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(OrdinarySignaturePartParseNode osppn)
        {
            var parameters = new List<Node>();
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
                    generics.Add(new IdentifierNode(id.Token, id));
                }
                else
                {
                    throw new Exception("unimplemented - bad generic parameters");
                }
            }
            return new OrdinarySignaturePartNode(osppn.Token, osppn,
                    parameters, generics);
        }

        /// <inheritdoc />
        public Node Visit(MethodDeclarationParseNode d)
        {
            var ret = new MethodNode(d.Token, d);
            var sig = (SignatureNode)d.Signature.Visit(this);
            ret.Signature = sig;
            string name = sig.Name;
            ret.Confidential = (d.Signature.Annotations != null
                    && d.Signature.Annotations.HasAnnotation("confidential"));
            foreach (ParseNode p in d.Body)
                if (!(p is CommentParseNode))
                    ret.Add(p.Visit(this));
            // Indicate whether this method returns a fresh object
            ret.Fresh = (d.Body.Count > 0 && d.Body[d.Body.Count - 1] is
                    ObjectParseNode);
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

        private Node createDestructuringPattern(ParseNode n,
                List<Node> parameters)
        {
            if (n is NumberParseNode
                    || n is StringLiteralParseNode
                    || n is OperatorParseNode)
            {
                return n.Visit(this);
            }
            var id = n as IdentifierParseNode;
            var tppn = n as TypedParameterParseNode;
            if (id != null)
            {
                parameters.Add(new ParameterNode(id.Token, id));
                var ret = new PreludeRequestNode(id.Token, id);
                ret.AddPart(new RequestPartNode("_VariablePattern",
                            new List<Node>(), new List<Node>()));
                return ret;
            }
            if (tppn != null)
            {
                parameters.Add(new ParameterNode(tppn.Token,
                            tppn.Name as IdentifierParseNode));
                var ret = new PreludeRequestNode(tppn.Token, tppn);
                var varPattern = new PreludeRequestNode(tppn.Token, tppn);
                varPattern.AddPart(new RequestPartNode("_VariablePattern",
                            new List<Node>(), new List<Node>()));
                ret.AddPart(new RequestPartNode("_AndPattern",
                            new List<Node>(), new List<Node>
                            {
                                varPattern,
                                createDestructuringPattern(tppn.Type,
                                        parameters)
                            }));
                return ret;
            }
            var irrpn = n as ImplicitReceiverRequestParseNode;
            var errpn = n as ExplicitReceiverRequestParseNode;
            List<List<ParseNode>> argLists = null;
            if (irrpn != null)
                argLists = irrpn.Arguments;
            if (errpn != null)
                argLists = errpn.Arguments;
            if (argLists != null)
            {
                if (argLists.Count > 1)
                    ErrorReporting.ReportStaticError(tppn.Token.Module,
                            tppn.Line, "P1027",
                            "Invalid multi-part destructuring pattern match");
                var args = argLists[0];
                if (args.Count == 0)
                {
                    return n.Visit(this);
                }
                // At this point, we know it is another destructuring match
                var madpArgs = new List<Node>();
                if (irrpn != null)
                    madpArgs.Add(new ImplicitReceiverRequestParseNode
                            (irrpn.NameParts[0]).Visit(this));
                else if (errpn != null)
                {
                    var tmp = new ExplicitReceiverRequestParseNode
                            (errpn.Receiver);
                    tmp.AddPart(errpn.NameParts[0]);
                    madpArgs.Add(tmp.Visit(this));
                }
                foreach (var a in args)
                {
                    madpArgs.Add(createDestructuringPattern(a, parameters));
                }
                var ret = new PreludeRequestNode(n.Token, n);
                ret.AddPart(new RequestPartNode("_MatchAndDestructuringPattern",
                            new List<Node>(), madpArgs));
                return ret;
            }
            return null;
        }

        private Node handlePossibleDestructuringParam(
                TypedParameterParseNode tppn, List<Node> parameters)
        {
            var typeNode = tppn.Type;
            if (typeNode is NumberParseNode
                    || typeNode is StringLiteralParseNode
                    || typeNode is OperatorParseNode)
            {
                parameters.Add(
                        new ParameterNode(tppn.Token,
                            tppn.Name as IdentifierParseNode,
                            typeNode.Visit(this)));
                return null;
            }
            var irrpn = typeNode as ImplicitReceiverRequestParseNode;
            var errpn = typeNode as ExplicitReceiverRequestParseNode;
            List<List<ParseNode>> argLists = null;
            if (irrpn != null)
                argLists = irrpn.Arguments;
            if (errpn != null)
                argLists = errpn.Arguments;
            if (argLists != null)
            {
                if (argLists.Count > 1)
                    ErrorReporting.ReportStaticError(tppn.Token.Module,
                            tppn.Line, "P1027",
                            "Invalid multi-part destructuring pattern match");
                var args = argLists[0];
                if (args.Count == 0)
                {
                    parameters.Add(
                            new ParameterNode(tppn.Token,
                                tppn.Name as IdentifierParseNode,
                                typeNode.Visit(this)));
                    return null;
                }
                // At this point, we know it is a destructuring match
                return createDestructuringPattern(tppn, parameters);
            }
            parameters.Add(
                    new ParameterNode(tppn.Token,
                        tppn.Name as IdentifierParseNode,
                        typeNode.Visit(this)));
            return null;
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
                    if (parameters.Count == 0)
                    {
                        forcedPattern = handlePossibleDestructuringParam(tppn, parameters);
                    }
                    else
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
            var clsObj = new ObjectParseNode(d.Token);
            var constructor = new MethodDeclarationParseNode(d.Token);
            constructor.Signature = d.Signature;
            var instanceObj = new ObjectParseNode(d.Token);
            instanceObj.Body = d.Body;
            constructor.Body.Add(instanceObj);
            clsObj.Body.Add(constructor);
            var dpn = new DefDeclarationParseNode(d.Token,
                    d.BaseName,
                    clsObj,
                    null, // Type
                    null);
            var ret = (DefDeclarationNode)dpn.Visit(this);
            // Classes are public by default.
            // The next line makes them public always; it is not
            // possible to have a confidential class. It is unclear
            // whether that should be permitted or not.
            ret.Public = true;
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
            var tpn = tspn.Body as TypeParseNode;
            if (tpn != null)
            {
                tpn.Name = ((IdentifierParseNode)tspn.BaseName).Name;
            }
            meth.Body.Add(tspn.Body);
            return meth.Visit(this);
        }

        /// <inheritdoc />
        public Node Visit(TypeParseNode tpn)
        {
            var ret = new TypeNode(tpn.Token, tpn);
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
            return new DialectNode(dpn.Token, dpn);
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

            return new InheritsNode(ipn.Token, ipn, frm);
        }

        /// <inheritdoc />
        public Node Visit(ParenthesisedParseNode ppn)
        {
            return ppn.Expression.Visit(this);
        }

        /// <inheritdoc />
        public Node Visit(ImplicitBracketRequestParseNode ibrpn)
        {
            ImplicitReceiverRequestNode ret = new ImplicitReceiverRequestNode(ibrpn.Token, ibrpn);
            RequestPartNode rpn = new RequestPartNode("circumfix" + ibrpn.Name,
                    new List<Node>(),
                    map(ibrpn.Arguments));
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
