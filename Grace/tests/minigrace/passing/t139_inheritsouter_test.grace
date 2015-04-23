def a = object {
    method new {
        object {
            print(outer.asString)
        }
    }

    method asString { "a" }
}

method asString { "m" }

a.new
object {
    inherits a.new
    print(outer.asString)
}

