type PairType = {
    left
    right
}

def Pair = object {
    method match(o) {
        def m = PairType.match(o)
        if (m) then {
            return _SuccessfulMatch(o, o.left, o.right)
        }
        return m
    }
}
method tryMatch(o) {
    match(o)
        case { p : Pair(1, 2) -> print "The pair 1 and 2" }
        case { p : Pair(a : 5, b) -> print "Pair starting 5! ({a},{b})" }
        case { p : Pair(a : Pair(_, _), b : Pair(c : 3, d)) ->
            print "Pair of pairs, second starting 3. {a} {b} {c} {d}" }
        case { p : Pair(a, b) -> print "({a}, {b}) from {p}" }
        case { p : Pair(_, _) -> print "PAIR ({p.left}, {p.right})" }
}

class pair(l, r) {
    def left is public = l
    def right is public = r
    def asString is public = "pair({l}, {r})"
}

tryMatch(pair(1, 2))
tryMatch(pair(3,4))
tryMatch(pair(5,6))
tryMatch(pair(7,8))
tryMatch(pair(pair(1, 2), pair(3, 4)))
