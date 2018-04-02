using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grace.Execution
{
    /// <summary>Visitor for execution tree Nodes</summary>
    /// <typeparam name="T">Type nodes are mapped to</typeparam>
    public interface ASTNodeVisitor<T>
    {
        /// <summary>Visit a Node</summary>
        /// <param name="n">Node to visit</param>
        T Visit(Node n);
        /// <summary>Visit an ImplicitNode</summary>
        /// <param name="n">ImplicitNode to visit</param>
        T Visit(ImplicitNode n);
        /// <summary>Visit a DialectNode</summary>
        /// <param name="n">DialectNode to visit</param>
        T Visit(DialectNode n);
        /// <summary>Visit an ImportNode</summary>
        /// <param name="n">ImportNode to visit</param>
        T Visit(ImportNode n);
        /// <summary>Visit an ExplicitReceiverRequestNode</summary>
        /// <param name="n">ExplicitReceiverRequestNode to visit</param>
        T Visit(ExplicitReceiverRequestNode n);
        /// <summary>Visit an ImplicitReceiverRequestNode</summary>
        /// <param name="n">ImplicitReceiverRequestNode to visit</param>
        T Visit(ImplicitReceiverRequestNode n);
        /// <summary>Visit a PreludeRequestNode</summary>
        /// <param name="n">PreludeRequestNode to visit</param>
        T Visit(PreludeRequestNode n);
        /// <summary>Visit a RequestPartNode</summary>
        /// <param name="n">RequestPartNode to visit</param>
        T Visit(RequestPartNode n);
        /// <summary>Visit an ObjectConstructorNode</summary>
        /// <param name="n">ObjectConstructorNode to visit</param>
        T Visit(ObjectConstructorNode n);
        /// <summary>Visit a MethodNode</summary>
        /// <param name="n">MethodNode to visit</param>
        T Visit(MethodNode n);
        /// <summary>Visit a BlockNode</summary>
        /// <param name="n">BlockNode to visit</param>
        T Visit(BlockNode n);
        /// <summary>Visit a NumberLiteralNode</summary>
        /// <param name="n">NumberLiteralNode to visit</param>
        T Visit(NumberLiteralNode n);
        /// <summary>Visit a StringLiteralNode</summary>
        /// <param name="n">StringLiteralNode to visit</param>
        T Visit(StringLiteralNode n);
        /// <summary>Visit an IdentifierNode</summary>
        /// <param name="n">IdentifierNode to visit</param>
        T Visit(IdentifierNode n);
        /// <summary>Visit a VarDeclarationNode</summary>
        /// <param name="n">VarDeclarationNode to visit</param>
        T Visit(VarDeclarationNode n);
        /// <summary>Visit a DefDeclarationNode</summary>
        /// <param name="n">DefDeclarationNode to visit</param>
        T Visit(DefDeclarationNode n);
        /// <summary>Visit a ReturnNode</summary>
        /// <param name="n">ReturnNode to visit</param>
        T Visit(ReturnNode n);
        /// <summary>Visit a NoopNode</summary>
        /// <param name="n">NoopNode to visit</param>
        T Visit(NoopNode n);
        /// <summary>Visit an InterfaceNode</summary>
        /// <param name="n">InterfaceNode to visit</param>
        T Visit(InterfaceNode n);
        /// <summary>Visit a ParameterNode</summary>
        /// <param name="n">ParameterNode to visit</param>
        T Visit(ParameterNode n);
        /// <summary>Visit an InheritsNode</summary>
        /// <param name="n">InheritsNode to visit</param>
        T Visit(InheritsNode n);
        /// <summary>Visit an AnnotationsNode</summary>
        /// <param name="n">AnnotationsNode to visit</param>
        T Visit(AnnotationsNode n);
        /// <summary>Visit an OrdinarySignaturePartNode</summary>
        /// <param name="n">OrdinarySignaturePartNode to visit</param>
        T Visit(OrdinarySignaturePartNode n);
    }
}
