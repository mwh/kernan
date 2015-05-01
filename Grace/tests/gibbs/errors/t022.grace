// ERROR = P1023
catch {
    var x := 1
    x.nonExistentMethod
} case({e : Error -> print "OK; Caught an error."}
