using System;
using System.Collections.Generic;
using System.Xml;
using Grace.Parsing;

namespace Grace.Utility
{
    class ParseTreeJSONVisitor : ParseNodeVisitor<XmlElement>
    {
        private XmlDocument document;
        private XmlElement parent;
        private XmlElement existingNode;

        public ParseTreeJSONVisitor(XmlDocument doc, XmlElement parent)
        {
            this.document = doc;
            this.parent = parent;
            existingNode = parent;
        }

        public XmlElement Visit(ParseNode p)
        {
            throw new NotImplementedException();
        }

        private XmlElement makeNode(ParseNode o, string kind)
        {
            XmlElement el = existingNode;
            existingNode = null;
            if (el == null)
                el = document.CreateElement("parsenode");
            var type = document.CreateElement("nodetype");
            type.AppendChild(document.CreateTextNode(kind));
            var line = document.CreateElement("line");
            line.SetAttribute("type", "number");
            line.AppendChild(document.CreateTextNode(o.Line + ""));
            var column = document.CreateElement("column");
            column.SetAttribute("type", "number");
            column.AppendChild(document.CreateTextNode(o.Column + ""));
            el.AppendChild(type);
            el.AppendChild(line);
            el.AppendChild(column);
            if (!(o is CommentParseNode))
            {
                var comments = document.CreateElement("comments");
                comments.SetAttribute("type", "array");
                var com = o.Comment as CommentParseNode;
                while (com != null)
                {
                    existingNode = document.CreateElement("item");
                    comments.AppendChild(Visit(com));
                    com = com.Comment as CommentParseNode;
                }
                el.AppendChild(comments);
            }
            el.SetAttribute("type", "object");
            existingNode = null;
            return el;
        }

        private void addProperty<T>(XmlElement parent, string name,
            IList<T> body) where T : ParseNode
        {
            var list = document.CreateElement(name);
            list.SetAttribute("type", "array");
            parent.AppendChild(list);
            foreach (var x in body)
            {
                var item = document.CreateElement("item");
                existingNode = item;
                list.AppendChild(x.Visit(this));
            }
        }

        private void addProperty(XmlElement parent,
            string name, string value)
        {
            var n = document.CreateElement(name);
            n.AppendChild(document.CreateTextNode(value));
            parent.AppendChild(n);
        }

        private void addProperty(XmlElement parent,
            string name, int value)
        {
            var n = document.CreateElement(name);
            n.SetAttribute("type", "number");
            n.AppendChild(document.CreateTextNode(value + ""));
            parent.AppendChild(n);
        }

        private void addProperty(XmlElement parent,
            string name, bool value)
        {
            var n = document.CreateElement(name);
            n.SetAttribute("type", "boolean");
            n.AppendChild(document.CreateTextNode(value ? "true" : "false"));
            parent.AppendChild(n);
        }

        private void addProperty(XmlElement parent,
            string name, ParseNode value)
        {
            var n = document.CreateElement(name);
            if (value == null)
            {
                n.SetAttribute("type", "null");
                parent.AppendChild(n);
                return;
            }
            n.SetAttribute("type", "object");
            existingNode = n;
            parent.AppendChild(value.Visit(this));
        }

        public XmlElement Visit(ObjectParseNode o)
        {
            var el = makeNode(o, "object");
            addProperty(el, "body", o.Body);
            return el;
        }

        public XmlElement Visit(NumberParseNode n)
        {
            var el = makeNode(n, "number");
            addProperty(el, "digits", n.Digits);
            addProperty(el, "base", n.NumericBase);
            return el;
        }

        public XmlElement Visit(MethodDeclarationParseNode d)
        {
            var el = makeNode(d, "method-declaration");
            addProperty(el, "signature", d.Signature);
            addProperty(el, "returntype", d.ReturnType);
            addProperty(el, "body", d.Body);
            return el;
        }

        public XmlElement Visit(IdentifierParseNode i)
        {
            var el = makeNode(i, "identifier");
            addProperty(el, "name", i.Name);
            return el;
        }

