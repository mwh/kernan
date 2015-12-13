method test(a) and(b) {
    if (a < b) then {
        print "'{a}' < '{b}'"
    } else {
        if (a > b) then {
            print "'{a}' > '{b}'"
        } else {
            if (a == b) then {
                print "'{a}' = '{b}'"
            } else {
                print "Incomparable: '{a}' & '{b}'"
            }
        }
    }
}

test "ab" and "bc"
test "Ab" and "ab"
test "sé" and "se"
test "sé" and "sf"
test "sé" and "sed"
test "sé" and "sè"
test "sė" and "sē"
test "sė́" and "sē"
test "sȩ́" and "sē"
test "sȩ́" and "sȩ́"
test "😹" and "😴"
test "a" and ""
test "" and "a"
