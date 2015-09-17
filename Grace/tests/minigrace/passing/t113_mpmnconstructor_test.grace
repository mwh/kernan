
class aCat(aName) coloured (aColour) {
    def colour = aColour
    def name = aName
    var miceEaten := 0
    method describe {
        print "{name} is a {colour} cat"
    }
}

def myCat = aCat "Timothy" coloured ("black")
def yourCat = aCat "Gregory" coloured ("tortoiseshell")

myCat.describe
yourCat.describe
