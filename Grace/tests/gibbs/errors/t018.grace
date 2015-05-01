// ERROR = P1007
type A<T = {
    foo(_ : T) -> Number
    bar(_ : Number) -> T
}
