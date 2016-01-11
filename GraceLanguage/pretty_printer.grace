// This script "pretty-prints" Grace source. The output should be
// semantically equivalent and syntactically valid, but the script
// does not make any effort to produce good-looking output. It
// inserts semicolons everywhere they are permitted to allow other
// parsers that are not layout-aware to read in transformed code.
//
// It is quite slow on large inputs because it performs very many
// O(n) string concatenations, and could be made much faster with
// better library support.

// Alias the patterns exposed by the compilation infrastructure.
// Each pattern matches a native class: parseNodes.Object matches
// any native object proxies for an ObjectParseNode, and so on.
def pnObject = parseNodes.Object
def pnNumber = parseNodes.Number
def pnStringLiteral = parseNodes.StringLiteral
def pnInterpolatedString = parseNodes.InterpolatedString
def pnIdentifier = parseNodes.Identifier
def pnOperator = parseNodes.Operator
def pnMethodDeclaration = parseNodes.MethodDeclaration
def pnSignature = parseNodes.Signature
def pnSignaturePart = parseNodes.SignaturePart
def pnClassDeclaration = parseNodes.ClassDeclaration
def pnTraitDeclaration = parseNodes.TraitDeclaration
def pnImplicitReceiverRequest = parseNodes.ImplicitReceiverRequest
def pnExplicitReceiverRequest = parseNodes.ExplicitReceiverRequest
def pnTypedParameter = parseNodes.TypedParameter
def pnBlock = parseNodes.Block
def pnVarDeclaration = parseNodes.VarDeclaration
def pnDefDeclaration = parseNodes.DefDeclaration
def pnParenthesised = parseNodes.Parenthesised
def pnComment = parseNodes.Comment
def pnReturn = parseNodes.Return
def pnInherits = parseNodes.Inherits
def pnUses = parseNodes.Uses
def pnAlias = parseNodes.Alias
def pnExclude = parseNodes.Exclude
def pnBind = parseNodes.Bind
def pnDialect = parseNodes.Dialect
def pnImport = parseNodes.Import
def pnVarArgsParameter = parseNodes.VarArgsParameter
def pnPrefixOperator = parseNodes.PrefixOperator
def pnAnnotations = parseNodes.Annotations
def pnExplicitBracketRequest = parseNodes.ExplicitBracketRequest
def pnType = parseNodes.Type
def pnTypeStatement = parseNodes.TypeStatement

def breakLines = false

var useSemicolons := false

method prettyPrintObjectBodyWithSemicolons(obj) {
    useSemicolons := true
    def ret = prettyPrintObjectBody(obj, "")
    useSemicolons := false
    ret
}

method formatParseNode(obj) {
    return prettyPrint(obj, "")
}

