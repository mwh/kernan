// ERROR = P1040
def x = 1

match(x)
    case {1 | 2 -> print "One or two"}
    else

print "hi"
