using System;
using System.Collections.Generic;
using System.Linq;
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

        public static GraceObject Instantiate(
            EvaluationContext ctx)
        {
            return new ExposedCompiler();
        }

        public ExposedCompiler() : base("platform/kernancompiler")
        {
            AddMethod("parse(_)", new DelegateMethod1(code => mParse(GraceString.Create("source code"), code)));
            AddMethod("parse(_,_)",
                new DelegateMethodReq((ctx, req) => {
                MethodHelper.CheckArity(ctx, req, 2);
                return mParse(req[0].Arguments[0], req[0].Arguments[1]); }));
            AddMethod("parseFile(_)", new DelegateMethod1(mParseFile));
            AddMethod("readGraceModule(_)", new DelegateMethod1Ctx(mReadGraceModule));
            AddMethod("translateFile(_)", new DelegateMethod1(mTranslateFile));
            AddMethod("parseNodes", new DelegateMethod0(() => ParseNodeMeta.GetPatternDict()));
            AddMethod("args", new DelegateMethod0(
                () => GraceVariadicList.Of(UnusedArguments.UnusedArgs.Select(GraceString.Create))));
        }

        private GraceObject mParse(GraceObject gmodulename, GraceObject gcode)
        {
            String modulename = gmodulename.FindNativeParent<GraceString>().Value;
            String code = gcode.FindNativeParent<GraceString>().Value;
            return new GraceObjectProxy(new Parser(modulename, code));
        }

        private GraceObject mParseFile(GraceObject gpath)
        {
            String path = gpath.FindNativeParent<GraceString>().Value;
            using (StreamReader reader = File.OpenText(path))
            {
                return new GraceObjectProxy(new Parser(path, reader.ReadToEnd()).Parse());
            }
        }

        private GraceObject mReadGraceModule(EvaluationContext ctx, GraceObject gpath)
        {
            var itp = (Interpreter)ctx;
            String path = gpath.FindNativeParent<GraceString>().Value;

            var name = Path.GetFileName(path);
            var bases = itp.GetModulePaths();
            foreach (var p in bases)
            {
                string filePath;

                filePath = Path.Combine(p, path + ".grace");

                String mod_contents = itp.TryReadModuleFile(filePath, path);
                if (mod_contents != null)
                {
                    return GraceString.Create(mod_contents.Replace(Environment.NewLine, "\u2028"));
                }
            }
            if (itp.FailedImportHook != null)
            {
                // Optionally, the host program can try to satisfy a module
                // and indicate that we should retry the import.
                if (itp.FailedImportHook(path, itp))
                {
                    return mReadGraceModule(itp, gpath);
                }
            }
            ErrorReporting.RaiseError(itp, "R2005",
                new Dictionary<string, string> { { "path", path } },
                "LookupError: Could not find module ${path}");
            return null;
        }

        private GraceObject mTranslateFile(GraceObject gpath)
        {
            String path = gpath.FindNativeParent<GraceString>().Value;
            using (StreamReader reader = File.OpenText(path))
            {
                var p = new Parser(reader.ReadToEnd());
                var module = p.Parse();
                ExecutionTreeTranslator ett = new ExecutionTreeTranslator();
                Node eModule = ett.Translate(module as ObjectParseNode);
                return eModule;
            }
        }
    }
}
