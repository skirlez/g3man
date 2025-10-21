{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs";

  outputs = { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };

      wasi-sdk = pkgs.stdenv.mkDerivation {
        name = "wasi-sdk";
        version = "27";
        src = pkgs.fetchurl {
          url = "https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-27/wasi-sdk-27.0-x86_64-linux.tar.gz";
          sha256 = "sha256-t9TZRMiFA+TyHYSvB6wpPjRAsbYhC/1/544K/ZLCO8I=";
        };
        unpackPhase = ''
          mkdir -p unpacked
          echo $src
          tar -xzf $src -C unpacked
        '';
        installPhase = ''
          runHook preInstall
          mkdir $out
          cp -r ./unpacked/* $out/
          runHook postInstall
        '';
      };
    in {
      devShells.x86_64-linux.default = pkgs.mkShell {
        packages = with pkgs; [
          dotnetCorePackages.sdk_8_0
        ];
        buildInputs = with pkgs; [
          git
          gtk4
          libadwaita
        ];
        strictDeps = true;
        shellHook = ''
          export LD_LIBRARY_PATH=${pkgs.lib.makeLibraryPath 
                      [ pkgs.gtk4 pkgs.libadwaita ]}
          export XDG_DATA_DIRS=$XDG_DATA_DIRS:$GSETTINGS_SCHEMAS_PATH
          export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_8_0}
          # don't actually need this?
          # export XDG_DATA_DIRS=$XDG_DATA_DIRS:${pkgs.hicolor-icon-theme}/share:${pkgs.adwaita-icon-theme}/share

          export WASI_SDK_PATH=${wasi-sdk}/wasi-sdk-27.0-x86_64-linux
        '';
      };
    };
}
