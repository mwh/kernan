using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grace.Execution;
using System.Xml;

namespace Grace.Utility
{
    class ExecutionTreeJSONVisitor : ASTNodeVisitor<XmlElement>
    {
        private XmlDocument document;
        private XmlElement parent;
        private XmlElement existingNode;

        public ExecutionTreeJSONVisitor(XmlDocument doc, XmlElement parent)
        {
            this.document = doc;
            this.parent = parent;
            existingNode = parent;
        }

        private XmlElement makeNode(Node o, string kind)
        {
            XmlElement el = makeNode((object)o, kind);
            var line = document.CreateElement("line");
            line.SetAttribute("type", "number");
            line.AppendChild(document.CreateTextNode(o.Origin.Line + ""));
            var column = document.CreateElement("column");
            column.SetAttribute("type", "number");
            column.AppendChild(document.CreateTextNode(o.Origin.Column + ""));
            el.AppendChild(line);
            el.AppendChild(column);
            el.SetAttribute("type", "object");
            existingNode = null;
            return el;
        }

        private XmlElement makeNode(object o, string kind)
        {
            XmlElement el = existingNode;
            if (el == null)
                el = document.CreateElement("node");
            var type = document.CreateElement("nodetype");
            type.AppendChild(document.CreateTextNode(kind));
            el.AppendChild(type);
            el.SetAttribute("type", "object");
            existingNode = null;
            return el;
        }

        private void addProperty<T>(XmlElement parent, string name,
            IList<T> body) where T : Node
        {
            var list = document.CreateElement(name);
            list.SetAttribute("type", "array");
            parent.AppendChild(list);
            foreach (var x in body)
            {
                var item = document.CreateElement("item");
                existingNode = item;
                list.AppendChild(x.Accept(this));
            }
        }

        private void addPropertyEnumRPN<T>(XmlElement parent, string name,
            IEnumerable<T> body) where T : RequestPartNode
        {
            var list = document.CreateElement(name);
            list.SetAttribute("type", "array");
            parent.AppendChild(list);
            foreach (var x in body)
            {
                var item = document.CreateElement("item");
                existingNode = item;
                list.AppendChild(x.Accept(this));
            }
        }


        private void addProperty(XmlElement parent, string name,
            IEnumerable<string> body)
        {
            var list = document.CreateElement(name);
            list.SetAttribute("type", "array");
            parent.AppendChild(list);
            foreach (var x in body)
            {
                var item = document.CreateElement("item");
                item.AppendChild(document.CreateTextNode(x));
                list.AppendChild(item);
            }
        }

        private void addProperty<T>(XmlElement parent, string name,
            IDictionary<string, T> body) where T : Node
        {
            var list = document.CreateElement(name);
            list.SetAttribute("type", "object");
            parent.AppendChild(list);
            foreach (var x in body)
            {
                addProperty(list, x.Key, x.Value);
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
            string name, AnnotationsNode value)
        {
            // For our purposes, an AnnotationsNode is just a list of
            // the annotations rather than handled as a node.
            addProperty(parent, name, (from x in value select x).ToList());
        }

        private void addProperty(XmlElement parent,
            string name, Node value)
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
            parent.AppendChild(value.Accept(this));
        }

        public XmlElement Visit(Node n)
        {
            throw new NotImplementedException();
        }

        public XmlElement Visit(ImplicitNode n)
        {
            var el = makeNode(n, "implicit");
            addProperty(el, "kind", n.Kind);
            return el;
        }

        public XmlElement Visit(DialectNode n)
        {
            var el = makeNode(n, "dialect");
            addProperty(el, "path", n.Path);
            return el;
        }

        public XmlElement Visit(ImportNode n)
        {
            var el = makeNode(n, "import");
            addProperty(el, "path", n.Path);
            addProperty(el, "type", n.Type);
            addProperty(el, "name", n.Name);
            return el;
        }

        public XmlElement Visit(ExplicitReceiverRequestNode n)
        {
            var el = makeNode(n, "explicit-receiver-request");
            addProperty(el, "receiver", n.Receiver);
            addProperty(el, "name", n.Name);
            addPropertyEnumRPN(el, "parts", n);
            return el;
        }

        public XmlElement Visit(ImplicitReceiverRequestNode n)
        {
            var el = makeNode(n, "implicit-receiver-request");
            addProperty(el, "name", n.Name);
            addPropertyEnumRPN(el, "parts", n);
            return el;
        }

