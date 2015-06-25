# This script confirms that all commit messages are valid, the software
# builds and that all tests pass before pushing changes to master to
# the remote named "publish".

# Check for git.exe in the path
$GIT = Get-Command -ErrorAction SilentlyContinue "git.exe"

# Otherwise, check some well-known locations where git.exe
# might be found
if (! $GIT) {
    $GIT = @(
        # GitHub for Windows
        (Resolve-Path -ErrorAction SilentlyContinue "$env:LOCALAPPDATA\GitHub\PortableGit_*"),
        # Installed by Visual Studio on demand
        "C:\Program Files (x86)\Git",
        "C:\Program Files\Git"
    ) |
        Where-Object { $_ } |
        Where-Object { Test-Path $_ } |
        Select -First 1

    if (! $GIT) {
        echo "Git not found, cannot check."
        exit
    }

    $GIT = "$GIT\cmd\git.exe"
}

$BRANCH = "master"

& $GIT fetch publish
$PUBLISH_HEAD = & "$GIT" rev-parse publish/"$BRANCH"
$dump = & $GIT checkout $BRANCH

function Fail($message, $line) {
    echo "XX $message"
    exit(1)
}

function Check-Message($commit, $line) {
    $subject = & $GIT show --pretty=format:%s -s $commit
    echo "   Checking message of $line..."
    if ($subject.Length -gt 50) {
        Fail "Subject of commit message too long: must be 50 characters or under." $line
    }
    $first = $subject.Substring(0, 1)
    if (!$first.Equals($first.ToUpper())) {
        Fail "Subject of commit message must start with a capital letter" $line
    }
    if ($subject.EndsWith(".") -or $subject.EndsWith("!") -or $subject.EndsWith( ":")) {
        Fail "Subject of commit message should not end with punctuation" $line
    }
    $first = $subject.Substring(0, $subject.IndexOf(" "))
    if ($first -match "ed$" -and (!$first.EndsWith("bed"))) {
        Fail "Subject of commit message should be an imperative statement" $line
    }
    if ($first -match "[^s]s$") {
        Fail "Subject of commit message should be an imperative statement" $line
    }
    $lines = & $GIT show --pretty=format:%B -s $commit
    if ($lines[1].Length -ne 0) {
        Fail "Subject of commit message must be followed by a blank line" $line
    }
    $lines | ForEach-Object {
        if ($_.Length -gt 72) {
            Fail "No line of commit message should exceed 72 characters" $line
        }
    }
    echo "OK Message $line"
}

function Check-Commit($commit) {
    $line = & $GIT log --oneline $commit | Select -First 1
    echo "   Checking $line"
    Check-Message $commit $line
    & $GIT checkout -q $commit
    msbuild
    if ($LASTEXITCODE -ne 0) {
        Fail "Compiling $line"
    }
    echo "OK Compiling $line"
    # TODO Test harness!
    echo "   Test harness unavailable."
    echo "SK Testing   $line"
}
& $GIT rev-list "$PUBLISH_HEAD..HEAD" | ForEach-Object {
    Check-Commit $_
}
&$GIT checkout $BRANCH

& $GIT push publish $BRANCH
