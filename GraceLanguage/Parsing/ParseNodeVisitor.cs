using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grace.Parsing
{
    public interface ParseNodeVisitor<T>
    {
        T Visit(ParseNode p);
        T Visit(ObjectParseNode o);
        T Visit(NumberParseNode n);
        T Visit(MethodDeclarationParseNode d);
        T Visit(IdentifierParseNode i);
        T Visit(ImplicitReceiverRequestParseNode irrpn);
        T Visit(ExplicitReceiverRequestParseNode errpn);
        T Visit(OperatorParseNode opn);
        T Visit(StringLiteralParseNode slpn);
        T Visit(InterpolatedStringParseNode ispn);
        T Visit(VarDeclarationParseNode vdpn);
        T Visit(DefDeclarationParseNode vdpn);
        T Visit(BindParseNode bpn);
        T Visit(PrefixOperatorParseNode popn);
        T Visit(BlockParseNode bpn);
        T Visit(ClassDeclarationParseNode bpn);
        T Visit(ReturnParseNode rpn);
        T Visit(CommentParseNode cpn);
        T Visit(TypeStatementParseNode tspn);
        T Visit(TypeParseNode tpn);
        T Visit(TypeMethodParseNode tpn);
        T Visit(ImportParseNode ipn);
        T Visit(DialectParseNode ipn);
    }
}
