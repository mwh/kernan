// ERROR = P1040
def x = 1

match(x)
    case {1 | 2 -> print "One or two"}
    else print "Not one or two"

print "hi"
