class A(v') {
    var v := v'
    method foo {
        print "A's foo: {self.v}"
    }
    method baz {
        print "A's baz"
    }
}
class B(x) {
    inherits A(x)
        alias abaz = baz
    method bar {
        print "B's bar"
    }
    method baz {
        print "B's baz invokes..."
        abaz
    }
}
class C(y) {
    inherits B(y)
        alias bbaz = baz
    method baz {
        print "C's baz invokes..."
        bbaz
    }
}

var b := C("ARGUMENT")
b.foo
b.bar
b.baz
