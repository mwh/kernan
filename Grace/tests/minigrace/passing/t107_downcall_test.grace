
class A {
  method a {
    b
  }
  method b {
    print("A")
  }
}

class B {
  inherits A
  method b {
    print("B")
  }
}

B.a
