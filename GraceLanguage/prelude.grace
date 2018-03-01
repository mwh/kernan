
method if(cond) then(blk) {
    cond.ifTrue(blk)
}

method if(cond) then(blk1) else(blk2) {
    cond.ifTrue(blk1) ifFalse(blk2)
}

method if(cond1) then(blk1) elseif(cond2) then(blk2) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2)
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2) else(elseblk) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse(elseblk)
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2)
        elseif(cond3) then(blk3) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse {
            cond3.apply.ifTrue(blk3)
        }
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2)
        elseif(cond3) then(blk3) else(elseblk) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse {
            cond3.apply.ifTrue(blk3) ifFalse(elseblk)
        }
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2)
        elseif(cond3) then(blk3) elseif(cond4) then(blk4) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse {
            cond3.apply.ifTrue(blk3) ifFalse {
                cond4.apply.ifTrue(blk4)
            }
        }
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2)
        elseif(cond3) then(blk3) elseif(cond4) then(blk4) else(elseblk) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse {
            cond3.apply.ifTrue(blk3) ifFalse {
                cond4.apply.ifTrue(blk4) ifFalse(elseblk)
            }
        }
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2)
        elseif(cond3) then(blk3) elseif(cond4) then(blk4)
        elseif(cond5) then(blk5) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse {
            cond3.apply.ifTrue(blk3) ifFalse {
                cond4.apply.ifTrue(blk4) ifFalse {
                    cond5.apply.ifTrue(blk5)
                }
            }
        }
    }
}

method if(cond1) then(blk1) elseif(cond2) then(blk2)
        elseif(cond3) then(blk3) elseif(cond4) then(blk4)
        elseif(cond5) then(blk5) else(elseblk) {
    cond1.ifTrue(blk1) ifFalse {
        cond2.apply.ifTrue(blk2) ifFalse {
            cond3.apply.ifTrue(blk3) ifFalse {
                cond4.apply.ifTrue(blk4) ifFalse {
                    cond5.apply.ifTrue(blk5) ifFalse(elseblk)
                }
            }
        }
    }
}

method while2(cond) do(blk) {
    cond.apply.ifTrue {
        blk.apply

        while(cond)do(blk)
    }
}

method while(cond) do(blk) {
    _base_while_do(cond, blk)
}

method _range(from, to) {
    _rangeStep(from, to, 1)
}

method _rangeStep(from, to, by) {
    object {
        method do(blk) {
            var x := from
            while { x <= to } do {
                blk.apply(x)
                x := x + by
            }
        }

        method ..(ns) {
            _rangeStep(from, to, by * ns)
        }
    }
}

method for(iterable) do(blk) {
    iterable.do(blk)
}

type String = interface {
    asString -> String
    ++(o : String) -> String
}

type Number = interface {
    asString -> String
    +(o : Number) -> Number
    *(o : Number) -> Number
    -(o : Number) -> Number
    /(o : Number) -> Number
    ^(o : Number) -> Number
    %(o : Number) -> Number
}

type Boolean = interface {
    ifTrue(b) -> Done
    ifFalse(b) -> Done
    ifTrue(b1) ifFalse(b2)
    &&(o : Boolean) -> Boolean
    ||(o : Boolean) -> Boolean
    andAlso(b)
    orElse(b)
}

method _SuccessfulMatch(obj) {
    object {
        def succeeded is public = true
        def asString is public = "SuccessfulMatch[{obj}]"
        method result {
            obj
        }
        method ifTrue(blk) {
            blk.apply
        }
        method ifFalse(blk) { }
        method ifTrue(blk) ifFalse(_) {
            blk.apply
        }
    }
}

method _FailedMatch(obj) {
    object {
        def succeeded is public = false
        def asString is public = "FailedMatch[{obj}]"
        method result {
            obj
        }
        method ifFalse(blk) {
            blk.apply
        }
        method ifTrue(blk) { }
        method ifTrue(_) ifFalse(blk) {
            blk.apply
        }
    }
}

method _MatchResultFromBoolean(bool, obj) {
    if (bool) then {
        _SuccessfulMatch(obj)
    } else {
        _FailedMatch(obj)
    }
}

