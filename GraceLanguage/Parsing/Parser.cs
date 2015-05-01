using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Globalization;
using Grace.Unicode;

namespace Grace.Parsing
{
    /// <summary>Parser for Grace code</summary>
    public class Parser
    {
        private Lexer lexer;
        private string code;
        private int indentColumn = 0;

        private string moduleName = "source code";

        private List<ParseNode> comments;

        private bool doNotAcceptDelimitedBlock = false;

        /// <param name="module">Module name for debugging</param>
        /// <param name="code">Complete source code of this module</param>
        public Parser(string module, string code)
        {
            this.moduleName = module;
            this.code = code;
        }

        /// <param name="code">Complete source code of this module</param>
        public Parser(string code)
        {
            this.code = code;
        }

        /// <summary>Parse the source code of this instance from the
        /// beginning</summary>
        /// <returns>Module object created from the code</returns>
        public ParseNode Parse()
        {
            ObjectParseNode module = new ObjectParseNode(
                    new UnknownToken(moduleName, 0, 0));
            if (code.Length == 0)
                return module;
            List<ParseNode> body = module.Body;
            lexer = new Lexer(moduleName, this.code);
            Token was = lexer.current;
            while (!lexer.Done())
            {
                consumeBlankLines();
                indentColumn = lexer.current.column;
                ParseNode n = parseStatement();
                body.Add(n);
                if (lexer.current == was)
                {
                    reportError("P1000", lexer.current, "Unknown construct");
                    break;
                }
                while (lexer.current is NewLineToken)
                    lexer.NextToken();
                was = lexer.current;
            }
            return module;
        }

        private void reportError(string code, Dictionary<string, string> vars,
                string localDescription)
        {
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line,
                    code,
                    vars,
                    localDescription);
        }

