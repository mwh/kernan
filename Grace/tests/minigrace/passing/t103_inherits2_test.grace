class A {
    method foo {
        "world"
    }
}

class B {
    inherits A
    method bar {
        print "hello {foo}"
    }
}

class C {
    inherits B
    method quux {
        print "X"
        bar
    }
}

object {
    inherits A
}

def x = B
x.bar
type T = {
    foo
    quux
}
def y : T = C
y.quux