// Examine a node and decide where to send it. Each of the methods
// below addresses one kind of node and converts it into a string,
// concatenating its child nodes in as required.
method prettyPrint(obj, indent) {
    match (obj)
        case { n : pnNumber -> prettyPrintNumber(n, indent) }
        case { n : pnStringLiteral -> prettyPrintStringLiteral(n, indent) }
        case { n : pnInterpolatedString ->
            prettyPrintInterpolatedString(n, indent) }
        case { o : pnOperator -> prettyPrintOperator(o, indent) }
        case { o : pnMethodDeclaration ->
            prettyPrintMethodDeclaration(o, indent) }
        case { o : pnClassDeclaration ->
            prettyPrintClassDeclaration(o, indent) }
        case { o : pnTraitDeclaration ->
            prettyPrintTraitDeclaration(o, indent) }
        case { p : pnSignaturePart -> prettyPrintSignaturePart(p, indent) }
        case { s : pnSignature -> prettyPrintSignature(s, indent) }
        case { o : pnObject -> prettyPrintObject(o, indent) }
        case { r : pnImplicitReceiverRequest ->
            prettyPrintImplicitReceiverRequest(r, indent) }
        case { r : pnExplicitReceiverRequest ->
            prettyPrintExplicitReceiverRequest(r, indent) }
        case { o : pnIdentifier -> prettyPrintIdentifier(o, indent) }
        case { r : pnTypedParameter ->
            prettyPrintTypedParameter(r, indent) }
        case { b : pnBlock -> prettyPrintBlock(b, indent) }
        case { b : pnVarDeclaration -> prettyPrintVarDeclaration(b, indent) }
        case { b : pnDefDeclaration -> prettyPrintDefDeclaration(b, indent) }
        case { b : pnParenthesised -> prettyPrintParenthesised(b, indent) }
        case { b : pnComment -> prettyPrintComment(b, indent) }
        case { b : pnReturn -> prettyPrintReturn(b, indent) }
        case { b : pnInherits -> prettyPrintInherits(b, indent) }
        case { b : pnUses -> prettyPrintUses(b, indent) }
        case { b : pnBind -> prettyPrintBind(b, indent) }
        case { b : pnDialect -> prettyPrintDialect(b, indent) }
        case { b : pnImport -> prettyPrintImport(b, indent) }
        case { b : pnVarArgsParameter ->
            prettyPrintVarArgsParameter(b, indent) }
        case { b : pnPrefixOperator ->
            prettyPrintPrefixOperator(b, indent) }
        case { b : pnAnnotations -> prettyPrintAnnotations(b, indent) }
        case { r : pnExplicitBracketRequest ->
            prettyPrintExplicitBracketRequest(r, indent) }
        case { b : pnType -> prettyPrintType(b, indent) }
        case { b : pnTypeStatement -> prettyPrintTypeStatement(b, indent) }
        case { _ ->
                print "Printer does not support node type {obj}"
                "<<Untranslated: {obj}>>"
            }
}

method prettyPrintStatement(o, indent) {
    var ret := indent
    // Native properties are available with "x.get_PropertyName"
    def comment = o.get_Comment
    match (o)
        case { _ : pnComment ->
            // Comments should be printed as themselves, not with
            // their comments first.
        }
        case { _ : pnObject ->
            // Object comments live inside the body, since they
            // aren't useful standalone.
        }
        case { _ ->
            // The "isNull" method on GraceObjectProxies indicates
            // whether the proxy represents a null value. No other methods
            // are available on a null value.
            if (!comment.isNull) then {
                ret := ret ++ "\n{indent}" ++ prettyPrint(comment, indent)
                ret := ret ++ "\n{indent}"
            }
        }
    ret := ret ++ prettyPrint(o, indent)
    match (o)
        case { m : pnMethodDeclaration | pnClassDeclaration | pnTraitDeclaration
                -> }
        case { m : pnComment -> ret := ret ++ "\n" }
        case { _ ->
            if (useSemicolons) then {
                ret := ret ++ ";"
            }
        }
    "{ret}\n"
}

method prettyPrintNumber(n, indent) {
    if (n.get_NumericBase != 10) then {
        "{n.get_NumericBase}x{n.get_Digits}"
    } else {
        n.get_Digits
    }
}

method prettyPrintStringLiteral(s, indent) {
    "\"{s.get_Raw}\""
}

method prettyPrintInterpolatedString(s, indent) {
    def parts = s.get_Parts
    def partCount = parts.get_Count
    var ret := "\""
    // Native lists are available as objects with a Count property
    // that can be indexed using []. These lists are zero-indexed
    // as the host is, and do not yet support iteration.
    for (0 .. (partCount - 1)) do { i ->
        def part = parts[i]
        ret := ret ++ match(part)
            case { _ : pnStringLiteral -> part.get_Raw }
            case { _ -> "\{{prettyPrint(part, indent)}\}" }
    }
    "{ret}\""
}

method prettyPrintIdentifier(i, indent) {
    i.get_Name
}

method prettyPrintOperator(o, ind) {
    "{prettyPrint(o.get_Left, ind)} {o.get_Name} {prettyPrint(o.get_Right,ind)}"
}

method helper_Generics(g, indent) {
    def gCount = g.get_Count
    if (gCount == 0) then {
        return ""
    }
    var ret := "<"
    for (0 .. (gCount - 1)) do { i ->
        if (i > 0) then {
            ret := ret ++ ", "
        }
        ret := ret ++ prettyPrint(g[i], indent)
    }
    "{ret}>"
}

