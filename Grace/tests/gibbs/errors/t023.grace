// ERROR = P1023
catch {
    Exception.raise "1"
} case {
    e : Exception -> print "2"
} finally ({ print "3" }
