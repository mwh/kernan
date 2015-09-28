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
    method bar {
        print "B's bar"
    }
    method baz {
        print "B's baz invokes..."
        super.baz
    }
}
class C(y) {
    inherits B(y)
    method baz {
        print "C's baz invokes..."
        super.baz
    }
}

var b := C("ARGUMENT")
b.foo
b.bar
b.baz
