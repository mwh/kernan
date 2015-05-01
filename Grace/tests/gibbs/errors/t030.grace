// ERROR = P1024
type A<T> = {
    foo(_ : T) -> Number
    bar(_ : Number) -> T
}

var a : A<String := object {
    method foo(x : String) -> Number { x.size }
    method bar(y : Number) -> String { "{y}" }
}