method prettyPrintClassDeclaration(m, indent) {
    var ret := "class "
    ret := ret ++ prettyPrint(m.get_Signature, indent)
    ret := ret ++ " \{\n"
    def body = m.get_Body
    def count = body.get_Count
    for (0 .. (count - 1)) do {i->
        def node = body[i]
        ret := ret ++ prettyPrintStatement(node, "{indent}    ")
    }
    "{ret}{indent}\}"
}

method prettyPrintTraitDeclaration(m, indent) {
    var ret := "trait "
    ret := ret ++ prettyPrint(m.get_Signature, indent)
    ret := ret ++ " \{\n"
    def body = m.get_Body
    def count = body.get_Count
    for (0 .. (count - 1)) do {i->
        def node = body[i]
        ret := ret ++ prettyPrintStatement(node, "{indent}    ")
    }
    "{ret}{indent}\}"
}

method prettyPrintMethodDeclaration(m, indent) {
    var ret := "method "
    ret := ret ++ prettyPrint(m.get_Signature, indent)
    ret := ret ++ " \{\n"
    def body = m.get_Body
    def count = body.get_Count
    for (0 .. (count - 1)) do {i->
        def node = body[i]
        ret := ret ++ prettyPrintStatement(node, "{indent}    ")
    }
    "{ret}{indent}\}"
}

method prettyPrintSignaturePart(p, indent) {
    def name = p.get_Name
    def params = p.get_Parameters
    def genericParameters = p.get_GenericParameters
    var ret := "{name}"
    if (genericParameters.get_Count > 0) then {
        ret := ret ++ "<"
        for (0 .. (genericParameters.get_Count - 1)) do { i ->
            if (i > 0) then {
                ret := ret ++ ","
            }
            ret := ret ++ prettyPrint(genericParameters[i], indent)
        }
        ret := ret ++ ">"
    }
    if (params.get_Count > 0) then {
        ret := ret ++ "("
        for (0 .. (params.get_Count - 1)) do { i ->
            if (i > 0) then {
                ret := ret ++ ","
            }
            ret := ret ++ prettyPrint(params[i], indent)
        }
        ret := ret ++ ")"
    }
    ret
}

method prettyPrintSignature(s, indent) {
    def parts = s.get_Parts
    def returnType = s.get_ReturnType
    def size = parts.get_Count
    var ret := ""
    for (0 .. (size - 1)) do { i ->
        ret := ret ++ prettyPrint(parts[i], indent)
    }
    if (!returnType.isNull) then {
        ret := ret ++ " -> " ++ prettyPrint(returnType, indent)
    }
    if (!s.get_Annotations.isNull) then {
        ret := ret ++ prettyPrint(s.get_Annotations, indent)
    }
    ret
}


method prettyPrintObjectBody(body, indent) {
    var ret := ""
    def count = body.get_Count
    for (0 .. (count - 1)) do {i->
        def node = body[i]
        ret := ret ++ prettyPrintStatement(node, indent)
    }
    ret
}

method prettyPrintObject(o, indent) {
    var ret := "object \{\n"
    def comment = o.get_Comment
    if (!comment.isNull) then {
        ret := ret ++ indent ++ "    "
            ++ prettyPrint(comment, "{indent}    ") ++ "\n"
    }
    ret := ret ++ prettyPrintObjectBody(o.get_Body, "{indent}    ")
    "{ret}{indent}\}"
}

