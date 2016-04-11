class A {
    method foo {
        "world"
    }
}

class B {
    inherit A
    method bar {
        print "hello {foo}"
    }
}

class C {
    inherit B
    method quux {
        print "X"
        bar
    }
}

object {
    inherit A
}

def x = B
x.bar
type T = {
    foo
    quux
}
def y : T = C
y.quux

