// ERROR = P1030
catch
    print "Hello world"
case {
    e : Error -> print "OK; Caught an error."
}
