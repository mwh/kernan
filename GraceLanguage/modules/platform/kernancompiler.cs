using System.Collections.Generic;
using System.IO;
using Grace.Parsing;
using Grace.Execution;
using Grace.Runtime;
using Grace.Utility;
using Grace;


namespace KernanCompiler
{
    [ModuleEntryPoint]
    public class ExposedCompiler : GraceObject
    {
        private DictionaryDataObject parseNodes;

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new ExposedCompiler();
        }

        public ExposedCompiler() : base("platform/kernancompiler")
        {
            AddMethod("parse(_)", new DelegateMethod1(
                        new NativeMethod1(mParse)));
            AddMethod("parseFile(_)", new DelegateMethod1(
                        new NativeMethod1(mParseFile)));
            AddMethod("translateFile(_)", new DelegateMethod1(
                        new NativeMethod1(mTranslateFile)));
            AddMethod("parseNodes", new DelegateMethod0(
                        new NativeMethod0(mParseNodes)));
            AddMethod("args", new DelegateMethod0(
                        new NativeMethod0(mArguments)));
        }

        private GraceObject mParse(GraceObject code)
        {
            GraceString gs = code.FindNativeParent<GraceString>();
            string s = gs.Value;
            var p = new Parser(s);
            return new GraceObjectProxy(p.Parse());
        }

        private GraceObject mParseFile(GraceObject code)
        {
            GraceString gs = code.FindNativeParent<GraceString>();
            string path = gs.Value;
            using (StreamReader reader = File.OpenText(path))
            {
                var p= new Parser(reader.ReadToEnd());
                return new GraceObjectProxy(p.Parse());
            }
        }

        private GraceObject mTranslateFile(GraceObject code)
        {
            GraceString gs = code.FindNativeParent<GraceString>();
            string path = gs.Value;
            using (StreamReader reader = File.OpenText(path))
            {
                var p = new Parser(reader.ReadToEnd());
                var module = p.Parse();
                ExecutionTreeTranslator ett = new ExecutionTreeTranslator();
                Node eModule = ett.Translate(module as ObjectParseNode);
                return eModule;
            }
        }

        private GraceObject mArguments()
        {
            IList<string> unusedArguments = UnusedArguments.UnusedArgs;
            IList<GraceString> graceUnusedArguments = new List<GraceString>();

            foreach (var a in unusedArguments)
                graceUnusedArguments.Add(GraceString.Create(a));

            return GraceVariadicList.Of(graceUnusedArguments);
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
                    { "Interface", new NativeTypePattern<InterfaceParseNode>() },
                    { "Signature",
                        new NativeTypePattern<SignatureParseNode>() },
                    { "SignaturePart",
                        new NativeTypePattern<SignaturePartParseNode>() },
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
            AddMethod("match(_)", new DelegateMethod1Ctx(
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
