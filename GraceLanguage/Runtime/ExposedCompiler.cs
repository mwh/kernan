using System.Collections.Generic;
using Grace.Parsing;
using Grace.Execution;

namespace Grace.Runtime
{
    class ExposedCompiler : GraceObject
    {
        private DictionaryDataObject parseNodes;

        public ExposedCompiler() : base("!compiler")
        {
            AddMethod("parse", new DelegateMethodNode1(
                        new NativeMethod1(mParse)));
            AddMethod("parseNodes", new DelegateMethodNode0(
                        new NativeMethod0(mParseNodes)));
        }

        private GraceObject mParse(GraceObject code)
        {
            GraceString gs = code.FindNativeParent<GraceString>();
            string s = gs.Value;
            var p = new Parser(s);
            return new GraceObjectProxy(p.Parse());
        }

        private GraceObject mParseNodes()
        {
            if (parseNodes == null)
            {
                parseNodes = new DictionaryDataObject {
                    { "Object", new NativeTypePattern<ObjectParseNode>() },
                    { "MethodDeclaration",
                        new NativeTypePattern<MethodDeclarationParseNode>() },
                    { "ClassDeclaration",
                        new NativeTypePattern<ClassDeclarationParseNode>() },
                    { "TypeStatement",
                        new NativeTypePattern<TypeStatementParseNode>() },
                    { "Type", new NativeTypePattern<TypeParseNode>() },
                    { "TypeMethod",
                        new NativeTypePattern<TypeMethodParseNode>() },
                    { "Block", new NativeTypePattern<BlockParseNode>() },
                    { "VarArgsParameter",
                        new NativeTypePattern<VarArgsParameterParseNode>() },
                    { "TypedParameter",
                        new NativeTypePattern<TypedParameterParseNode>() },
                    { "VarDeclaration",
                        new NativeTypePattern<VarDeclarationParseNode>() },
                    { "DefDeclaration",
                        new NativeTypePattern<DefDeclarationParseNode>() },
                    { "Annotations",
                        new NativeTypePattern<AnnotationsParseNode>() },
                    { "Operator", new NativeTypePattern<OperatorParseNode>() },
                    { "PrefixOperator",
                        new NativeTypePattern<PrefixOperatorParseNode>() },
                    { "Bind", new NativeTypePattern<BindParseNode>() },
                    { "Number", new NativeTypePattern<NumberParseNode>() },
                    { "Identifier",
                        new NativeTypePattern<IdentifierParseNode>() },
                    { "StringLiteral",
                        new NativeTypePattern<StringLiteralParseNode>() },
                    { "InterpolatedString",
                        new NativeTypePattern<InterpolatedStringParseNode>() },
                    { "ImplicitBracketRequest",
                        new NativeTypePattern
                            <ImplicitBracketRequestParseNode>() },
                    { "ExplicitBracketRequest",
                        new NativeTypePattern
                            <ExplicitBracketRequestParseNode>() },
                    { "ImplicitReceiverRequest",
                        new NativeTypePattern
                            <ImplicitReceiverRequestParseNode>() },
                    { "ExplicitReceiverRequest",
                        new NativeTypePattern
                            <ExplicitReceiverRequestParseNode>() },
                    { "Inherits", new NativeTypePattern<InheritsParseNode>() },
                    { "Import", new NativeTypePattern<ImportParseNode>() },
                    { "Dialect", new NativeTypePattern<DialectParseNode>() },
                    { "Return", new NativeTypePattern<ReturnParseNode>() },
                    { "Parenthesised",
                        new NativeTypePattern<ParenthesisedParseNode>() },
                    { "Comment", new NativeTypePattern<CommentParseNode>() },

                };
            }
            return parseNodes;
        }
    }

    class NativeTypePattern<T> : GraceObject {
        public NativeTypePattern()
        {
            AddMethod("match", new DelegateMethodNode1Ctx(
                        new NativeMethod1Ctx(mMatch)));
            AddMethod("|", Matching.OrMethod);
            AddMethod("&", Matching.AndMethod);
        }

        /// <summary>Native method for Grace match</summary>
        /// <param name="ctx">Current interpreter</param>
        /// <param name="target">Target of the match</param>
        private GraceObject mMatch(EvaluationContext ctx, GraceObject target)
        {
            var gop = target as GraceObjectProxy;
            if (gop == null)
                return Matching.FailedMatch(ctx, target);
            if (gop.Object is T)
                return Matching.SuccessfulMatch(ctx, target);
            return Matching.FailedMatch(ctx, target);
        }
    }

    class DictionaryDataObject : GraceObject,
        IEnumerable<KeyValuePair<string, GraceObject>>
    {
        private Dictionary<string, GraceObject> data =
            new Dictionary<string, GraceObject>();

        public void Add(string key, GraceObject val)
        {
            data.Add(key, val);
        }

        public override GraceObject Request(EvaluationContext ctx,
                MethodRequest req)
        {
            if (data.ContainsKey(req.Name))
                return data[req.Name];
            return base.Request(ctx, req);
        }

        public IEnumerator<KeyValuePair<string, GraceObject>> GetEnumerator()
        {
            return data.GetEnumerator();
        }

        System.Collections.IEnumerator
            System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