        public XmlElement Visit(ImplicitReceiverRequestParseNode irrpn)
        {
            var el = makeNode(irrpn, "implicit-receiver-request");
            addProperty(el, "name", irrpn.Name);
            var parts = document.CreateElement("parts");
            parts.SetAttribute("type", "array");
            for (int i = 0; i < irrpn.NameParts.Count; i++)
            {
                var item = document.CreateElement("item");
                item.SetAttribute("type", "object");
                addProperty(item, "name", irrpn.NameParts[i]);
                addProperty(item, "arguments", irrpn.Arguments[i]);
                addProperty(item, "genericarguments", irrpn.GenericArguments[i]);
                parts.AppendChild(item);
            }
            el.AppendChild(parts);
            return el;
        }

        public XmlElement Visit(ExplicitReceiverRequestParseNode errpn)
        {
            var el = makeNode(errpn, "explicit-receiver-request");
            addProperty(el, "receiver", errpn.Receiver);
            var parts = document.CreateElement("parts");
            parts.SetAttribute("type", "array");
            for (int i = 0; i < errpn.NameParts.Count; i++)
            {
                var item = document.CreateElement("item");
                item.SetAttribute("type", "object");
                addProperty(item, "name", errpn.NameParts[i]);
                addProperty(item, "arguments", errpn.Arguments[i]);
                addProperty(item, "genericarguments", errpn.GenericArguments[i]);
                parts.AppendChild(item);
            }
            el.AppendChild(parts);
            return el;
        }

        public XmlElement Visit(OperatorParseNode opn)
        {
            var el = makeNode(opn, "operator");
            addProperty(el, "operator", opn.Name);
            addProperty(el, "left", opn.Left);
            addProperty(el, "right", opn.Right);
            return el;
        }

        public XmlElement Visit(TypedParameterParseNode tppn)
        {
            var el = makeNode(tppn, "typed-parameter");
            addProperty(el, "name", tppn.Name);
            addProperty(el, "type", tppn.Type);
            return el;
        }

        public XmlElement Visit(StringLiteralParseNode slpn)
        {
            var el = makeNode(slpn, "string-literal");
            addProperty(el, "raw", slpn.Raw);
            addProperty(el, "processed", slpn.Value);
            return el;
        }

        public XmlElement Visit(InterpolatedStringParseNode ispn)
        {
            var el = makeNode(ispn, "interpolated-string");
            addProperty(el, "parts", ispn.Parts);
            return el;
        }

        public XmlElement Visit(VarDeclarationParseNode vdpn)
        {
            var el = makeNode(vdpn, "var-declaration");
            addProperty(el, "name", vdpn.Name);
            addProperty(el, "type", vdpn.Type);
            addProperty(el, "value", vdpn.Value);
            return el;
        }

        public XmlElement Visit(DefDeclarationParseNode vdpn)
        {
            var el = makeNode(vdpn, "def-declaration");
            addProperty(el, "name", vdpn.Name);
            addProperty(el, "type", vdpn.Type);
            addProperty(el, "value", vdpn.Value);
            return el;
        }

        public XmlElement Visit(BindParseNode bpn)
        {
            var el = makeNode(bpn, "bind");
            addProperty(el, "left", bpn.Left);
            addProperty(el, "right", bpn.Right);
            return el;
        }

        public XmlElement Visit(PrefixOperatorParseNode popn)
        {
            var el = makeNode(popn, "prefix-operator");
            addProperty(el, "name", popn.Name);
            addProperty(el, "receiver", popn.Receiver);
            return el;
        }

        public XmlElement Visit(BlockParseNode bpn)
        {
            var el = makeNode(bpn, "block");
            addProperty(el, "parameters", bpn.Parameters);
            addProperty(el, "body", bpn.Body);
            return el;
        }

        public XmlElement Visit(ClassDeclarationParseNode bpn)
        {
            var el = makeNode(bpn, "class-declaration");
            addProperty(el, "signature", bpn.Signature);
            addProperty(el, "body", bpn.Body);
            return el;
        }

        public XmlElement Visit(TraitDeclarationParseNode bpn)
        {
            var el = makeNode(bpn, "trait-declaration");
            addProperty(el, "signature", bpn.Signature);
            addProperty(el, "body", bpn.Body);
            return el;
        }

