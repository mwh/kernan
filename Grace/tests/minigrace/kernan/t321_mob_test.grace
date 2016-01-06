// Algol Bulletin, issue 17, Jul. 1964. Letter by Donald Knuth, p7.
method A(k, x1, x2, x3, x4, x5) {
    var k' := k
    var aRet
    def B = {
        var bRet
        k' := k' - 1
        aRet := A(k', B, x1, x2, x3, x4)
        bRet := aRet
        bRet
    }
    if (k' <= 0) then {
        aRet := x4.apply + x5.apply
    } else {
        B.apply
    }
    aRet
}
print(A(10, {1}, {-1}, {-1}, {1}, {0}))
