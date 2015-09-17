class X {
    method a {
        Y("Hello")
    }
}
class Y(v') {
    def v = v'
    method asString {
        "Y({v})"
    }
}

print(X.a)
