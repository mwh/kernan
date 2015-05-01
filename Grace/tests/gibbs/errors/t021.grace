// ERROR = P1023
var x := 1
catch({x.nonExistentMethod}
case {
    e : Error -> print "OK; Caught an error."
}
