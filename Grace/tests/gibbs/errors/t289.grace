// ERROR = P1018
match(.)
    case { 1 -> "ONE" }
    case { _ -> "NOT ONE" }
