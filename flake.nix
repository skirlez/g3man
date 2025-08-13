{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs";

  outputs = { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
    in {
      devShells.x86_64-linux.default = pkgs.mkShell {
        buildInputs = [
          pkgs.dotnetCorePackages.sdk_8_0
          pkgs.xorg.libX11
          pkgs.xorg.libXcursor
          pkgs.git
          pkgs.curl
          pkgs.unzip
        ];

        shellHook = ''
          export LD_LIBRARY_PATH=${pkgs.lib.makeLibraryPath 
            [ pkgs.xorg.libX11 pkgs.xorg.libXcursor pkgs.fontconfig pkgs.xorg.libICE pkgs.xorg.libSM]}
        '';
      };
    };
}