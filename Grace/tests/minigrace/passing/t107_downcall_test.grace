
class A {
  method a {
    b
  }
  method b {
    print("A")
  }
}

class B {
  inherit A
  method b {
    print("B")
  }
}

B.a
