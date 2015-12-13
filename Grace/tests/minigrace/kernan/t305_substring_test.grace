def str = "aáēì̧😹निअॆz"

for (1 .. str.size) do { start ->
    for (start .. str.size) do { end ->
        print "{start}-{end}: {str.substringFrom(start) to(end)}"
    }
}
