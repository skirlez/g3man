{
  description = "";
  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs?ref=nixos-unstable";
  };

  outputs = { self, nixpkgs }:
  let
    system = "x86_64-linux";
    pkgs = import nixpkgs { inherit system; };
  in {
    devShell.x86_64-linux = pkgs.mkShell {
      nativeBuildInputs = with pkgs; [
        stdenv.cc
        pkg-config
        glib # for GSETTINGS_SCHEMAS_PATH
        bear
        gdb
        xxd
      ];
      buildInputs = with pkgs; [
        gtkmm4
        fontconfig
        nlohmann_json
        libzip
      ];
      strictDeps = true;
      shellHook = ''
        export XDG_DATA_DIRS=$XDG_DATA_DIRS:$GSETTINGS_SCHEMAS_PATH

        # don't actually need this?
        # export XDG_DATA_DIRS=$XDG_DATA_DIRS:${pkgs.hicolor-icon-theme}/share:${pkgs.adwaita-icon-theme}/share
      '';
    };
    packages.x86_64-linux.default = 
    let
      undertaleModCli = "/home/david/Desktop/Apps/UndertaleModCli 0.8.0.0/UndertaleModCli";
    in
    pkgs.stdenv.mkDerivation {
        name = "forgery-manager";
        src = ./.;
        nativeBuildInputs = with pkgs; [
          stdenv.cc
          pkg-config
          wrapGAppsHook4
          xxd
        ];
        buildInputs = with pkgs; [
          gtkmm4
          nlohmann_json
          libzip
        ];
        strictDeps = true;
        buildPhase = "make nix FORGERYMANAGER_UMC_PATH=\"${undertaleModCli}\"";
        installPhase = ''
          mkdir -p $out/bin
          cp -r out/release/forgery-manager $out/bin/forgery-manager
        '';
    };
  };

}