        public XmlElement Visit(PreludeRequestNode n)
        {
            var el = makeNode(n, "prelude-request");
            addProperty(el, "name", n.Name);
            addPropertyEnumRPN(el, "parts", n);
            return el;
        }

        public XmlElement Visit(RequestPartNode n)
        {
            var el = makeNode(n, "request-part");
            addProperty(el, "name", n.Name);
            addProperty(el, "arguments", n.Arguments);
            addProperty(el, "genericarguments", n.GenericArguments);
            addProperty(el, "basename", n.BaseName);
            return el;
        }

        public XmlElement Visit(ObjectConstructorNode n)
        {
            var el = makeNode(n, "object-constructor");
            addProperty(el, "body", n.Body);
            return el;
        }

        public XmlElement Visit(MethodNode n)
        {
            var el = makeNode(n, "method");
            addProperty(el, "signature", n.Signature);
            addProperty(el, "body", n.Body);
            addProperty(el, "annotations", n.Annotations);
            addProperty(el, "abstract", n.Abstract);
            addProperty(el, "fresh", n.Fresh);
            return el;
        }

        public XmlElement Visit(BlockNode n)
        {
            var el = makeNode(n, "block");
            addProperty(el, "body", n.Body);
            addProperty(el, "parameters", n.Parameters);
            return el;
        }

        public XmlElement Visit(NumberLiteralNode n)
        {
            var el = makeNode(n, "number-literal");
            addProperty(el, "numerator", n.Value.Numerator.ToString());
            addProperty(el, "denominator", n.Value.Denominator.ToString());
            return el;
        }

        public XmlElement Visit(StringLiteralNode n)
        {
            var el = makeNode(n, "string-literal");
            addProperty(el, "value", n.Value);
            return el;
        }

        public XmlElement Visit(IdentifierNode n)
        {
            var el = makeNode(n, "identifier");
            addProperty(el, "name", n.Name);
            return el;
        }

        public XmlElement Visit(VarDeclarationNode n) {
            var el = makeNode(n, "var-declaration");
            addProperty(el, "name", n.Name);
            addProperty(el, "value", n.Value);
            addProperty(el, "annotations", n.Annotations);
            addProperty(el, "type", n.Type);
            addProperty(el, "readable", n.Readable);
            addProperty(el, "writable", n.Writable);
            return el;
        }

        public XmlElement Visit(DefDeclarationNode n)
        {
            var el = makeNode(n, "def-declaration");
            addProperty(el, "name", n.Name);
            addProperty(el, "value", n.Value);
            addProperty(el, "annotations", n.Annotations);
            addProperty(el, "type", n.Type);
            return el;
        }

        public XmlElement Visit(ReturnNode n)
        {
            var el = makeNode(n, "return");
            addProperty(el, "value", n.Value);
            return el;
        }

        public XmlElement Visit(NoopNode n)
        {
            var el = makeNode(n, "noop");
            return el;
        }

        public XmlElement Visit(InterfaceNode n)
        {
            var el = makeNode(n, "interface");
            addProperty(el, "name", n.Name);
            addProperty(el, "body", n.Body);
            return el;
        }

        public XmlElement Visit(ParameterNode n)
        {
            var el = makeNode(n, "parameter");
            addProperty(el, "name", n.Name);
            addProperty(el, "type", n.Type);
            addProperty(el, "variadic", n.Variadic);
            return el;
        }

        public XmlElement Visit(InheritsNode n)
        {
            var el = makeNode(n, "inherits");
            addProperty(el, "from", n.From);
            addProperty(el, "aliases", n.Aliases);
            addProperty(el, "excludes", n.Excludes);
            return el;
        }

        public XmlElement Visit(SignatureNode n)
        {
            var el = makeNode(n, "signature");
            addProperty(el, "parts", n.Parts);
            addProperty(el, "annotations", n.Annotations);
            addProperty(el, "returnType", n.ReturnType);
            addProperty(el, "name", n.Name);
            return el;
        }

        public XmlElement Visit(AnnotationsNode n)
        {
            throw new NotImplementedException();
        }

        public XmlElement Visit(OrdinarySignaturePartNode n)
        {
            var el = makeNode(n, "ordinary-signature-part");
            addProperty(el, "name", n.Name);
            addProperty(el, "parameters", n.Parameters);
            addProperty(el, "genericparameters", n.GenericParameters);
            return el;
        }

    }
}
