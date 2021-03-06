#!/bin/bash
#
# Unit test all subsystems in repo
#
# Optional Flags:
# -c  Run tests in continuous integration mode
#
# usage:
# ./unit-test-all.bash
# ./unit-test-all.bash -c

# shellcheck source=./tools/common.bash
source "$(dirname "$0")"/common.bash || exit


main () {
  ci_mode='false'

  while getopts ':c' arg; do
    case "${arg}" in
      c )
        ci_mode='true'
        ;;
      * )
        echo "usage: [-c]"
        exit 1
        ;;
    esac
  done

  subsystems=(dashboard etl match metrics query-tool shared)

  for s in "${subsystems[@]}"
  do
    pushd ../"$s"/
      echo "Testing ${s}"
      if [ "$ci_mode" = "true" ]; then
        ./build.bash test -c
      else
        ./build.bash test
      fi
    popd
  done

  script_completed
}

main "$@"