method _OrPattern(l, r) {
    object {
        method match(o) {
            def mr = l.match(o)
            if (mr) then {
                return _SuccessfulMatch(mr.result)
            }
            def mr2 = r.match(o)
            if (mr2) then {
                return _SuccessfulMatch(mr2.result)
            }
            _FailedMatch(o)
        }
        method |(o) {
            _OrPattern(self, o)
        }
        method &(o) {
            _AndPattern(self, o)
        }
    }
}

method _AndPattern(l, r) {
    object {
        method match(o) {
            def mr = l.match(o)
            if (mr) then {
                def mr2 = r.match(o)
                return mr2
            }
            return mr
        }
        method |(o) {
            _OrPattern(self, o)
        }
        method &(o) {
            _AndPattern(self, o)
        }
    }
}

method match(target) case(case1) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) case(case29) {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case29.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) case(case29)
        case(case30)
        {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case29.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case30.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) case(case29)
        case(case30) case(case31)
        {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case29.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case30.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case31.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) case(case29)
        case(case30) case(case31) case(case32)
        {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case29.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case30.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case31.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case32.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) case(case29)
        case(case30) case(case31) case(case32) case(case33)
        {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case29.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case30.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case31.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case32.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case33.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method match(target) case(case1) case(case2) case(case3) case(case4)
        case(case5) case(case6) case(case7) case(case8) case(case9)
        case(case10) case(case11) case(case12) case(case13) case(case14)
        case(case15) case(case16) case(case17) case(case18) case(case19)
        case(case20) case(case21) case(case22) case(case23) case(case24)
        case(case25) case(case26) case(case27) case(case28) case(case29)
        case(case30) case(case31) case(case32) case(case33) case(case34)
        {
    var mr := case1.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case2.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case3.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case4.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case5.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case6.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case7.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case8.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case9.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case10.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case11.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case12.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case13.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case14.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case15.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case16.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case17.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case18.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case19.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case20.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case21.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case22.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case23.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case24.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case25.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case26.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case27.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case28.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case29.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case30.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case31.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case32.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case33.match(target)
    if (mr) then {
        return mr.result
    }
    mr := case34.match(target)
    if (mr) then {
        return mr.result
    }
    fail "match-case fell through"
}

method fail(msg) {
    RuntimeError.raise(msg)
}

def Error = Exception.refine "Error"
def RuntimeError = Error.refine "RuntimeError"
def LookupError = RuntimeError.refine "LookupError"
def ArgumentTypeError = RuntimeError.refine "ArgumentTypeError"
def InsufficientArgumentsError = RuntimeError.refine "InsufficientArgumentsError"

method try(b) finally(f) {
    _base_try_catch_finally(b, f)
}

method try(b) catch(e1) finally(f) {
    _base_try_catch_finally(b, f, e1)
}

method try(b) catch(e1) catch(e2) finally(f) {
    _base_try_catch_finally(b, f, e1, e2)
}

method try(b) catch(e1) catch(e2) catch(e3) finally(f) {
    _base_try_catch_finally(b, f, e1, e2, e3)
}

method try(b) catch(e1) catch(e2) catch(e3) catch(e4) finally(f) {
    _base_try_catch_finally(b, f, e1, e2, e3, e4)
}

method try(b) catch(e1)  catch(e2) catch(e3) catch(e4) catch (e5) finally(f) {
    _base_try_catch_finally(b, f, e1, e2, e3, e4, e5)
}

method try(b) catch(e1) {
    _base_try_catch_finally(b, {}, e1)
}

method try(b) catch(e1) catch(e2) {
    _base_try_catch_finally(b, {}, e1, e2)
}

method try(b) catch(e1) catch(e2) catch(e3) {
    _base_try_catch_finally(b, {}, e1, e2, e3)
}

method try(b) catch(e1) catch(e2) catch(e3) catch(e4) {
    _base_try_catch_finally(b, {}, e1, e2, e3, e4)
}

method try(b) catch(e1) catch(e2) catch(e3) catch(e4) catch (e5) {
    _base_try_catch_finally(b, {}, e1, e2, e3, e4, e5)
}

method circumfix[ *x ] {
    x
}
