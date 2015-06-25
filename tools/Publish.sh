#!/bin/bash
# This script confirms that all commit messages are valid, the software
# builds and that all tests pass before pushing changes to master to
# the remote named "publish".

BRANCH=${1:-master}

messages=()
print_successes() {
    [ "${#messages[@]}" -gt 0 ] && echo "Checklist:"
    for m in "${messages[@]}"
    do
        echo -e "\e[0;32m✓\e[0m $m."
    done
}
succeed() {
    print_successes
    echo -e "\e[0;32m✓\e[0m $1."
    messages+=("$1")
}
fail() {
    print_successes
    echo -e "\e[0;31m✗\e[0m $1"
    shift
    for l in "$@"
    do
        printf "  %s\n" "$l"
    done
    echo
    echo "Left on $(git name-rev HEAD|cut -d' ' -f2):"
    echo "  $(git log --oneline | head -n 1)"
    exit 1
}
try() {
    msg="$1"
    echo "  Checking ${msg,?}..."
    shift
    "$@" && succeed "$msg" || fail "$msg"
}
quiet() {
    "$@" >/dev/null 2>&1
}
check_message() {
    local commit="$1"
    local i="$2"
    local max="$3"
    local line="$4"
    rebase="git rebase -i $commit^ $BRANCH"
    rebasemsg="You can correct the message using \`$rebase\`."
    local subject=$(git show --pretty=format:%s -s)
    if [[ "${#subject}" -gt 50 ]]
    then
        fail "Message   $((i + 1))/${max} $line" \
            "Subject of commit message too long: must be 50 characters or under, but is ${#subject}:" \
            "" "$subject" "" \
            "$rebasemsg"
    fi
    if [[ "${subject^?}" != "${subject}" ]]
    then
        fail "Message   $((i + 1))/${max} $line" \
            "Subject of commit message must start with capital letter:" \
            "" "  ${subject}" "" \
            "$rebasemsg"
    fi
    last="${subject: -1}"
    if ! [[ "${last#[.,!:]}" ]]
    then
        fail "Message   $((i + 1))/${max} $line" \
            "Subject of commit message should not end with punctuation:" \
            "" "  ${subject}" "" \
            "$rebasemsg"
    fi
    first=${subject%% *}
    if [[ "${first: -2}" == "ed" ]] \
        || [[ "$first" = *[^s]s ]]
    then
        fail "Message   $((i + 1))/${max} $line" \
            "Subject of commit message should be an imperative statement:" \
            "" "  ${subject}" "" \
            "$rebasemsg"
    fi
    git show --pretty=format:%B -s > msg-$$
    second="$(sed -n -e 2p msg-$$)"
    if [[ "$second" ]]
    then
        rm -f msg-$$
        fail "Message   $((i + 1))/${max} $line" \
            "Subject of commit message must be followed by blank line, not:" \
            "" "  $second" "" \
            "$rebasemsg"
    fi
    read num long < <(awk '/.{73}/{print NR,$0;exit}' msg-$$) || true
    if [[ "$long" ]]
    then
        rm -f msg-$$
        fail "Message   $((i + 1))/${max} $line" \
            "No line of commit message should exceed 72 characters, but line $num has ${#long}:" \
            "" "  $long" "" \
            "$rebasemsg"
    fi
    rm -f msg-$$
    succeed "Message   $((i + 1))/${max} $line"
}
check_commit() {
    local commit="$1"
    local i="$2"
    local max="$3"
    local line="$(git log --oneline "$commit"|head -n 1)"
    echo "  Commit    $((i + 1))/${max} $line"
    git checkout -q "$commit"
    check_message "$commit" "$i" "$max" "$line"
    try "Compiling $((i + 1))/${max} $line" xbuild
    pushd tests
    try "Testing   $((i + 1))/${max} $line" ./all
    popd
}
set -e
quiet pushd "$(dirname "$0")/.." || fail "Bad directory path"
git fetch publish
try "No uncommitted changes" git diff --quiet HEAD
git checkout -q "$BRANCH"
quiet pushd Grace
PUBLISH_HEAD=$(git rev-parse publish/"$BRANCH")
COMMITS=($(git rev-list ${PUBLISH_HEAD}..HEAD))
i=0
max="${#COMMITS[@]}"
if [[ "0" == "$max" ]]
then
    succeed "Remote $(git config --get remote.publish.url) up-to-date"
    exit 0
fi
echo "  $max commits to check."
for commit in "${COMMITS[@]}"
do
    check_commit "$commit" $i $max
    i=$((i + 1))
done
git checkout -q "$BRANCH"
try "Publishing to $(git config --get remote.publish.url)" git push publish "$BRANCH"
