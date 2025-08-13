with import <nixpkgs> {};

mkShell {
  packages = [
    gcc
    gdb
    gtkmm4
    bear
    pkg-config
    fontconfig
    nlohmann_json
    pkgsCross.mingwW64.buildPackages.gcc
  ];
}