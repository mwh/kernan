def half = 0.5
def third = 1 / 3
def seventh = 1 / 7

print(third * 3)
print(seventh * 7 / half)
print(1 / third)
print(1 / (half ^ 2))
print((half * third + seventh * half) * 21)
print(third / half * 9)
print(third / seventh * 3)
print(((1 + seventh) * third) * 21)
print(1 / (third * third))

var x := -1
for (1 .. 231) do { i ->
    x := x + third * seventh
}
print(x)

var y := 0
for (1 .. 10) do { i ->
    y := y + 1.1
}
print(y)

var z := 0.0000000000000000001
z := z + 0.00000000000000000002
print(z * (10 ^ 10) * (10 ^ 10))
