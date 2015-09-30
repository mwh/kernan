using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grace.Parsing
{
    /// <summary>Visitor for tree of ParseNodes</summary>
    /// <typeparam name="T">Type nodes are mapped to</typeparam>
    public interface ParseNodeVisitor<T>
    {
        /// <summary>Visit a ParseNode</summary>
        /// <param name="p">ParseNode to visit</param>
        T Visit(ParseNode p);
        /// <summary>Visit an ObjectParseNode</summary>
        /// <param name="o">ObjectParseNode to visit</param>
        T Visit(ObjectParseNode o);
        /// <summary>Visit a NumberParseNode</summary>
        /// <param name="n">NumberParseNode to visit</param>
        T Visit(NumberParseNode n);
        /// <summary>Visit a MethodDeclarationParseNode</summary>
        /// <param name="d">MethodDeclarationParseNode to visit</param>
        T Visit(MethodDeclarationParseNode d);
        /// <summary>Visit an IdentifierParseNode</summary>
        /// <param name="i">IdentifierParseNode to visit</param>
        T Visit(IdentifierParseNode i);
        /// <summary>Visit an ImplicitReceiverRequestParseNode</summary>
        /// <param name="irrpn">ImplicitReceiverRequestParseNode to visit</param>
        T Visit(ImplicitReceiverRequestParseNode irrpn);
        /// <summary>Visit an ExplicitReceiverRequestParseNode</summary>
        /// <param name="errpn">ExplicitReceiverRequestParseNode to visit</param>
        T Visit(ExplicitReceiverRequestParseNode errpn);
        /// <summary>Visit an OperatorParseNode</summary>
        /// <param name="opn">OperatorParseNode to visit</param>
        T Visit(OperatorParseNode opn);
        /// <summary>Visit a StringLiteralParseNode</summary>
        /// <param name="slpn">StringLiteralParseNode to visit</param>
        T Visit(StringLiteralParseNode slpn);
        /// <summary>Visit an InterpolatedStringParseNode</summary>
        /// <param name="ispn">InterpolatedStringParseNode to visit</param>
        T Visit(InterpolatedStringParseNode ispn);
        /// <summary>Visit a VarDeclarationParseNode</summary>
        /// <param name="vdpn">VarDeclarationParseNode to visit</param>
        T Visit(VarDeclarationParseNode vdpn);
        /// <summary>Visit a DefDeclarationParseNode</summary>
        /// <param name="vdpn">DefDeclarationParseNode to visit</param>
        T Visit(DefDeclarationParseNode vdpn);
        /// <summary>Visit a BindParseNode</summary>
        /// <param name="bpn">BindParseNode to visit</param>
        T Visit(BindParseNode bpn);
        /// <summary>Visit a PrefixOperatorParseNode</summary>
        /// <param name="popn">PrefixOperatorParseNode to visit</param>
        T Visit(PrefixOperatorParseNode popn);
        /// <summary>Visit a BlockParseNode</summary>
        /// <param name="bpn">BlockParseNode to visit</param>
        T Visit(BlockParseNode bpn);
        /// <summary>Visit a ClassDeclarationParseNode</summary>
        /// <param name="bpn">ClassDeclarationParseNode to visit</param>
        T Visit(ClassDeclarationParseNode bpn);
        /// <summary>Visit a TraitDeclarationParseNode</summary>
        /// <param name="bpn">TraitDeclarationParseNode to visit</param>
        T Visit(TraitDeclarationParseNode bpn);
        /// <summary>Visit a ReturnParseNode</summary>
        /// <param name="rpn">ReturnParseNode to visit</param>
        T Visit(ReturnParseNode rpn);
        /// <summary>Visit a CommentParseNode</summary>
        /// <param name="cpn">CommentParseNode to visit</param>
        T Visit(CommentParseNode cpn);
        /// <summary>Visit a TypeStatementParseNode</summary>
        /// <param name="tspn">TypeStatementParseNode to visit</param>
        T Visit(TypeStatementParseNode tspn);
        /// <summary>Visit a TypeParseNode</summary>
        /// <param name="tpn">TypeParseNode to visit</param>
        T Visit(TypeParseNode tpn);
        /// <summary>Visit a ImportParseNode</summary>
        /// <param name="ipn">ImportParseNode to visit</param>
        T Visit(ImportParseNode ipn);
        /// <summary>Visit a DialectParseNode</summary>
        /// <param name="dpn">DialectParseNode to visit</param>
        T Visit(DialectParseNode dpn);
        /// <summary>Visit an InheritsParseNode</summary>
        /// <param name="ipn">InheritsParseNode to visit</param>
        T Visit(InheritsParseNode ipn);
        /// <summary>Visit a UsesParseNode</summary>
        /// <param name="upn">UsesParseNode to visit</param>
        T Visit(UsesParseNode upn);
        /// <summary>Visit an AliasParseNode</summary>
        /// <param name="ipn">AliasParseNode to visit</param>
        T Visit(AliasParseNode ipn);
        /// <summary>Visit an ExcludeParseNode</summary>
        /// <param name="ipn">ExcludeParseNode to visit</param>
        T Visit(ExcludeParseNode ipn);
        /// <summary>Visit a ParenthesisedParseNode</summary>
        /// <param name="ppn">ParenthesisedParseNode to visit</param>
        T Visit(ParenthesisedParseNode ppn);
        /// <summary>Visit an ImplicitBracketRequestParseNode</summary>
        /// <param name="ibrpn">ImplicitBracketRequestParseNode to visit</param>
        T Visit(ImplicitBracketRequestParseNode ibrpn);
        /// <summary>Visit an ExplicitBracketRequestParseNode</summary>
        /// <param name="ebrpn">ExplicitBracketRequestParseNode to visit</param>
        T Visit(ExplicitBracketRequestParseNode ebrpn);
        /// <summary>Visit a SignatureParseNode</summary>
        /// <param name="spn">SignatureParseNode to visit</param>
        T Visit(SignatureParseNode spn);
        /// <summary>Visit an OrdinarySignaturePartParseNode</summary>
        /// <param name="osppn">OrdinarySignaturePartParseNode to visit</param>
        T Visit(OrdinarySignaturePartParseNode osppn);
    }
}
