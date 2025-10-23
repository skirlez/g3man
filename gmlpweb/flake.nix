{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs";

  outputs = { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };

      wasi-sdk = pkgs.stdenv.mkDerivation {
        name = "wasi-sdk";
        version = "25";
        src = pkgs.fetchurl {
          url = "https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-25/wasi-sdk-25.0-x86_64-linux.tar.gz";
          sha256 = "sha256-UmQN3hNZm/EnqVSZ5h1tZAJWEZRW0a+Il6tnJbzz2Jw=";
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
          dotnetCorePackages.sdk_10_0
        ];
        strictDeps = true;
        shellHook = ''
          export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_10_0}
          export WASI_SDK_PATH=${wasi-sdk}/wasi-sdk-25.0-x86_64-linux
        '';
      };
    };
}
