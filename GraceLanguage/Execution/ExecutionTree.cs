using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            ObjectConstructorNode ret = new ObjectConstructorNode(obj.Token, obj);
            foreach (ParseNode p in obj.body)
            {
                if (!(p is CommentParseNode))
                    ret.Add(p.Visit<Node>(this));
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
            foreach (ParseNode part in n.parts)
            {
                if (ret == null)
                {
                    ret = part.Visit(this);
                }
                else
                {
                    var errn = new ExplicitReceiverRequestNode(n.Token, n,
                            ret);
                    List<Node> args = new List<Node>();
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
                    RequestPartNode rpn = new RequestPartNode("++",
                            new List<Node>(), args);
                    errn.AddPart(rpn);
                    ret = errn;
                }
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(MethodDeclarationParseNode d)
        {
            MethodNode ret = new MethodNode(d.Token, d);
            string name = "";
            for (int i = 0; i < d.nameParts.Count; i++)
            {
                if (name != "")
                    name += " ";
                string partName = (d.nameParts[i] as IdentifierParseNode).name;
                name += partName;
                List<ParseNode> partParams = d.parameters[i];
                List<Node> parameters = new List<Node>();
                foreach (ParseNode p in partParams)
                {
                    IdentifierParseNode id = p as IdentifierParseNode;
                    TypedParameterParseNode tppn = p as TypedParameterParseNode;
                    VarArgsParameterParseNode vappn = p as VarArgsParameterParseNode;
                    if (id != null)
                        parameters.Add(new ParameterNode(id.Token, id));
                    else if (tppn != null)
                    {
                        parameters.Add(new ParameterNode(tppn.Token, tppn.name as IdentifierParseNode, tppn.type.Visit(this)));
                    }
                    else if (vappn != null)
                    {
                        // Inside could be either an identifier or a
                        // TypedParameterParseNode - check for both.
                        var inIPN = vappn.name as IdentifierParseNode;
                        var inTPPN = vappn.name as TypedParameterParseNode;
                        if (inIPN != null)
                            parameters.Add(new ParameterNode(inIPN.Token,
                                        inIPN,
                                        true // Variadic
                                        ));
                        else if (inTPPN != null)
                            parameters.Add(new ParameterNode(inTPPN.Token,
                                        inTPPN.name as IdentifierParseNode,
                                        true, // Variadic
                                        inTPPN.type.Visit(this)
                                        ));
                    }
                    else
                    {
                        throw new Exception("unimplemented - unusual parameters");
                    }
                }
                List<Node> generics = new List<Node>();
                foreach (ParseNode p in d.generics[i])
                {
                    IdentifierParseNode id = p as IdentifierParseNode;
                    if (id != null)
                    {
                        generics.Add(new IdentifierNode(id.Token, id));
                    }
                    else
                    {
                        throw new Exception("unimplemented - bad generic parameters");
                    }
                }
                RequestPartNode rpn = new RequestPartNode(partName, generics, parameters);
                ret.AddPart(rpn);
            }
            if (d.annotations != null
                    && d.annotations.HasAnnotation("confidential"))
                ret.Confidential = true;
            foreach (ParseNode p in d.body)
                if (!(p is CommentParseNode))
                    ret.Add(p.Visit(this));
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(IdentifierParseNode i)
        {
            ImplicitReceiverRequestNode ret = new ImplicitReceiverRequestNode(i.Token, i);
            RequestPartNode rpn = new RequestPartNode(i.name, new List<Node>(), new List<Node>());
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ImplicitReceiverRequestParseNode irrpn)
        {
            ImplicitReceiverRequestNode ret = new ImplicitReceiverRequestNode(irrpn.Token, irrpn);
            for (int i = 0; i < irrpn.arguments.Count; i++)
            {
                RequestPartNode rpn = new RequestPartNode((irrpn.nameParts[i] as IdentifierParseNode).name,
                    map(irrpn.genericArguments[i]),
                    map(irrpn.arguments[i]));
                ret.AddPart(rpn);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ExplicitReceiverRequestParseNode irrpn)
        {
            ExplicitReceiverRequestNode ret = new ExplicitReceiverRequestNode(irrpn.Token, irrpn,
                irrpn.receiver.Visit(this));
            for (int i = 0; i < irrpn.arguments.Count; i++)
            {
                RequestPartNode rpn = new RequestPartNode((irrpn.nameParts[i] as IdentifierParseNode).name,
                    map(irrpn.genericArguments[i]),
                    map(irrpn.arguments[i]));
                ret.AddPart(rpn);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(PrefixOperatorParseNode popn)
        {
            ExplicitReceiverRequestNode ret = new ExplicitReceiverRequestNode(popn.Token, popn,
                popn.receiver.Visit(this));
            RequestPartNode rpn = new RequestPartNode("prefix" + popn.name,
                    new List<Node>(),
                    new List<Node>());
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(OperatorParseNode opn)
        {
            ExplicitReceiverRequestNode ret = new ExplicitReceiverRequestNode(opn.Token, opn, opn.left.Visit(this));
            List<Node> args = new List<Node>();
            args.Add(opn.right.Visit(this));
            RequestPartNode rpn = new RequestPartNode(opn.name, new List<Node>(), args);
            ret.AddPart(rpn);
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(VarDeclarationParseNode vdpn)
        {
            Node val = null;
            Node type = null;
            if (vdpn.val != null)
                val = vdpn.val.Visit(this);
            if (vdpn.type != null)
                type = vdpn.type.Visit(this);
            var ret = new VarDeclarationNode(vdpn.Token, vdpn,
                    val, type);
            if (vdpn.annotations != null
                    && vdpn.annotations.HasAnnotation("public"))
            {
                ret.Readable = true;
                ret.Writable = true;
            }
            if (vdpn.annotations != null
                    && vdpn.annotations.HasAnnotation("readable"))
                ret.Readable = true;
            if (vdpn.annotations != null
                    && vdpn.annotations.HasAnnotation("writable"))
                ret.Writable = true;
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(DefDeclarationParseNode vdpn)
        {
            Node val = null;
            Node type = null;
            if (vdpn.val != null)
                val = vdpn.val.Visit(this);
            if (vdpn.type != null)
                type = vdpn.type.Visit(this);
            var ret = new DefDeclarationNode(vdpn.Token, vdpn,
                    val, type);
            if (vdpn.annotations != null
                    && vdpn.annotations.HasAnnotation("public"))
                ret.Public = true;
            if (vdpn.annotations != null
                    && vdpn.annotations.HasAnnotation("readable"))
                ret.Public = true;
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(BindParseNode bpn)
        {
            var ret = bpn.left.Visit(this);
            var right = bpn.right.Visit(this);
            var lrrn = ret as RequestNode;
            if (lrrn != null)
            {
                lrrn.MakeBind(right);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(BlockParseNode d)
        {
            var parameters = new List<Node>();
            foreach (ParseNode p in d.parameters)
            {
                IdentifierParseNode id = p as IdentifierParseNode;
                TypedParameterParseNode tppn = p as TypedParameterParseNode;
                if (id != null)
                    parameters.Add(new ParameterNode(id.Token, id));
                else if (tppn != null)
                {
                    parameters.Add(new ParameterNode(tppn.Token, tppn.name as IdentifierParseNode, tppn.type.Visit(this)));
                }
                else if (p is NumberParseNode || p is StringLiteralParseNode
                        || p is OperatorParseNode)
                {
                    parameters.Add(p.Visit(this));
                }
                else
                {
                    throw new Exception("unimplemented - unusual parameters");
                }
            }
            var ret = new BlockNode(d.Token, d,
                    parameters,
                    map(d.body));
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ClassDeclarationParseNode d)
        {
            var clsObj = new ObjectParseNode(d.Token);
            var constructor = new MethodDeclarationParseNode(d.Token);
            constructor.nameParts = d.nameParts;
            constructor.parameters = d.parameters;
            constructor.generics = d.generics;
            constructor.returnType = d.returnType;
            constructor.annotations = d.annotations;
            var instanceObj = new ObjectParseNode(d.Token);
            instanceObj.body = d.body;
            constructor.body.Add(instanceObj);
            clsObj.body.Add(constructor);
            var dpn = new DefDeclarationParseNode(d.Token,
                    d.baseName,
                    clsObj,
                    null, // Type
                    null);
            return dpn.Visit(this);
        }

        /// <inheritdoc />
        public Node Visit(ReturnParseNode rpn)
        {
            if (rpn.returnValue == null)
                return new ReturnNode(rpn.Token, rpn,
                        null);
            return new ReturnNode(rpn.Token, rpn,
                    rpn.returnValue.Visit(this));
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
            PartParameters pp = meth.AddPart(tspn.baseName);
            pp.Generics.AddRange(tspn.genericParameters);
            var tpn = tspn.body as TypeParseNode;
            if (tpn != null)
            {
                tpn.Name = (tspn.baseName as IdentifierParseNode).name;
            }
            meth.body.Add(tspn.body);
            return meth.Visit(this);
        }

        /// <inheritdoc />
        public Node Visit(TypeParseNode tpn)
        {
            var ret = new TypeNode(tpn.Token, tpn);
            if (tpn.Name != null)
                ret.Name = tpn.Name;
            foreach (var p in tpn.body)
                ret.Body.Add((MethodTypeNode)p.Visit(this));
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(TypeMethodParseNode d)
        {
            var ret = new MethodTypeNode(d.Token, d);
            if (d.returnType != null)
            {
                ret.Returns = d.returnType.Visit(this);
            }
            string name = "";
            for (int i = 0; i < d.nameParts.Count; i++)
            {
                if (name != "")
                    name += " ";
                string partName = (d.nameParts[i] as IdentifierParseNode).name;
                name += partName;
                List<ParseNode> partParams = d.parameters[i];
                List<Node> parameters = new List<Node>();
                foreach (ParseNode p in partParams)
                {
                    IdentifierParseNode id = p as IdentifierParseNode;
                    TypedParameterParseNode tppn = p as TypedParameterParseNode;
                    VarArgsParameterParseNode vappn = p as VarArgsParameterParseNode;
                    if (id != null)
                        parameters.Add(new IdentifierNode(id.Token, id));
                    else if (tppn != null)
                    {
                        parameters.Add(new ParameterNode(tppn.Token, tppn.name as IdentifierParseNode, tppn.type.Visit(this)));
                    }
                    else if (vappn != null)
                    {
                        // Inside could be either an identifier or a
                        // TypedParameterParseNode - check for both.
                        var inIPN = vappn.name as IdentifierParseNode;
                        var inTPPN = vappn.name as TypedParameterParseNode;
                        if (inIPN != null)
                            parameters.Add(new ParameterNode(inIPN.Token,
                                        inIPN,
                                        true // Variadic
                                        ));
                        else if (inTPPN != null)
                            parameters.Add(new ParameterNode(inTPPN.Token,
                                        inTPPN.name as IdentifierParseNode,
                                        true, // Variadic
                                        inTPPN.type.Visit(this)
                                        ));
                    }
                    else
                    {
                        throw new Exception("unimplemented - unusual parameters");
                    }
                }
                List<Node> generics = new List<Node>();
                foreach (ParseNode p in d.generics[i])
                {
                    IdentifierParseNode id = p as IdentifierParseNode;
                    if (id != null)
                    {
                        generics.Add(new IdentifierNode(id.Token, id));
                    }
                    else
                    {
                        throw new Exception("unimplemented - bad generic parameters");
                    }
                }
                RequestPartNode rpn = new RequestPartNode(partName, generics, parameters);
                ret.AddPart(rpn);
            }
            return ret;
        }

        /// <inheritdoc />
        public Node Visit(ImportParseNode ipn)
        {
            Node type = null;
            if (ipn.type != null)
                type = ipn.type.Visit(this);
            return new ImportNode(ipn.Token, ipn, type);
        }

        /// <inheritdoc />
        public Node Visit(DialectParseNode dpn)
        {
            return new DialectNode(dpn.Token, dpn);
        }

        /// <summary>Transforms a list of ParseNodes into a list of the
        /// corresponding Nodes</summary>
        private List<Node> map(List<ParseNode> l)
        {
            List<Node> ret = new List<Node>();
            foreach (ParseNode p in l)
                if (!(p is CommentParseNode))
                    ret.Add(p.Visit(this));
            return ret;
        }
    }

}
