using System.Collections.Generic;

namespace Grace.Parsing
{
    /// <summary>
    /// Abstract superclass visiting all nodes suitable for writing
    /// concrete subclasses that check properties of the parse tree.
    /// </summary>
    public abstract class CheckingParseNodeVisitor
        : ParseNodeVisitor<ParseNode>
    {
        /// <inheritdoc/>
        public virtual ParseNode Visit(ParseNode p)
        {
            return p;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ObjectParseNode o)
        {
            foreach (var p in o.Body)
            {
                p.Visit(this);
            }
            return o;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(NumberParseNode n)
        {
            return n;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(MethodDeclarationParseNode d)
        {
            d.Signature.Visit(this);
            foreach (var p in d.Body)
                p.Visit(this);
            return d;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(IdentifierParseNode i)
        {
            return i;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ImplicitReceiverRequestParseNode irrpn)
        {
            foreach (var args in irrpn.Arguments)
                foreach (var a in args)
                    a.Visit(this);
            foreach (var args in irrpn.GenericArguments)
                foreach (var a in args)
                    a.Visit(this);
            return irrpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ExplicitReceiverRequestParseNode errpn)
        {
            foreach (var args in errpn.Arguments)
                foreach (var a in args)
                    a.Visit(this);
            foreach (var args in errpn.GenericArguments)
                foreach (var a in args)
                    a.Visit(this);
            errpn.Receiver.Visit(this);
            return errpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(OperatorParseNode opn)
        {
            opn.Left.Visit(this);
            opn.Right.Visit(this);
            return opn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(StringLiteralParseNode slpn)
        {
            return slpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(InterpolatedStringParseNode ispn)
        {
            foreach (var p in ispn.Parts)
                p.Visit(this);
            return ispn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(VarDeclarationParseNode vdpn)
        {
            vdpn.Name.Visit(this);
            if (vdpn.Value != null)
                vdpn.Value.Visit(this);
            if (vdpn.Type != null)
                vdpn.Type.Visit(this);
            if (vdpn.Annotations != null)
                vdpn.Annotations.Visit(this);
            return vdpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(DefDeclarationParseNode vdpn)
        {
            vdpn.Name.Visit(this);
            if (vdpn.Value != null)
                vdpn.Value.Visit(this);
            if (vdpn.Type != null)
                vdpn.Type.Visit(this);
            if (vdpn.Annotations != null)
                vdpn.Annotations.Visit(this);
            return vdpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(BindParseNode bpn)
        {
            bpn.Left.Visit(this);
            bpn.Right.Visit(this);
            return bpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(PrefixOperatorParseNode popn)
        {
            popn.Receiver.Visit(this);
            return popn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(BlockParseNode bpn)
        {
            foreach (var p in bpn.Parameters)
                p.Visit(this);
            foreach (var s in bpn.Body)
                s.Visit(this);
            return bpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ClassDeclarationParseNode bpn)
        {
            bpn.Signature.Visit(this);
            foreach (var s in bpn.Body)
                s.Visit(this);
            return bpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(TraitDeclarationParseNode bpn)
        {
            bpn.Signature.Visit(this);
            foreach (var s in bpn.Body)
                s.Visit(this);
            return bpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ReturnParseNode rpn)
        {
            if (rpn.ReturnValue != null)
                rpn.ReturnValue.Visit(this);
            return rpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(CommentParseNode cpn)
        {
            return cpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(TypeStatementParseNode tspn)
        {
            tspn.BaseName.Visit(this);
            tspn.Body.Visit(this);
            return tspn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(TypeParseNode tpn)
        {
            foreach (var t in tpn.Body)
                t.Visit(this);
            return tpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ImportParseNode ipn)
        {
            ipn.Path.Visit(this);
            ipn.Name.Visit(this);
            return ipn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(DialectParseNode dpn)
        {
            dpn.Path.Visit(this);
            return dpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(InheritsParseNode ipn)
        {
            ipn.From.Visit(this);
            foreach (var ap in ipn.Aliases)
                ap.Visit(this);
            return ipn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(UsesParseNode upn)
        {
            upn.From.Visit(this);
            foreach (var ap in upn.Aliases)
                ap.Visit(this);
            return upn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(AliasParseNode ipn)
        {
            ipn.NewName.Visit(this);
            ipn.OldName.Visit(this);
            return ipn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ExcludeParseNode ipn)
        {
            ipn.Name.Visit(this);
            return ipn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ParenthesisedParseNode ppn)
        {
            ppn.Expression.Visit(this);
            return ppn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ImplicitBracketRequestParseNode ibrpn)
        {
            foreach (var a in ibrpn.Arguments)
                a.Visit(this);
            return ibrpn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(ExplicitBracketRequestParseNode ebrpn)
        {
            ebrpn.Receiver.Visit(this);
            foreach (var a in ebrpn.Arguments)
                a.Visit(this);
            return ebrpn;
        }
        /// <inheritdoc/>
        public virtual ParseNode Visit(SignatureParseNode spn)
        {
            foreach (var n in spn.Parts)
                n.Visit(this);
            return spn;
        }

        /// <inheritdoc/>
        public virtual ParseNode Visit(OrdinarySignaturePartParseNode osppn)
        {
            foreach (var p in osppn.Parameters)
                p.Visit(this);
            foreach (var p in osppn.GenericParameters)
                p.Visit(this);
            return osppn;
        }
    }

    class NonReceiverNameCheckingParseNodeVisitor
        : CheckingParseNodeVisitor
    {
        private HashSet<string> _names;

        public NonReceiverNameCheckingParseNodeVisitor(
                HashSet<string> names
                )
        {
            _names = names;
        }

        /// <inheritdoc/>
        public override ParseNode Visit(IdentifierParseNode ipn)
        {
            // A bare identifier that matches an element of the
            // set of disallowed names will always raise an error,
            // but the other cases below avoid visiting such a
            // node if it is in a valid place.
            if (_names.Contains(ipn.Name))
                ErrorReporting.ReportStaticError(ipn.Token.Module,
                        ipn.Line, "P1043",
                        new Dictionary<string, string> {
                            { "name", ipn.Name }
                        },
                        "Invalid use of parent name");
            return ipn;
        }

        /// <inheritdoc/>
        public override ParseNode Visit(
                ImplicitReceiverRequestParseNode irrpn
                )
        {
            if (irrpn.NameParts.Count != 1)
                return base.Visit(irrpn);
            // A single-part name could be a banned identifier.
            var n = (IdentifierParseNode)irrpn.NameParts[0];
            Visit(n);
            // The arguments require checking either way.
            foreach (var args in irrpn.Arguments)
                foreach (var a in args)
                    a.Visit(this);
            foreach (var args in irrpn.GenericArguments)
                foreach (var a in args)
                    a.Visit(this);
            return irrpn;
        }

        /// <inheritdoc/>
        public override ParseNode Visit(
                ExplicitReceiverRequestParseNode errpn
                )
        {
            if (!(errpn.Receiver is IdentifierParseNode))
            {
                return base.Visit(errpn);
            }
            // If the receiver was an identifier, we only
            // want to look at arguments for possible
            // problems - the receiver is OK.
            foreach (var args in errpn.Arguments)
                foreach (var a in args)
                    a.Visit(this);
            foreach (var args in errpn.GenericArguments)
                foreach (var a in args)
                    a.Visit(this);
            return errpn;
        }

        /// <inheritdoc/>
        public override ParseNode Visit(
                OperatorParseNode opn
                )
        {
            if (!(opn.Left is IdentifierParseNode))
            {
                return base.Visit(opn);
            }
            // If the left-hand side (the receiver) was
            // an identifier, only check the right.
            opn.Right.Visit(this);
            return opn;
        }

        /// <inheritdoc/>
        public override ParseNode Visit(
                PrefixOperatorParseNode popn
                )
        {
            // If the receiver is an identifier, it's ok,
            // and we just return successfully.
            if (popn.Receiver is IdentifierParseNode)
                return popn;
            return popn.Receiver.Visit(this);
        }

        /// <inheritdoc/>
        public override ParseNode Visit(
                InheritsParseNode ipn
                )
        {
            // Only the request part should be scanned.
            return ipn.From.Visit(this);
        }
    }
}