method prettyPrintImplicitReceiverRequest(r, indent) {
    def nameParts = r.get_NameParts
    def argLists = r.get_Arguments
    def size = nameParts.get_Count
    var ret := ""
    var firstPart := true
    var lineSoFar := indent.size
    for (0 .. (size - 1)) do { i ->
        def args = argLists[i]
        def argCount = args.get_Count
        var firstArg := true
        def retStart = ret.size
        if (breakLines && (lineSoFar > 60)) then {
            ret := ret ++ "\n{indent}   "
            lineSoFar := indent.size + 4
        }
        ret := "{ret}{if (!firstPart) then {" "} else {""}}"
        ret := ret ++ nameParts[i] .get_Name
        ret := ret ++ helper_Generics(r.get_GenericArguments[i], indent)
        var noParen := false
        // Don't parenthesise single blocks &c
        if (argCount == 1) then {
            def lone = args[0]
            match (lone)
                case {
                    b : pnBlock | pnNumber | pnStringLiteral
                        | pnInterpolatedString ->
                    ret := ret ++ " "
                    noParen := true
                }
                case { _ ->
                    ret := ret ++ "("
                }
        } else {
            if (argCount == 0) then {
                noParen := true
            } else {
                ret := ret ++ "("
            }
        }
        for (0 .. (argCount - 1)) do { j ->
            if (!firstArg) then {
                ret := "{ret}, "
            }
            ret := ret ++ prettyPrint(args[j], indent ++
                if (breakLines) then { "    " } else { "" })
            firstArg := false
        }
        firstPart := false
        if (!noParen) then {
            ret := ret ++ ")"
        }
        def len = ret.size - retStart
        lineSoFar := lineSoFar + len
    }
    ret
}

method prettyPrintExplicitReceiverRequest(r, indent) {
    def receiver = r.get_Receiver
    def nameParts = r.get_NameParts
    def argLists = r.get_Arguments
    def size = nameParts.get_Count
    var ret := "{prettyPrint(receiver, indent)}."
    ret ++ prettyPrintImplicitReceiverRequest(r, indent)
}

method prettyPrintTypedParameter(p, indent) {
    "{prettyPrint(p.get_Name, indent)} : {prettyPrint(p.get_Type, indent)}"
}

method prettyPrintBlock(b, indent) {
    var ret := "\{ "
    def params = b.get_Parameters
    def paramCount = params.get_Count
    if (paramCount > 0) then {
        var firstParam := true
        for (0 .. (paramCount - 1)) do { j ->
            if (!firstParam) then {
                ret := "{ret}, "
            }
            ret := ret ++ prettyPrint(params[j], indent)
            firstParam := false
        }
        ret := ret ++ " ->"
    }
    def body = b.get_Body
    def count = body.get_Count
    var multiLine := count > 1
    if (count == 1) then {
        if (pnComment.match(body[0])) then {
            multiLine := true
        } else {
            def flp = prettyPrint(body[0], "{indent}    ")
            if ((flp.size + indent.size + 4 + 2) > 40) then {
                multiLine := breakLines
            }
        }
    }
    if (multiLine) then {
        ret := ret ++ "\n"
    } else {
        if (paramCount > 0) then {
            ret := ret ++ " "
        }
    }
    for (0 .. (count - 1)) do {i->
        def node = body[i]
        if (multiLine) then {
            ret := ret ++ prettyPrintStatement(node, "{indent}    ")
        } else {
            ret := ret ++ prettyPrint(node, "{indent}    ")
        }
    }
    if (multiLine) then {
        ret ++ "{indent}\}"
    } else {
        "{ret} \}"
    }
}

method prettyPrintVarDeclaration(v, indent) {
    var ret := "var "
    ret := ret ++ prettyPrint(v.get_Name, indent)
    if (!v.get_Annotations.isNull) then {
        ret := ret ++ prettyPrint(v.get_Annotations, indent)
    }
    if (!v.get_Value.isNull) then {
        ret := ret ++ " := "
        ret := ret ++ prettyPrint(v.get_Value, indent)
    }
    ret
}

method prettyPrintDefDeclaration(v, indent) {
    var ret := "def "
    ret := ret ++ prettyPrint(v.get_Name, indent)
    if (!v.get_Annotations.isNull) then {
        ret := ret ++ prettyPrint(v.get_Annotations, indent)
    }
    ret := ret ++ " = "
    ret := ret ++ prettyPrint(v.get_Value, indent)
    ret
}

method prettyPrintParenthesised(p, indent) {
    def newIndent = indent ++ "    "
    "({prettyPrint(p.get_Expression, newIndent)})"
}

method prettyPrintComment(c, indent) {
    def comment = c.get_Comment
    if (!comment.isNull) then {
        "//{c.get_Value}\n{indent}{prettyPrint(comment, indent)}"
    } else {
        "//{c.get_Value}"
    }
}