        private void reportError(string code, Token t1, string localDescription)
        {
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line,
                    code,
                    new Dictionary<string, string>() {
                        {"token", t1.ToString()}
                    },
                    localDescription);
        }

        private void reportError(string code, string localDescription)
        {
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line, code, localDescription);
        }

        /// <summary>True until a particular kind of token
        /// is found, for use in a loop</summary>
        /// <remarks>If EOF is reached, reports an error.</remarks>
        /// <param name="start">First token that led to this
        /// sequence, for use in error reporting</param>
        /// <typeparam name="T">Token class to search for</typeparam>
        private bool awaiting<T>(Token start) where T : Token
        {
            if (lexer.current is T)
                return false;
            if (lexer.current is EndToken)
                ErrorReporting.ReportStaticError(moduleName, start.line,
                        "P1001",
                        new Dictionary<string, string>
                        {
                            { "expected", typeof(T).Name },
                            { "found", lexer.current.ToString() }
                        },
                        "Unexpected end of file");
            return true;
        }

        /// <summary>Report an error if the current token is not
        /// a particular kind</summary>
        /// <typeparam name="T">Token class to expect</typeparam>
        private void expect<T>() where T : Token
        {
            if (lexer.current is T)
                return;
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line,
                    lexer.current is EndToken ? "P1001" : "P1002",
                    new Dictionary<string, string>() {
                        { "expected", typeof(T).Name },
                        { "found", lexer.current.ToString() }
                    },
                    "Expected something else, got " + lexer.current);
        }

        /// <summary>Report an error if the current token is not
        /// a particular kind</summary>
        /// <typeparam name="T">Token class to expect</typeparam>
        /// <param name="expectation">Description of what was expected
        /// instead</param>
        private void expect<T>(string expectation) where T : Token
        {
            if (lexer.current is T)
                return;
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line,
                    lexer.current is EndToken ? "P1001" : "P1002",
                    new Dictionary<string, string>() {
                        { "expected", expectation },
                        { "found", lexer.current.ToString() }
                    },
                    "Expected something else, got " + lexer.current);
        }

        /// <summary>Report a specific error if the current token is not
        /// a particular kind</summary>
        /// <typeparam name="T">Token class to expect</typeparam>
        /// <param name="code">Error code to report</param>
        private void expectWithError<T>(string code) where T : Token
        {
            if (lexer.current is T)
                return;
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line,
                    code,
                    new Dictionary<string, string>() {
                        { "expected", typeof(T).Name },
                        { "found", lexer.current.ToString() }
                    },
                    "Expected something else, got " + lexer.current);
        }

        /// <summary>Report a specific error if the current token is not
        /// a particular kind, with a string expectation</summary>
        /// <typeparam name="T">Token class to expect</typeparam>
        /// <param name="code">Error code to report</param>
        /// <param name="expectation">Description of what was expected
        /// instead</param>
        private void expectWithError<T>(string code, string expectation)
            where T : Token
        {
            if (lexer.current is T)
                return;
            ErrorReporting.ReportStaticError(moduleName, lexer.current.line,
                    code,
                    new Dictionary<string, string>() {
                        { "expected", expectation },
                        { "found", lexer.current.ToString() }
                    },
                    "Expected something else, got " + lexer.current);
        }


        /// <summary>Obtain the next meaningful token from the lexer,
        /// accounting for indentation rules and comments</summary>
        private Token nextToken()
        {
            lexer.NextToken();
            if (lexer.current is CommentToken)
            {
                takeComments();
            }
            if (lexer.current is NewLineToken)
            {
                // Check for continuation lines
                Token t = lexer.Peek();
                if (t.column > indentColumn)
                    lexer.NextToken();
            }
            if (lexer.current is CommentToken)
            {
                takeComments();
            }
            return lexer.current;
        }

        private void consumeBlankLines()
        {
            while (lexer.current is NewLineToken)
            {
                lexer.NextToken();
            }
        }

        private void skipSpaces()
        {
            while (lexer.current is SpaceToken || lexer.current is NewLineToken)
                lexer.NextToken();
        }

        /// <summary>Take a comment, if present, attach it to a node,
        /// and return that node</summary>
        /// <param name="to">Node to attach comment to</param>
        /// <typeparam name="T">Type of node</typeparam>
        private T attachComment<T>(T to)
            where T : ParseNode
        {
            if (lexer.current is CommentToken)
                comments.Add(parseComment());
            return to;
        }

        /// <summary>Attach many comments to a node</summary>
        /// <param name="node">Node to attach comments to</param>
        /// <param name="comments">Comment nodes to attach</param>
        private void attachComments(ParseNode node, List<ParseNode> comments)
        {
            if (comments.Count == 0)
            {
                return;
            }
            int startAt = 0;
            if (node.Comment == null)
            {
                ParseNode dest = comments.First();
                node.Comment = dest;
                startAt = 1;
            }
            ParseNode append = node.Comment;
            while (append.Comment != null)
                append = append.Comment;
            for (int i = startAt; i < comments.Count; i++)
            {
                ParseNode cur = comments[i];
                append.Comment = cur;
                while (append.Comment != null)
                    append = append.Comment;
            }
        }

        private List<ParseNode> prepareComments()
        {
            List<ParseNode> orig = comments;
            comments = new List<ParseNode>();
            return orig;
        }

        private void restoreComments(List<ParseNode> orig)
        {
            comments = orig;
        }

        private ParseNode parseStatement()
        {
            List<ParseNode> origComments = comments;
            comments = new List<ParseNode>();
            takeLineComments();
            Token start = lexer.current;
            ParseNode ret;
            if (lexer.current is RBraceToken && comments.Count == 0)
            {
                reportError("P1029", "Unpaired closing brace found");
            }
            if ((lexer.current is NewLineToken || lexer.current is EndToken
                        || lexer.current is RBraceToken)
                    && comments.Count != 0)
            {
                // Took line comments, followed by a blank
                ret = collapseComments(comments);
                comments = new List<ParseNode>();
            }
            else if (lexer.current is CommentToken)
                ret = parseComment();
            else if (lexer.current is VarKeywordToken)
                ret = parseVarDeclaration();
            else if (lexer.current is DefKeywordToken)
                ret = parseDefDeclaration();
            else if (lexer.current is MethodKeywordToken)
                ret = parseMethodDeclaration();
            else if (lexer.current is ClassKeywordToken)
                ret = parseClassDeclaration();
            else if (lexer.current is InheritsKeywordToken)
                ret = parseInherits();
            else if (lexer.current is ImportKeywordToken)
                ret = parseImport();
            else if (lexer.current is DialectKeywordToken)
                ret = parseDialect();
            else if (lexer.current is ReturnKeywordToken)
                ret = parseReturn();
            else if (lexer.current is TypeKeywordToken)
                ret = parseTypeStatement();
            else
            {
                ret = parseExpression();
                if (lexer.current is BindToken)
                {
                    nextToken();
                    ParseNode expr = parseExpression();
                    ret = new BindParseNode(start, ret, expr);
                }
            }
            if (lexer.current is SemicolonToken)
            {
                lexer.NextToken();
                if (!(lexer.current is NewLineToken
                            || lexer.current is CommentToken
                            || lexer.current is EndToken
                            || lexer.current is RBraceToken))
                    reportError("P1003", "Other code cannot follow a semicolon on the same line.");
            }
            if (!(lexer.current is NewLineToken
                        || lexer.current is CommentToken
                        || lexer.current is EndToken
                        || lexer.current is RBraceToken))
            {
                if (start.line == lexer.current.line
                        || lexer.current.line == lexer.previous.line)
                    reportError("P1004", lexer.current,
                            "Unexpected token after statement.");
                else
                    reportError("P1030", lexer.current,
                            "Unexpected continuation token after statement.");
            }
            while (lexer.current is NewLineToken)
                lexer.NextToken();
            attachComments(ret, comments);
            comments = origComments;
            return ret;
        }

        private ParseNode parseVarDeclaration()
        {
            Token start = lexer.current;
            nextToken();
            expect<IdentifierToken>();
            ParseNode name = parseIdentifier();
            ParseNode type = null;
            if (lexer.current is ColonToken)
            {
                type = parseTypeAnnotation();
            }
            var annotations = parseAnnotations();
            ParseNode val = null;
            if (lexer.current is BindToken)
            {
                nextToken();
                val = parseExpression();
            }
            else if (lexer.current is SingleEqualsToken)
            {
                reportError("P1005", "var declarations use ':='.");
            }
            return new VarDeclarationParseNode(start, name, val,
                        type, annotations);
        }

        private ParseNode parseDefDeclaration()
        {
            Token start = lexer.current;
            nextToken();
            expect<IdentifierToken>();
            ParseNode name = parseIdentifier();
            ParseNode type = null;
            if (lexer.current is ColonToken)
            {
                type = parseTypeAnnotation();
            }
            var annotations = parseAnnotations();
            if (lexer.current is BindToken)
            {
                reportError("P1006", "def declarations use '='.");
            }
            expect<SingleEqualsToken>();
            nextToken();
            ParseNode val = parseExpression();
            return new DefDeclarationParseNode(start, name, val,
                        type, annotations);
        }

        private AnnotationsParseNode parseAnnotations()
        {
            if (!(lexer.current is IsKeywordToken))
                return null;
            AnnotationsParseNode ret = new AnnotationsParseNode(lexer.current);
            nextToken();
            while (lexer.current is IdentifierToken)
            {
                doNotAcceptDelimitedBlock = true;
                ret.AddAnnotation(parseExpression());
                doNotAcceptDelimitedBlock = false;
                if (lexer.current is CommaToken)
                    nextToken();
                else
                    break;
            }
            return ret;
        }

        private void parseMethodHeader(Token start, MethodHeader ret)
        {
            var op = lexer.current as OperatorToken;
            var ob = lexer.current as OpenBracketToken;;
            if (op != null)
            {
                ParseNode partName = new IdentifierParseNode(op);
                nextToken();
                PartParameters pp = ret.AddPart(partName);
                List<ParseNode> theseParameters = pp.Ordinary;
                if (lexer.current is LParenToken)
                {
                    Token lp = lexer.current;
                    nextToken();
                    parseParameterList<RParenToken>(lp, theseParameters);
                    expect<RParenToken>();
                    nextToken();
                }
            }
            else if (ob != null)
            {
                ParseNode partName = new IdentifierParseNode(ob);
                nextToken();
                expectWithError<CloseBracketToken>("P1033",  ob.Name + ob.Other);
                var cb = (CloseBracketToken)lexer.current;
                if (cb.Name != ob.Other)
                {
                    ErrorReporting.ReportStaticError(moduleName, cb.line,
                            "P1033",
                            new Dictionary<string, string>() {
                                { "expected", ob.Name + ob.Other },
                                { "found", cb.Name }
                            },
                            "Expected bracket name ${expected}, got ${found}.");
                }
                nextToken();
                PartParameters pp = ret.AddPart(partName);
                List<ParseNode> theseParameters = pp.Ordinary;
                if (lexer.current is LParenToken)
                {
                    Token lp = lexer.current;
                    nextToken();
                    parseParameterList<RParenToken>(lp, theseParameters);
                    expect<RParenToken>();
                    nextToken();
                }
            }
            else
            {
                expectWithError<IdentifierToken>("P1032");
                IdentifierParseNode partName;
                bool first = true;
                while (lexer.current is IdentifierToken)
                {
                    partName = parseIdentifier();
                    if (lexer.current is BindToken)
                    {
                        partName.Name += ":=";
                        nextToken();
                    }
                    else if ("prefix" == partName.Name && first
                          && lexer.current is OperatorToken)
                    {
                        op = lexer.current as OperatorToken;
                        partName.Name += op.Name;
                        nextToken();
                    }
                    else if ("circumfix" == partName.Name && first
                          && lexer.current is OpenBracketToken)
                    {
                        ob = (OpenBracketToken)lexer.current;
                        partName.Name += ob.Name + ob.Other;
                        nextToken();
                        nextToken();
                    }
                    first = false;
                    PartParameters pp = ret.AddPart(partName);
                    List<ParseNode> theseParameters = pp.Ordinary;
                    List<ParseNode> theseGenerics = pp.Generics;
                    if (lexer.current is LGenericToken)
                    {
                        Token lp = lexer.current;
                        nextToken();
                        parseParameterList<RGenericToken>(lp, theseGenerics);
                        expect<RGenericToken>();
                        nextToken();
                    }
                    if (lexer.current is LParenToken)
                    {
                        Token lp = lexer.current;
                        nextToken();
                        parseParameterList<RParenToken>(lp, theseParameters);
                        expect<RParenToken>();
                        nextToken();
                    }
                }
            }
            if (lexer.current is ArrowToken)
            {
                nextToken();
                doNotAcceptDelimitedBlock = true;
                ret.ReturnType = parseExpression();
                doNotAcceptDelimitedBlock = false;
            }
            ret.Annotations = parseAnnotations();
        }


        private ParseNode parseMethodDeclaration()
        {
            Token start = lexer.current;
            nextToken();
            MethodDeclarationParseNode ret = new MethodDeclarationParseNode(start);
            parseMethodHeader(start, ret);
            expect<LBraceToken>();
            List<ParseNode> origComments = prepareComments();
            parseBraceDelimitedBlock(ret.Body);
            attachComments(ret, comments);
            restoreComments(origComments);
            return ret;
        }

        private ParseNode parseClassDeclaration()
        {
            Token start = lexer.current;
            nextToken();
            expect<IdentifierToken>();
            ParseNode baseName = parseIdentifier();
            expect<DotToken>();
            nextToken();
            ClassDeclarationParseNode ret = new ClassDeclarationParseNode(start, baseName);
            parseMethodHeader(start, ret);
            expect<LBraceToken>();
            List<ParseNode> origComments = prepareComments();
            parseBraceDelimitedBlock(ret.Body);
            attachComments(ret, comments);
            restoreComments(origComments);
            return ret;
        }

        private ParseNode parseTypeStatement()
        {
            Token start = lexer.current;
            Token peeked = lexer.Peek();
            if (peeked is LBraceToken)
                return parseType();
            nextToken();
            expectWithError<IdentifierToken>("P1034");
            ParseNode name = parseIdentifier();
            List<ParseNode> genericParameters = new List<ParseNode>();
            if (lexer.current is LGenericToken)
            {
                nextToken();
                while (lexer.current is IdentifierToken)
                {
                    genericParameters.Add(parseIdentifier());
                    if (lexer.current is CommaToken)
                        nextToken();
                }
                if (!(lexer.current is RGenericToken))
                    reportError("P1007", "Unterminated generic type parameter list.");
                nextToken();
            }
            else if (lexer.current is OperatorToken)
            {
                OperatorToken op = lexer.current as OperatorToken;
                if (op.Name == "<")
                    reportError("P1008", "Generic '<' must not have spaces around it.");
                else
                    reportError("P1009", "Unexpected operator in type name, expected '<'.");
            }
            expect<SingleEqualsToken>();
            nextToken();
            Token ts = lexer.current;
            ParseNode type = null;
            if (lexer.current is TypeKeywordToken)
            {
                nextToken();
            }
            else if (lexer.current is IdentifierToken)
            {
                type = parseIdentifier();
            }
            if (type == null)
            {
                List<ParseNode> origComments = prepareComments();
                List<ParseNode> body = parseTypeBody();
                type = new TypeParseNode(ts, body);
                attachComments(type, comments);
                restoreComments(origComments);
            }
            type = expressionRest(type);
            return new TypeStatementParseNode(start, name, type, genericParameters);
        }

        private ParseNode parseType()
        {
            Token start = lexer.current;
            expect<TypeKeywordToken>();
            nextToken();
            expect<LBraceToken>();
            List<ParseNode> origComments = prepareComments();
            List<ParseNode> body = parseTypeBody();
            ParseNode ret = new TypeParseNode(start, body);
            attachComments(ret, comments);
            restoreComments(origComments);
            return ret;
        }

        private List<ParseNode> parseTypeBody()
        {
            expect<LBraceToken>();
            int indentBefore = indentColumn;
            Token start = lexer.current;
            nextToken();
            takeLineComments();
            consumeBlankLines();
            if (lexer.current is RBraceToken)
            {
                nextToken();
                return new List<ParseNode>();
            }
            indentColumn = lexer.current.column;
            if (indentColumn <= indentBefore)
                reportError("P1010", new Dictionary<string, string>() {
                        { "previous indent", "" + (indentBefore - 1) },
                        { "new indent", "" + (indentColumn - 1) }
                    },
                    "Indentation must increase inside {}.");
            List<ParseNode> ret = new List<ParseNode>();
            while (awaiting<RBraceToken>(start))
            {
                List<ParseNode> origComments = prepareComments();
                takeLineComments();
                TypeMethodParseNode tmn = new TypeMethodParseNode(lexer.current);
                parseMethodHeader(lexer.current, tmn);
                ret.Add(tmn);
                attachComments(tmn, comments);
                restoreComments(origComments);
                consumeBlankLines();
            }
            lexer.NextToken();
            indentColumn = indentBefore;
            return ret;
        }

        private void parseParameterList<Terminator>(Token start,
                List<ParseNode> parameters)
            where Terminator : Token
        {
            while (awaiting<Terminator>(start))
            {
                ParseNode param = null;
                if (lexer.current is IdentifierToken)
                {
                    Token after = lexer.Peek();
                    if (after is ColonToken)
                    {
                        ParseNode id = parseTerm();
                        ParseNode type = parseTypeAnnotation();
                        param = new TypedParameterParseNode(id, type);
                    }
                    else if (after is CommaToken || after is Terminator)
                    {
                        param = parseTerm();
                    }
                    else if (lexer.current is IdentifierToken)
                    {
                        param = parseIdentifier();
                    }
                    else
                    {
                        reportError("P1013", after,
                                "In parameter list, expected "
                                + " ',' or end of list.");
                    }
                }
                else if (lexer.current is OperatorToken)
                {
                    // This must be varargs
                    OperatorToken op = lexer.current as OperatorToken;
                    if ("*" != op.Name)
                        reportError("P1012", new Dictionary<string, string>() { { "operator", op.Name } },
                                "Unexpected operator in parameter list.");
                    nextToken();
                    expectWithError<IdentifierToken>("P1031");
                    ParseNode id = parseIdentifier();
                    param = id;
                    if (lexer.current is ColonToken)
                    {
                        ParseNode type = parseTypeAnnotation();
                        param = new TypedParameterParseNode(param, type);
                    }
                    param = new VarArgsParameterParseNode(param);
                }
                if (param != null)
                    parameters.Add(param);
                else
                    expectWithError<IdentifierToken>("P1031");
                if (lexer.current is CommaToken)
                    nextToken();
                else if (!(lexer.current is Terminator))
                {
                    reportError("P1013",
                            new Dictionary<string, string> {
                                { "token", lexer.current.ToString() },
                                { "end", Token.DescribeSubclass<Terminator>() }
                            },
                            "In parameter list, expected "
                            + " ',' or end of list.");
                    break;
                }
            }
        }

        private ParseNode parseTypeAnnotation()
        {
            expect<ColonToken>();
            nextToken();
            return parseExpression();
        }

        private ParseNode parseInherits()
        {
            Token start = lexer.current;
            nextToken();
            ParseNode val = parseExpression();
            IdentifierParseNode name = null;
            if (lexer.current is AsToken)
            {
                nextToken();
                expect<IdentifierToken>();
                name = parseIdentifier();
            }
            return new InheritsParseNode(start, val, name);
        }

        private ParseNode parseImport()
        {
            Token start = lexer.current;
            nextToken();
            expect<StringToken>();
            if ((lexer.current as StringToken).BeginsInterpolation)
                reportError("P1014", "Import path uses string interpolation.");
            ParseNode path = parseString();
            expect<AsToken>();
            nextToken();
            expect<IdentifierToken>();
            ParseNode name = parseIdentifier();
            ParseNode type = null;
            if (lexer.current is ColonToken)
            {
                type = parseTypeAnnotation();
            }
            return new ImportParseNode(start, path, name, type);
        }

        private ParseNode parseDialect()
        {
            Token start = lexer.current;
            nextToken();
            expect<StringToken>();
            if ((lexer.current as StringToken).BeginsInterpolation)
                reportError("P1015", "Dialect path uses string interpolation.");
            ParseNode path = parseString();
            return new DialectParseNode(start, path);
        }

        private ParseNode parseReturn()
        {
            Token start = lexer.current;
            nextToken();
            if (lexer.current is NewLineToken || lexer.current is CommentToken)
            {
                // Void return
                return new ReturnParseNode(start, null);
            }
            ParseNode val = parseExpression();
            return new ReturnParseNode(start, val);
        }

        private ParseNode parsePostcircumfixRequest(ParseNode rec)
        {
            var startToken = lexer.current as OpenBracketToken;
            var arguments = new List<ParseNode>();
            parseBracketConstruct(arguments);
            return new ExplicitBracketRequestParseNode(startToken,
                    startToken.Name + startToken.Other, rec, arguments);
        }

        private ParseNode expressionRestNoOp(ParseNode ex)
        {
            ParseNode lhs = ex;
            while (lexer.current is DotToken)
            {
                lhs = parseDotRequest(lhs);
            }
            if (lexer.current is OpenBracketToken)
            {
                lhs = parsePostcircumfixRequest(lhs);
                return expressionRestNoOp(lhs);
            }
            return lhs;
        }

        private ParseNode maybeParseOperator(ParseNode lhs)
        {
            if (lexer.current is OperatorToken)
            {
                lhs = parseOperator(lhs);
            }
            return lhs;
        }

        private ParseNode expressionRest(ParseNode lhs)
        {
            lhs = expressionRestNoOp(lhs);
            return maybeParseOperator(lhs);
        }

        private void parseBracketConstruct(List<ParseNode> arguments)
        {
            var startToken = lexer.current as OpenBracketToken;
            if (startToken != null)
            {
                nextToken();
                while (awaiting<CloseBracketToken>(startToken))
                {
                    var expr = parseExpression();
                    arguments.Add(expr);
                    consumeBlankLines();
                    if (lexer.current is CommaToken)
                    {
                        nextToken();
                        if (lexer.current is CloseBracketToken)
                            reportError("P1018", lexer.current,
                                    "Expected argument after comma.");
                    }
                    else
                    {
                        expect<CloseBracketToken>("CloseBracketToken '"
                                + startToken.Other + "'");
                    }
                }
                var cb = (CloseBracketToken)lexer.current;
                if (cb.Name != startToken.Other)
                {
                    reportError("P1028",
                            new Dictionary<string, string>
                            {
                                { "start", startToken.Name },
                                { "expected", startToken.Other },
                                { "found", cb.Name },
                            },
                            "Mismatched bracket construct"
                            );
                }
                nextToken();
            }
        }

        private ParseNode parseImplicitBracket()
        {
            var startToken = lexer.current as OpenBracketToken;
            var arguments = new List<ParseNode>();
            parseBracketConstruct(arguments);
            return new ImplicitBracketRequestParseNode(startToken,
                    startToken.Name + startToken.Other, arguments);
        }

        private ParseNode parseParenthesisedExpression()
        {
            if (lexer.current is LParenToken)
            {
                var startToken = lexer.current;
                nextToken();
                var expr = parseExpression();
                consumeBlankLines();
                if (lexer.current is RParenToken)
                {
                    nextToken();
                }
                else
                {
                    reportError("P1017", "Parenthesised expression does not have closing parenthesis");
                }
                return new ParenthesisedParseNode(startToken, expr);
            }
            return null;
        }

        private ParseNode parseExpressionNoOp()
        {
            ParseNode lhs;
            if (lexer.current is LParenToken)
            {
                lhs = parseParenthesisedExpression();
            }
            else if (lexer.current is OpenBracketToken)
            {
                lhs = parseImplicitBracket();
            }
            else
            {
                lhs = parseTerm();
            }
            if (lhs is IdentifierParseNode)
            {
                if (lexer.current is LParenToken)
                {
                    lhs = parseImplicitReceiverRequest(lhs);
                }
                else if (hasDelimitedTerm())
                {
                    lhs = parseImplicitReceiverRequest(lhs);
                }
                else if (lexer.current is LGenericToken)
                {
                    lhs = parseImplicitReceiverRequest(lhs);
                }
            }
            lhs = expressionRestNoOp(lhs);
            return lhs;
        }

        private ParseNode parseExpression()
        {
            ParseNode lhs = parseExpressionNoOp();
            lhs = maybeParseOperator(lhs);
            return lhs;
        }

        private bool hasDelimitedTerm()
        {
            if (lexer.current is NumberToken)
                return true;
            if (lexer.current is StringToken)
                return true;
            if (lexer.current is LBraceToken && !doNotAcceptDelimitedBlock)
                return true;
            return false;
        }

        private bool hasTermStart()
        {
            if (lexer.current is IdentifierToken)
                return true;
            if (lexer.current is NumberToken)
                return true;
            if (lexer.current is StringToken)
                return true;
            if (lexer.current is LBraceToken)
                return true;
            if (lexer.current is TypeKeywordToken)
                return true;
            return false;
        }

        private ParseNode parseTerm()
        {
            ParseNode ret = null;
            if (lexer.current is IdentifierToken)
            {
                ret = parseIdentifier();
            }
            else if (lexer.current is NumberToken)
            {
                ret = parseNumber();
            }
            else if (lexer.current is StringToken)
            {
                ret = parseString();
            }
            else if (lexer.current is LBraceToken)
            {
                ret = parseBlock();
            }
            else if (lexer.current is ObjectKeywordToken)
            {
                ret = parseObject();
            }
            else if (lexer.current is TypeKeywordToken)
            {
                ret = parseType();
            }
            else if (lexer.current is OperatorToken)
            {
                ret = parsePrefixOperator();
            }
            if (ret == null)
            {
                reportError("P1018", lexer.current, "Expected term.");
            }
            return ret;
        }

        private ParseNode parsePrefixOperator()
        {
            OperatorToken op = lexer.current as OperatorToken;
            nextToken();
            ParseNode expr;
            if (lexer.current is LParenToken)
            {
                expr = parseParenthesisedExpression();
            }
            else
            {
                expr = parseTerm();
                expr = expressionRestNoOp(expr);
            }
            return new PrefixOperatorParseNode(op, expr);
        }

        private ParseNode parseString()
        {
            StringToken tok = lexer.current as StringToken;
            if (tok.BeginsInterpolation)
            {
                InterpolatedStringParseNode ret = new InterpolatedStringParseNode(tok);
                StringToken lastTok = tok;
                lexer.NextToken();
                while (lastTok.BeginsInterpolation)
                {
                    ret.Parts.Add(new StringLiteralParseNode(lastTok));
                    lexer.NextToken();
                    ParseNode expr = parseExpression();
                    ret.Parts.Add(expr);
                    if (lexer.current is RBraceToken)
                    {
                        lexer.TreatAsString();
                        lastTok = lexer.current as StringToken;
                    }
                    else
                    {
                        reportError("P1019",
                                "Interpolation not terminated by }");
                        throw new Exception();
                    }
                    lexer.NextToken();
                }
                ret.Parts.Add(new StringLiteralParseNode(lastTok));
                return ret;
            }
            else
            {
                nextToken();
                return new StringLiteralParseNode(tok);
            }
        }

        private ParseNode parseNumber()
        {
            ParseNode ret = new NumberParseNode(lexer.current);
            nextToken();
            return ret;
        }

        private IdentifierParseNode parseIdentifier()
        {
            IdentifierParseNode ret = new IdentifierParseNode(lexer.current);
            nextToken();
            return ret;
        }

        private ParseNode parseOperator(ParseNode lhs)
        {
            return parseOperatorStream(lhs);
        }

        private ParseNode oldParseOperator(ParseNode lhs)
        {
            OperatorToken tok = lexer.current as OperatorToken;
            if ((!tok.SpaceBefore || !tok.SpaceAfter) && (tok.Name != ".."))
                reportError("P1020",
                        new Dictionary<string, string>()
                        {
                            { "operator", tok.Name }
                        },
                        "Infix operators must be surrounded by spaces.");
            nextToken();
            ParseNode rhs = parseExpressionNoOp();
            ParseNode ret = new OperatorParseNode(tok, tok.Name, lhs, rhs);
            tok = lexer.current as OperatorToken;
            while (tok != null)
            {
                if ((!tok.SpaceBefore || !tok.SpaceAfter) && (tok.Name != ".."))
                    reportError("P1020",
                            new Dictionary<string, string>()
                            {
                                { "operator", tok.Name }
                            },
                            "Infix operators must be surrounded by spaces.");
                nextToken();
                ParseNode comment = null;
                if (lexer.current is CommentToken)
                {
                    comment = parseComment();
                }
                rhs = parseExpressionNoOp();
                ret = new OperatorParseNode(tok, tok.Name, ret, rhs);
                ret.Comment = comment;
                tok = lexer.current as OperatorToken;
            }
            return ret;
        }

        private static int precedence(string op)
        {
            if (op == "*")
                return 10;
            if (op == "/")
                return 10;
            return 0;
        }

        private ParseNode parseOperatorStream(ParseNode lhs)
        {
            var opstack = new Stack<OperatorToken>();
            var valstack = new Stack<ParseNode>();
            valstack.Push(lhs);
            OperatorToken tok = lexer.current as OperatorToken;
            string firstOp = null;
            bool allArith = true;
            while (tok != null)
            {
                if ((!tok.SpaceBefore || !tok.SpaceAfter) && (tok.Name != ".."))
                    reportError("P1020",
                            new Dictionary<string, string>()
                            {
                                { "operator", tok.Name }
                            },
                            "Infix operators must be surrounded by spaces.");
                nextToken();
                if (lexer.current is CommentToken)
                {
                    parseComment();
                }
                switch (tok.Name)
                {
                    case "*":
                    case "-":
                    case "/":
                    case "+":
                        break;
                    default:
                        allArith = false;
                        break;
                }
                if (firstOp != null && !allArith && firstOp != tok.Name)
                {
                    reportError("P1026",
                            new Dictionary<string, string>()
                            {
                                { "operator", tok.Name }
                            },
                            "Mixed operators without parentheses");
                }
                else if (firstOp == null)
                {
                    firstOp = tok.Name;
                }
                int myprec = precedence(tok.Name);
                while (opstack.Count > 0
                        && myprec <= precedence(opstack.Peek().Name))
                {
                    var o2 = opstack.Pop();
                    var tmp2 = valstack.Pop();
                    var tmp1 = valstack.Pop();
                    valstack.Push(
                            new OperatorParseNode(o2, o2.Name,
                                tmp1, tmp2));
                }
                opstack.Push(tok);
                ParseNode rhs = parseExpressionNoOp();
                valstack.Push(rhs);
                tok = lexer.current as OperatorToken;
            }
            while (opstack.Count > 0)
            {
                var o = opstack.Pop();
                var tmp2 = valstack.Pop();
                var tmp1 = valstack.Pop();
                valstack.Push(
                        new OperatorParseNode(o, o.Name, tmp1, tmp2));
            }
            return valstack.Pop();
        }

        private void parseBraceDelimitedBlock(List<ParseNode> body)
        {
            int indentBefore = indentColumn;
            Token start = lexer.current;
            // Skip the {
            lexer.NextToken();
            if (lexer.current is CommentToken)
            {
                comments.Add(parseComment());
            }
            consumeBlankLines();
            if (lexer.current is RBraceToken)
            {
                nextToken();
                return;
            }
            consumeBlankLines();
            takeLineComments();
            consumeBlankLines();
            if (lexer.current is RBraceToken)
            {
                nextToken();
                return;
            }
            indentColumn = lexer.current.column;
            if (indentColumn <= indentBefore)
                reportError("P1011", new Dictionary<string, string>() {
                        { "previous indent", "" + (indentBefore - 1) },
                        { "new indent", "" + (indentColumn - 1) }
                    },
                    "Indentation must increase inside {}.");
            Token lastToken = lexer.current;
            while (awaiting<RBraceToken>(start))
            {
                if (lexer.current.column != indentColumn)
                {
                    reportError("P1016", new Dictionary<string, string>() 
                            {
                                { "required indentation", "" + (indentColumn - 1) },
                                { "given indentation", "" + (lexer.current.column - 1) }
                            },
                            "Indentation mismatch; is "
                            + (lexer.current.column - 1) + ", should be "
                            + (indentColumn - 1) + ".");
                }
                body.Add(parseStatement());
                if (lexer.current == lastToken)
                {
                    reportError("P1000", lexer.current,
                            "Nothing consumed in {} body.");
                    break;
                }
            }
            nextToken();
            indentColumn = indentBefore;
        }
        private ParseNode parseObject()
        {
            ObjectParseNode ret = new ObjectParseNode(lexer.current);
            lexer.NextToken();
            if (!(lexer.current is LBraceToken))
            {
                reportError("P1021", "object must have '{' after.");
            }
            List<ParseNode> origComments = prepareComments();
            parseBraceDelimitedBlock(ret.Body);
            attachComments(ret, comments);
            restoreComments(origComments);
            return ret;
        }

        private ParseNode parseBlock()
        {
            int indentStart = indentColumn;
            BlockParseNode ret = new BlockParseNode(lexer.current);
            Token start = lexer.current;
            lexer.NextToken();
            consumeBlankLines();
            Token firstBodyToken = lexer.current;
            indentColumn = firstBodyToken.column;
            // TODO fix to handle indentation properly
            // does not at all now. must recalculate after params list too
            if (lexer.current is IdentifierToken
                    || lexer.current is NumberToken
                    || lexer.current is StringToken
                    || lexer.current is LParenToken)
            {
                // It *might* be a parameter.
                ParseNode expr = parseExpression();
                if (lexer.current is BindToken)
                {
                    // Definitely not a parameter
                    nextToken();
                    ParseNode val = parseExpression();
                    ret.Body.Add(new BindParseNode(start, expr, val));
                    if (lexer.current is CommaToken
                        || lexer.current is ArrowToken)
                        reportError("P1022", lexer.current, "Block parameter list contained invalid symbol.");
                }
                else if (lexer.current is SemicolonToken)
                {
                    // Definitely not a parameter
                    lexer.NextToken();
                    if (!(lexer.current is NewLineToken
                                || lexer.current is CommentToken
                                || lexer.current is EndToken
                                || lexer.current is RBraceToken))
                        reportError("P1003", "Other code cannot follow a semicolon on the same line.");
                    ret.Body.Add(expr);
                }
                else if (lexer.current is ColonToken)
                {
                    // Definitely a parameter of some sort, has a type.
                    ParseNode type = parseTypeAnnotation();
                    ret.Parameters.Add(new TypedParameterParseNode(expr, type));
                }
                else if (lexer.current is CommaToken)
                {
                    // Can only be a parameter.
                    ret.Parameters.Add(expr);
                }
                else if (lexer.current is ArrowToken)
                {
                    // End of parameter list
                    ret.Parameters.Add(expr);
                }
                else
                {
                    ret.Body.Add(expr);
                }
                if (lexer.current is CommaToken)
                {
                    nextToken();
                    parseParameterList<ArrowToken>(start, ret.Parameters);
                }
            }
            if (lexer.current is ArrowToken)
            {
                lexer.NextToken();
                consumeBlankLines();
                firstBodyToken = lexer.current;
            }
            else
            {
                consumeBlankLines();
            }
            Token lastToken = lexer.current;
            indentColumn = firstBodyToken.column;
            while (!(lexer.current is RBraceToken))
            {
                ret.Body.Add(parseStatement());
                if (lexer.current == lastToken)
                {
                    reportError("P1000", lexer.current,
                            "Nothing consumed in block body.");
                    break;
                }
            }
            indentColumn = indentStart;
            nextToken();
            return ret;
        }

        private void parseArgumentList(List<ParseNode> arguments)
        {
            if (lexer.current is LParenToken)
            {
                Token start = lexer.current;
                nextToken();
                while (awaiting<RParenToken>(start))
                {
                    ParseNode expr = parseExpression();
                    arguments.Add(expr);
                    consumeBlankLines();
                    if (lexer.current is CommaToken)
                    {
                        nextToken();
                        if (lexer.current is RParenToken)
                            reportError("P1018", lexer.current,
                                    "Expected argument after comma.");
                    }
                    else if (!(lexer.current is RParenToken))
                    {
                        reportError("P1023", lexer.current,
                                "In argument list of request, expected "
                                + " ',' or ')'.");
                        break;
                    }
                }
                nextToken();
            }
            else if (hasDelimitedTerm())
            {
                arguments.Add(parseTerm());
            }
        }

        private void parseGenericArgumentList(List<ParseNode> arguments)
        {
            if (lexer.current is LGenericToken)
            {
                Token start = lexer.current;
                nextToken();
                while (awaiting<RGenericToken>(start))
                {
                    ParseNode expr = parseExpression();
                    arguments.Add(expr);
                    consumeBlankLines();
                    if (lexer.current is CommaToken)
                        nextToken();
                    else if (!(lexer.current is RGenericToken))
                    {
                        reportError("P1024", lexer.current,
                                "In generic argument list of request, expected "
                                + " ',' or '>'.");
                        break;
                    }
                }
                nextToken();
            }
        }

        private ParseNode parseImplicitReceiverRequest(ParseNode lhs)
        {
            ImplicitReceiverRequestParseNode ret = new ImplicitReceiverRequestParseNode(lhs);
            parseGenericArgumentList(ret.GenericArguments[0]);
            parseArgumentList(ret.Arguments[0]);
            while (lexer.current is IdentifierToken)
            {
                // This is a multi-part method name
                ret.AddPart(parseIdentifier());
                var hadParen = lexer.current is LParenToken;
                parseArgumentList(ret.Arguments.Last());
                if (ret.Arguments.Last().Count == 0 && !hadParen)
                    return ret;
            }
            return ret;
        }

        private ParseNode parseDotRequest(ParseNode lhs)
        {
            ExplicitReceiverRequestParseNode ret = new ExplicitReceiverRequestParseNode(lhs);
            nextToken();
            bool named = false;
            while (lexer.current is IdentifierToken)
            {
                // Add this part of the method name
                ret.AddPart(parseIdentifier());
                parseGenericArgumentList(ret.GenericArguments.Last());
                var hadParen = lexer.current is LParenToken;
                parseArgumentList(ret.Arguments.Last());
                if (ret.Arguments.Last().Count == 0 && !hadParen)
                    return ret;
                named = true;
            }
            if (!named)
            {
                reportError("P1025", lexer.current,
                        "Expected identifier after '.'.");
            }
            return ret;
        }

        private ParseNode parseComment()
        {
            ParseNode ret = new CommentParseNode(lexer.current);
            nextToken();
            return ret;
        }

        private ParseNode collapseComments(List<ParseNode> comments)
        {
            ParseNode first = comments[0];
            ParseNode last = first;
            for (int i = 1; i < comments.Count; i++)
            {
                last.Comment = comments[i];
                last = comments[i];
            }
            return first;
        }

        private void takeLineComments()
        {
            if (!(lexer.current is CommentToken))
                return;
            ParseNode ret = new CommentParseNode(lexer.current);
            comments.Add(ret);
            lexer.NextToken();
            if (lexer.current is NewLineToken)
            {
                lexer.NextToken();
                if (lexer.current is CommentToken)
                    takeLineComments();
            }
        }

        private void takeComments()
        {
            if (!(lexer.current is CommentToken))
                return;
            ParseNode ret = new CommentParseNode(lexer.current);
            comments.Add(ret);
            lexer.NextToken();
            if (lexer.current is NewLineToken)
            {
                // Check for continuation lines
                Token t = lexer.Peek();
                if (t.column > indentColumn)
                    lexer.NextToken();
            }
            if (lexer.current is CommentToken)
                takeComments();
        }

    }

}