        public XmlElement Visit(ReturnParseNode rpn)
        {
            var el = makeNode(rpn, "return");
            addProperty(el, "returnvalue", rpn.ReturnValue);
            return el;
        }

        public XmlElement Visit(CommentParseNode cpn)
        {
            var el = makeNode(cpn, "comment");
            addProperty(el, "value", cpn.Value);
            return el;
        }

        public XmlElement Visit(TypeStatementParseNode tspn)
        {
            var el = makeNode(tspn, "type-statement");
            addProperty(el, "basename", tspn.BaseName);
            addProperty(el, "genericparameters", tspn.GenericParameters);
            addProperty(el, "body", tspn.Body);
            return el;
        }

        public XmlElement Visit(InterfaceParseNode tpn)
        {
            var el = makeNode(tpn, "interface");
            addProperty(el, "name", tpn.Name);
            addProperty(el, "body", tpn.Body);
            return el;
        }

        public XmlElement Visit(ImportParseNode ipn)
        {
            var el = makeNode(ipn, "import");
            addProperty(el, "name", ipn.Name);
            addProperty(el, "path", ipn.Path);
            addProperty(el, "type", ipn.Type);
            return el;
        }

        public XmlElement Visit(DialectParseNode dpn)
        {
            var el = makeNode(dpn, "dialect");
            addProperty(el, "path", dpn.Path);
            return el;
        }

        public XmlElement Visit(InheritsParseNode ipn)
        {
            var el = makeNode(ipn, "inherits");
            addProperty(el, "from", ipn.From);
            addProperty(el, "aliases", ipn.Aliases);
            addProperty(el, "excludes", ipn.Excludes);
            return el;
        }

        public XmlElement Visit(UsesParseNode upn)
        {
            var el = makeNode(upn, "uses");
            addProperty(el, "from", upn.From);
            addProperty(el, "aliases", upn.Aliases);
            addProperty(el, "excludes", upn.Excludes);
            return el;
        }

        public XmlElement Visit(AliasParseNode ipn)
        {
            var el = makeNode(ipn, "alias");
            addProperty(el, "newname", ipn.NewName);
            addProperty(el, "oldname", ipn.OldName);
            return el;
        }

        public XmlElement Visit(ExcludeParseNode ipn)
        {
            var el = makeNode(ipn, "exclude");
            addProperty(el, "name", ipn.Name);
            return el;
        }

        public XmlElement Visit(ParenthesisedParseNode ppn)
        {
            var el = makeNode(ppn, "parenthesised");
            addProperty(el, "expression", ppn.Expression);
            return el;
        }

        public XmlElement Visit(ImplicitBracketRequestParseNode ibrpn)
        {
            var el = makeNode(ibrpn, "implicit-bracket-request");
            addProperty(el, "name", ibrpn.Name);
            addProperty(el, "arguments", ibrpn.Arguments);
            return el;
        }

        public XmlElement Visit(ExplicitBracketRequestParseNode ebrpn)
        {
            var el = makeNode(ebrpn, "explicit-bracket-request");
            addProperty(el, "name", ebrpn.Name);
            addProperty(el, "receiver", ebrpn.Receiver);
            addProperty(el, "arguments", ebrpn.Arguments);
            return el;
        }

        public XmlElement Visit(SignatureParseNode spn)
        {
            var el = makeNode(spn, "signature");
            addProperty(el, "name", spn.Name);
            addProperty(el, "returntype", spn.ReturnType);
            addProperty(el, "annotations", spn.Annotations);
            addProperty(el, "parts", spn.Parts);
            return el;
        }

        public XmlElement Visit(OrdinarySignaturePartParseNode osppn)
        {
            var el = makeNode(osppn, "ordinary-signature-part");
            addProperty(el, "name", osppn.Name);
            addProperty(el, "final", osppn.Final);
            addProperty(el, "genericparameters", osppn.GenericParameters);
            addProperty(el, "parameters", osppn.Parameters);
            return el;
        }
    }

}
