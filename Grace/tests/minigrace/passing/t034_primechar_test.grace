class Cat(name' : String) {
 def name : String = name'
 method purr {print("Purr") }
 method mew {print("Meow") }
}

var c := Cat("Macavity")

c.purr
c.mew