method prettyPrintReturn(p, indent) {
    if (p.get_ReturnValue.isNull) then {
        return "return"
    }
    def newIndent = indent ++ "    "
    "return {prettyPrint(p.get_ReturnValue, newIndent)}"
}

method prettyPrintInherits(p, indent) {
    def newIndent = indent ++ "    "
    def aliases = p.get_Aliases
    def excludes = p.get_Excludes
    var ret := "inherits {prettyPrint(p.get_From, newIndent)}"
    if (aliases.get_Count > 0) then {
        ret := ret ++ prettyPrintAliases(aliases, newIndent)
    }
    if (excludes.get_Count > 0) then {
        ret := ret ++ prettyPrintExcludes(excludes, newIndent)
    }
    ret
}

method prettyPrintUses(p, indent) {
    def newIndent = indent ++ "    "
    def aliases = p.get_Aliases
    def excludes = p.get_Excludes
    var ret := "uses {prettyPrint(p.get_From, newIndent)}"
    if (aliases.get_Count > 0) then {
        ret := ret ++ prettyPrintAliases(aliases, newIndent)
    }
    if (excludes.get_Count > 0) then {
        ret := ret ++ prettyPrintExcludes(excludes, newIndent)
    }
    ret
}

method prettyPrintAliases(aliases, indent) {
    def ac = aliases.get_Count
    var ret := ""
    def newIndent = indent ++ "    "
    for (0 .. (ac - 1)) do { i ->
        def a = aliases[i]
        ret := ret ++ "\n" ++ indent ++ "alias "
            ++ prettyPrint(a.get_NewName, newIndent)
            ++ " = "
            ++ prettyPrint(a.get_OldName, newIndent)
    }
    ret
}

method prettyPrintExcludes(excludes, indent) {
    def ac = excludes.get_Count
    var ret := ""
    def newIndent = indent ++ "    "
    for (0 .. (ac - 1)) do { i ->
        def a = excludes[i]
        ret := ret ++ "\n" ++ indent ++ "exclude "
            ++ prettyPrint(a.get_Name, newIndent)
    }
    ret
}

method prettyPrintBind(o, ind) {
    "{prettyPrint(o.get_Left, ind)} := {prettyPrint(o.get_Right,ind)}"
}

method prettyPrintDialect(d, ind) {
    "dialect \"{d.get_Path.get_Raw}\""
}

method prettyPrintImport(d, ind) {
    "import \"{d.get_Path.get_Raw}\" as {prettyPrint(d.get_Name, ind)}"
}

method prettyPrintVarArgsParameter(v, ind) {
    "*{prettyPrint(v.get_Name, ind)}"
}

method prettyPrintPrefixOperator(o, ind) {
    "{o.get_Name}{prettyPrint(o.get_Receiver, ind)}"
}

method prettyPrintAnnotations(o, ind) {
    var ret := " is "
    def anns = o.get_Annotations
    def annCount = o.get_Annotations.get_Count
    for (0 .. (annCount - 1)) do { i ->
        def a = anns[i]
        if (i > 0) then {
            ret := ret ++ ", "
        }
        ret := ret ++ prettyPrint(a, ind)
    }
    ret
}

method prettyPrintExplicitBracketRequest(b, indent) {
    var ret := "{prettyPrint(b.get_Receiver, indent)}{b.get_Token.get_Name}"
    def args = b.get_Arguments
    def argc = args.get_Count
    for (0 .. (argc - 1)) do { i ->
        if (i > 0) then {
            ret := ret ++ ", "
        }
        ret := ret ++ prettyPrint(args[i], indent)
    }
    "{ret}{b.get_Token.get_Other}"
}

method prettyPrintType(o, indent) {
    var ret := "type \{\n"
    ret := ret ++ prettyPrintObjectBody(o.get_Body, "{indent}    ")
    "{ret}{indent}\}"
}

method prettyPrintTypeStatement(t, indent) {
    "type {prettyPrint(t.get_BaseName, indent)}{helper_Generics(t.get_GenericParameters, indent)} = " ++
        prettyPrint(t.get_Body, indent)
}
